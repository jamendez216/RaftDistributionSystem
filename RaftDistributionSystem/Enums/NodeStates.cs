using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftDistributionSystem.Enums
{
    public enum NodeState
    {
        Follower,
        Candidate,
        Leader
    }
}
