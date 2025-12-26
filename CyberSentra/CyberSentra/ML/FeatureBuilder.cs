using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberSentra.ML
{
    public static class FeatureBuilder
    {
        // Features (6):
        // 0 TotalEvents
        // 1 FailedLogins
        // 2 Errors/Failures
        // 3 Warnings
        // 4 UniqueProcesses
        // 5 UniqueSources
        public static List<UserFeatureRow> BuildPerUserFeatures(IReadOnlyList<EventRecord> events)
        {
            var grouped = events
                .GroupBy(e =>
                {
                    var u = string.IsNullOrWhiteSpace(e.User) ? "Unknown" : e.User;
                    return u;
                }).Where(g => g.Key != "Unknown");


            var rows = new List<UserFeatureRow>();

            foreach (var g in grouped)
            {
                var user = g.Key;

                int total = g.Count();

                int failed = g.Count(e =>
                    (e.Details ?? "").Contains("failed", StringComparison.OrdinalIgnoreCase));

                int errors = g.Count(e =>
                    (e.Severity ?? "").Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                    (e.Severity ?? "").Contains("Failure", StringComparison.OrdinalIgnoreCase) ||
                    (e.Severity ?? "").Equals("Critical", StringComparison.OrdinalIgnoreCase));

                int warnings = g.Count(e =>
                    (e.Severity ?? "").Equals("Warning", StringComparison.OrdinalIgnoreCase));

                int uniqueProc = g.Select(e => e.Process ?? "")
                                  .Where(x => !string.IsNullOrWhiteSpace(x))
                                  .Distinct().Count();

                int uniqueSrc = g.Select(e => e.Source ?? "")
                                 .Where(x => !string.IsNullOrWhiteSpace(x))
                                 .Distinct().Count();

                rows.Add(new UserFeatureRow
                {
                    User = user,
                    Features = new float[]
                    {
                        total,
                        failed,
                        errors,
                        warnings,
                        uniqueProc,
                        uniqueSrc
                    }
                });
            }

            return rows;
        }
        public static List<UserFeatureRow> BuildPerUserHourlyFeatures(IReadOnlyList<EventRecord> events, int lastHours = 24)
        {
            var now = DateTime.Now;
            var cutoff = now.AddHours(-lastHours);

            // Parse time once
            var parsed = events
                .Select(e => new
                {
                    Event = e,
                    ParsedTime = DateTime.TryParse(e.Time, out var t) ? t : (DateTime?)null
                })
                .Where(x => x.ParsedTime.HasValue && x.ParsedTime.Value >= cutoff)
                .ToList();

            // Group by (User, HourBucket)
            var grouped = parsed.GroupBy(x =>
            {
                var u = string.IsNullOrWhiteSpace(x.Event.User) ? "Unknown" : x.Event.User;
                var t = x.ParsedTime!.Value;
                var bucket = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0);
                return (User: u, Bucket: bucket);
            });
           // .Where(g => g.Key.User != "Unknown");


            var rows = new List<UserFeatureRow>();

            foreach (var g in grouped)
            {
                var user = g.Key.User;
                var evs = g.Select(x => x.Event).ToList();

                int total = evs.Count;
                int failed = evs.Count(e => (e.Details ?? "").Contains("failed", StringComparison.OrdinalIgnoreCase));
                int errors = evs.Count(e =>
                    (e.Severity ?? "").Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                    (e.Severity ?? "").Contains("Failure", StringComparison.OrdinalIgnoreCase) ||
                    (e.Severity ?? "").Equals("Critical", StringComparison.OrdinalIgnoreCase));
                int warnings = evs.Count(e => (e.Severity ?? "").Equals("Warning", StringComparison.OrdinalIgnoreCase));

                int uniqueProc = evs.Select(e => e.Process ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count();
                int uniqueSrc = evs.Select(e => e.Source ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count();

                rows.Add(new UserFeatureRow
                {
                    User = $"{user} | {g.Key.Bucket:MM-dd HH}:00",
                    Features = new float[] { total, failed, errors, warnings, uniqueProc, uniqueSrc }
                });
            }

            return rows;
        }

    }
}
