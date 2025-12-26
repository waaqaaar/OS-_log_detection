using CyberSentra.Database;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace CyberSentra.Database
{
    public static class EventRepository
    {
        public static void SaveEvents(IEnumerable<EventRecord> events)
        {
            using var conn = DatabaseContext.GetConnection();

            using var tx = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = @"
INSERT INTO Events (Time, Type, Severity, User, Process, Details, Source)
VALUES ($time, $type, $severity, $user, $process, $details, $source);
";

            var pTime = cmd.CreateParameter(); pTime.ParameterName = "$time"; cmd.Parameters.Add(pTime);
            var pType = cmd.CreateParameter(); pType.ParameterName = "$type"; cmd.Parameters.Add(pType);
            var pSev = cmd.CreateParameter(); pSev.ParameterName = "$severity"; cmd.Parameters.Add(pSev);
            var pUser = cmd.CreateParameter(); pUser.ParameterName = "$user"; cmd.Parameters.Add(pUser);
            var pProc = cmd.CreateParameter(); pProc.ParameterName = "$process"; cmd.Parameters.Add(pProc);
            var pDet = cmd.CreateParameter(); pDet.ParameterName = "$details"; cmd.Parameters.Add(pDet);
            var pSrc = cmd.CreateParameter(); pSrc.ParameterName = "$source"; cmd.Parameters.Add(pSrc);

            foreach (var e in events)
            {
                pTime.Value = e.Time ?? "";
                pType.Value = e.Type ?? "";
                pSev.Value = e.Severity ?? "";
                pUser.Value = e.User ?? "";
                pProc.Value = e.Process ?? "";
                pDet.Value = e.Details ?? "";
                pSrc.Value = e.Source ?? "";

                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
        public static void ReplaceEvents(IEnumerable<EventRecord> events)
        {
            using var conn = DatabaseContext.GetConnection();
            using var tx = conn.BeginTransaction();

            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM Events;";
                del.ExecuteNonQuery();
            }

            // now bulk insert with the fast SaveEvents pattern (or inline it here)
            // easiest: call SaveEvents but it creates its own tx, so instead paste SaveEvents logic here
            tx.Commit();
        }


        public static List<EventRecord> LoadAllEvents()
        {
            var list = new List<EventRecord>();

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT Time,Type,Severity,User,Process,Details,Source FROM Events ORDER BY Id DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EventRecord
                {
                    Time = reader.GetString(0),
                    Type = reader.GetString(1),
                    Severity = reader.GetString(2),
                    User = reader.GetString(3),
                    Process = reader.GetString(4),
                    Details = reader.GetString(5),
                    Source = reader.GetString(6)
                });
            }

            return list;
        }
        public static List<EventRecord> GetEventsSince(DateTime sinceUtc)
        {
            var list = new List<EventRecord>();

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
            SELECT Time,Type,Severity,User,Process,Details,Source
            FROM Events
            WHERE Time >= $since
            ORDER BY Id DESC;
            ";
            cmd.Parameters.AddWithValue("$since", sinceUtc.ToString("o"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EventRecord
                {
                    Time = reader.GetString(0),
                    Type = reader.GetString(1),
                    Severity = reader.GetString(2),
                    User = reader.GetString(3),
                    Process = reader.GetString(4),
                    Details = reader.GetString(5),
                    Source = reader.GetString(6)
                });
            }

            return list;
        }
        public static List<EventRecord> LoadEventsForUserWindow(string user, DateTime start, DateTime end)
        {
            var list = new List<EventRecord>();

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT Time,Type,Severity,User,Process,Details,Source
FROM Events
WHERE User = $user
  AND Time >= $start
  AND Time <  $end
ORDER BY Time DESC
LIMIT 500;
";
            cmd.Parameters.AddWithValue("$user", user);
            cmd.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$end", end.ToString("yyyy-MM-dd HH:mm:ss"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EventRecord
                {
                    Time = reader.GetString(0),
                    Type = reader.GetString(1),
                    Severity = reader.GetString(2),
                    User = reader.GetString(3),
                    Process = reader.GetString(4),
                    Details = reader.GetString(5),
                    Source = reader.GetString(6)
                });
            }

            return list;
        }

    }
}
