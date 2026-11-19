using System;
using System.Collections.Generic;
using Beep.SimEngine.Kernel;

namespace Beep.SimEngine.Standard
{
    /// <summary>
    /// The saved state of a squad: its formation, seed, the slot count of each roster entry, the sorted lost slots, the sorted
    /// materialized slots with their entities, and how far the casualty order has been walked. The factory is its only writer.
    /// </summary>
    public sealed class SquadComponent : Component<SquadDef>, ISquad, IEntityCheckpointValidation
    {
        private int[] _counts = new int[0];
        private readonly List<int> _lost = new List<int>();
        private readonly List<int> _slots = new List<int>();
        private readonly List<int> _characters = new List<int>();
        private int _baseTotal;
        private readonly List<int> _waveEntry = new List<int>();
        private readonly List<int> _waveCount = new List<int>();

        public string Formation { get; private set; } = "";
        public ulong Seed { get; private set; }
        public int Total { get; private set; }
        /// <summary>How many reinforcement waves the squad has taken in. Each wave appends slots after every earlier slot and starts a new casualty order.</summary>
        public int Waves => _waveEntry.Count;
        public int Lost => _lost.Count;
        public int Materialized => _slots.Count;
        public int Headcount => Total - _lost.Count;
        public int Aggregate => Total - _lost.Count - _slots.Count;
        /// <summary>How many positions of the casualty order have been used (lost slots and slots skipped because they were materialized).</summary>
        public int CasualtyCursor { get; private set; }
        public IReadOnlyList<int> Counts => _counts;

        public bool IsLost(int slot) => _lost.BinarySearch(slot) >= 0;

        public bool TryMaterialized(int slot, out EntityId character)
        {
            int at = _slots.BinarySearch(slot);
            character = at < 0 ? EntityId.None : new EntityId(_characters[at]);
            return at >= 0;
        }

        /// <summary>The materialized slots in ascending order.</summary>
        public IReadOnlyList<int> MaterializedSlots => _slots;

        internal void Raise(string formation, ulong seed, int[] counts)
        {
            Formation = formation;
            Seed = seed;
            _counts = (int[])counts.Clone();
            _baseTotal = Sum(_counts);
            Total = _baseTotal;
        }

        /// <summary>Appends <paramref name="count"/> slots of one roster entry after every existing slot. The casualty order starts again over the larger squad.</summary>
        internal void Reinforce(int rosterIndex, int count)
        {
            long total = (long)Total + count;
            if (total > SquadDef.Ceiling) throw new InvalidOperationException("squad slots would exceed " + SquadDef.Ceiling);
            _waveEntry.Add(rosterIndex);
            _waveCount.Add(count);
            Total = (int)total;
            CasualtyCursor = 0;
        }

        internal void MarkLost(int slot)
        {
            int at = _lost.BinarySearch(slot);
            if (at >= 0) throw new InvalidOperationException("slot " + slot + " is already lost");
            _lost.Insert(~at, slot);
        }

        internal void AdvanceCursor(int by) => CasualtyCursor = checked(CasualtyCursor + by);

        internal void Materialize(int slot, EntityId character)
        {
            int at = _slots.BinarySearch(slot);
            if (at >= 0) throw new InvalidOperationException("slot " + slot + " is already materialized");
            _slots.Insert(~at, slot);
            _characters.Insert(~at, character.Value);
        }

        internal void Release(int slot)
        {
            int at = _slots.BinarySearch(slot);
            if (at < 0) throw new InvalidOperationException("slot " + slot + " is not materialized");
            _slots.RemoveAt(at);
            _characters.RemoveAt(at);
        }

        /// <summary>The roster index of a slot: slots are numbered along the roster in order.</summary>
        public int RosterIndexOf(int slot)
        {
            int start = 0;
            for (int i = 0; i < _counts.Length; i++)
            {
                start += _counts[i];
                if (slot < start) return i;
            }
            for (int i = 0; i < _waveEntry.Count; i++)
            {
                start += _waveCount[i];
                if (slot < start) return _waveEntry[i];
            }
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        public override void Sync(IStateSync s)
        {
            string formation = Formation; s.String(ref formation); Formation = formation ?? "";
            ulong seed = Seed; s.ULong(ref seed); Seed = seed;
            int groups = s.Count(_counts.Length);
            if (s.Reading) _counts = new int[groups];
            for (int i = 0; i < _counts.Length; i++) s.Int(ref _counts[i]);
            int cursor = CasualtyCursor; s.Int(ref cursor); CasualtyCursor = cursor;
            int lost = s.Count(_lost.Count);
            if (s.Reading) _lost.Clear();
            for (int i = 0; i < lost; i++)
            {
                int slot = s.Reading ? 0 : _lost[i];
                s.Int(ref slot);
                if (s.Reading) _lost.Add(slot);
            }
            int materialized = s.Count(_slots.Count);
            if (s.Reading) { _slots.Clear(); _characters.Clear(); }
            for (int i = 0; i < materialized; i++)
            {
                int slot = s.Reading ? 0 : _slots[i];
                int character = s.Reading ? 0 : _characters[i];
                s.Int(ref slot);
                s.Int(ref character);
                if (s.Reading) { _slots.Add(slot); _characters.Add(character); }
            }
            int waves = s.Count(_waveEntry.Count);
            if (s.Reading) { _waveEntry.Clear(); _waveCount.Clear(); }
            for (int i = 0; i < waves; i++)
            {
                int entry = s.Reading ? 0 : _waveEntry[i];
                int count = s.Reading ? 0 : _waveCount[i];
                s.Int(ref entry);
                s.Int(ref count);
                if (s.Reading) { _waveEntry.Add(entry); _waveCount.Add(count); }
            }
            if (s.Reading) { _baseTotal = Sum(_counts); Total = Sum(_counts, _waveCount); CheckLoaded(); }
        }

        /// <summary>A squad the factory did not raise has no formation and cannot be saved, loaded or hashed.</summary>
        public void ValidateCheckpoint()
        {
            if (string.IsNullOrEmpty(Formation)) throw new CorruptStateException("squad entity " + Entity.Id.Value + " was not raised by the character factory");
        }

        private static int Sum(int[] counts, List<int> waves = null)
        {
            long total = 0;
            for (int i = 0; i < counts.Length; i++) total += counts[i];
            if (waves != null) for (int i = 0; i < waves.Count; i++) total += waves[i];
            if (total > SquadDef.Ceiling) throw new CorruptStateException("squad slots exceed " + SquadDef.Ceiling);
            return (int)total;
        }

        private void CheckLoaded()
        {
            for (int i = 0; i < _counts.Length; i++) if (_counts[i] < 0) throw new CorruptStateException("squad roster count is negative");
            for (int i = 0; i < _waveEntry.Count; i++)
                if (_waveEntry[i] < 0 || _waveEntry[i] >= _counts.Length || _waveCount[i] < 1) throw new CorruptStateException("squad reinforcement wave is out of range");
            for (int i = 0; i < _lost.Count; i++)
                if (_lost[i] < 0 || _lost[i] >= Total || (i > 0 && _lost[i] <= _lost[i - 1])) throw new CorruptStateException("squad lost slots are not sorted and in range");
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] < 0 || _slots[i] >= Total || (i > 0 && _slots[i] <= _slots[i - 1]) || _lost.BinarySearch(_slots[i]) >= 0 || _characters[i] <= 0)
                    throw new CorruptStateException("squad materialized slots are not sorted, in range and alive");
            if (CasualtyCursor < 0 || CasualtyCursor > Total) throw new CorruptStateException("squad casualty cursor is out of range");
        }
    }
}
