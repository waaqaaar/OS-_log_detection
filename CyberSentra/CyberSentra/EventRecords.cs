using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberSentra
{
    public class EventRecord
    {
        public string Time { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Process { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
    }
}
