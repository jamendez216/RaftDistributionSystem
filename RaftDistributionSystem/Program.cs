using RaftDistributionSystem.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

class Program
{
    static void Main()
    {
        /*// Test 1: Basic leader election
        TestLeaderElection();

        // Test 2: Value consensus
        TestValueConsensus();

        // Test 3: Leader failure and re-election
        TestLeaderFailure();

        // Test 4: Network partition
        TestNetworkPartition();

        // Test 5: Log consistency after failures
        TestLogConsistency();*/
        Console.WriteLine("Distributed Consensus System Tester");
        Console.WriteLine("==================================");

        var tester = new InteractiveTester();
        tester.Run();
    }

    static void TestLeaderElection()
    {
        Console.WriteLine("=== Testing Leader Election ===");
        using var system = new DistributedSystem(3);
        Thread.Sleep(3000); // Allow time for election

        var leaders = new List<Node>();
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader)
            {
                leaders.Add(node);
            }
        }

        Console.WriteLine(leaders.Count == 1
            ? $"✓ Leader elected: {leaders[0].GetCurrentState()["nodeId"]}"
            : "✗ Should have exactly one leader");
    }

    static void TestValueConsensus()
    {
        Console.WriteLine("\n=== Testing Value Consensus ===");
        using var system = new DistributedSystem(3);
        Thread.Sleep(3000); // Allow time for election

        Node leader = null;
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader)
            {
                leader = node;
                break;
            }
        }

        if (leader == null)
        {
            Console.WriteLine("✗ No leader found");
            return;
        }

        var success = leader.ProposeNewValue("SET X=5");
        Console.WriteLine(success
            ? "✓ Leader accepted proposal"
            : "✗ Leader should accept proposal");

        Thread.Sleep(1000); // Allow time for replication

        // Verify all nodes have the same log length
        var logLengths = new List<int>();
        foreach (var node in system.GetNodes())
        {
            logLengths.Add((int)node.GetCurrentState()["logLength"]);
        }

        var allSame = true;
        for (int i = 1; i < logLengths.Count; i++)
        {
            if (logLengths[i] != logLengths[0])
            {
                allSame = false;
                break;
            }
        }

        Console.WriteLine(allSame
            ? "✓ All logs have same length"
            : "✗ Logs should be same length");
    }

    static void TestLeaderFailure()
    {
        Console.WriteLine("\n=== Testing Leader Failure ===");
        using var system = new DistributedSystem(5);
        Thread.Sleep(3000); // Allow time for election

        Node leader = null;
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader)
            {
                leader = node;
                break;
            }
        }

        if (leader == null)
        {
            Console.WriteLine("✗ No leader found");
            return;
        }

        Console.WriteLine($"Current leader: {leader.GetCurrentState()["nodeId"]}");

        // Fail the leader
        system.FailNode(leader.GetCurrentState()["nodeId"].ToString());
        Thread.Sleep(3000); // Allow time for new election

        // Verify new leader was elected
        Node newLeader = null;
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader &&
                node.GetCurrentState()["nodeId"].ToString() != leader.GetCurrentState()["nodeId"].ToString())
            {
                newLeader = node;
                break;
            }
        }

        Console.WriteLine(newLeader != null
            ? $"✓ New leader elected: {newLeader.GetCurrentState()["nodeId"]}"
            : "✗ Should have new leader after failure");
    }

    static void TestNetworkPartition()
    {
        Console.WriteLine("\n=== Testing Network Partition ===");
        using var system = new DistributedSystem(5);
        Thread.Sleep(3000); // Allow time for election

        Node leader = null;
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader)
            {
                leader = node;
                break;
            }
        }

        if (leader == null)
        {
            Console.WriteLine("✗ No leader found");
            return;
        }

        Console.WriteLine($"Current leader: {leader.GetCurrentState()["nodeId"]}");

        // Create partition - isolate leader
        var leaderId = leader.GetCurrentState()["nodeId"].ToString();
        var majorityNodes = new List<string>();
        foreach (var node in system.GetNodes())
        {
            var nodeId = node.GetCurrentState()["nodeId"].ToString();
            if (nodeId != leaderId)
            {
                majorityNodes.Add(nodeId);
            }
        }

        system.CreatePartition(new List<string> { leaderId }, majorityNodes);
        Thread.Sleep(3000); // Allow time for new election

        // Majority partition should elect new leader
        Node newLeader = null;
        foreach (var node in system.GetNodes())
        {
            var nodeId = node.GetCurrentState()["nodeId"].ToString();
            if (node.State == NodeState.Leader && nodeId != leaderId && majorityNodes.Contains(nodeId))
            {
                newLeader = node;
                break;
            }
        }

        Console.WriteLine(newLeader != null
            ? $"✓ New leader in majority partition: {newLeader.GetCurrentState()["nodeId"]}"
            : "✗ Majority partition should elect new leader");

        // Heal partition
        system.HealPartition();
        Thread.Sleep(3000); // Allow time for stabilization

        // Verify only one leader remains
        var leaders = new List<Node>();
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader)
            {
                leaders.Add(node);
            }
        }

        Console.WriteLine(leaders.Count == 1
            ? $"✓ After partition heals, one leader remains: {leaders[0].GetCurrentState()["nodeId"]}"
            : "✗ After partition heals, only one leader should remain");
    }

    static void TestLogConsistency()
    {
        Console.WriteLine("\n=== Testing Log Consistency ===");
        using var system = new DistributedSystem(5);
        Thread.Sleep(3000); // Allow time for election

        // Propose several values
        Node leader = null;
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader)
            {
                leader = node;
                break;
            }
        }

        if (leader == null)
        {
            Console.WriteLine("✗ No leader found");
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            var success = leader.ProposeNewValue($"SET X={i}");
            Console.WriteLine(success ? $"✓ Proposed SET X={i}" : $"✗ Failed to propose SET X={i}");
            Thread.Sleep(500);
        }

        // Fail leader and wait for new election
        system.FailNode(leader.GetCurrentState()["nodeId"].ToString());
        Thread.Sleep(3000);

        // Propose more values to new leader
        Node newLeader = null;
        foreach (var node in system.GetNodes())
        {
            if (node.State == NodeState.Leader &&
                node.GetCurrentState()["nodeId"].ToString() != leader.GetCurrentState()["nodeId"].ToString())
            {
                newLeader = node;
                break;
            }
        }

        if (newLeader == null)
        {
            Console.WriteLine("✗ No new leader found after failure");
            return;
        }

        for (int i = 5; i < 10; i++)
        {
            var success = newLeader.ProposeNewValue($"SET X={i}");
            Console.WriteLine(success ? $"✓ Proposed SET X={i}" : $"✗ Failed to propose SET X={i}");
            Thread.Sleep(500);
        }

        // Verify all nodes have consistent logs (may take time for catch-up)
        Thread.Sleep(2000);
        var logLengths = new List<int>();
        foreach (var node in system.GetNodes())
        {
            var nodeId = node.GetCurrentState()["nodeId"].ToString();
            if (nodeId != leader.GetCurrentState()["nodeId"].ToString())
            {
                logLengths.Add((int)node.GetCurrentState()["logLength"]);
            }
        }

        var allSame = true;
        for (int i = 1; i < logLengths.Count; i++)
        {
            if (logLengths[i] != logLengths[0])
            {
                allSame = false;
                break;
            }
        }

        Console.WriteLine(allSame
            ? "✓ All logs consistent in majority partition"
            : "✗ Logs should be consistent");

        // Recover old leader and verify it catches up
        system.RecoverNode(leader.GetCurrentState()["nodeId"].ToString());
        Thread.Sleep(5000); // Allow time for catch-up

        var recoveredNode = system.GetNode(leader.GetCurrentState()["nodeId"].ToString());
        var recoveredLogLength = (int)recoveredNode.GetCurrentState()["logLength"];

        Console.WriteLine(recoveredLogLength == logLengths[0]
            ? "✓ Recovered node caught up with log"
            : "✗ Recovered node should catch up with log");
    }


}