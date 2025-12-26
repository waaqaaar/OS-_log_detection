using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.ML.Data;

namespace CyberSentra.ML
{
    public class UserFeatureRow
    {
        public string User { get; set; } = "Unknown";

        // Must be fixed-length for ML.NET
      
        [VectorType(6)]
        public float[] Features { get; set; } = new float[6];

    }

    public class PcaPrediction
    {
        public bool PredictedLabel { get; set; }   // provided by trainer (may not be perfect)
        public float Score { get; set; }           // higher usually = more anomalous
    }

    public class UserAnomaly
    {
        public string User { get; set; } = "Unknown";
        public float Score { get; set; }
        public bool IsAnomaly { get; set; }
    }
}

