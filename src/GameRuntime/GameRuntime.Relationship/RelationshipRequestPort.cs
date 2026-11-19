namespace GameRuntime;

/// <summary>What a request asks for. One list, so a component that serves requests and the registry that refuses the unserved ones cannot disagree about which requests exist.</summary>
internal enum RelationshipRequestKind { ActorAdd, ActorRemove, Activity, Interact, Grant, Remember, Need, Axis, Bond, Mood, Opinion }

/// <summary>
/// One queued request. <see cref="A"/> to <see cref="E"/> are the texts the kind takes, <see cref="N"/> and <see cref="M"/> its numbers; the rest are empty. ActorAdd: actor, traits, tastes. ActorRemove: actor.
/// Activity: actor, activity. Interact: actor, target, interaction, gift, witnesses. Grant: actor, moodlet, source, scale in N. Remember: holder, subject, memory, scale in N. Need: actor, need, delta in N.
/// Axis: holder, other, axis, delta in N. Bond: holder, other, bond, set in N (1 or 0). Mood: actor. Opinion: from, to, axis.
/// </summary>
internal readonly record struct RelationshipPending(RelationshipRequestKind Kind, string RequestId, string A = "", string B = "", string C = "", string D = "", string E = "", long N = 0, long M = 0);

/// <summary>The one place every relationship request event is connected: the handler gets the request channel and the request as a <see cref="RelationshipPending"/>.</summary>
internal static class RelationshipRequestPort
{
    public static void Connect(IEventPort events, Action<string, RelationshipPending> handler)
    {
        events.Connect<RelationshipActorAddRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.ActorAdd, r.RequestId, r.Actor, r.Traits, r.Tastes)));
        events.Connect<RelationshipActorRemoveRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.ActorRemove, r.RequestId, r.Actor)));
        events.Connect<RelationshipActivityRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Activity, r.RequestId, r.Actor, r.Activity)));
        events.Connect<RelationshipInteractRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Interact, r.RequestId, r.Actor, r.Target, r.Interaction, r.Gift, r.Witnesses)));
        events.Connect<RelationshipGrantRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Grant, r.RequestId, r.Actor, r.Moodlet, r.Source, N: r.ScalePermille)));
        events.Connect<RelationshipRememberRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Remember, r.RequestId, r.Holder, r.Subject, r.Memory, N: r.ScalePermille)));
        events.Connect<RelationshipNeedRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Need, r.RequestId, r.Actor, r.Need, N: r.Delta)));
        events.Connect<RelationshipAxisRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Axis, r.RequestId, r.Holder, r.Other, r.Axis, N: r.Delta)));
        events.Connect<RelationshipBondRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Bond, r.RequestId, r.Holder, r.Other, r.Bond, N: r.Set ? 1 : 0)));
        events.Connect<RelationshipMoodRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Mood, r.RequestId, r.Actor)));
        events.Connect<RelationshipOpinionRequested>(r => handler(r.Channel, new RelationshipPending(RelationshipRequestKind.Opinion, r.RequestId, r.From, r.To, r.Axis)));
    }
}
