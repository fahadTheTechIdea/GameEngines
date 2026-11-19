using System.Globalization;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// The dialogue rules as one game component. A request event (start, advance, choose, skip, history, quests, reload) only queues; the component
/// answers it inside its own Update, in the order the requests arrived, with events. Nothing in a conversation changes outside Update,
/// and the component reads no clock: the tick of that Update is written on the answers and on the history entries the call keeps. The facts a document tests that the world
/// owns (<c>ext.*</c>) come from the host's delegate at the moment of each answer.
/// </summary>
/// <remarks>
/// One component holds one session with at most one open conversation (plus the interruptions, preemptions and calls inside it, and the conversations queued behind it). A game that talks in more than one place at once adds a component for each, with
/// its own id and its own <paramref name="channel"/>; a component answers the requests of its channel only. A request is answered by exactly one component: the one that serves its channel, or, when
/// the registry (<see cref="DialogueChannels"/>, which every component is built with and which must be installed on the session) refuses it with <see cref="DialogueChannels.UnknownChannel"/> when nobody serves the channel.
/// </remarks>
public sealed partial class DialogueComponent(DialogueRuntime runtime, ISpeakerCatalog speakers, Func<IReadOnlyDictionary<string, FactValue>> facts, DialogueChannels channels, string id = "dialogue", string channel = "") : IGameComponent
{
    public const string NotQuiet = "DIALOGUE_NOT_QUIET";

    private readonly List<Pending> _pending = new();

    /// <summary>The registry this component is served through and refused through; the one installed on its session by <see cref="DialogueChannels.Install"/>.</summary>
    public DialogueChannels Channels { get; } = channels ?? throw new ArgumentNullException(nameof(channels));

    private IEventPort? _events;
    private long _lastTick;
    private long _answered;

    public string Id { get; } = id;

    /// <summary>The channel whose requests this component answers and which it writes on everything it raises; empty for the only dialogue of a game.</summary>
    public string Channel { get; } = channel;

    /// <summary>The runtime this component answers from; its state is the dialogue session's state.</summary>
    public DialogueRuntime Runtime { get; private set; } = runtime;

    /// <summary>How many requests have been answered, refused ones included.</summary>
    public long Answered => _answered;

    /// <summary>True when no request is waiting for the next Update. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0;

    public string Fingerprint() => $"dialogue tick={_lastTick} state={DialogueSave.StateDigest(Runtime.State)[..16]} answered={_answered} pending={_pending.Count}";

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        Channels.Serve(Channel, Id);
        DialogueRequestPort.Connect(context.Events, Queue);
    }

    /// <summary>Takes a request that is addressed to this component's channel; a request for another channel is that channel's, not this one's.</summary>
    private void Queue(string requestChannel, Pending request)
    {
        if (requestChannel == Channel) _pending.Add(request);
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        if (!Channels.RunsOn(events)) throw new InvalidOperationException("the dialogue channels registry of component '" + Id + "' does not run on this session (it was installed on another one, or its session was not initialised), so requests for channels nobody serves could not be answered");
        _lastTick = context.Tick;
        if (_pending.Count == 0) return;
        Runtime.Tick = context.Tick;
        var requests = _pending.ToArray();
        _pending.Clear();
        foreach (var request in requests) Answer(events, context.Tick, request);
    }

    private void Answer(IEventPort events, long tick, in Pending request)
    {
        try
        {
            switch (request.Kind)
            {
                case RequestKind.Start: Show(events, tick, request.RequestId, request.Argument, Runtime.Start(request.Argument, facts())); break;
                case RequestKind.Advance: { var open = Runtime.State.Conversation ?? ""; Show(events, tick, request.RequestId, open, Runtime.Next(facts())); break; }
                case RequestKind.Choose: { var open = Runtime.State.Conversation ?? ""; Show(events, tick, request.RequestId, open, Runtime.Choose(request.Argument, facts())); break; }
                case RequestKind.QuestStart: Raise(events, tick, request.RequestId, Runtime.StartQuest(request.Argument, facts())); break;
                case RequestKind.QuestsUpdate: Raise(events, tick, request.RequestId, Runtime.UpdateQuests(facts())); break;
                case RequestKind.Reload: Reload(events, tick, request.RequestId, request.Argument, request.Map, ReloadMode.Refuse); break;
                case RequestKind.ReloadRestart: Reload(events, tick, request.RequestId, request.Argument, request.Map, ReloadMode.RestartConversation); break;
                case RequestKind.Interrupt: { var open = Runtime.State.Conversation ?? ""; Show(events, tick, request.RequestId, open, Runtime.Interrupt(request.Argument, facts())); break; }
                case RequestKind.Timeout: { var open = Runtime.State.Conversation ?? ""; Show(events, tick, request.RequestId, open, Runtime.Timeout(facts())); break; }
                case RequestKind.Skip: { var open = Runtime.State.Conversation ?? ""; Show(events, tick, request.RequestId, open, Runtime.SkipSeen(facts())); break; }
                case RequestKind.History: ListHistory(events, tick, request); break;
            }
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the runtime has already put its state back.
            events.Emit(new DialogueRefused(request.RequestId, tick, refused.Code, Channel));
        }
        _answered++;
    }

    private void Reload(IEventPort events, long tick, string requestId, string document, string map, ReloadMode mode)
    {
        var report = Runtime.Reload(DialogueLoader.Load(document, speakers), mode, map.Length == 0 ? null : ReloadMap.Parse(map));
        events.Emit(new DialogueReloaded(requestId, tick, string.Join(',', report.AddedVariables), string.Join(',', report.AddedQuests), report.DroppedVisits, report.ClosedConversation ?? "", report.RenamedVisits, Channel, report.DroppedHistory, report.DroppedQueued));
    }

    /// <summary>Lists the newest entries of the kept history, oldest first, and then how many were listed; changes nothing in the session.</summary>
    private void ListHistory(IEventPort events, long tick, in Pending request)
    {
        if (request.Number < 0) throw new EngineRefusedException(DialogueCodes.Range, "a history request asks for 0 (all) or more entries");
        var entries = Runtime.History(request.Number == 0 ? int.MaxValue : request.Number, request.Flag);
        var index = 0;
        foreach (var entry in entries)
        {
            events.Emit(new DialogueHistoryEntryListed(request.RequestId, tick, index++, entry.Turn, entry.Tick, entry.Kind == HistoryKind.Line ? "line" : "answer", entry.Conversation, entry.Node, entry.Speaker, entry.Key,
                string.Join(';', entry.Args.Select(a => a.Name + "=" + Escaped(Text(a.Value)))), entry.Chosen, entry.Seen, entry.Skipped, entry.Replaced, Channel));
        }
        events.Emit(new DialogueHistoryListed(request.RequestId, tick, entries.Count, Runtime.State.History.Count, Channel));
    }
}
