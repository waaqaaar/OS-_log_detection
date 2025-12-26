using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberSentra
{
    public class UserRiskInfo
    {
        public string UserName { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RiskScore { get; set; }
        public int Anomalies { get; set; }
    }
}
