using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using RaftDistributionSystem.Enums;
using RaftDistributionSystem.Models;
using RaftDistributionSystem.Services;

public class Node
{
    private readonly string _nodeId;
    private readonly Network _network;
    private readonly List<string> _allNodeIds;
    private readonly List<string> _peers;

    // Raft state
    public NodeState State { get; private set; } = NodeState.Follower;
    public int CurrentTerm { get; private set; } = 0;
    private string _votedFor = null;
    private readonly List<LogEntry> _log = new();
    private int _commitIndex = -1;
    private int _lastApplied = -1;

    // Volatile state
    private string _leaderId = null;
    private DateTime _lastHeartbeat = DateTime.Now;

    // Leader state
    private readonly Dictionary<string, int> _nextIndex = new();
    private readonly Dictionary<string, int> _matchIndex = new();

    // Logging
    private readonly List<Dictionary<string, object>> _messageLog = new();
    private readonly List<Dictionary<string, object>> _stateLog = new();

    // Timers
    private double _electionTimeout;
    private double _heartbeatTimeout;
    private DateTime _lastHeartbeatSent;

    // Thread control
    private bool _running = true;
    private Thread _thread;
    private int _votesReceived = 0;

    public Node(string nodeId, Network network, List<string> allNodeIds)
    {
        _nodeId = nodeId;
        _network = network;
        _allNodeIds = allNodeIds;
        _peers = allNodeIds.Where(id => id != nodeId).ToList();

        ResetElectionTimer();
        Start();
    }

    private void Start()
    {
        _thread = new Thread(Run);
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread.Join();
    }

    private void ResetElectionTimer()
    {
        var random = new Random();
        _electionTimeout = random.Next(1500, 3000) / 1000.0;
        _lastHeartbeat = DateTime.Now;
    }

    private void ResetHeartbeatTimer()
    {
        _heartbeatTimeout = 0.5;
        _lastHeartbeatSent = DateTime.Now;
    }

