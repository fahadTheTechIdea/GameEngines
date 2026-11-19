namespace GameContent.Catalog;

/// <summary>
/// Everything the game decides about the people of its formations that the catalog does not say: which entity definitions they use, which
/// name pools they draw from, how their stats and skills grow with training, which traits they may have and the limits of a spawn. The
/// catalog says how many people a formation has, their role, training and equipment; the template says nothing about those.
/// </summary>
public sealed record CharacterTemplate(
    string IndividualEntity,
    string SquadEntity,
    string GivenNames,
    string FamilyNames,
    IReadOnlyList<StatPlan> Stats,
    IReadOnlyList<StatPlan> Skills,
    IReadOnlyList<TraitWeight> Traits,
    int TraitMin,
    int TraitMax,
    int MaxSpawn,
    int MaxSquads);
