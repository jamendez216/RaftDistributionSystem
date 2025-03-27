using RaftDistributionSystem.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftDistributionSystem.Models
{
    public class Message
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public MessageType Type { get; set; }
        public int Term { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public string MessageId { get; set; }

        public Message(string senderId, string receiverId, MessageType type, int term)
        {
            SenderId = senderId;
            ReceiverId = receiverId;
            Type = type;
            Term = term;
            Data = new Dictionary<string, object>();
            MessageId = Guid.NewGuid().ToString();
        }
    }
}
