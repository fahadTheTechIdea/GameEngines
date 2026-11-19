using System.Globalization;
using System.Text;
using System.Text.Json;
using CombatEngine;

namespace GameRuntime;

/// <summary>What a verified combat checkpoint holds: the session tick, the roll attempt counter and the state of every group.</summary>
public sealed record CombatCheckpointParts(long Tick, long Attempt, long Engagements, int DistanceMetres, IReadOnlyDictionary<string, Group> Groups);

/// <summary>
/// One checkpoint for a skirmish: the tick, the attempt counter and each group's mutable state (health, standing, rounds and fractions
/// of a shot held), with a SHA-256 over all of it and a digest of the forces it was taken from, so a checkpoint never loads against
/// other forces. It never repairs or migrates; the first failed check refuses with its code.
/// </summary>
public static class CombatCheckpoint
{
    public const string FormatCode = "COMBAT_CHECKPOINT_FORMAT";
    public const string DigestCode = "COMBAT_CHECKPOINT_DIGEST";
    public const string ForcesCode = "COMBAT_CHECKPOINT_FORCES";
    public const string NotQuietCode = "COMBAT_CHECKPOINT_NOT_QUIET";
    // 3: a group has a platform pool beside its people. A format-2 checkpoint has none to restore, so it is refused, never migrated.
    // 4: the forces digest covers the crew a battery needs per weapon. A format-3 checkpoint is refused, never migrated.
    private const int Format = 4;

