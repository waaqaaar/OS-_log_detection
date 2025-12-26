using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberSentra
{
    public static class Preprocessor
    {
        /// <summary>
        /// Remove noisy / low-value events before analysis.
        /// </summary>
        public static IReadOnlyList<EventRecord> FilterNoise(IReadOnlyList<EventRecord> events)
        {
            var result = new List<EventRecord>(events.Count);

            foreach (var ev in events)
            {
                var severity = ev.Severity ?? string.Empty;
                var source = ev.Source ?? string.Empty;
                var type = ev.Type ?? string.Empty;
                var detailsLower = (ev.Details ?? string.Empty).ToLowerInvariant();

                // 1. Skip ultra-noisy informational events from generic sources
                if (severity.Equals("Information", StringComparison.OrdinalIgnoreCase) ||
                    severity.Equals("Informational", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep only if text suggests security relevance
                    if (!IsSecurityRelevantInfo(detailsLower))
                        continue;
                }

                // 2. Skip some boring "Application"/"System" chatter
                if (type.Equals("Application", StringComparison.OrdinalIgnoreCase) ||
                    type.Equals("System", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsKnownNoiseSource(source))
                        continue;
                }

                // 3. If we reach here, keep it
                result.Add(ev);
            }

            return result;
        }

        private static bool IsSecurityRelevantInfo(string detailsLower)
        {
            // Keep informational events that mention these keywords
            string[] keywords =
            {
                "logon", "login", "authentication", "password", "lockout",
                "access denied", "privilege", "firewall",
                "policy", "permission", "audit"
            };

            return keywords.Any(k => detailsLower.Contains(k));
        }

        private static bool IsKnownNoiseSource(string source)
        {
            // You can expand this list as you see noisy sources on your machine
            string[] noisySources =
            {
                "MsiInstaller",
                "Disk",
                "Service Control Manager",
                "DistributedCOM",
                "ESENT"
            };

            return noisySources.Any(s => source.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
