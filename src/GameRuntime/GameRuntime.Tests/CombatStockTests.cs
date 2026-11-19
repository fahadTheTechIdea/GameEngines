using System.Globalization;
using System.Text.Json;
using Beep.SimEngine;
using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The stock-flow layer: people and rounds as the inventory's own resources on each squad, recorded as its lifecycle flows (issued,
/// consumed, destroyed, lost), refused whole when they do not fit, and kept in step with combat and the squads through a whole fight.
/// </summary>
public sealed class CombatStockTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("stock-game-");

    public void Dispose() => _dir.Delete(true);

    private string Path(string name) => System.IO.Path.Combine(_dir.FullName, name);

    // ---- the effect, one request at a time -------------------------------------------------------------------------------------------

    [Fact]
    public void EachFlowIsRecordedAsItsOwnLifecycleKindOnTheSquadStock()
    {
        var h = new StockHarness();
        var (ammo, rounds) = h.Ammunition();
        Assert.True(h.Flows(("issued", h.SquadA, StockResources.People, 100), ("issued", h.SquadA, ammo, (int)rounds)).Applied);
        Assert.Equal(100, h.Stock(h.SquadA)[StockResources.People]);
        Assert.Equal(rounds, h.Stock(h.SquadA)[ammo]);
        Assert.True(h.Flows(("consumed", h.SquadA, ammo, 20)).Applied);
        Assert.True(h.Flows(("destroyed", h.SquadA, StockResources.People, 7)).Applied);
        Assert.True(h.Flows(("lost", h.SquadA, ammo, 5)).Applied);
        Assert.Equal(93, h.Stock(h.SquadA)[StockResources.People]);
        Assert.Equal(rounds - 25, h.Stock(h.SquadA)[ammo]);
        Assert.True(h.Flows(("abandoned", h.SquadA, ammo, 0)).Applied);
        Assert.DoesNotContain(ammo, h.Stock(h.SquadA).Keys);
        Assert.Equal(["Initialized", "Initialized", "Consumed", "Destroyed", "Lost", "Lost"], h.FlowKinds());
        Assert.Empty(h.Reports);
    }

    [Fact]
    public void ARequestWithOneLineTooBigIsRefusedWholeAndChangesNothing()
    {
        var h = new StockHarness();
        var (ammo, rounds) = h.Ammunition();
        Assert.True(h.Flows(("issued", h.SquadA, StockResources.People, 100), ("issued", h.SquadA, ammo, (int)rounds)).Applied);
        var hash = h.EntitiesHash();
        var stock = h.Stock(h.SquadA);
        var refused = h.Flows(("consumed", h.SquadA, ammo, 10), ("consumed", h.SquadA, ammo, (int)rounds + 1));
        Assert.False(refused.Applied);
        Assert.Equal(StockCodes.Short.Id, refused.Code);
        Assert.Equal(stock, h.Stock(h.SquadA));
        Assert.Equal(hash, h.EntitiesHash());
        Assert.DoesNotContain("Consumed", h.FlowKinds());
    }

    [Fact]
    public void LinesOfOneRequestShareARunningBalance()
    {
        var h = new StockHarness();
        var (ammo, rounds) = h.Ammunition();
        Assert.True(h.Flows(("issued", h.SquadA, ammo, (int)rounds)).Applied);
        // Each line fits the stock alone; together they take more than the squad holds.
        var refused = h.Flows(("consumed", h.SquadA, ammo, (int)rounds - 10), ("consumed", h.SquadA, ammo, 20));
        Assert.False(refused.Applied);
        Assert.Equal(StockCodes.Short.Id, refused.Code);
        Assert.Equal(rounds, h.Stock(h.SquadA)[ammo]);
        // An abandon after a consume takes what is left, not what was held when the request was written.
        Assert.True(h.Flows(("consumed", h.SquadA, ammo, 10), ("abandoned", h.SquadA, ammo, 0)).Applied);
        Assert.DoesNotContain(ammo, h.Stock(h.SquadA).Keys);
        Assert.Equal(["Initialized", "Consumed", "Lost"], h.FlowKinds());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"flows\":[]}")]
    [InlineData("{\"flows\":[{\"kind\":\"consumed\",\"squad\":1,\"resource\":\"x\"}]}")]
    [InlineData("{\"flows\":[{\"kind\":\"consumed\",\"squad\":1,\"resource\":\"x\",\"amount\":1,\"extra\":1}]}")]
    [InlineData("{\"flows\":[{\"kind\":\"vanished\",\"squad\":1,\"resource\":\"x\",\"amount\":1}]}")]
    [InlineData("{\"flows\":[{\"kind\":\"consumed\",\"squad\":1,\"resource\":\"x\",\"amount\":0}]}")]
    [InlineData("{\"flows\":[{\"kind\":\"consumed\",\"squad\":1,\"resource\":\"x\",\"amount\":-3}]}")]
    [InlineData("{\"flows\":[{\"kind\":\"consumed\",\"squad\":1,\"resource\":\"x\",\"amount\":1.5}]}")]
    [InlineData("{\"flows\":[{\"kind\":\"abandoned\",\"squad\":1,\"resource\":\"x\",\"amount\":4}]}")]
    [InlineData("{\"flows\":[{\"kind\":\"consumed\",\"squad\":0,\"resource\":\"x\",\"amount\":1}]}")]
    public void AMalformedPayloadIsRefusedAsAnInvalidArgument(string payload)
    {
        var h = new StockHarness();
        var hash = h.EntitiesHash();
        var answer = h.Send(StockFlowEffect.EffectName, payload);
        Assert.False(answer.Applied);
        Assert.Equal(Codes.InvalidArgument.Id, answer.Code);
        Assert.Equal(hash, h.EntitiesHash());
    }

    [Fact]
    public void AnUnknownSquadOrResourceIsRefusedAsNotFound()
    {
        var h = new StockHarness();
        var (ammo, rounds) = h.Ammunition();
        Assert.True(h.Flows(("issued", h.SquadA, ammo, (int)rounds)).Applied);
        Assert.Equal(Codes.NotFound.Id, h.Flows(("consumed", 9999, ammo, 1)).Code);
        Assert.Equal(Codes.NotFound.Id, h.Flows(("consumed", h.SquadA, "stock.ammo.none", 1)).Code);
        Assert.Equal(rounds, h.Stock(h.SquadA)[ammo]);
    }

    [Fact]
    public void AbandoningNothingIsAnAnsweredRequestThatRecordsNothing()
    {
        var h = new StockHarness();
        var (ammo, _) = h.Ammunition();
        Assert.True(h.Flows(("abandoned", h.SquadA, ammo, 0)).Applied);
        Assert.Empty(h.FlowKinds());
    }

    // ---- supply, reinforcement and withdrawal -------------------------------------------------------------------------------------------

    private static CommandResolved Move(StockHarness h, params (int From, int To, string Resource, int Amount)[] moves) =>
        h.Send(StockSupplyEffect.EffectName, StockMovePayload.Write([.. moves.Select(m => new StockMove(m.From, m.To, m.Resource, m.Amount))]));

    private static CommandResolved Reinforce(StockHarness h, int depot, int squad, int count) =>
        h.Send(StockReinforceEffect.EffectName, $"{{\"depot\":{depot},\"squad\":{squad},\"count\":{count}}}");

    [Fact]
    public void RoundsMoveFromADepotToASquadAsATransferAndNoRoundIsMadeOrLost()
    {
        var h = new StockHarness();
        var (ammo, _) = h.Ammunition();
        Assert.True(h.Flows(("issued", h.DepotA, ammo, 100)).Applied);
        Assert.True(Move(h, (h.DepotA, h.SquadA, ammo, 40)).Applied);
        Assert.Equal(60, h.Stock(h.DepotA)[ammo]);
        Assert.Equal(40, h.Stock(h.SquadA)[ammo]);
        Assert.True(Move(h, (h.DepotA, h.SquadA, ammo, 20), (h.DepotA, h.SquadB, ammo, 40)).Applied);
        Assert.DoesNotContain(ammo, h.Stock(h.DepotA).Keys);
        Assert.Equal(60, h.Stock(h.SquadA)[ammo]);
        Assert.Equal(40, h.Stock(h.SquadB)[ammo]);
        Assert.Contains("StockTransfer", h.FlowKinds());
        Assert.Empty(h.Reports);
    }

    [Fact]
    public void AMoveSetThatDoesNotFitIsRefusedWholeAndChangesNothing()
    {
        var h = new StockHarness();
        var (ammo, _) = h.Ammunition();
        Assert.True(h.Flows(("issued", h.DepotA, ammo, 100)).Applied);
        var hash = h.EntitiesHash();
        // Each move fits alone; together they take more than the depot holds.
        var jointly = Move(h, (h.DepotA, h.SquadA, ammo, 60), (h.DepotA, h.SquadB, ammo, 60));
        Assert.False(jointly.Applied);
        Assert.Equal(StockCodes.Short.Id, jointly.Code);
        // The first move is good and the second names no such holder: nothing is applied.
        var unknown = Move(h, (h.DepotA, h.SquadA, ammo, 10), (h.DepotA, 9999, ammo, 10));
        Assert.Equal(Codes.NotFound.Id, unknown.Code);
        Assert.Equal(Codes.NotFound.Id, Move(h, (h.DepotA, h.SquadA, "stock.ammo.none", 1)).Code);
        Assert.Equal(100, h.Stock(h.DepotA)[ammo]);
        Assert.DoesNotContain(ammo, h.Stock(h.SquadA).Keys);
        Assert.Equal(hash, h.EntitiesHash());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"moves\":[]}")]
    [InlineData("{\"moves\":[{\"from\":3,\"to\":4,\"resource\":\"x\"}]}")]
    [InlineData("{\"moves\":[{\"from\":3,\"to\":3,\"resource\":\"x\",\"amount\":1}]}")]
    [InlineData("{\"moves\":[{\"from\":3,\"to\":4,\"resource\":\"x\",\"amount\":0}]}")]
    [InlineData("{\"moves\":[{\"from\":0,\"to\":4,\"resource\":\"x\",\"amount\":1}]}")]
    [InlineData("{\"moves\":[{\"from\":3,\"to\":4,\"resource\":\"x\",\"amount\":1,\"extra\":2}]}")]
    public void AMalformedSupplyIsRefusedAsAnInvalidArgument(string payload)
    {
        var h = new StockHarness();
        var answer = h.Send(StockSupplyEffect.EffectName, payload);
        Assert.False(answer.Applied);
        Assert.Equal(Codes.InvalidArgument.Id, answer.Code);
    }

    [Fact]
    public void AReinforcementMovesPeopleFromTheDepotAndAddsTheSameNumberOfMembersToTheSquad()
    {
        var h = new StockHarness();
        Assert.True(h.Flows(("issued", h.DepotA, StockResources.People, 50), ("issued", h.SquadA, StockResources.People, 100)).Applied);
        Assert.True(Reinforce(h, h.DepotA, h.SquadA, 10).Applied);
        Assert.Equal(40, h.Stock(h.DepotA)[StockResources.People]);
        Assert.Equal(110, h.Stock(h.SquadA)[StockResources.People]);
        var added = h.Observed.Where(o => o.Kind == "sim.event.squad-reinforced").Select(o => JsonDocument.Parse(o.Payload).RootElement).Single();
        Assert.Equal(h.SquadA, added.GetProperty("squad").GetInt32());
        Assert.Equal(10, added.GetProperty("added").GetInt32());
        Assert.Equal(110, added.GetProperty("remaining").GetInt32());   // the squad had its 100 and took in 10
        Assert.Empty(h.Reports);
    }

    [Fact]
    public void ARefusedReinforcementMovesNoPeopleAndAddsNoMembers()
    {
        var h = new StockHarness();
        Assert.True(h.Flows(("issued", h.DepotA, StockResources.People, 5), ("issued", h.SquadA, StockResources.People, 100)).Applied);
        var hash = h.EntitiesHash();
        Assert.Equal(StockCodes.Short.Id, Reinforce(h, h.DepotA, h.SquadA, 6).Code);   // the depot holds five
        Assert.Equal("characters.unknown-squad", Reinforce(h, h.DepotA, 9999, 1).Code);
        Assert.Equal("characters.too-many", Reinforce(h, h.DepotA, h.SquadA, 20_000).Code);
        Assert.Equal(Codes.InvalidArgument.Id, h.Send(StockReinforceEffect.EffectName, "{\"depot\":1}").Code);
        Assert.Equal(5, h.Stock(h.DepotA)[StockResources.People]);
        Assert.Equal(100, h.Stock(h.SquadA)[StockResources.People]);
        Assert.DoesNotContain(h.Observed, o => o.Kind == "sim.event.squad-reinforced");
        Assert.Equal(hash, h.EntitiesHash());
    }

    [Fact]
    public void AWithdrawnGroupLosesEveryPersonAndRoundItHeldAsLostNotDestroyed()
    {
        var h = new StockHarness();
        var (ammo, rounds) = h.Ammunition();
        Assert.True(h.Flows(("issued", h.SquadA, StockResources.People, 100), ("issued", h.SquadA, ammo, (int)rounds)).Applied);
        Assert.True(h.Flows(("destroyed", h.SquadA, StockResources.People, 7), ("withdrawn", h.SquadA, StockResources.People, 0), ("withdrawn", h.SquadA, ammo, 0)).Applied);
        Assert.Empty(h.Stock(h.SquadA));
        Assert.Equal(["Initialized", "Initialized", "Destroyed", "Lost", "Lost"], h.FlowKinds());
    }

    // ---- a whole fight ---------------------------------------------------------------------------------------------------------------

    private string[] Play(params string[] args)
    {
        var output = new StringWriter();
        var exit = FieldHeadlessGame.Run([.. args, "--audit", Path("audit.jsonl")], output, new StringWriter());
        Assert.Equal(0, exit);
        return output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] Field(int squadsPerSide, params string[] more) =>
        ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", squadsPerSide.ToString(CultureInfo.InvariantCulture),
         "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", squadsPerSide.ToString(CultureInfo.InvariantCulture),
         "--frame-ms", "33", "--distance", "250", .. more];

    private sealed record LedgerLine(long PeopleA, long RoundsA, long PeopleB, long RoundsB, long DepotPeopleA, long DepotRoundsA, long DepotPeopleB, long DepotRoundsB, bool Reconciled);

    private static LedgerLine Ledger(string[] lines)
    {
        var fields = lines.Single(l => l.StartsWith("ledger ", StringComparison.Ordinal)).Split(' ').Skip(1).Select(f => f.Split('=')).ToDictionary(f => f[0], f => f[1]);
        long[] Pair(string key) => fields[key].Split('/').Select(n => long.Parse(n, CultureInfo.InvariantCulture)).ToArray();
        var (a, b, da, db) = (Pair("a"), Pair("b"), Pair("depot-a"), Pair("depot-b"));
        return new LedgerLine(a[0], a[1], b[0], b[1], da[0], da[1], db[0], db[1], fields["reconciled"] == "True");
    }

    [Fact]
    public void AfterAFightTheLedgerAgreesWithTheSquadsAndWithCombat()
    {
        var args = Field(5, "--seconds", "60");
        var lines = Play(args);
        var ledger = Ledger(lines);
        Assert.True(ledger.Reconciled, string.Join('\n', lines.Where(l => l.StartsWith("ledger", StringComparison.Ordinal) || l.StartsWith("final", StringComparison.Ordinal))));
        // The people in the ledger are the people the squads report (the final line is "final a=<left>/<raised> b=<left>/<raised>").
        var final = lines.Single(l => l.StartsWith("final ", StringComparison.Ordinal)).Split(' ');
        long Left(string field) => long.Parse(field[(field.IndexOf('=') + 1)..].Split('/')[0], CultureInfo.InvariantCulture);
        // A routed group is out of the fight and holds nothing in the ledger, so the ledger holds at most the people the squads report.
        Assert.InRange(ledger.PeopleA, 0, Left(final[1]));
        Assert.InRange(ledger.PeopleB, 0, Left(final[2]));
        // Rounds were spent, and a side that was wiped out holds none.
        var forces = FieldForceSource.Load(FieldOptions.Parse(args));
        long Issued(IEnumerable<string> side) => side.Sum(g => StockContent.Rounds(forces.Groups.Single(x => x.Id == g)).Values.Sum());
        Assert.InRange(ledger.RoundsA, 0, Issued(forces.SideA) - 1);
        Assert.InRange(ledger.RoundsB, 0, Issued(forces.SideB) - 1);
        Assert.True(ledger.PeopleA == 0 || ledger.PeopleB == 0 || ledger.RoundsA > 0 || ledger.RoundsB > 0);
        Assert.Equal(0, ledger.PeopleB == 0 ? ledger.RoundsB : 0);
        Assert.Equal(0, ledger.PeopleA == 0 ? ledger.RoundsA : 0);
    }

    [Fact]
    public void AGroupWipedOutInOneRoundStillSpendsItsRoundsThenLosesWhatIsLeft()
    {
        // One squad against eight dies in the first round, but it fires in that round too: its rounds fired are consumed first, then the rest is lost.
        string[] args = ["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "1",
            "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "8", "--frame-ms", "33", "--distance", "250", "--seconds", "120"];
        var ledger = Ledger(Play(args));
        Assert.True(ledger.Reconciled);
        Assert.Equal(0, ledger.PeopleA);
        Assert.Equal(0, ledger.RoundsA);
        Assert.InRange(ledger.PeopleB, 1, 799);
        var forces = FieldForceSource.Load(FieldOptions.Parse(args));
        Assert.InRange(ledger.RoundsB, 1, forces.SideB.Sum(g => StockContent.Rounds(forces.Groups.Single(x => x.Id == g)).Values.Sum()) - 1);
    }

    private static long EndedAt(string[] lines) =>
        long.Parse(lines.Single(l => l.Contains(" ended ", StringComparison.Ordinal)).Split(' ')[0][1..], CultureInfo.InvariantCulture);

    [Fact]
    public void TheWorldRecordsAtMostTheCappedLinesPerTickAndTheFightWaitsForTheLedgerToCatchUp()
    {
        var quick = Play([.. Field(5), "--seconds", "60", "--stock-lines", "1000"]);
        var slow = Play([.. Field(5), "--seconds", "60", "--stock-lines", "2"]);
        // Two lines a tick is far fewer than a round produces, so the ledger runs behind combat; the fight is not declared over until it has caught up.
        Assert.True(Ledger(slow).Reconciled);
        Assert.True(Ledger(quick).Reconciled);
        Assert.True(EndedAt(slow) > EndedAt(quick), $"ended at {EndedAt(slow)} with 2 lines a tick and at {EndedAt(quick)} with 1000");
    }

    [Fact]
    public void SavingMidFightAndLoadingKeepsTheLedgerExactlyAsAnUninterruptedRunHasIt()
    {
        var whole = Ledger(Play([.. Field(5), "--seconds", "60"]));
        var save = Path("half.save");
        var first = Play([.. Field(5), "--seconds", "1.2", "--save", save]);
        Assert.True(Ledger(first).Reconciled);
        Assert.NotEqual(whole, Ledger(first));
        var second = Ledger(Play([.. Field(5), "--seconds", "60", "--load", save]));
        Assert.Equal(whole, second);
        Assert.True(second.Reconciled);
    }

    [Fact]
    public void AReplayOfTheWholeFightMatchesTickForTickWithTheLedgerInTheHash()
    {
        var journal = Path("stock.journal");
        Play([.. Field(5), "--seconds", "60", "--journal", journal]);
        var replay = Play([.. Field(5), "--seconds", "60", "--replay", journal]);
        Assert.Contains("replay matches=True code=REPLAY_MATCHES diverged-at=none", replay);
    }

    // ---- the layer rules, kept as tests ----------------------------------------------------------------------------------------------

    private static string StockSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "src", "GameRuntime", "GameRuntime.CombatStock"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return System.IO.Path.Combine(directory!.FullName, "src", "GameRuntime", "GameRuntime.CombatStock");
    }

    [Fact]
    public void SharedStockCodeNamesNoFlowKindAndOnlyTheFlowClassesNameTheEngineKinds()
    {
        var files = Directory.GetFiles(StockSource(), "*.cs").ToDictionary(f => System.IO.Path.GetFileName(f), f => string.Join('\n', File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))));
        var flowClasses = new[] { "IssuedFlow.cs", "ConsumedFlow.cs", "DestroyedFlow.cs", "LostFlow.cs", "AbandonedFlow.cs", "WithdrawnFlow.cs" };
        Assert.All(flowClasses, f => Assert.Contains(f, files.Keys));
        foreach (var (name, text) in files.Where(f => !flowClasses.Contains(f.Key)))
        {
            // The registry is the one place the kinds are listed; everything else reads the contract.
            if (name == "StockFlows.cs") continue;
            Assert.DoesNotContain("ResourceFlowKind.", text, StringComparison.Ordinal);
            // "destroyed" is also the name of a combat standing the bridge compares against, so the flow kind of that name is guarded by the class check below.
            foreach (var kind in new[] { "\"issued\"", "\"consumed\"", "\"lost\"", "\"abandoned\"", "\"withdrawn\"" })
                Assert.DoesNotContain(kind, text, StringComparison.Ordinal);
        }
        // The only writer of the inventory is the effect, and the bridge component never touches the world.
        Assert.Contains("CommitInventory", files["StockFlowEffect.cs"], StringComparison.Ordinal);
        Assert.DoesNotContain("CommitInventory", files["CombatStockBridge.cs"], StringComparison.Ordinal);
        Assert.DoesNotContain("World", files["CombatStockBridge.cs"], StringComparison.Ordinal);
    }
}
