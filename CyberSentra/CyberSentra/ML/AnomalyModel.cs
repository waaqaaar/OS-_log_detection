using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberSentra.ML
{
    public static class AnomalyModel
    {
        private static readonly MLContext _ml = new(seed: 1);

        // Normalize scoring rows using min/max learned from baseline rows
        public static void NormalizeUsingReference(List<UserFeatureRow> baseline, List<UserFeatureRow> target)
        {
            if (baseline.Count == 0) return;

            int dims = baseline[0].Features.Length;
            var min = new float[dims];
            var max = new float[dims];

            for (int j = 0; j < dims; j++)
            {
                min[j] = float.MaxValue;
                max[j] = float.MinValue;
            }

            foreach (var r in baseline)
            {
                for (int j = 0; j < dims; j++)
                {
                    min[j] = Math.Min(min[j], r.Features[j]);
                    max[j] = Math.Max(max[j], r.Features[j]);
                }
            }

            void norm(List<UserFeatureRow> rows)
            {
                foreach (var r in rows)
                {
                    for (int j = 0; j < dims; j++)
                    {
                        var denom = (max[j] - min[j]);
                        r.Features[j] = denom < 1e-6 ? 0f : (r.Features[j] - min[j]) / denom;
                    }
                }
            }

            norm(baseline);
            norm(target);
        }

        public static List<UserAnomaly> TrainOnBaselineScoreTarget(
            List<UserFeatureRow> baselineRows,
            List<UserFeatureRow> targetRows)
        {
            if (baselineRows.Count < 10 || targetRows.Count == 0)
            {
                return targetRows.Select(r => new UserAnomaly
                {
                    User = r.User,
                    Score = 0,
                    IsAnomaly = false
                }).ToList();
            }

            var baselineData = _ml.Data.LoadFromEnumerable(baselineRows);
            var targetData = _ml.Data.LoadFromEnumerable(targetRows);

            var pipeline = _ml.AnomalyDetection.Trainers.RandomizedPca(
                featureColumnName: nameof(UserFeatureRow.Features),
                rank: 3,
                ensureZeroMean: true
            );

            var model = pipeline.Fit(baselineData);

            var transformed = model.Transform(targetData);
            var preds = _ml.Data.CreateEnumerable<PcaPrediction>(transformed, reuseRowObject: false).ToList();

            var scored = targetRows.Zip(preds, (r, p) => new UserAnomaly
            {
                User = r.User,
                Score = (float.IsNaN(p.Score) || float.IsInfinity(p.Score)) ? 0f : p.Score,
                IsAnomaly = false
            }).ToList();

            // Robust detection: z-score on target scores
            var mean = scored.Average(x => x.Score);
            var std = Math.Sqrt(scored.Average(x => Math.Pow(x.Score - mean, 2)));
            if (std < 1e-6) std = 1e-6;

            foreach (var s in scored)
            {
                var z = (s.Score - mean) / std;
                s.IsAnomaly = z >= 2.0;
            }

            // fallback: mark top score if nothing flagged
            if (!scored.Any(x => x.IsAnomaly) && scored.Count >= 3)
                scored.OrderByDescending(x => x.Score).First().IsAnomaly = true;

            return scored.OrderByDescending(x => x.Score).ToList();
        }


        public static List<UserAnomaly> TrainBaselineScoreTarget(
    List<UserFeatureRow> baselineRows,
    List<UserFeatureRow> targetRows)
        {
            if (baselineRows.Count < 10 || targetRows.Count == 0)
                return targetRows.Select(r => new UserAnomaly { User = r.User, Score = 0, IsAnomaly = false }).ToList();

            // Normalize using baseline min/max so scoring is consistent
            NormalizeUsingBaselineMinMax(baselineRows, targetRows);

            var baselineData = _ml.Data.LoadFromEnumerable(baselineRows);
            var targetData = _ml.Data.LoadFromEnumerable(targetRows);

            var pipeline = _ml.AnomalyDetection.Trainers.RandomizedPca(
                featureColumnName: nameof(UserFeatureRow.Features),
                rank: 3,
                ensureZeroMean: true
            );

            var model = pipeline.Fit(baselineData);
            var transformed = model.Transform(targetData);

            var preds = _ml.Data.CreateEnumerable<PcaPrediction>(transformed, reuseRowObject: false).ToList();

            var scored = targetRows.Zip(preds, (r, p) => new UserAnomaly
            {
                User = r.User,
                Score = (float.IsNaN(p.Score) || float.IsInfinity(p.Score)) ? 0f : p.Score,
                IsAnomaly = false
            }).ToList();

            // Z-score detection on target scores
            var mean = scored.Average(x => x.Score);
            var std = Math.Sqrt(scored.Average(x => Math.Pow(x.Score - mean, 2)));
            if (std < 1e-6) std = 1e-6;

            foreach (var s in scored)
            {
                var z = (s.Score - mean) / std;
                s.IsAnomaly = z >= 2.0;
            }

            // fallback: mark top one if none flagged
            if (!scored.Any(x => x.IsAnomaly) && scored.Count >= 3)
                scored.OrderByDescending(x => x.Score).First().IsAnomaly = true;

            return scored.OrderByDescending(x => x.Score).ToList();
        }

        private static void NormalizeUsingBaselineMinMax(List<UserFeatureRow> baseline, List<UserFeatureRow> target)
        {
            if (baseline.Count == 0) return;

            int dims = baseline[0].Features.Length;
            var min = new float[dims];
            var max = new float[dims];

            for (int j = 0; j < dims; j++)
            {
                min[j] = float.MaxValue;
                max[j] = float.MinValue;
            }

            foreach (var r in baseline)
            {
                for (int j = 0; j < dims; j++)
                {
                    min[j] = Math.Min(min[j], r.Features[j]);
                    max[j] = Math.Max(max[j], r.Features[j]);
                }
            }

            void norm(List<UserFeatureRow> rows)
            {
                foreach (var r in rows)
                {
                    for (int j = 0; j < dims; j++)
                    {
                        var denom = max[j] - min[j];
                        r.Features[j] = denom < 1e-6 ? 0f : (r.Features[j] - min[j]) / denom;
                    }
                }
            }

            norm(baseline);
            norm(target);
        }

    }
}
