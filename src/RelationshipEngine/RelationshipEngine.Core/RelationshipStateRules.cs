using System.Text.RegularExpressions;
using EngineCommon;

namespace RelationshipEngine;

/// <summary>
/// The rules a state must satisfy to belong to a document. The digest is not a secret, so a save passes through these as well; it cannot bring in a state the document could not have made. Ranges are checked before
/// the digest, what the state says about the document (who exists, what bonds mirror) after it.
/// </summary>
internal static class RelationshipStateRules
{
    private static readonly Regex IdText = new("^[a-z0-9._-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static EngineRefusedException Refuse(string message) => new(RelationshipCodes.SaveState, message);

    private static bool Ascending(IEnumerable<string> ids) => ids.Zip(ids.Skip(1)).All(p => string.CompareOrdinal(p.First, p.Second) < 0);

    public static void CheckRanges(RelationshipSet set, RelationshipImage image)
    {
        if (image.Step < 0) throw Refuse("the step is negative");
        if (image.Rolls < 0) throw Refuse("the roll count is negative");
        if (image.Actors.Count > set.Limits.MaxActors) throw Refuse("more actors than the document allows (" + set.Limits.MaxActors + ")");
        if (!Ascending(image.Actors.Select(a => a.Id))) throw Refuse("actors are not in strictly increasing id order (an actor listed twice counts)");
        foreach (var actor in image.Actors) CheckActor(set, image, actor);
    }

    private static void CheckActor(RelationshipSet set, RelationshipImage image, ImageActor actor)
    {
        var where = "actor '" + actor.Id + "'";
        if (!IdText.IsMatch(actor.Id)) throw Refuse("an actor id is not an id");
        if (actor.Traits.Count > RelationshipRuntime.MaxTraitsPerActor || actor.Traits.Any(t => !IdText.IsMatch(t)) || !Ascending(actor.Traits)) throw Refuse(where + " has traits that are too many, not ids, or not in strictly increasing order");
        if (actor.Tastes.Count > RelationshipRuntime.MaxTastes || actor.Tastes.Any(t => !IdText.IsMatch(t.Tag) || t.Level is < -2 or > 2) || !Ascending(actor.Tastes.Select(t => t.Tag))) throw Refuse(where + " has tastes that are too many, out of range, or not in strictly increasing order");
        if (actor.Activity.Length > 0 && !IdText.IsMatch(actor.Activity)) throw Refuse(where + " has an activity that is not an id");
        if (actor.MoodState < 0 || actor.MoodState >= set.Mood.States.Count) throw Refuse(where + " has a mood state outside the document's " + set.Mood.States.Count);
        if (!IdText.IsMatch(actor.Community) || string.CompareOrdinal(actor.Community, actor.Id) > 0) throw Refuse(where + " has a community that is not the id of a member at or before it");
        if (actor.Standing < 0 || actor.Standing > RelationshipRuntime.StandingMax) throw Refuse(where + " has a standing outside 0 to " + RelationshipRuntime.StandingMax);
        if (actor.Joined < 0 || actor.Joined > image.Step) throw Refuse(where + " joined outside the time lived");
        if (actor.Needs.Count != set.Needs.Count) throw Refuse(where + " has " + actor.Needs.Count + " needs, the document " + set.Needs.Count);
        for (var n = 0; n < set.Needs.Count; n++)
        {
            var need = set.Needs[n];
            var saved = actor.Needs[n];
            if (saved.Level < 0 || saved.Level > need.Max * set.StepsPerDay) throw Refuse(where + " need '" + need.Id + "' is outside 0 to its max");
            var level = saved.Level / set.StepsPerDay;
            if (saved.Critical && level >= need.Rearm && need.Rearm > need.Critical) throw Refuse(where + " need '" + need.Id + "' is critical above its re-arm line");
            if (!saved.Critical && level <= need.Critical) throw Refuse(where + " need '" + need.Id + "' is at or below critical but not marked critical");
        }
        if (actor.Moodlets.Count > set.Limits.MaxMoodlets) throw Refuse(where + " has more moodlets than the document allows");
        foreach (var moodlet in actor.Moodlets)
        {
            if (moodlet.Moodlet < 0 || moodlet.Moodlet >= set.Moodlets.Count) throw Refuse(where + " has a moodlet that the document does not have");
            if (moodlet.Start < 0 || moodlet.Start > image.Step) throw Refuse(where + " has a moodlet that began outside the time lived");
            if (moodlet.Scale is < -10_000 or > 10_000) throw Refuse(where + " has a moodlet scale outside -10000 to 10000");
            if (moodlet.Source.Length > 0 && !IdText.IsMatch(moodlet.Source)) throw Refuse(where + " has a moodlet source that is not an id");
        }
        if (actor.Edges.Count > set.Limits.MaxEdgesPerActor) throw Refuse(where + " has more edges than the document allows");
        if (!Ascending(actor.Edges.Select(e => e.To))) throw Refuse(where + " edges are not in strictly increasing order");
        foreach (var edge in actor.Edges) CheckEdge(set, image, actor, edge);
    }

    private static void CheckEdge(RelationshipSet set, RelationshipImage image, ImageActor actor, ImageEdge edge)
    {
        var where = "edge '" + actor.Id + "' to '" + edge.To + "'";
        if (!IdText.IsMatch(edge.To) || edge.To == actor.Id) throw Refuse(where + ": the target is not an id other than the holder");
        if (edge.Touched < 0 || edge.Touched > image.Step) throw Refuse(where + " was touched outside the time lived");
        if (edge.Base.Count != set.Axes.Count) throw Refuse(where + " has " + edge.Base.Count + " axes, the document " + set.Axes.Count);
        for (var a = 0; a < set.Axes.Count; a++)
            if (edge.Base[a] < set.Axes[a].Min || edge.Base[a] > set.Axes[a].Max) throw Refuse(where + " axis '" + set.Axes[a].Id + "' is outside its range");
        if (edge.Ladder.Count != set.Ladders.Count) throw Refuse(where + " has " + edge.Ladder.Count + " ladders, the document " + set.Ladders.Count);
        for (var l = 0; l < set.Ladders.Count; l++)
            if (edge.Ladder[l] < 0 || edge.Ladder[l] >= set.Ladders[l].Steps.Count) throw Refuse(where + " ladder '" + set.Ladders[l].Id + "' has a step the document does not have");
        if (edge.Bonds.Any(b => b < 0 || b >= set.Bonds.Count) || edge.Bonds.Zip(edge.Bonds.Skip(1)).Any(p => p.First >= p.Second)) throw Refuse(where + " has bonds the document does not have, or not in strictly increasing order");
        if (edge.Memories.Count > set.Limits.MaxMemoriesPerEdge) throw Refuse(where + " has more memories than the document allows");
        foreach (var memory in edge.Memories)
        {
            if (memory.Memory < 0 || memory.Memory >= set.Memories.Count) throw Refuse(where + " has a memory that the document does not have");
            if (memory.Start < 0 || memory.Start > image.Step) throw Refuse(where + " has a memory that began outside the time lived");
            if (memory.Scale is < -10_000 or > 10_000) throw Refuse(where + " has a memory scale outside -10000 to 10000");
            if (memory.Hops is < 0 or > 4) throw Refuse(where + " has a memory with hops outside 0 to 4");
            if (memory.Origin.Length > 0 && !IdText.IsMatch(memory.Origin)) throw Refuse(where + " has a memory origin that is not an id");
        }
        if (edge.Cooldowns.Any(c => c.Interaction < 0 || c.Interaction >= set.Interactions.Count) || edge.Cooldowns.Zip(edge.Cooldowns.Skip(1)).Any(p => p.First.Interaction >= p.Second.Interaction))
            throw Refuse(where + " has cooldowns the document does not have, or not in strictly increasing order");
        foreach (var cooldown in edge.Cooldowns)
            if (cooldown.ReadyAt < 0 || cooldown.ReadyAt > image.Step + set.Interactions[cooldown.Interaction].Cooldown) throw Refuse(where + " has a cooldown ending beyond what the interaction allows");
    }

    public static void CheckAgainstDocument(RelationshipSet set, RelationshipImage image)
    {
        var actors = image.Actors.ToDictionary(a => a.Id, StringComparer.Ordinal);
        foreach (var actor in image.Actors)
        {
            // A community is named by its lowest member: that actor exists and belongs to it, and an actor of a disabled feature stands alone.
            if (!actors.TryGetValue(actor.Community, out var namer) || namer.Community != actor.Community) throw Refuse("actor '" + actor.Id + "' belongs to community '" + actor.Community + "', which has no member of that name");
            if (!set.Social.Communities.Enabled && actor.Community != actor.Id) throw Refuse("actor '" + actor.Id + "' belongs to a community, but the document turns communities off");
            if (!set.Social.Standing.Enabled && actor.Standing != RelationshipRuntime.NeutralStanding) throw Refuse("actor '" + actor.Id + "' has a standing, but the document turns standing off");
            foreach (var edge in actor.Edges)
            {
                var where = "edge '" + actor.Id + "' to '" + edge.To + "'";
                if (!actors.TryGetValue(edge.To, out var other)) throw Refuse(where + " points at an actor that does not exist");
                foreach (var bond in edge.Bonds)
                    if (set.Bonds[bond].Symmetric && !other.Edges.Any(e => e.To == actor.Id && e.Bonds.Contains(bond))) throw Refuse(where + " holds the symmetric bond '" + set.Bonds[bond].Id + "' but the way back does not");
            }
            foreach (var bond in set.Bonds.Where(b => b.Exclusive))
                if (actor.Edges.Count(e => e.Bonds.Contains(bond.Index)) > 1) throw Refuse("actor '" + actor.Id + "' holds the exclusive bond '" + bond.Id + "' toward more than one actor");
        }
    }
}
