using System.Collections.Generic;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// The one owner of creating characters. Every operation has a pure <c>Check</c> that returns a refusal code and changes nothing,
    /// and an operation that creates all it was asked for or nothing. Characters are made only here; nothing else in a game creates one.
    /// </summary>
    public interface ICharacterFactory
    {
        Code CheckSpawnFormation(SpawnFormationRequest request);

        /// <summary>Raises the squads; returns their entity ids in batch order. Throws if the check would refuse.</summary>
        IReadOnlyList<EntityId> SpawnFormation(SpawnFormationRequest request);

        Code CheckSpawnIndividuals(SpawnIndividualsRequest request);

        /// <summary>Creates the individuals; returns their entity ids in index order. Throws if the check would refuse.</summary>
        IReadOnlyList<EntityId> SpawnIndividuals(SpawnIndividualsRequest request);

        Code CheckMaterialize(EntityId squad, int slot);

        /// <summary>Makes one unnamed slot of a squad an individual entity. One way: the individual stays until it dies.</summary>
        EntityId Materialize(EntityId squad, int slot);

        Code CheckCasualties(EntityId squad, int count);

        /// <summary>Removes <paramref name="count"/> members that exist only as a count, by the squad's seeded casualty order.</summary>
        void Casualties(EntityId squad, int count);

        Code CheckReinforce(EntityId squad, int rosterEntry, int count);

        /// <summary>Adds <paramref name="count"/> members of one roster entry to a squad as new slots. They exist only as a count until materialized, like the original members.</summary>
        void Reinforce(EntityId squad, int rosterEntry, int count);

        Code CheckReinforceByRoster(EntityId squad, int count);

        /// <summary>
        /// Adds <paramref name="count"/> members in the proportions of the squad formation roster (largest remainder, the earlier roster entry winning a tie), so
        /// replacements keep the mix of ranks the squad was raised with. One <see cref="SquadReinforced"/> for the whole number.
        /// </summary>
        void ReinforceByRoster(EntityId squad, int count);

        Code CheckKill(EntityId character);

        /// <summary>A materialized character dies: its slot is lost and its entity is removed.</summary>
        void Kill(EntityId character);

        /// <summary>The character a squad slot stands for, whether or not it is materialized. Pure.</summary>
        CharacterRecord Describe(EntityId squad, int slot);
    }
}
