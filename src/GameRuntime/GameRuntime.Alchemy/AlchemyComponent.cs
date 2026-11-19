using AlchemyEngine;

namespace GameRuntime;

/// <summary>
/// The alchemy rules as one game component. A <see cref="CombineRequested"/> event only queues the request; the component
/// answers it inside its own Update, in the order the requests arrived, using the tick of that Update and the book's attempt
/// counter as the only sources of variation. It owns the <see cref="DiscoveryBook"/> and answers with events.
/// </summary>
public sealed class AlchemyComponent(AlchemyRules rules, DiscoveryBook book, ulong seed, string id = "alchemy") : IGameComponent
{
    private readonly List<CombineRequested> _pending = new();
    private readonly List<RecipeLearnedExternally> _external = new();
    private IEventPort? _events;
    private long _lastTick;

    public string Id { get; } = id;
    public DiscoveryBook Book => book;

    /// <summary>True when no request is waiting for the next Update. A checkpoint is only consistent then.</summary>
    public bool IsQuiet => _pending.Count == 0 && _external.Count == 0;

    /// <summary>The state a journal records after every tick: the tick this component last ran, the book's hash and the waiting requests.</summary>
    public string Fingerprint() => $"alchemy tick={_lastTick} book={book.Hash()} pending={_pending.Count + _external.Count}";

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<CombineRequested>(request => _pending.Add(request));
        context.Events.Connect<RecipeLearnedExternally>(learned => _external.Add(learned));
        context.Events.Emit(new KnowledgeRestored(string.Join(',', book.Elements), string.Join(',', book.Recipes), string.Join(';', book.Tried), book.Attempts));
    }

    public void Update(in UpdateContext context)
    {
        var events = _events ?? throw new InvalidOperationException("Update before Init");
        _lastTick = context.Tick;
        var learned = _external.ToArray();
        _external.Clear();
        foreach (var item in learned) Learn(events, context.Tick, item);
        if (_pending.Count == 0) return;
        var requests = _pending.ToArray();
        _pending.Clear();
        foreach (var request in requests) Answer(events, context.Tick, request);
    }

    private void Learn(IEventPort events, long tick, in RecipeLearnedExternally learned)
    {
        var taught = book.LearnRecipe(learned.RecipeId);
        var requestId = "external:" + learned.Source;
        foreach (var element in taught.NewElements) events.Emit(new DiscoveryMade(requestId, tick, "element", element));
        if (taught.NewRecipe is { } recipe) events.Emit(new DiscoveryMade(requestId, tick, "recipe", recipe));
    }

    private void Answer(IEventPort events, long tick, in CombineRequested request)
    {
        var items = Split(request.Items, '+');
        var environment = Split(request.Environment, ',');
        var result = rules.Combine(items, new CombineContext(seed, book.Attempts, environment));
        var refusal = result.Outcome == CombineOutcome.Refused ? result.Code : book.CheckUsable(items);
        if (refusal is not null)
        {
            events.Emit(new CombineRefused(request.RequestId, tick, refusal));
            return;
        }
        var learned = book.Record(result);
        events.Emit(new Combined(request.RequestId, tick, OutcomeName(result.Outcome), result.Code, result.RecipeId ?? "",
            string.Join('+', result.Items), string.Join(',', result.Environment), string.Join(',', result.Produced), string.Join(',', result.Kept)));
        foreach (var element in learned.NewElements) events.Emit(new DiscoveryMade(request.RequestId, tick, "element", element));
        if (learned.NewRecipe is { } recipe) events.Emit(new DiscoveryMade(request.RequestId, tick, "recipe", recipe));
    }

    private static string[] Split(string joined, char separator) =>
        joined.Length == 0 ? Array.Empty<string>() : joined.Split(separator);

    // Indexed by CombineOutcome; a value outside the enum fails the app (IndexOutOfRangeException) instead of being named by a default arm.
    private static readonly string[] OutcomeNames = ["reacted", "no-reaction", "fizzled", "refused"];

    private static string OutcomeName(CombineOutcome outcome) => OutcomeNames[(int)outcome];
}