    private void LogMessage(Message message, bool received)
    {
        _messageLog.Add(new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.Now,
            ["messageId"] = message.MessageId,
            ["type"] = message.Type.ToString(),
            ["sender"] = message.SenderId,
            ["receiver"] = message.ReceiverId,
            ["term"] = message.Term,
            ["data"] = message.Data,
            ["direction"] = received ? "in" : "out"
        });
    }

    private void LogStateTransition(NodeState oldState, NodeState newState)
    {
        _stateLog.Add(new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.Now,
            ["nodeId"] = _nodeId,
            ["oldState"] = oldState.ToString(),
            ["newState"] = newState.ToString(),
            ["term"] = CurrentTerm
        });
    }

    public void ChangeState(NodeState newState)
    {
        if (State != newState)
        {
            var oldState = State;
            State = newState;
            LogStateTransition(oldState, newState);

            switch (newState)
            {
                case NodeState.Leader:
                    BecomeLeader();
                    break;
                case NodeState.Follower:
                    BecomeFollower();
                    break;
                case NodeState.Candidate:
                    BecomeCandidate();
                    break;
            }
        }
    }

    private void BecomeFollower()
    {
        _leaderId = null;
        ResetElectionTimer();
    }

    private void BecomeCandidate()
    {
        CurrentTerm++;
        _votedFor = _nodeId;
        _leaderId = null;
        ResetElectionTimer();

        // Request votes from all peers
        var voteRequest = new Message(
            _nodeId,
            null, // Will be set when sending to each peer
            MessageType.RequestVote,
            CurrentTerm)
        {
            Data = new Dictionary<string, object>
            {
                ["lastLogIndex"] = _log.Count - 1,
                ["lastLogTerm"] = _log.Count > 0 ? _log[^1].Term : 0
            }
        };

        _votesReceived = 1; // Vote for self
        foreach (var peer in _peers)
        {
            voteRequest.ReceiverId = peer;
            _network.SendMessage(voteRequest);
            LogMessage(voteRequest, false);
        }

        // In candidate state:
        Console.WriteLine($"[Node {_nodeId}] Became candidate.");
    }

    private void BecomeLeader()
    {
        _leaderId = _nodeId;
        ResetHeartbeatTimer();

        // Initialize leader state
        foreach (var peer in _peers)
        {
            _nextIndex[peer] = _log.Count;
            _matchIndex[peer] = -1;
        }

        // Send initial empty AppendEntries
        SendHeartbeat();
    }

    private void SendHeartbeat()
    {
        if (State != NodeState.Leader) return;

        foreach (var peer in _peers)
        {
            var nextIdx = _nextIndex[peer];
            var prevLogIndex = nextIdx - 1;
            var prevLogTerm = prevLogIndex >= 0 ? _log[prevLogIndex].Term : 0;
            var entries = nextIdx < _log.Count ? _log.Skip(nextIdx).ToList() : new List<LogEntry>();

            var aeMessage = new Message(
                _nodeId,
                peer,
                MessageType.AppendEntries,
                CurrentTerm)
            {
                Data = new Dictionary<string, object>
                {
                    ["prevLogIndex"] = prevLogIndex,
                    ["prevLogTerm"] = prevLogTerm,
                    ["entries"] = entries,
                    ["leaderCommit"] = _commitIndex
                }
            };

            _network.SendMessage(aeMessage);
            LogMessage(aeMessage, false);
        }

        _lastHeartbeatSent = DateTime.Now;
    }

    private void HandleAppendEntries(Message message)
    {
        var success = false;

        // Reply false if term < currentTerm
        if (message.Term < CurrentTerm)
        {
            success = false;
        }
        else
        {
            // Reset election timer since we heard from a valid leader
            ResetElectionTimer();

            // If RPC request contains term T > currentTerm, set currentTerm = T
            if (message.Term > CurrentTerm)
            {
                CurrentTerm = message.Term;
                ChangeState(NodeState.Follower);
                _votedFor = null;
            }

            // Check log consistency
            var prevLogIndex = (int)message.Data["prevLogIndex"];
            var prevLogTerm = (int)message.Data["prevLogTerm"];

            if (prevLogIndex >= _log.Count ||
                (prevLogIndex >= 0 && _log[prevLogIndex].Term != prevLogTerm))
            {
                success = false;
            }
            else
            {
                // Delete conflicting entries and append new ones
                var entries = (List<LogEntry>)message.Data["entries"];
                if (entries.Count > 0)
                {
                    _log.RemoveRange(prevLogIndex + 1, _log.Count - (prevLogIndex + 1));
                    _log.AddRange(entries);
                }

                // Update commit index
                var leaderCommit = (int)message.Data["leaderCommit"];
                if (leaderCommit > _commitIndex)
                {
                    _commitIndex = Math.Min(leaderCommit, _log.Count - 1);
                }

                success = true;
            }

            // Update leader ID
            _leaderId = message.SenderId;
        }

        // Send response
        var response = new Message(
            _nodeId,
            message.SenderId,
            MessageType.AppendResponse,
            CurrentTerm)
        {
            Data = new Dictionary<string, object>
            {
                ["success"] = success,
                ["matchIndex"] = _log.Count - 1
            }
        };

        _network.SendMessage(response);
        LogMessage(response, false);
    }

    private void HandleRequestVote(Message message)
    {
        var voteGranted = false;

        if (message.Term < CurrentTerm)
        {
            voteGranted = false;
        }
        else
        {
            if (message.Term > CurrentTerm)
            {
                CurrentTerm = message.Term;
                ChangeState(NodeState.Follower);
                _votedFor = null;
            }

            var lastLogIndex = _log.Count - 1;
            var lastLogTerm = _log.Count > 0 ? _log[^1].Term : 0;
            var candidateLogOk =
                (int)message.Data["lastLogTerm"] > lastLogTerm ||
                ((int)message.Data["lastLogTerm"] == lastLogTerm &&
                 (int)message.Data["lastLogIndex"] >= lastLogIndex);

            if ((_votedFor == null || _votedFor == message.SenderId) && candidateLogOk)
            {
                _votedFor = message.SenderId;
                voteGranted = true;
                ResetElectionTimer();
            }
        }

        var response = new Message(
            _nodeId,
            message.SenderId,
            MessageType.VoteResponse,
            CurrentTerm)
        {
            Data = new Dictionary<string, object> { ["voteGranted"] = voteGranted }
        };

        Console.WriteLine($"[Node {_nodeId}] Voting {voteGranted} for {_votedFor} (Term {CurrentTerm})");
        _network.SendMessage(response);
        LogMessage(response, false);
    }

    private void HandleVoteResponse(Message message)
    {
        if (message.Term == CurrentTerm && State == NodeState.Candidate)
        {
            if ((bool)message.Data["voteGranted"])
            {
                _votesReceived++;

                // If votes from majority of servers, become leader
                if (_votesReceived > (decimal)_allNodeIds.Count / 2)
                {
                    Console.WriteLine($"[Node {_nodeId} got selected as leader with {_votesReceived} votes]");
                    ChangeState(NodeState.Leader);
                }
            }
        }
        else if (message.Term > CurrentTerm)
        {
            CurrentTerm = message.Term;
            ChangeState(NodeState.Follower);
            _votedFor = null;
        }
    }

    private void HandleAppendResponse(Message message)
    {
        if (message.Term > CurrentTerm)
        {
            CurrentTerm = message.Term;
            ChangeState(NodeState.Follower);
            _votedFor = null;
            return;
        }

        if (State != NodeState.Leader) return;

        var peer = message.SenderId;
        var success = (bool)message.Data["success"];
        var matchIndex = (int)message.Data["matchIndex"];

        if (success)
        {
            _nextIndex[peer] = matchIndex + 1;
            _matchIndex[peer] = matchIndex;

            // Update commit index
            UpdateCommitIndex();
        }
        else
        {
            _nextIndex[peer] = Math.Max(_nextIndex[peer] - 1, 0);
        }
    }

    private void UpdateCommitIndex()
    {
        // Find the highest index replicated on majority of servers
        var matchIndices = _matchIndex.Values.OrderBy(x => x).ToList();
        var newCommitIndex = matchIndices[_matchIndex.Count / 2];

        // Only commit entries from current term
        if (newCommitIndex > _commitIndex &&
            _log[newCommitIndex].Term == CurrentTerm)
        {
            _commitIndex = newCommitIndex;
        }
    }

    public bool ProposeNewValue(string command)
    {
        if (State != NodeState.Leader)
        {
            // Redirect to leader or forward the request
            if (_leaderId != null)
            {
                var message = new Message(
                    _nodeId,
                    _leaderId,
                    MessageType.ClientRequest,
                    CurrentTerm)
                {
                    Data = new Dictionary<string, object> { ["command"] = command }
                };

                _network.SendMessage(message);
                LogMessage(message, false);
            }
            return false;
        }

        // Append entry to local log
        var newEntry = new LogEntry(
            CurrentTerm,
            _log.Count,
            command);

        _log.Add(newEntry);

        // Replicate to followers
        SendHeartbeat();
        return true;
    }

    private void Run()
    {
        while (_running)
        {
            // Check for messages
            var message = _network.GetMessage(_nodeId);
            if (message != null)
            {
                LogMessage(message, true);

                switch (message.Type)
                {
                    case MessageType.AppendEntries:
                        HandleAppendEntries(message);
                        break;
                    case MessageType.RequestVote:
                        HandleRequestVote(message);
                        break;
                    case MessageType.VoteResponse:
                        HandleVoteResponse(message);
                        break;
                    case MessageType.AppendResponse:
                        HandleAppendResponse(message);
                        break;
                    case MessageType.ClientRequest when State == NodeState.Leader:
                        ProposeNewValue(message.Data["command"].ToString());
                        break;
                }
            }

            // State-specific behavior
            switch (State)
            {
                case NodeState.Follower:
                    // Check election timeout
                    if ((DateTime.Now - _lastHeartbeat).TotalSeconds > _electionTimeout)
                    {
                        ChangeState(NodeState.Candidate);
                    }
                    break;

                case NodeState.Candidate:
                    // Check election timeout
                    if ((DateTime.Now - _lastHeartbeat).TotalSeconds > _electionTimeout)
                    {
                        BecomeCandidate();
                    }
                    break;

                case NodeState.Leader:
                    // Send periodic heartbeats
                    if ((DateTime.Now - _lastHeartbeatSent).TotalSeconds > _heartbeatTimeout)
                    {
                        SendHeartbeat();
                    }
                    break;
            }

            // Apply committed entries to state machine (simplified)
            while (_lastApplied < _commitIndex)
            {
                _lastApplied++;
                var entry = _log[_lastApplied];
                // In a real system, we would apply the command to the state machine here
            }

            Thread.Sleep(10);
        }
    }

    public List<Dictionary<string, object>> GetMessageLog() => _messageLog;
    public List<Dictionary<string, object>> GetStateLog() => _stateLog;

    public Dictionary<string, object> GetCurrentState() => new()
    {
        ["nodeId"] = _nodeId,
        ["state"] = State.ToString(),
        ["term"] = CurrentTerm,
        ["commitIndex"] = _commitIndex,
        ["lastApplied"] = _lastApplied,
        ["logLength"] = _log.Count,
        ["leaderId"] = _leaderId
    };
}