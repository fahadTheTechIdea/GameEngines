using System.Globalization;

namespace WorldEventsEngine;

/// <summary>One line of plain text for each change, by ids, never by words in a language: the tools print these and tests compare them. A game renders its own words from <see cref="WorldNames"/>.</summary>
public static class WorldChangeText
{
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Line(WorldSet set, WorldChange change)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(change);
        var at = Number(change.Step) + " ";
        switch (change.Kind)
        {
            case ChangeKind.Season: return at + "season " + (change.Sub < 0 ? "-" : set.Seasons[change.Sub].Id) + " -> " + set.Seasons[change.Index].Id;
            case ChangeKind.Regime: return at + "regime " + set.Regimes[change.Sub].Id + " -> " + set.Regimes[change.Index].Id;
            case ChangeKind.Weather: return at + "weather " + set.Regions[change.Region].Id + " " + set.Weathers[change.Sub].Id + " -> " + set.Weathers[change.Index].Id + " temp " + Number(change.Amount);
            case ChangeKind.Hazard: return at + "hazard " + set.Hazards[change.Index].Id + " in " + set.Regions[change.Region].Id + " " + Words.Name((HazardChange)change.Sub2) + " at " + set.Hazards[change.Index].Ladder[change.Sub].Id + " power " + Number(change.Amount);
            case ChangeKind.Outbreak: return at + "disease " + set.Diseases[change.Index].Id + " in " + set.Regions[change.Region].Id + " " + Words.Name((OutbreakChange)change.Sub2) + " infected " + Number(change.Amount) + " (" + Number(change.Amount2) + " per mille)";
            case ChangeKind.Card: return at + "card " + set.Cards[change.Index].Id + (change.Region < 0 ? "" : " in " + set.Regions[change.Region].Id) + " " + Words.Name((CardReason)change.Sub2) + " stress " + Number(change.Amount);
            case ChangeKind.Director: return at + "director " + Words.Name((DirectorPhase)change.Sub) + " -> " + Words.Name((DirectorPhase)change.Index) + " stress " + Number(change.Amount);
            case ChangeKind.Effect: return at + "effect " + Words.Name((EffectKind)change.Index) + " " + change.Name + (change.Region < 0 ? "" : " in " + set.Regions[change.Region].Id) + (change.Index == (int)EffectKind.Shock ? " scale " + Number(change.Amount) : "") + " from " + change.Source;
            case ChangeKind.Intervention: return at + "intervention " + set.Interventions[change.Index].Id + " in " + set.Regions[change.Region].Id + (change.Sub == 1 ? " started" : " stopped") + (change.Sub2 == 1 ? " by itself" : "");
        }
        throw new InvalidOperationException("unknown change kind " + change.Kind);
    }
}
