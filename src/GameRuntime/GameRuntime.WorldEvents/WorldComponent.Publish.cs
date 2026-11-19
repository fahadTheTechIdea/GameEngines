using WorldEventsEngine;

namespace GameRuntime;

public sealed partial class WorldComponent
{
    /// <summary>What the changes of a call or of the steps lived become as events, in the order they happened. Each is built now (the names are read now) and raised later.</summary>
    private List<Action<IEventPort>> EventsOf(long tick, WorldChanges changes)
    {
        var set = Runtime.Set;
        var events = new List<Action<IEventPort>>(changes.Items.Count);
        foreach (var c in changes.Items)
        {
            var region = c.Region < 0 ? "" : set.Regions[c.Region].Id;
            switch (c.Kind)
            {
                case ChangeKind.Season:
                {
                    var e = new WorldSeasonChanged(Id, Channel, tick, c.Step, set.Seasons[c.Index].Id, c.Sub < 0 ? "" : set.Seasons[c.Sub].Id);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Regime:
                {
                    var e = new WorldRegimeChanged(Id, Channel, tick, c.Step, set.Regimes[c.Index].Id, set.Regimes[c.Sub].Id);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Weather:
                {
                    var e = new WorldWeatherChanged(Id, Channel, tick, c.Step, region, set.Weathers[c.Index].Id, set.Weathers[c.Sub].Id, c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Hazard:
                {
                    var hazard = set.Hazards[c.Index];
                    var e = new WorldHazardChanged(Id, Channel, tick, c.Step, region, hazard.Id, Words.Name(hazard.Kind), hazard.Ladder[c.Sub].Id, Words.Name((HazardChange)c.Sub2), (int)c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Outbreak:
                {
                    var e = new WorldOutbreakChanged(Id, Channel, tick, c.Step, region, set.Diseases[c.Index].Id, Words.Name((OutbreakChange)c.Sub2), c.Amount, c.Amount2);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Card:
                {
                    var card = set.Cards[c.Index];
                    var e = new WorldCardPlayed(Id, Channel, tick, c.Step, card.Id, region, Words.Name(card.Tier), Words.Name((CardReason)c.Sub2), c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Director:
                {
                    var e = new WorldDirectorChanged(Id, Channel, tick, c.Step, Words.Name((DirectorPhase)c.Sub), Words.Name((DirectorPhase)c.Index), c.Amount);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Effect:
                {
                    var market = c.Region < 0 ? "" : set.Regions[c.Region].Market;
                    var e = new WorldEffectIssued(Id, Channel, tick, c.Step, Words.Name((EffectKind)c.Index), c.Name, region, market, c.Amount, c.Source);
                    events.Add(port => port.Emit(e));
                    break;
                }
                case ChangeKind.Intervention:
                {
                    var e = new WorldInterventionChanged(Id, Channel, tick, c.Step, region, set.Interventions[c.Index].Id, c.Sub == 1, c.Sub2 == 1);
                    events.Add(port => port.Emit(e));
                    break;
                }
            }
        }
        return events;
    }

    /// <summary>The changes of the steps lived are owed: raised at most <c>maxEvents</c> per Update, in order, oldest first.</summary>
    private void Owe(long tick, WorldChanges changes)
    {
        foreach (var raise in EventsOf(tick, changes)) _owed.Enqueue(raise);
    }
}
