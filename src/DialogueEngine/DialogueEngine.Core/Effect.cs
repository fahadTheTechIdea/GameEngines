namespace DialogueEngine;

/// <summary>A change a node or an option makes. Each kind is its own class; Check says whether it may be applied now, and refuses with the code when it may not.</summary>
public abstract class Effect
{
    internal abstract void Apply(StepContext context);
}