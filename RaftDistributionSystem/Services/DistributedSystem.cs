using RaftDistributionSystem.Services;
using System.Collections.Concurrent;

public class DistributedSystem : IDisposable
{
    private readonly Network _network = new();
    public readonly ConcurrentDictionary<string, Node> _nodes = new();
    private readonly List<string> _nodeIds;

    public DistributedSystem(int nodeCount = 3)
    {
        _nodeIds = Enumerable.Range(0, nodeCount).Select(i => i.ToString()).ToList();

        foreach (var nodeId in _nodeIds)
        {
            _network.RegisterNode(nodeId);
            _nodes.TryAdd(nodeId, new Node(nodeId, _network, _nodeIds));
        }
    }

    public void Dispose()
    {
        foreach (var node in _nodes.Values)
        {
            node.Stop();
        }
    }

    public Node GetNode(string nodeId) => _nodes.TryGetValue(nodeId, out var node) ? node : null;

    public void CreatePartition(List<string> group1, List<string> group2)
        => _network.CreatePartition(group1, group2);

    public void HealPartition() => _network.HealPartition();

    public void FailNode(string nodeId) => _network.FailNode(nodeId);

    public void RecoverNode(string nodeId) => _network.RecoverNode(nodeId);

    public bool ProposeValue(string nodeId, string value)
    {
        var node = GetNode(nodeId);
        return node?.ProposeNewValue(value) ?? false;
    }

    public Dictionary<string, Dictionary<string, object>> GetLogs()
    {
        return _nodes.ToDictionary(
            kvp => kvp.Key,
            kvp => new Dictionary<string, object>
            {
                ["messages"] = kvp.Value.GetMessageLog(),
                ["stateTransitions"] = kvp.Value.GetStateLog(),
                ["currentState"] = kvp.Value.GetCurrentState()
            });
    }
    // Helper method to get all nodes
    public IEnumerable<Node> GetNodes()
    {
        return _nodes.Values;
    }

}