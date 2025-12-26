using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberSentra
{
    public static class EventContext
    {
        public static TimeRange CurrentRange { get; private set; } = TimeRange.All;

        public static void SetTimeRange(TimeRange range)
        {
            CurrentRange = range;
        }

        public static IReadOnlyList<EventRecord> GetCurrentEvents(
         bool applyNoiseFilter = false,
         string? sourceContains = null,
         string? sourceNotContains = null)
        {
            var all = EventSource.GetEvents();
            IEnumerable<EventRecord> baseList = all;

            // Noise filter (your existing behavior)
            if (applyNoiseFilter)
                baseList = Preprocessor.FilterNoise(all);

            // ✅ Source include filter (optional)
            if (!string.IsNullOrWhiteSpace(sourceContains))
            {
                baseList = baseList.Where(ev =>
                    !string.IsNullOrWhiteSpace(ev.Source) &&
                    ev.Source.Contains(sourceContains, StringComparison.OrdinalIgnoreCase));
            }

            // ✅ Source exclude filter (optional)
            if (!string.IsNullOrWhiteSpace(sourceNotContains))
            {
                baseList = baseList.Where(ev =>
                    string.IsNullOrWhiteSpace(ev.Source) ||
                    !ev.Source.Contains(sourceNotContains, StringComparison.OrdinalIgnoreCase));
            }

            // Time range (your existing behavior)
            if (CurrentRange == TimeRange.All)
                return baseList.ToList();

            var now = DateTime.Now;
            TimeSpan window = CurrentRange == TimeRange.Last1Hour
                ? TimeSpan.FromHours(1)
                : TimeSpan.FromHours(24);

            var cutoff = now - window;

            return baseList
                .Where(ev =>
                {
                    if (!DateTime.TryParse(ev.Time, out var t))
                        return false;

                    return t >= cutoff && t <= now;
                })
                .ToList();
        }

    }
}
