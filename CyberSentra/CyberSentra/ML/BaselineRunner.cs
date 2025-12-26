using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberSentra.ML
{
    public static class BaselineRunner
    {
        public static (List<UserAnomaly> scored, int anomalyCount) ScoreLastHourAgainstBaseline(
            List<EventRecord> allHistory,
            int baselineDays = 7)
        {
            var now = DateTime.Now;
            var baselineStart = now.AddDays(-baselineDays);
            var scoringStart = now.AddHours(-1);

            // Split
            var baselineEvents = allHistory
                .Where(e => DateTime.TryParse(e.Time, out var t) && t >= baselineStart && t < scoringStart)
                .ToList();

            var lastHourEvents = allHistory
                .Where(e => DateTime.TryParse(e.Time, out var t) && t >= scoringStart)
                .ToList();

            // Build rows
            var baselineRows = FeatureBuilder.BuildPerUserHourlyFeatures(baselineEvents, lastHours: baselineDays * 24);
            var scoringRows = FeatureBuilder.BuildPerUserHourlyFeatures(lastHourEvents, lastHours: 1);

            // Normalize using BASELINE distribution (critical!)
            AnomalyModel.NormalizeUsingReference(baselineRows, scoringRows);

            // Train on baseline, score last hour rows
            var scored = AnomalyModel.TrainOnBaselineScoreTarget(baselineRows, scoringRows);

            var count = scored.Count(a => a.IsAnomaly);
            return (scored, count);
        }
    }
}
