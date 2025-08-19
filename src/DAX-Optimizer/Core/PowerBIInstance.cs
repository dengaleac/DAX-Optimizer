using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX_Optimizer.Core
{
    public class PowerBIInstance
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public int Port { get; set; }
        public string Server { get; set; }
        public DateTime StartTime { get; set; }
        public string WindowTitle { get; set; }
        public List<string> Databases { get; set; } = new List<string>();
        public string DefaultDatabase { get; set; }

        public override string ToString()
        {
            return $"{WindowTitle} - {Server}";
        }
    }
}
