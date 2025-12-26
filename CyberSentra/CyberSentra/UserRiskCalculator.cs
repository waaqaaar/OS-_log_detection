using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberSentra
{
    public static class UserRiskCalculator
    {
        public static List<UserRiskInfo> GetUserRisks(IReadOnlyList<EventRecord> events)
        {
            // Group by the raw user string from EventRecord
            var groups = events
                .Where(e => !string.IsNullOrWhiteSpace(e.User))
                .GroupBy(e => e.User);

            var result = new List<UserRiskInfo>();

            foreach (var g in groups)
            {
                var rawUser = g.Key; // e.g. "CORP\\jdoe" or "jdoe"
                SplitDomainAndUser(rawUser, out var domain, out var userName);

                // Count anomalies for this user (very simple rules)
                var anomalies = g.Count(e =>
                    e.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
                    e.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                    e.Details.Contains("failed login", StringComparison.OrdinalIgnoreCase) ||
                    e.Details.Contains("failed logon", StringComparison.OrdinalIgnoreCase));

                // Simple risk formula: base + anomalies * weight, capped at 100
                var risk = 20 + anomalies * 15;
                if (risk > 100) risk = 100;
                if (risk < 0) risk = 0;

                result.Add(new UserRiskInfo
                {
                    UserName = userName,
                    Domain = domain,
                    DisplayName = ToDisplayName(userName),
                    Email = $"{userName}@{(string.IsNullOrEmpty(domain) ? "local" : domain.ToLower())}.local",
                    RiskScore = risk,
                    Anomalies = anomalies
                });
            }

            // Sort by highest risk first
            return result
                .OrderByDescending(u => u.RiskScore)
                .ToList();
        }

        private static void SplitDomainAndUser(string raw, out string domain, out string user)
        {
            var parts = raw.Split('\\');
            if (parts.Length == 2)
            {
                domain = parts[0];
                user = parts[1];
            }
            else
            {
                domain = "CORP";
                user = raw;
            }
        }

        private static string ToDisplayName(string userName)
        {
            // crude "jdoe" -> "Jdoe"
            if (string.IsNullOrWhiteSpace(userName))
                return string.Empty;

            if (userName.Contains('.'))
            {
                // "john.doe" -> "John Doe"
                var parts = userName.Split('.');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Length > 0)
                        parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
                }
                return string.Join(" ", parts);
            }

            return char.ToUpper(userName[0]) + userName.Substring(1);
        }
    }
}
