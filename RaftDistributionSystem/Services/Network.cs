using RaftDistributionSystem.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftDistributionSystem.Services
{
    public class Network
    {
        private readonly ConcurrentDictionary<string, BlockingCollection<Message>> _queues = new();
        private readonly ConcurrentDictionary<(string, string), bool> _partitions = new();
        private readonly ConcurrentBag<string> _failedNodes = new();

        public void RegisterNode(string nodeId)
        {
            _queues.TryAdd(nodeId, new BlockingCollection<Message>());
        }

        public void SendMessage(Message message)
        {
            if (_failedNodes.Contains(message.SenderId)) return;
            if (_failedNodes.Contains(message.ReceiverId)) return;
            if (_partitions.TryGetValue((message.SenderId, message.ReceiverId), out var partitioned) && partitioned) return;
            if (_partitions.TryGetValue((message.ReceiverId, message.SenderId), out partitioned) && partitioned) return;

            if (_queues.TryGetValue(message.ReceiverId, out var queue))
            {
                queue.Add(message);
            }
        }

        public Message GetMessage(string nodeId, int timeoutMs = 100)
        {
            if (_queues.TryGetValue(nodeId, out var queue))
            {
                return queue.TryTake(out var message, timeoutMs) ? message : null;
            }
            return null;
        }

        public void CreatePartition(List<string> group1, List<string> group2)
        {
            foreach (var node1 in group1)
            {
                foreach (var node2 in group2)
                {
                    _partitions.AddOrUpdate((node1, node2), true, (_, _) => true);
                }
            }
        }

        public void HealPartition()
        {
            _partitions.Clear();
        }

        public void FailNode(string nodeId)
        {
            _failedNodes.Add(nodeId);
        }

        public void RecoverNode(string nodeId)
        {
            _failedNodes.TryTake(out _);
        }
    }
}
