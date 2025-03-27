using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftDistributionSystem.Models
{
    public class LogEntry
    {
        public int Term { get; set; }
        public int Index { get; set; }
        public string Command { get; set; }

        public LogEntry(int term, int index, string command)
        {
            Term = term;
            Index = index;
            Command = command;
        }
    }
}
