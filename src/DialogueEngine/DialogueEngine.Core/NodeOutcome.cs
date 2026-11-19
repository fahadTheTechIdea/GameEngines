namespace DialogueEngine;

public enum OutcomeKind { Continue, Wait, Finish, Call, Return }

/// <summary>What entering a node led to: on to another node, a stop for the player, or the end of the conversation.</summary>
internal readonly record struct NodeOutcome(OutcomeKind Kind, string? Next, DialogueStep? Step, string? Result, CallNode? Caller = null)
{
    public static NodeOutcome Go(string next) => new(OutcomeKind.Continue, next, null, null);

    public static NodeOutcome Stop(DialogueStep step) => new(OutcomeKind.Wait, null, step, null);

    public static NodeOutcome End(string result) => new(OutcomeKind.Finish, null, null, result);

    public static NodeOutcome Call(CallNode call) => new(OutcomeKind.Call, null, null, null, call);

    public static NodeOutcome Return(string result) => new(OutcomeKind.Return, null, null, result);
}