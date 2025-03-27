using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftDistributionSystem.Enums
{
    public enum MessageType
    {
        AppendEntries,
        RequestVote,
        VoteResponse,
        AppendResponse,
        ClientRequest
    }
}
