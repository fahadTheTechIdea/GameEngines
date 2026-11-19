using System;
using System.Collections.Generic;
using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Standard
{
    /// <summary>The saved identity of one individually simulated character. The factory is its only writer.</summary>
    public sealed class CharacterComponent : Component<CharacterDef>, ICharacter, IEntityCheckpointValidation
    {
        private const int MaxListLength = 4096;
        private int[] _stats = new int[0];
        private int[] _skills = new int[0];
        private string[] _traits = new string[0];

        public string Archetype { get; private set; } = "";
        public string Name { get; private set; } = "";
        public ulong Seed { get; private set; }
        public int Index { get; private set; }
        public EntityId Squad { get; private set; }
        public int Slot { get; private set; }
        public IReadOnlyList<int> Stats => _stats;
        public IReadOnlyList<int> Skills => _skills;
        public IReadOnlyList<string> Traits => _traits;

        internal void Assign(string archetype, CharacterRecord record, ulong seed, int index, EntityId squad, int slot)
        {
            Archetype = archetype;
            Name = record.Name;
            _stats = (int[])record.Stats.Clone();
            _skills = (int[])record.Skills.Clone();
            _traits = (string[])record.Traits.Clone();
            Seed = seed;
            Index = index;
            Squad = squad;
            Slot = slot;
        }

        public override void Sync(IStateSync s)
        {
            string archetype = Archetype; s.String(ref archetype); Archetype = archetype ?? "";
            string name = Name; s.String(ref name); Name = name ?? "";
            ulong seed = Seed; s.ULong(ref seed); Seed = seed;
            int index = Index; s.Int(ref index); Index = index;
            int slot = Slot; s.Int(ref slot); Slot = slot;
            EntityId squad = Squad; s.Entity(ref squad); Squad = squad;
            SyncInts(s, ref _stats);
            SyncInts(s, ref _skills);
            int traits = s.Count(_traits.Length);
            if (s.Reading)
            {
                if (traits > MaxListLength) throw new CorruptStateException("character traits exceed " + MaxListLength);
                _traits = new string[traits];
            }
            for (int i = 0; i < _traits.Length; i++) s.String(ref _traits[i]);
        }

        /// <summary>A character the factory did not make has no archetype and cannot be saved, loaded or hashed.</summary>
        public void ValidateCheckpoint()
        {
            if (string.IsNullOrEmpty(Archetype)) throw new CorruptStateException("character entity " + Entity.Id.Value + " was not made by the character factory");
        }

        private static void SyncInts(IStateSync s, ref int[] values)
        {
            int count = s.Count(values.Length);
            if (s.Reading)
            {
                if (count > MaxListLength) throw new CorruptStateException("character list exceeds " + MaxListLength);
                values = new int[count];
            }
            for (int i = 0; i < values.Length; i++) s.Int(ref values[i]);
        }
    }
}
