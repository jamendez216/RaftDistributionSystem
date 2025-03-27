using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public class InteractiveTester
{
    private DistributedSystem _system;
    private bool _running = true;

    public void Run()
    {
        Console.WriteLine("Initializing distributed system with 5 nodes...");
        _system = new DistributedSystem(5);

        // Wait for initial leader election
        Thread.Sleep(3000);

        while (_running)
        {
            PrintMenu();
            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    ShowSystemStatus();
                    break;

                case "2":
                    ProposeValue();
                    break;

                case "3":
                    FailRandomNode();
                    break;

                case "4":
                    RecoverNode();
                    break;

                case "5":
                    CreatePartition();
                    break;

                case "6":
                    HealPartition();
                    break;

                case "7":
                    RunChaosTest();
                    break;

                case "8":
                    ShowLogs();
                    break;

                case "9":
                    _running = false;
                    break;

                default:
                    Console.WriteLine("Invalid option");
                    break;
            }
        }

        _system.Dispose();
        Console.WriteLine("System shut down");
    }

    private void PrintMenu()
    {
        Console.WriteLine("\n=== Distributed System Tester ===");
        Console.WriteLine("1. Show system status");
        Console.WriteLine("2. Propose new value");
        Console.WriteLine("3. Fail random node");
        Console.WriteLine("4. Recover node");
        Console.WriteLine("5. Create network partition");
        Console.WriteLine("6. Heal network partition");
        Console.WriteLine("7. Run chaos test");
        Console.WriteLine("8. Show logs");
        Console.WriteLine("9. Exit");
        Console.Write("Select an option: ");
    }

    private void ShowSystemStatus()
    {
        Console.WriteLine("\nCurrent System Status:");
        Console.WriteLine("{0,-10} {1,-15} {2,-10} {3,-10} {4,-10}",
            "Node", "State", "Term", "Log Len", "Leader");

        foreach (var node in _system.GetNodes())
        {
            var state = node.GetCurrentState();
            Console.WriteLine("{0,-10} {1,-15} {2,-10} {3,-10} {4,-10}",
                state["nodeId"],
                state["state"],
                state["term"],
                state["logLength"],
                state["leaderId"] ?? "None");
        }
    }

    private void ProposeValue()
    {
        Console.Write("\nEnter node ID to propose through: ");
        var nodeId = Console.ReadLine();

        Console.Write("Enter value to propose: ");
        var value = Console.ReadLine();

        var success = _system.ProposeValue(nodeId, value);
        Console.WriteLine(success ? "✓ Value proposed successfully" : "✗ Failed to propose value");
    }

    private void FailRandomNode()
    {
        var operationalNodes = _system.GetNodes()
            .Where(n => n.GetCurrentState()["state"].ToString() != "Follower" ||
                       n.GetCurrentState()["state"].ToString() != "Candidate")
            .ToList();

        if (!operationalNodes.Any())
        {
            Console.WriteLine("No operational nodes available to fail");
            return;
        }

        var random = new Random();
        var nodeToFail = operationalNodes[random.Next(operationalNodes.Count)];
        var nodeId = nodeToFail.GetCurrentState()["nodeId"].ToString();

        Console.WriteLine($"\nFailing node {nodeId}...");
        _system.FailNode(nodeId);

        Thread.Sleep(1000);
        ShowSystemStatus();
    }

    private void RecoverNode()
    {
        var failedNodes = _system.GetNodes()
            .Where(n => n.GetCurrentState()["state"].ToString() == "Follower" &&
                       n.GetCurrentState()["leaderId"] == null)
            .ToList();

        if (!failedNodes.Any())
        {
            Console.WriteLine("No failed nodes available to recover");
            return;
        }

        Console.WriteLine("\nFailed nodes:");
        foreach (var node in failedNodes)
        {
            Console.WriteLine(node.GetCurrentState()["nodeId"]);
        }

        Console.Write("Enter node ID to recover: ");
        var nodeId = Console.ReadLine();

        _system.RecoverNode(nodeId);
        Console.WriteLine($"Node {nodeId} recovered");

        Thread.Sleep(1000);
        ShowSystemStatus();
    }

    private void CreatePartition()
    {
        Console.WriteLine("\nCurrent nodes:");
        foreach (var node in _system.GetNodes())
        {
            Console.WriteLine(node.GetCurrentState()["nodeId"]);
        }

        Console.Write("Enter first group nodes (comma separated): ");
        var group1 = Console.ReadLine().Split(',').Select(s => s.Trim()).ToList();

        Console.Write("Enter second group nodes (comma separated): ");
        var group2 = Console.ReadLine().Split(',').Select(s => s.Trim()).ToList();

        _system.CreatePartition(group1, group2);
        Console.WriteLine($"Partition created between group1 [{string.Join(",", group1)}] and group2 [{string.Join(",", group2)}]");

        Thread.Sleep(2000);
        ShowSystemStatus();
    }

    private void HealPartition()
    {
        _system.HealPartition();
        Console.WriteLine("\nNetwork partition healed");

        Thread.Sleep(2000);
        ShowSystemStatus();
    }

    private void RunChaosTest()
    {
        Console.Write("\nEnter test duration (seconds): ");
        var duration = int.Parse(Console.ReadLine());

        var endTime = DateTime.Now.AddSeconds(duration);
        var random = new Random();

        Console.WriteLine($"Running chaos test for {duration} seconds...");

        while (DateTime.Now < endTime)
        {
            var action = random.Next(4);

            switch (action)
            {
                case 0: // Fail random node
                    var operationalNodes = _system.GetNodes().ToList();
                    if (operationalNodes.Any())
                    {
                        var node = operationalNodes[random.Next(operationalNodes.Count)];
                        _system.FailNode(node.GetCurrentState()["nodeId"].ToString());
                        Console.WriteLine($"Chaos: Failed node {node.GetCurrentState()["nodeId"]}");
                    }
                    break;

                case 1: // Recover random node
                    var failedNodes = _system.GetNodes()
                        .Where(n => n.GetCurrentState()["state"].ToString() == "Follower" &&
                                   n.GetCurrentState()["leaderId"] == null)
                        .ToList();
                    if (failedNodes.Any())
                    {
                        var node = failedNodes[random.Next(failedNodes.Count)];
                        _system.RecoverNode(node.GetCurrentState()["nodeId"].ToString());
                        Console.WriteLine($"Chaos: Recovered node {node.GetCurrentState()["nodeId"]}");
                    }
                    break;

                case 2: // Create random partition
                    var allNodes = _system.GetNodes().Select(n => n.GetCurrentState()["nodeId"].ToString()).ToList();
                    if (allNodes.Count > 1)
                    {
                        var groupSize = random.Next(1, allNodes.Count);
                        var group1 = allNodes.OrderBy(x => random.Next()).Take(groupSize).ToList();
                        var group2 = allNodes.Except(group1).ToList();
                        _system.CreatePartition(group1, group2);
                        Console.WriteLine($"Chaos: Created partition between {string.Join(",", group1)} and {string.Join(",", group2)}");
                    }
                    break;

                case 3: // Heal partition
                    _system.HealPartition();
                    Console.WriteLine("Chaos: Healed partition");
                    break;
            }

            Thread.Sleep(random.Next(1000, 3000));
        }

        Console.WriteLine("Chaos test completed");
        ShowSystemStatus();
    }

    private void ShowLogs()
    {
        Console.WriteLine("\nSelect log type:");
        Console.WriteLine("1. Message logs");
        Console.WriteLine("2. State transition logs");
        Console.Write("Enter choice: ");

        var choice = Console.ReadLine();
        var logs = _system.GetLogs();

        if (choice == "1")
        {
            foreach (var nodeLog in logs)
            {
                Console.WriteLine($"\nMessages for Node {nodeLog.Key}:");
                foreach (var msg in (List<Dictionary<string, object>>)nodeLog.Value["messages"])
                {
                    Console.WriteLine($"[{msg["timestamp"]}] {msg["direction"]} {msg["type"]} " +
                        $"from {msg["sender"]} to {msg["receiver"]} (term {msg["term"]})");
                }
            }
        }
        else if (choice == "2")
        {
            foreach (var nodeLog in logs)
            {
                Console.WriteLine($"\nState transitions for Node {nodeLog.Key}:");
                foreach (var transition in (List<Dictionary<string, object>>)nodeLog.Value["stateTransitions"])
                {
                    Console.WriteLine($"[{transition["timestamp"]}] {transition["oldState"]} -> " +
                        $"{transition["newState"]} (term {transition["term"]})");
                }
            }
        }
    }
}