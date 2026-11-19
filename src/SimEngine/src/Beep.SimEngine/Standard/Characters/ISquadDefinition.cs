using Beep.SimEngine.Content;

namespace Beep.SimEngine.Standard
{
    /// <summary>The contract an entity definition is asked for when it must hold a squad.</summary>
    public interface ISquadDefinition : IComponentDefinition
    {
        /// <summary>The most slots one squad of this entity may be raised with.</summary>
        int MaxMembers { get; }
    }
}
