using System.Text.Json.Nodes;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>The weather: the Markov chain keeps the climate whatever its persistence, the temperature follows the zone, rain and snow and the water they make add up, and nothing depends on anything but the seed.</summary>
public sealed class ClimateTests
{
    private static void Persistence(JsonObject root, int permille)
    {
        foreach (var season in Tiny.Zone(root)["seasons"]!.AsArray()) season!["persistence"] = permille;
    }

    private static void SameClimate(JsonObject root)
    {
        // Both seasons the same, so a long run measures one climate.
        var seasons = Tiny.Zone(root)["seasons"]!.AsArray();
        seasons[1]!["temp"] = seasons[0]!["temp"]!.GetValue<int>();
    }

    private static (double Wet, double SpellLength, int Spells) Weather(int persistence, int steps = 20_000, int seed = 7)
    {
        var runtime = Fixture.MiniWorld(root => { Persistence(root, persistence); SameClimate(root); root["seed"] = seed; });
        var wet = 0;
        var spells = 0;
        var last = -1;
        for (var step = 0; step < steps; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            var weather = runtime.Regions[0].Weather;
            if (weather == 1) wet++;
            if (weather != last) spells++;
            last = weather;
        }
        return ((double)wet / steps, (double)steps / spells, spells);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(900)]
    public void TheLongRunShareOfEachWeatherIsItsWeightWhateverThePersistence(int persistence)
    {
        var (wet, _, _) = Weather(persistence);
        Assert.InRange(wet, 0.44, 0.56);
    }

    [Fact]
    public void PersistenceLengthensSpellsWithoutChangingTheShare()
    {
        var (_, restless, _) = Weather(0);
        var (_, sticky, _) = Weather(900);
        Assert.InRange(restless, 1.7, 2.3);          // each step a fresh fair draw: a spell of the same weather lasts two steps on average
        Assert.InRange(sticky, 15.0, 25.0);          // stays with probability 0.95: one change in twenty
    }

    [Fact]
    public void TheTemperatureFollowsTheZoneSeasonAndItsSpreadIsTheZonesSigma()
    {
        var runtime = Fixture.MiniWorld(root => Tiny.Zone(root)["seasons"]![0]!["sigma"] = 400);
        var warm = new List<double>();
        var cold = new List<double>();
        for (var step = 0; step < 20_000; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            (step % 20 < 10 ? warm : cold).Add(runtime.Regions[0].Temp);
        }
        Assert.InRange(warm.Average(), 1400, 1600);
        Assert.InRange(cold.Average(), -600, -400);
        var spread = Math.Sqrt(warm.Sum(t => (t - warm.Average()) * (t - warm.Average())) / warm.Count);
        Assert.InRange(spread, 300, 480);            // sigma 400 in the warm season (the anomaly carries over from the cold, whose sigma is 300)
    }

    [Fact]
    public void TheWeathersOwnOffsetMovesTheTemperature()
    {
        var plain = Fixture.MiniWorld(root => { SameClimate(root); root["weathers"]![1]!["tempOffset"] = 0; });
        var offset = Fixture.MiniWorld(root => { SameClimate(root); root["weathers"]![1]!["tempOffset"] = -500; });
        double dry = 0, wet = 0;
        int nDry = 0, nWet = 0;
        for (var step = 0; step < 6000; step++)
        {
            plain.Advance(step, plain.CycleSeasonAt, Fixture.Host());
            offset.Advance(step, offset.CycleSeasonAt, Fixture.Host());
            Assert.Equal(plain.Regions[0].Weather, offset.Regions[0].Weather);        // the offset moves the temperature, not the weather
            Assert.Equal(plain.Regions[0].Anomaly, offset.Regions[0].Anomaly);
            if (plain.Regions[0].Weather == 1) { wet += offset.Regions[0].Temp - plain.Regions[0].Temp; nWet++; }
            else { dry += offset.Regions[0].Temp - plain.Regions[0].Temp; nDry++; }
        }
        Assert.Equal(-500.0, wet / nWet, 3);
        Assert.Equal(0.0, dry / nDry, 3);
    }

    [Fact]
    public void RainFallsOnlyInAWetWeatherAndIsItsMeanAmountOnAverage()
    {
        var runtime = Fixture.MiniWorld(SameClimate);
        long total = 0;
        var wet = 0;
        for (var step = 0; step < 20_000; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            var state = runtime.Regions[0];
            if (state.Weather == 0) Assert.Equal(0, state.Precip);
            else
            {
                Assert.InRange(state.Precip, 40, 160);          // the weather's 100 times a uniform 0.4 to 1.6
                total += state.Precip;
                wet++;
            }
        }
        Assert.InRange((double)total / wet, 95, 105);
    }

    [Fact]
    public void AHeavyTailMakesNowAndThenSeveralTimesTheRain()
    {
        var runtime = Fixture.MiniWorld(root => { SameClimate(root); root["hydrology"]!["heavyPermille"] = 100; root["hydrology"]!["heavyFactor"] = 3000; });
        var heavy = 0;
        var wet = 0;
        for (var step = 0; step < 20_000; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            var state = runtime.Regions[0];
            if (state.Weather == 0) continue;
            wet++;
            if (state.Precip > 160) heavy++;
        }
        Assert.InRange((double)heavy / wet, 0.08, 0.12);
    }