    public static byte[] Capture(long tick, CombatComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!component.IsQuiet) throw new CombatRefusedException(NotQuietCode, "an engagement request is still waiting for the next Update");
        var states = component.Groups.Values.Select(g => new GroupState
        {
            Id = g.Id, Health = g.Health, PlatformHealth = g.PlatformHealth, Fuel = g.Fuel, Suppressed = g.Suppressed, Routed = g.Routed, Morale = g.Morale,
            Batteries = g.Batteries.Select(b => new BatteryState { Id = b.Id, Rounds = b.Rounds, Carry = b.CarryMilli }).ToList(),
        }).ToList();
        var envelope = new Envelope { Format = Format, Tick = tick, Attempt = component.Attempt, Engagements = component.Engagements, Distance = component.LastDistanceMetres, Forces = ForcesDigest(component.Groups.Values), Groups = states };
        envelope.Digest = Digest(envelope);
        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    /// <param name="forces">The groups of the session being resumed, as built from the catalog; they must be the ones the checkpoint was taken from.</param>
    public static CombatCheckpointParts Open(IReadOnlyList<Group> forces, ReadOnlyMemory<byte> bytes)
    {
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes.Span); }
        catch (JsonException exception) { throw new CombatRefusedException(FormatCode, "not a checkpoint: " + exception.Message); }
        if (envelope is null || envelope.Format != Format || envelope.Tick < 0 || envelope.Attempt < 1 || envelope.Engagements < 0 || envelope.Distance < -1) throw new CombatRefusedException(FormatCode, "unsupported checkpoint format");
        // Ranges come before the digest: the digest is not a secret, so a forged one must not carry a pool the forces cannot hold into the session.
        var known = forces.ToDictionary(g => g.Id, StringComparer.Ordinal);
        foreach (var state in envelope.Groups)
        {
            if (!known.TryGetValue(state.Id, out var shape)) continue;   // an unknown group is refused below, with the forces code
            if (state.PlatformHealth < 0 || state.PlatformHealth > shape.InitialPlatforms * shape.PlatformUnitHealth)
                throw new CombatRefusedException(ForcesCode, "group '" + state.Id + "' has a platform pool its forces cannot hold");
            if (state.Fuel < 0 || state.Fuel > shape.FuelCapacity || state.Fuel > shape.FuelCapacity * Math.Min(shape.InitialPlatforms, shape.PlatformUnitHealth <= 0 ? 0 : (state.PlatformHealth + shape.PlatformUnitHealth - 1) / shape.PlatformUnitHealth) / Math.Max(1, shape.InitialPlatforms)) throw new CombatRefusedException(ForcesCode, "group '" + state.Id + "' has fuel its tanks cannot hold");
        }
        if (envelope.Digest != Digest(envelope)) throw new CombatRefusedException(DigestCode, "checkpoint digest does not match its parts");
        if (envelope.Forces != ForcesDigest(forces)) throw new CombatRefusedException(ForcesCode, "the checkpoint was taken from other forces");

        var byId = forces.ToDictionary(g => g.Id, StringComparer.Ordinal);
        var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var state in envelope.Groups)
        {
            if (!byId.TryGetValue(state.Id, out var group) || state.Batteries.Count != group.Batteries.Count) throw new CombatRefusedException(ForcesCode, "group '" + state.Id + "' does not match");
            var batteries = group.Batteries.Select((b, i) => state.Batteries[i].Id == b.Id ? b with { Rounds = state.Batteries[i].Rounds, CarryMilli = state.Batteries[i].Carry }
                : throw new CombatRefusedException(ForcesCode, "battery '" + b.Id + "' does not match")).ToList();
            groups[state.Id] = group with { Health = state.Health, PlatformHealth = state.PlatformHealth, Fuel = state.Fuel, Suppressed = state.Suppressed, Routed = state.Routed, Morale = state.Morale, Batteries = batteries };
        }
        return new CombatCheckpointParts(envelope.Tick, envelope.Attempt, envelope.Engagements, envelope.Distance, groups);
    }

    /// <summary>The fixed shape of the forces (ids, sides, health pools, weapons), not their changing state. Numbers are written in the invariant culture.</summary>
    private static string ForcesDigest(IEnumerable<Group> groups)
    {
        var text = new StringBuilder("combat-forces-4");
        foreach (var group in groups.OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            text.Append(string.Create(CultureInfo.InvariantCulture, $"\n{group.Id}|{group.Side}|{group.Training}|{group.InitialMembers}|{group.MemberHealth}|{group.ArmorRating}|{group.SpeedMilli}|{group.InitialPlatforms}:{group.PlatformUnitHealth}:{group.PlatformArmor}:{group.CrewPerPlatform}:{group.FuelCapacity}:{group.BurnPerHourMilli}:{group.FuelType}"));
            foreach (var battery in group.Batteries)
                text.Append(string.Create(CultureInfo.InvariantCulture, $"|{battery.Id}:{battery.Count}:{battery.Weapon.Damage}:{battery.Weapon.Penetration}:{battery.Weapon.RangeMilli}:{battery.Capacity}:{battery.CrewPerUnit}"));
        }
        return EngineCommon.Digest.Of(text.ToString());
    }

    private static string Digest(Envelope envelope)
    {
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"combat-checkpoint-4|{envelope.Format}|{envelope.Tick}|{envelope.Attempt}|{envelope.Engagements}|{envelope.Distance}|{envelope.Forces}"));
        foreach (var group in envelope.Groups)
        {
            text.Append(string.Create(CultureInfo.InvariantCulture, $"\n{group.Id}|{group.Health}|{group.PlatformHealth}|{group.Fuel}|{(group.Suppressed ? 1 : 0)}|{(group.Routed ? 1 : 0)}|{group.Morale}"));
            foreach (var battery in group.Batteries) text.Append(string.Create(CultureInfo.InvariantCulture, $"|{battery.Id}:{battery.Rounds}:{battery.Carry}"));
        }
        return EngineCommon.Digest.Of(text.ToString());
    }

    private sealed class Envelope
    {
        public int Format { get; set; }
        public long Tick { get; set; }
        public long Attempt { get; set; }
        public long Engagements { get; set; }
        public int Distance { get; set; }
        public string Forces { get; set; } = "";
        public List<GroupState> Groups { get; set; } = new();
        public string Digest { get; set; } = "";
    }

    private sealed class GroupState
    {
        public string Id { get; set; } = "";
        public long Health { get; set; }
        public long PlatformHealth { get; set; }
        public long Fuel { get; set; }
        public bool Suppressed { get; set; }
        public bool Routed { get; set; }
        public long Morale { get; set; }
        public List<BatteryState> Batteries { get; set; } = new();
    }

    private sealed class BatteryState
    {
        public string Id { get; set; } = "";
        public long Rounds { get; set; }
        public long Carry { get; set; }
    }
}
