using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace CyberSentra.Database
{
    public static class MlAnomalyRepository
    {

        public static void SaveBatch(DateTime createdUtc, IEnumerable<MlAnomalyRow> rows)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();

            foreach (var r in rows)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                cmd.CommandText = @"
INSERT OR IGNORE INTO MlAnomalies
(RunKey, CreatedUtc, UserWindow, Score, IsAnomaly,
 F0_TotalEvents, F1_FailedLogins, F2_ErrorsFailures, F3_Warnings, F4_UniqueProcesses, F5_UniqueSources)
VALUES
($runKey, $createdUtc, $userWindow, $score, $isAnomaly,
 $f0, $f1, $f2, $f3, $f4, $f5);
";
                cmd.Parameters.AddWithValue("$runKey", r.RunKey);


                cmd.Parameters.AddWithValue("$createdUtc", createdUtc.ToString("o"));
                cmd.Parameters.AddWithValue("$userWindow", r.UserWindow);
                cmd.Parameters.AddWithValue("$score", r.Score);
                cmd.Parameters.AddWithValue("$isAnomaly", r.IsAnomaly ? 1 : 0);

                cmd.Parameters.AddWithValue("$f0", r.F0_TotalEvents);
                cmd.Parameters.AddWithValue("$f1", r.F1_FailedLogins);
                cmd.Parameters.AddWithValue("$f2", r.F2_ErrorsFailures);
                cmd.Parameters.AddWithValue("$f3", r.F3_Warnings);
                cmd.Parameters.AddWithValue("$f4", r.F4_UniqueProcesses);
                cmd.Parameters.AddWithValue("$f5", r.F5_UniqueSources);

                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public static List<MlAnomalyRow> LoadRecent(int limit = 500)
        {
            var list = new List<MlAnomalyRow>();

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT RunKey, CreatedUtc, UserWindow, Score, IsAnomaly,
       F0_TotalEvents, F1_FailedLogins, F2_ErrorsFailures, F3_Warnings, F4_UniqueProcesses, F5_UniqueSources
FROM MlAnomalies
ORDER BY Id DESC
LIMIT $limit;
";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MlAnomalyRow
                {
                    CreatedUtc = reader.GetString(0),
                    UserWindow = reader.GetString(1),
                    Score = reader.GetDouble(2),
                    IsAnomaly = reader.GetInt32(3) == 1,

                    F0_TotalEvents = reader.GetDouble(4),
                    F1_FailedLogins = reader.GetDouble(5),
                    F2_ErrorsFailures = reader.GetDouble(6),
                    F3_Warnings = reader.GetDouble(7),
                    F4_UniqueProcesses = reader.GetDouble(8),
                    F5_UniqueSources = reader.GetDouble(9),
                });
            }

            return list;
        }
        public static List<string> LoadRecentRunKeys(int limit = 48)
        {
            var list = new List<string>();

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT RunKey
FROM MlAnomalies
GROUP BY RunKey
ORDER BY RunKey DESC
LIMIT $limit;
";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(r.GetString(0));

            return list;
        }

        public static List<MlAnomalyRow> LoadByRunKey(string runKey)
        {
            var list = new List<MlAnomalyRow>();

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT RunKey, CreatedUtc, UserWindow, Score, IsAnomaly,
       F0_TotalEvents, F1_FailedLogins, F2_ErrorsFailures, F3_Warnings, F4_UniqueProcesses, F5_UniqueSources
FROM MlAnomalies
WHERE RunKey = $runKey
ORDER BY Score DESC;
";
            cmd.Parameters.AddWithValue("$runKey", runKey);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MlAnomalyRow
                {
                    RunKey = reader.GetString(0),
                    CreatedUtc = reader.GetString(1),
                    UserWindow = reader.GetString(2),
                    Score = reader.GetDouble(3),
                    IsAnomaly = reader.GetInt32(4) == 1,
                    F0_TotalEvents = reader.GetDouble(5),
                    F1_FailedLogins = reader.GetDouble(6),
                    F2_ErrorsFailures = reader.GetDouble(7),
                    F3_Warnings = reader.GetDouble(8),
                    F4_UniqueProcesses = reader.GetDouble(9),
                    F5_UniqueSources = reader.GetDouble(10),
                });
            }

            return list;
        }

        public static List<(string RunKey, int AnomalyCount, double MaxScore)> LoadRunStats(int limit = 72)
        {
            var list = new List<(string, int, double)>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT RunKey,
       SUM(CASE WHEN IsAnomaly = 1 THEN 1 ELSE 0 END) AS AnomCount,
       MAX(Score) AS MaxScore
FROM MlAnomalies
GROUP BY RunKey
ORDER BY RunKey DESC
LIMIT $limit;
";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var runKey = r.GetString(0);
                var anom = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                var max = r.IsDBNull(2) ? 0 : r.GetDouble(2);
                list.Add((runKey, anom, max));
            }

            return list;
        }

    }

    public class MlAnomalyRow
    {
        public string RunKey { get; set; } = "";

        public string CreatedUtc { get; set; } = "";
        public string UserWindow { get; set; } = "";
        public double Score { get; set; }
        public bool IsAnomaly { get; set; }

        public double F0_TotalEvents { get; set; }
        public double F1_FailedLogins { get; set; }
        public double F2_ErrorsFailures { get; set; }
        public double F3_Warnings { get; set; }
        public double F4_UniqueProcesses { get; set; }
        public double F5_UniqueSources { get; set; }
    }
}
