using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberSentra
{
    public class ThreatInfo
    {
        public string Time { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Technique { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Tactic { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
