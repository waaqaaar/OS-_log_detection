using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CyberSentra
{
    public static class ThreatDetector
    {
        // Security event IDs (common)
        private const int EVT_FAILED_LOGON = 4625;
        private const int EVT_SUCCESS_LOGON = 4624;
        private const int EVT_ACCOUNT_LOCKED = 4740;
        private const int EVT_USER_CREATED = 4720;
        private const int EVT_PASSWORD_RESET = 4724;
        private const int EVT_PASSWORD_CHANGED = 4723;

        // Sysmon IDs (common)
        private const int SYSMON_PROCESS_CREATE = 1;
        private const int SYSMON_NETWORK_CONNECT = 3;
        private const int SYSMON_FILE_CREATE = 11;
        private const int SYSMON_REG_SET = 13;

        // Try to pull "Event ID: 4625" or "EventID 4625" etc. from message text
        private static readonly Regex _eventIdRx =
            new Regex(@"Event\s*ID\s*[:=]?\s*(\d{3,5})|EventID\s*[:=]?\s*(\d{3,5})",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // crude indicators for encoded powershell, downloaders, and LOLBins
        private static readonly string[] _lolbins = new[]
        {
            "powershell", "pwsh", "cmd.exe", "wscript", "cscript",
            "rundll32", "regsvr32", "mshta", "wmic", "certutil",
            "bitsadmin", "schtasks", "net.exe", "sc.exe"
        };

        private static bool LooksSysmon(EventRecord ev)
        {
            var type = ev.Type ?? "";
            var src = ev.Source ?? "";
            return type.Equals("Sysmon", StringComparison.OrdinalIgnoreCase)
                || src.Equals("Sysmon", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Sysmon", StringComparison.OrdinalIgnoreCase);
        }

        private static int? TryGetEventId(EventRecord ev)
        {
            // If you ever add EventId field in EventRecord, use it here first.
            var text = ev.Details ?? "";
            var m = _eventIdRx.Match(text);
            if (!m.Success) return null;

            // capture group 1 or 2 depending on which matched
            var s = m.Groups[1].Success ? m.Groups[1].Value :
                    m.Groups[2].Success ? m.Groups[2].Value : null;

            if (int.TryParse(s, out var id)) return id;
            return null;
        }

        private static string NormalizeUser(EventRecord ev)
        {
            // Prefer explicit user field; else attempt to extract from details
            var u = ev.User ?? "";
            if (!string.IsNullOrWhiteSpace(u)) return u;

            // Try to find "Account Name:" pattern (Security log text often includes it)
            var d = ev.Details ?? "";
            var m = Regex.Match(d, @"Account\s+Name:\s*(.+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var line = m.Groups[1].Value.Trim();
                // sometimes line contains extra fields; take first token
                var first = line.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }

            return "Unknown";
        }

        private static bool ContainsAny(string hay, params string[] needles)
        {
            foreach (var n in needles)
                if (hay.Contains(n, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static List<ThreatInfo> GetThreats(IReadOnlyList<EventRecord> events)
        {
            var threats = new List<ThreatInfo>();

            foreach (var ev in events)
            {
                var details = ev.Details ?? "";
                var proc = ev.Process ?? "";
                var type = ev.Type ?? "";
                var src = ev.Source ?? "";

                var isSysmon = LooksSysmon(ev);
                var eventId = TryGetEventId(ev);
                var user = NormalizeUser(ev);

                // ---------- Rule A: Security 4625 failed logon ----------
                // Better than searching "failed" only.
                if (eventId == EVT_FAILED_LOGON ||
                    (type.Equals("Security", StringComparison.OrdinalIgnoreCase) &&
                     ContainsAny(details, "An account failed to log on", "failed logon", "logon failure")))
                {
                    threats.Add(new ThreatInfo
                    {
                        Time = ev.Time,
                        User = user,
                        Source = string.IsNullOrWhiteSpace(src) ? type : src,
                        Technique = "T1110",
                        Name = "Brute Force / Failed Logon",
                        Tactic = "Credential Access",
                        Severity = "High",
                        Details = details
                    });
                    continue;
                }

                // ---------- Rule B: Account lockout 4740 ----------
                if (eventId == EVT_ACCOUNT_LOCKED || ContainsAny(details, "account was locked out", "4740"))
                {
                    threats.Add(new ThreatInfo
                    {
                        Time = ev.Time,
                        User = user,
                        Source = string.IsNullOrWhiteSpace(src) ? type : src,
                        Technique = "T1110",
                        Name = "Account Lockout Detected",
                        Tactic = "Credential Access",
                        Severity = "High",
                        Details = details
                    });
                    continue;
                }

                // ---------- Rule C: New user created 4720 ----------
                if (eventId == EVT_USER_CREATED || ContainsAny(details, "A user account was created", "4720"))
                {
                    threats.Add(new ThreatInfo
                    {
                        Time = ev.Time,
                        User = user,
                        Source = string.IsNullOrWhiteSpace(src) ? type : src,
                        Technique = "T1136",
                        Name = "New User Account Created",
                        Tactic = "Persistence",
                        Severity = "High",
                        Details = details
                    });
                    continue;
                }

                // ---------- Rule D: Password reset/change ----------
                if (eventId == EVT_PASSWORD_RESET || eventId == EVT_PASSWORD_CHANGED ||
                    ContainsAny(details, "password was reset", "password was changed", "4724", "4723"))
                {
                    threats.Add(new ThreatInfo
                    {
                        Time = ev.Time,
                        User = user,
                        Source = string.IsNullOrWhiteSpace(src) ? type : src,
                        Technique = "T1098",
                        Name = "Credential Change / Reset",
                        Tactic = "Persistence",
                        Severity = "Medium",
                        Details = details
                    });
                    continue;
                }

                // ---------- Rule E: Suspicious PowerShell / LOLBins ----------
                // Works even without Sysmon by checking Process + details
                var procLower = proc.ToLowerInvariant();
                var detLower = details.ToLowerInvariant();

                bool looksScriptExec =
                    ContainsAny(procLower, _lolbins) &&
                    (ContainsAny(detLower, "encodedcommand", "frombase64string", "iex", "invoke-", "downloadstring", "wget", "curl", "http://", "https://")
                     || ContainsAny(detLower, "executionpolicy", "bypass", "hidden", "-nop", "-w hidden"));

                if (looksScriptExec)
                {
                    threats.Add(new ThreatInfo
                    {
                        Time = ev.Time,
                        User = user,
                        Source = string.IsNullOrWhiteSpace(src) ? type : src,
                        Technique = "T1059",
                        Name = "Suspicious Script/Command Execution",
                        Tactic = "Execution",
                        Severity = "High",
                        Details = details
                    });
                    continue;
                }

                // ---------- Rule F: Sysmon Process Create (EventID 1) ----------
                if (isSysmon && (eventId == SYSMON_PROCESS_CREATE || ContainsAny(details, "EventID 1", "Process Create")))
                {
                    // Look for LOLBins and common attacker flags
                    if (ContainsAny(detLower, "powershell", "encodedcommand", "rundll32", "regsvr32", "mshta", "certutil", "bitsadmin"))
                    {
                        threats.Add(new ThreatInfo
                        {
                            Time = ev.Time,
                            User = user,
                            Source = "Sysmon",
                            Technique = "T1059",
                            Name = "Sysmon: Suspicious Process Creation",
                            Tactic = "Execution",
                            Severity = "High",
                            Details = details
                        });
                        continue;
                    }
                }

                // ---------- Rule G: Sysmon Network Connect (EventID 3) ----------
                if (isSysmon && (eventId == SYSMON_NETWORK_CONNECT || ContainsAny(details, "EventID 3", "Network connection")))
                {
                    // Flag external connections / suspicious tools
                    if (ContainsAny(detLower, "destinationip", "remote address", "http", "https", "powershell", "rundll32", "mshta"))
                    {
                        threats.Add(new ThreatInfo
                        {
                            Time = ev.Time,
                            User = user,
                            Source = "Sysmon",
                            Technique = "T1071",
                            Name = "Sysmon: Suspicious Network Connection",
                            Tactic = "Command and Control",
                            Severity = "Medium",
                            Details = details
                        });
                        continue;
                    }
                }

                // ---------- IMPORTANT: remove your old "generic high severity catch-all" ----------
                // That rule creates tons of false "threats" and ruins the dashboard.
            }

            return threats.OrderByDescending(t => t.Time).ToList();
        }
    }
}