    [Fact]
    public void SnowCollectsBelowFreezingAndMeltsIntoTheWaterWhenItIsWarm()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Zone(root)["seasons"]![1]!["temp"] = -2000;
            Tiny.Zone(root)["seasons"]![1]!["sigma"] = 0;
            Tiny.Zone(root)["seasons"]![0]!["temp"] = 1500;
            Tiny.Zone(root)["seasons"]![0]!["sigma"] = 0;
            root["weathers"]![1]!["tempOffset"] = 0;
        });
        long peakSnow = 0;
        for (var step = 10; step < 20; step++)
        {
            runtime.Advance(step - 10, runtime.CycleSeasonAt, Fixture.Host());
        }
        // steps 0..9 are warm; 10..19 are cold
        for (var step = 10; step < 20; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            peakSnow = Math.Max(peakSnow, runtime.Regions[0].Snow);
            if (runtime.Regions[0].Weather == 1) Assert.True(runtime.Regions[0].Temp < 0);
        }
        Assert.True(peakSnow > 0);
        var cold = runtime.Regions[0].Snow;
        var river = runtime.Regions[0].River;
        for (var step = 20; step < 30; step++) runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
        Assert.True(runtime.Regions[0].Snow < cold || cold == 0);
        Assert.True(runtime.Regions[0].River >= 0 && runtime.Regions[0].River != river || cold == 0);
    }

    [Fact]
    public void TheWaterObeysItsOwnRules()
    {
        var runtime = Fixture.MiniWorld();
        long wetness = 0, dry = 0;
        for (var step = 0; step < 3000; step++)
        {
            runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host());
            var state = runtime.Regions[0];
            wetness = wetness * (1000 - 140) / 1000 + state.Precip;
            dry = state.Precip == 0 ? dry + 1 : 0;
            Assert.Equal(wetness, state.Wetness);             // an independent running sum of the same rule
            Assert.Equal(dry, state.DryStreak);
            Assert.InRange(state.SoilTenths, 0, 1000);        // the bucket never holds more than its capacity or less than nothing
            Assert.True(state.River >= 0 && state.Snow >= 0);
        }
    }

    [Fact]
    public void ARegimeThatForbidsRainIsNeverWet()
    {
        var dry = Fixture.MiniWorld(root => Tiny.Array(root, "regimes")[0]!["bias"] = new JsonArray(1000, 0));
        var wet = Fixture.MiniWorld(root => Tiny.Array(root, "regimes")[0]!["bias"] = new JsonArray(0, 1000));
        for (var step = 0; step < 300; step++)
        {
            dry.Advance(step, dry.CycleSeasonAt, Fixture.Host());
            wet.Advance(step, wet.CycleSeasonAt, Fixture.Host());
            if (step > 0) Assert.Equal(0, dry.Regions[0].Weather);
            if (step > 0) Assert.Equal(1, wet.Regions[0].Weather);
        }
    }

    [Fact]
    public void TheRegimeChangesBetweenSeasonsByTheirFrequencyAndHoldsByItsPersistence()
    {
        var runtime = Fixture.MiniWorld(root =>
        {
            Tiny.Array(root, "regimes")[0]!["persistence"] = 900;
            var second = Tiny.Copy(Tiny.Array(root, "regimes")[0]!).AsObject();
            second["id"] = "r2";
            second["key"] = "r.r2";
            second["frequency"] = new JsonArray(0, 1);       // only in the cold
            Tiny.Array(root, "regimes").Add(second);
        });
        var changes = new List<WorldChange>();
        for (var step = 0; step < 4000; step++) changes.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, Fixture.Host()).Items);
        var regime = changes.Where(c => c.Kind == ChangeKind.Regime).ToList();
        Assert.NotEmpty(regime);
        Assert.All(regime.Where(c => c.Index == 1), c => Assert.Equal("cold", Fixture.Season(c.Step)));      // the second regime is chosen only when the season is cold
        Assert.InRange(regime.Count, 20, 400);                                                                  // one change in ten steps at most
    }

    [Fact]
    public void TheSeedAloneDecidesTheWeather()
    {
        string Run(int seed)
        {
            var runtime = Fixture.MiniWorld(root => root["seed"] = seed);
            Fixture.Live(runtime, 400);
            return runtime.StateDigest();
        }
        Assert.Equal(Run(7), Run(7));
        Assert.NotEqual(Run(7), Run(8));
    }

    [Fact]
    public void AWeatherChangeIsRecordedWithTheNewWeatherTheOldOneAndTheTemperature()
    {
        var runtime = Fixture.MiniWorld();
        var changes = Fixture.Live(runtime, 200).Of(ChangeKind.Weather);
        Assert.NotEmpty(changes);
        foreach (var change in changes)
        {
            Assert.NotEqual(change.Index, change.Sub);
            Assert.InRange(change.Amount, -WorldFacts.MaxTemperature, WorldFacts.MaxTemperature);
        }
    }
}
