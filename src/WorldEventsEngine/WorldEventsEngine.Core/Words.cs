namespace WorldEventsEngine;

/// <summary>
/// The names of the closed sets of the engine (kinds, tiers, phases, models) in documents, facts, journals and events: an enum member in lower case. One place turns a member into its name and a name into its member,
/// so a document, a fact and an event cannot disagree; a name that is none of the members is no member (null), and the caller refuses it by name.
/// </summary>
public static class Words
{
    public static string Name<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    public static T? Parse<T>(string name) where T : struct, Enum
    {
        foreach (var member in Enum.GetValues<T>())
            if (Name(member) == name) return member;
        return null;
    }

    public static string Choices<T>() where T : struct, Enum => string.Join(", ", Enum.GetValues<T>().Select(Name));
}

/// <summary>What a hazard is called to the people who consume it; the engine treats all kinds alike, a consumer may care.</summary>
public enum HazardKind { Flood, Fire, Storm, Quake, Drought, Cold, Other }

/// <summary>How a card weighs on the player: bad cards add stress, good ones relieve it.</summary>
public enum Tier { Bad, Neutral, Good }

/// <summary>What an effect does. A signal is a named story event; a shock is a request for the economy; a modifier is a number other layers read; a hazard and an outbreak start another process.</summary>
public enum EffectKind { Signal, Shock, Modifier, Hazard, Outbreak }

/// <summary>Where the director is: it lets trouble in while building, holds the peak, then rests.</summary>
public enum DirectorPhase { Build, Peak, Relax }

/// <summary>Where one hazard is in its life.</summary>
public enum HazardPhase { Onset, Peak, Recovery }

/// <summary>SIR has no latent period; SEIR has one.</summary>
public enum DiseaseModel { Sir, Seir }

/// <summary>Whether a disease is in a region and how much: introduced (a few cases), outbreak (prevalence reached the document's threshold).</summary>
public enum OutbreakState { None, Introduced, Outbreak }

/// <summary>Why a hazard changed.</summary>
public enum HazardChange { Started, Escalated, Receding, Ended }

/// <summary>Why a disease changed in a region.</summary>
public enum OutbreakChange { Introduced, Outbreak, Contained, Ended }
