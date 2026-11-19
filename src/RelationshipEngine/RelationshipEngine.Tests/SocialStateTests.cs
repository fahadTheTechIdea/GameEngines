using EngineCommon;
using Xunit;

namespace RelationshipEngine.Tests;

/// <summary>The derived social state is saved state: it round-trips, it is part of the digest, a forged value is refused as a state (ranges before the digest), the old format is refused with its own code, and the explanations read it.</summary>
public sealed class SocialStateTests
{
    private static RelationshipRuntime Village()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(), "ann", "bea", "cyd", "dov");
        foreach (var a in new[] { "ann", "bea", "cyd" })
            foreach (var b in new[] { "ann", "bea", "cyd" }.Where(b => b != a))
            {
                SocialFixture.Think(runtime, a, b, 500);
                SocialFixture.Trust(runtime, a, b, 400);
            }
        SocialFixture.NextDay(runtime);
        return runtime;
    }

    private static string Open(RelationshipRuntime runtime, byte[] bytes) => Fixture.Refusal(() => RelationshipSave.Open(runtime.Set, bytes));

    [Fact]
    public void TheCommunitiesAndStandingsSurviveASaveAndTheStateGoesOnTheSame()
    {
        var runtime = Village();
        Assert.Equal("ann", runtime.Actor("cyd").Community);
        var opened = RelationshipSave.Open(runtime.Set, RelationshipSave.Write(runtime));
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
        Assert.All(runtime.Actors.Values, a => Assert.Equal((a.Community, a.Standing), (opened.Actor(a.Id).Community, opened.Actor(a.Id).Standing)));
        foreach (var each in new[] { runtime, opened }) { SocialFixture.Think(each, "dov", "bea", 400); SocialFixture.NextDay(each); SocialFixture.NextDay(each); }
        Assert.Equal(runtime.StateDigest(), opened.StateDigest());
    }

    [Fact]
    public void ACommunityOrAStandingThatDiffersIsADifferentDigest()
    {
        var runtime = Village();
        var bytes = RelationshipSave.Write(runtime);
        // The digest is taken over them: a changed value with the old digest is refused as a digest mismatch, so it cannot be slipped in.
        Assert.Equal(RelationshipCodes.SaveDigest, Open(runtime, Tamper.Stale(bytes, i => i.Actors[2].Community = "cyd")));
        Assert.Equal(RelationshipCodes.SaveDigest, Open(runtime, Tamper.Stale(bytes, i => i.Actors[1].Standing += 1)));
    }

    [Fact]
    public void AForgedCommunityOrStandingWithARecomputedDigestIsRefusedAsAState()
    {
        var runtime = Village();
        var bytes = RelationshipSave.Write(runtime);
        void Refused(string name, Action<RelationshipImage> edit) => Assert.True(RelationshipCodes.SaveState == Open(runtime, Tamper.Forge(bytes, edit)), name);
        Refused("community that is not an id", i => i.Actors[0].Community = "Not An Id");
        Refused("community after the actor in id order", i => i.Actors[0].Community = "bea");
        Refused("community with no member of that name", i => i.Actors[2].Community = "aaa");
        Refused("community named by an actor in another community", i => { i.Actors[1].Community = "ann"; i.Actors[0].Community = "ann"; i.Actors[2].Community = "bea"; });
        Refused("negative standing", i => i.Actors[0].Standing = -1);
        Refused("standing beyond the most there can be", i => i.Actors[0].Standing = RelationshipRuntime.StandingMax + 1);
    }

    [Fact]
    public void AStateWithSocialFeaturesTurnedOffCannotCarryTheirResults()
    {
        var set = SocialFixture.Set(r => { SocialFixture.Block(r, "communities")["enabled"] = false; SocialFixture.Block(r, "standing")["enabled"] = false; });
        var runtime = SocialFixture.Village(set, "ann", "bea");
        SocialFixture.NextDay(runtime);
        var bytes = RelationshipSave.Write(runtime);
        Assert.Equal(RelationshipCodes.SaveState, Open(runtime, Tamper.Forge(bytes, i => { i.Actors[1].Community = "ann"; })));
        Assert.Equal(RelationshipCodes.SaveState, Open(runtime, Tamper.Forge(bytes, i => i.Actors[0].Standing = 1001)));
        Assert.Equal(runtime.StateDigest(), RelationshipSave.Open(runtime.Set, bytes).StateDigest());
    }

    [Fact]
    public void AFormatOneSaveAndAFormatOneDocumentAreRefusedByName()
    {
        var runtime = Village();
        var bytes = RelationshipSave.Write(runtime);
        var oldSave = System.Text.Encoding.UTF8.GetString(bytes).Replace("{\"Format\":2,", "{\"Format\":1,", StringComparison.Ordinal);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetString(bytes), oldSave);
        Assert.Equal(RelationshipCodes.SaveOldFormat, Open(runtime, System.Text.Encoding.UTF8.GetBytes(oldSave)));
        // A save whose Format is not first still gets the same answer; an unknown number is the general refusal.
        Assert.Equal(RelationshipCodes.SaveFormat, Open(runtime, System.Text.Encoding.UTF8.GetBytes("{\"Format\":7}")));
        Assert.Equal(RelationshipCodes.SaveFormat, Open(runtime, System.Text.Encoding.UTF8.GetBytes("{\"Format\":")));
        Assert.Equal(RelationshipCodes.OldFormat, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => r["format"] = 1))));
        Assert.Equal(RelationshipCodes.OldFormat, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => { r["format"] = 1; r.Remove("social"); }))));
    }

    [Fact]
    public void TheDocumentRefusesEveryRuleOfTheSocialBlock()
    {
        void Bad(string name, Action<System.Text.Json.Nodes.JsonObject> edit, string code) => Assert.True(code == Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(edit)), name), name);
        Bad("social missing", r => r.Remove("social"), RelationshipCodes.MissingField);
        Bad("unknown block", r => SocialFixture.Social(r)["extra"] = 1, RelationshipCodes.UnknownField);
        Bad("unknown field", r => SocialFixture.Block(r, "standing")["extra"] = 1, RelationshipCodes.UnknownField);
        Bad("axis not declared", r => SocialFixture.Block(r, "communities")["axis"] = "nope", RelationshipCodes.Reference);
        Bad("memory not declared", r => SocialFixture.Block(r, "balance")["memory"] = "nope", RelationshipCodes.Reference);
        Bad("rounds zero", r => SocialFixture.Block(r, "communities")["maxRounds"] = 0, RelationshipCodes.Range);
        Bad("alpha zero", r => SocialFixture.Block(r, "standing")["alphaPermille"] = 0, RelationshipCodes.Range);
        Bad("line at the top of the axis", r => SocialFixture.Block(r, "communities")["above"] = 1000, RelationshipCodes.Range);
        Bad("like at the bottom of the axis", r => SocialFixture.Block(r, "balance")["like"] = -1000, RelationshipCodes.Range);
        Bad("topK nine", r => SocialFixture.Block(r, "balance")["topK"] = 9, RelationshipCodes.Range);
        Bad("trait not an id", r => SocialFixture.Block(r, "standing")["preTrustTrait"] = "Kind", RelationshipCodes.Format);
        Bad("enabled is not a boolean", r => SocialFixture.Block(r, "balance")["enabled"] = "yes", RelationshipCodes.Format);
    }

    [Fact]
    public void ADisabledBlockStillHasToBeWellFormedButMayNameNoMemory()
    {
        RelationshipLoader.Load(Edit.Doc(r => { SocialFixture.Block(r, "balance")["enabled"] = false; SocialFixture.Block(r, "balance")["memory"] = ""; }));
        Assert.Equal(RelationshipCodes.Range, Fixture.Refusal(() => RelationshipLoader.Load(Edit.Doc(r => { SocialFixture.Block(r, "balance")["enabled"] = false; SocialFixture.Block(r, "balance")["topK"] = 0; }))));
    }

    [Fact]
    public void TheCommunityExplanationNamesTheMembersAndHowManyLikedActorsShareIt()
    {
        var runtime = Village();
        var text = RelationshipExplain.Community(runtime, "cyd");
        Assert.StartsWith("community of cyd = ann (3 members: ann, bea, cyd)", text);
        Assert.Contains("likes ann: affinity 500 (weight 400, same community)", text);
        Assert.Contains("2 of 2 liked actors share the community (above 100 on affinity)", text);
        Assert.Contains("communities are off", RelationshipExplain.Community(SocialFixture.Village(SocialFixture.Set(r => SocialFixture.Block(r, "communities")["enabled"] = false), "ann"), "ann"));
    }

    [Fact]
    public void TheStandingExplanationShowsTheStoredFigureTheOneFromTheOpinionsNowAndWhoPassesTrustOn()
    {
        var runtime = Village();
        var text = RelationshipExplain.Standing(runtime, "ann");
        Assert.StartsWith("standing of ann = " + runtime.Actor("ann").Standing, text);
        Assert.Contains("from the opinions now: " + runtime.Actor("ann").Standing, text);       // nothing has changed since the boundary, so the two agree
        Assert.Contains("trusted by ", text);
        Assert.Contains("pre-trust to actors with trait kind", text);
        SocialFixture.Trust(runtime, "dov", "ann", 600);                                           // moves the figure "now" and not the stored one
        Assert.DoesNotContain("from the opinions now: " + runtime.Actor("ann").Standing + " ", RelationshipExplain.Standing(runtime, "ann"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheOpinionExplanationNamesTheFriendThePressureCameThrough()
    {
        var runtime = SocialFixture.Village(SocialFixture.Set(), "ann", "bea", "cyd");
        SocialFixture.Think(runtime, "ann", "bea", 500);
        SocialFixture.Think(runtime, "bea", "cyd", 500);
        SocialFixture.Think(runtime, "ann", "cyd", 50);
        SocialFixture.NextDay(runtime);
        var opinion = RelationshipExplain.Opinion(runtime, "ann", "cyd", "affinity");
        Assert.Equal(110, opinion.Value);
        Assert.Equal("social-pressure", opinion.Terms.Last().Id);
        Assert.Contains("pressure of the triad through bea", opinion.Terms.Last().Detail);
        Assert.Equal(opinion.Terms.Sum(t => t.Value), opinion.Sum);
        var balance = RelationshipExplain.Balance(runtime, "ann", "cyd");
        Assert.Contains("via bea: ann about bea 500 (likes), bea about cyd 500 (likes): wants likes, has neither, unbalanced, pressed", balance);
        Assert.Contains("pressure now: +60 through bea (scale 1000, age 0 of 72)", balance);
    }
}
