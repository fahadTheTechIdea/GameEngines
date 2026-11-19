namespace GameRuntime;

/// <summary>
/// What distinguishes one engine's channels from another's: the id and codes of its registry, how its events are declared on a session, how its request events are connected (each handler gets the
/// request's channel and id), and the refusal event it raises for a request nobody serves. Everything else about channels is <see cref="ChannelRegistry"/>'s.
/// </summary>
/// <param name="Rank">Which place the registry takes among the registries of a session: the registry of rank n updates at <c>int.MaxValue - n</c>, so every registry updates after every ordinary component and no two share an order. Each engine has its own rank (dialogue 0, calendar 1, relationship 2).</param>
/// <param name="ComponentId">The id the registry has in its session.</param>
/// <param name="UnknownCode">The refusal for a request on a channel no component serves.</param>
/// <param name="DuplicateCode">The refusal at Init for a second component on a channel already served.</param>
/// <param name="Declare">Declares the engine's events on the session.</param>
/// <param name="Connect">Connects every request event of the engine; the handler receives the channel and the request id.</param>
/// <param name="Refuse">Emits the engine's refusal event for the unserved request.</param>
public sealed record ChannelProtocol(int Rank, string ComponentId, string UnknownCode, string DuplicateCode, Action<RuntimeSession> Declare, Action<IEventPort, Action<string, string>> Connect, Action<IEventPort, string, long, string> Refuse);
