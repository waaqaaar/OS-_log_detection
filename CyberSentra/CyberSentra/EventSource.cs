using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics.Eventing.Reader;

namespace CyberSentra
{
    public static class EventSource
    {
        private static List<EventRecord>? _cache;
        private const string EventsRelativePath = @"data\events.json";

        public static IReadOnlyList<EventRecord> GetEvents()
        {
            if (_cache == null)
            {
                Load();
            }

            return _cache!;
        }

        public static void Reload()
        {
            _cache = null;
            Load();

            try
            {
                Database.EventRepository.SaveEvents(_cache!);
                Debug.WriteLine("[DB] Saved events, cache count = " + _cache!.Count);
                Debug.WriteLine("[DB] Total DB events = " + Database.EventRepository.LoadAllEvents().Count);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("[DB] SaveEvents failed: " + ex.Message);
            }
        }


        private static void Load()
        {
            // 1) Try Windows Event Log if we’re on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var winEvents = LoadFromWindowsLogs();

                    if (winEvents.Count > 0)
                    {
                        _cache = winEvents;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EventSource] Error loading Windows logs: {ex}");
                }
            }

            // 2) Fallback: events.json
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var path = Path.Combine(baseDir, EventsRelativePath);

                Debug.WriteLine($"[EventSource] Looking for events at: {path}");

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var list = JsonSerializer.Deserialize<List<EventRecord>>(json, options);

                    if (list is { Count: > 0 })
                    {
                        Debug.WriteLine($"[EventSource] Loaded {list.Count} events from JSON.");
                        _cache = list;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EventSource] Error loading events.json: {ex}");
            }

            // 3) Last resort: built-in sample
            Debug.WriteLine("[EventSource] Using built-in sample events.");
            _cache = GetSampleEvents();
        }

        /// <summary>
        /// Load recent events from Windows Event Logs (Application + System + Security if possible).
        /// </summary>
        private static List<EventRecord> LoadFromWindowsLogs()
        {
            var results = new List<EventRecord>();

            string[] logNames =
            {
        "Application",
        "System",
        "Security",
       // "Microsoft-Windows-Sysmon/Operational" // Sysmon channel
    };

            foreach (var logName in logNames)
            {
                try
                {
                    using var log = new EventLog(logName);

                    var entries = log.Entries;
                    for (int i = entries.Count - 1; i >= 0 && results.Count < 1500; i--)
                    {
                        var entry = entries[i];

                        var isSysmon = logName.Contains("Sysmon", StringComparison.OrdinalIgnoreCase);

                        var record = new EventRecord
                        {
                            Time = entry.TimeGenerated.ToString("o"),

                            Type = isSysmon ? "Sysmon" : logName,            // "Sysmon" or original log
                            Severity = entry.EntryType.ToString(),           // Information, Warning, Error, etc.
                            User = entry.UserName ?? string.Empty,
                            Process = entry.Source,                          // Sysmon: "Microsoft-Windows-Sysmon"
                            Details = entry.Message ?? string.Empty,
                            Source = isSysmon ? "Sysmon" : "Windows"
                        };

                        results.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EventSource] Cannot read log '{logName}': {ex.Message}");
                }
            }
            // ✅ Read Sysmon (Operational) using EventLogReader (required for this channel)
            try
            {
                var sysmonQuery = new EventLogQuery("Microsoft-Windows-Sysmon/Operational", PathType.LogName)
                {
                    ReverseDirection = true // newest first
                };

                using var reader = new EventLogReader(sysmonQuery);

                for (int count = 0; count < 500; count++) // limit
                {
                    var ev = reader.ReadEvent();
                    if (ev == null) break;

                    results.Add(new EventRecord
                    {
                        Time = ev.TimeCreated?.ToString("o") ?? "",
                        Type = "Sysmon",
                        Severity = ev.LevelDisplayName ?? "Information",
                        User = ev.UserId?.ToString() ?? "",
                        Process = ev.ProviderName ?? "Microsoft-Windows-Sysmon",
                        Details = ev.FormatDescription() ?? "",
                        Source = "Sysmon"
                    });

                    ev.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EventSource] Cannot read Sysmon Operational via EventLogReader: {ex.Message}");
            }

            Debug.WriteLine($"[EventSource] Loaded {results.Count} events from Windows logs (incl. Sysmon if available).");
            return results;
        }


        private static List<EventRecord> GetSampleEvents()
        {
            return new List<EventRecord>
            {
                new EventRecord
                {
                    Time = "2025-12-09 10:15:00",
                    Type = "Security",
                    Severity = "Information",
                    User = "CORP\\jdoe",
                    Process = "explorer.exe",
                    Details = "User logon successful."
                },
                new EventRecord
                {
                    Time = "2025-12-09 10:45:22",
                    Type = "Sysmon",
                    Severity = "Warning",
                    User = "CORP\\svc_backup",
                    Process = "powershell.exe",
                    Details = "Script executed from unusual directory."
                },
                new EventRecord
                {
                    Time = "2025-12-09 09:03:10",
                    Type = "Application",
                    Severity = "Error",
                    User = "CORP\\asmith",
                    Process = "chrome.exe",
                    Details = "Crash reported in browser process."
                },
                new EventRecord
                {
                    Time = "2025-12-09 08:30:00",
                    Type = "Security",
                    Severity = "Error",
                    User = "CORP\\jdoe",
                    Process = "winlogon.exe",
                    Details = "Multiple failed login attempts."
                }
            };
        }
    }
}
