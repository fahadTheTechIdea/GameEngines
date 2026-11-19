using GameRuntime.Combat.Headless;
using GameRuntime.Field.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The commanders, the quartermaster and the skirmish bots used to be hand-written classes; they are now behaviours of two AI documents run by the AI engine. These are the output hashes
/// those hand-written classes produced for the configurations below, recorded before they were replaced. The AI-driven games must print exactly the same output, byte for byte, so
/// the migration changed no behaviour: every order, every tick, every round, every request to combat and every ledger line. A hash that changes is a behaviour that changed.
/// </summary>
public sealed class AiMigrationEquivalenceTests
{
    private static readonly (string Name, string Args, string Hash)[] Games =
    [
        ("sk-aggressor-aw-d40", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.iron-warband --frame-ms 100 --seconds 120 --player aggressor --distance 40 --round-seconds 3", "CA485EE2D81286EBB6FF2A5AEA66126322E144307E12E3E3DEB0135DE634EF38"),
        ("sk-aggressor-ah-d200-s2", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.hunting-band --frame-ms 100 --seconds 90 --player aggressor --distance 200 --seed 2", "71DDF05B44DC551F3BC8349DC03DFA34B46D88A6B4E5BDBF7EE4131ABA0FDE45"),
        ("sk-aggressor-mp-d300", "--era modern --side-a arms-modern.rifle-platoon --side-b arms-modern.rifle-platoon --frame-ms 100 --seconds 90 --player aggressor", "BEBAB72BCE67E5247F376C52466BC6C13413356A2660884326CBD4D24905EAF5"),
        ("sk-aggressor-mc-d500-r5", "--era modern --side-a arms-modern.rifle-company --side-b arms-modern.rifle-platoon --frame-ms 100 --seconds 90 --player aggressor --distance 500 --round-seconds 5", "BA4C372AC840175289DA0C1FA8C8780E3A85F5E69948E2C6A3654A95C0AEC54F"),
        ("sk-aggressor-cross-d150-s3", "--pack-a arms-modern --pack-b arms-ancient --side-a arms-modern.rifle-platoon --side-b arms-ancient.iron-warband --frame-ms 100 --seconds 90 --player aggressor --distance 150 --seed 3", "3FEE78FC4394AE79B0A6C0143DE71377F775495763D47775C0EE88DF06B5D8A9"),
        ("sk-skirmisher-aw-d40", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.iron-warband --frame-ms 100 --seconds 120 --player skirmisher --distance 40 --round-seconds 3", "671EBB60989F8C1BEDFF55E304F1DA8C3EE2EF83E604B452528E18AECCB38DD1"),
        ("sk-skirmisher-ah-d200-s2", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.hunting-band --frame-ms 100 --seconds 90 --player skirmisher --distance 200 --seed 2", "9C3E3C2004731595E8BACC5817387E2FD82E0BCF79AB0494E802CCC01094B12C"),
        ("sk-skirmisher-mp-d300", "--era modern --side-a arms-modern.rifle-platoon --side-b arms-modern.rifle-platoon --frame-ms 100 --seconds 90 --player skirmisher", "3E62A61D2EA88F80EEE4BD6E150FF52F6E1F497F9298B2729223882271DC3384"),
        ("sk-skirmisher-mc-d500-r5", "--era modern --side-a arms-modern.rifle-company --side-b arms-modern.rifle-platoon --frame-ms 100 --seconds 90 --player skirmisher --distance 500 --round-seconds 5", "9135F59F1AF12FFD86D8C1EBEBDEAADC6FEE9C9FA231F24FF2EAF00F5115F69B"),
        ("sk-skirmisher-cross-d150-s3", "--pack-a arms-modern --pack-b arms-ancient --side-a arms-modern.rifle-platoon --side-b arms-ancient.iron-warband --frame-ms 100 --seconds 90 --player skirmisher --distance 150 --seed 3", "1C917BCEE94C4C471C5A0600D8D007375480DA9FD47192B259E07C8E8BD30B1A"),
        ("sk-defender-aw-d40", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.iron-warband --frame-ms 100 --seconds 120 --player defender --distance 40 --round-seconds 3", "BA3EE0473D278901D6659E182A820CABC0082F0CA718DA26856A5A0183D9B4E9"),
        ("sk-defender-ah-d200-s2", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.hunting-band --frame-ms 100 --seconds 90 --player defender --distance 200 --seed 2", "F58033F903E30535CAB4E208B3976D4B41D56D660D3417FC3A3E7ECAD39B1E39"),
        ("sk-defender-mp-d300", "--era modern --side-a arms-modern.rifle-platoon --side-b arms-modern.rifle-platoon --frame-ms 100 --seconds 90 --player defender", "09AE7DCFFB80DA0F15BBD7E52ACCBD97641084A3863B43713B1E9517F7ADD946"),
        ("sk-defender-mc-d500-r5", "--era modern --side-a arms-modern.rifle-company --side-b arms-modern.rifle-platoon --frame-ms 100 --seconds 90 --player defender --distance 500 --round-seconds 5", "342F62C7ECBD2445C33D066721E055222FF2B9C5D0E1DD0F69ED78CEBDF5D6A0"),
        ("sk-defender-cross-d150-s3", "--pack-a arms-modern --pack-b arms-ancient --side-a arms-modern.rifle-platoon --side-b arms-ancient.iron-warband --frame-ms 100 --seconds 90 --player defender --distance 150 --seed 3", "9E4A2C329790C9C80DAB034182B6445FC73023B8C6053A215C93D67A5387FEEF"),
        ("sk-melee-3way", "--era modern --side-a arms-modern.rifle-platoon --side-b arms-modern.tank-platoon --side-c arms-modern.rifle-company --teams 1,2,1 --frame-ms 100 --seconds 90 --player melee", "3DE1179DB0FEF5E9D244C3CAD230396DFCB93BC37D1044A9C0EF38C1FA73DAA0"),
        ("sk-melee-3way-d80-s2", "--era modern --side-a arms-modern.rifle-platoon --side-b arms-modern.tank-platoon --side-c arms-modern.rifle-company --teams 1,2,1 --frame-ms 100 --seconds 90 --player melee --distance 80 --seed 2", "9E7B1F108EA8111CE2C7CB2017C50D4B4904A686BE5618AC957951EF6EC8080B"),
        ("sk-melee-4way", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.iron-warband --side-c arms-ancient.hunting-band --side-d arms-ancient.iron-warband --teams 1,1,2,2 --frame-ms 100 --seconds 90 --player melee --distance 120", "75F3E92D50A5033303E04CB087774893350DDAFD6ADF69F86762D649D41FC3FA"),
        ("sk-melee-duel", "--era modern --side-a arms-modern.rifle-platoon --side-b arms-modern.rifle-platoon --frame-ms 100 --seconds 90 --player melee --distance 100", "72FF6E850D856F1C48E4698A4E2AA92268214F107CBE637FE745C11305C1E2ED"),
        ("sk-none-script", "--era ancient --side-a arms-ancient.iron-warband --side-b arms-ancient.hunting-band --frame-ms 100 --seconds 10 --player none", "0F1B5AA8CBA0CA5B50C27B3081B06ADCC6079334095D194DFC6C9145EA2CA1B6"),
        ("fd-1v1", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 1 --squads-b 1 --frame-ms 100 --seconds 60", "7DB7ECB5F7D3453DA9A1EB008185357F004D2E4A855658DFC34A0F408988D602"),
        ("fd-2v2-d250", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 2 --squads-b 2 --frame-ms 100 --seconds 60 --distance 250", "353286E3EFFC6D9231C6F79BAFDF46156944AB484581C283E6F46B684AB9BB23"),
        ("fd-3v3-s2", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 3 --squads-b 3 --frame-ms 100 --seconds 90 --seed 2", "AF0A120C6CA5B1D729FEA37D0D4CEE6084C4A60D0C1248294B1071B85BC19AAE"),
        ("fd-5v5-d250", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 5 --squads-b 5 --frame-ms 100 --seconds 60 --distance 250", "10DABEA0A56AFED1C9C3019D41A3E87AF10D5A6863743EFE0A3D07678AF400A6"),
        ("fd-3v2-uneven", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 3 --squads-b 2 --frame-ms 100 --seconds 60 --distance 200 --round-seconds 3", "4984A1D0487F7532AF57FB8D422EE1B50DBA9DCFD58017C11C1F8603233ACC3F"),
        ("fd-depot0", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 2 --squads-b 2 --frame-ms 100 --seconds 60 --depot-percent 0", "7285D0ED1785F0B8C30970AADBF0A6679CD0B57F6169ABEEEBD22C90582E060B"),
        ("fd-depot100", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 2 --squads-b 2 --frame-ms 100 --seconds 90 --depot-percent 100 --distance 400", "D149B2428E403089E7D3BE941413284742D86197DBFE6F3FA8E2DC8C5DFB6DEF"),
        ("fd-stock-lines-2", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 2 --squads-b 2 --frame-ms 100 --seconds 60 --stock-lines 2", "406B8BC6E8DCB650A93D0B99A6B4EFBDAED06BB66749E5CBBA264F83DB718498"),
        ("fd-tanks", "--pack-a arms-modern --formation-a arms-modern.tank-platoon --squads-a 3 --pack-b arms-modern --formation-b arms-modern.tank-platoon --squads-b 3 --frame-ms 100 --seconds 90 --distance 600", "ECAAE26FEB1BBC4C39B0C08FC7BECC618378925C3C47F07A4F3D243004FB2DBA"),
        ("fd-cross-era", "--pack-a arms-modern --formation-a arms-modern.rifle-company --squads-a 2 --pack-b arms-ancient --formation-b arms-ancient.iron-warband --squads-b 4 --frame-ms 100 --seconds 60 --distance 100", "FE3E3B83F42F28BF5F53A090DA03CA836D3160292DB870DC00B194B9F3A1314E"),
        ("fd-long", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 4 --squads-b 4 --frame-ms 100 --seconds 180 --distance 300 --seed 7", "1311C7E9B61A20A8B0CBFFDEFDB64E2BF92B80D4C1300A97664DAF1F11658DD1"),
        ("fd-tanks-depot100", "--pack-a arms-modern --formation-a arms-modern.tank-platoon --squads-a 2 --pack-b arms-modern --formation-b arms-modern.tank-platoon --squads-b 2 --frame-ms 100 --seconds 120 --distance 500 --depot-percent 100", "044737C82FD828FAE9C1C28FEECE3CF727C6DBC416DBA4DCFAB9A951D31FD316"),
        ("fd-reinforce-s3-lines8", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 2 --squads-b 3 --frame-ms 100 --seconds 90 --distance 350 --depot-percent 100 --seed 3 --stock-lines 8", "07F1269DF3A410B1EED91056DE2BA5D19929CCB217F5B4A51B8153453C2B31C3"),
        ("fd-mixed-depot50", "--pack-a arms-modern --formation-a arms-modern.tank-platoon --squads-a 2 --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-b 3 --frame-ms 100 --seconds 120 --distance 450 --depot-percent 50 --seed 5", "A0DEC1284973CEE6759C7AE267117952A7BF4EA83B466C52A8800EA24F28F323"),
        ("fd-scarce-4v4-d3", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 4 --squads-b 4 --frame-ms 100 --seconds 120 --distance 400 --depot-percent 3", "C810F794FE62E6887AAF1F920BF6AE4478EF113AF8C19952BE068DBFFB13121B"),
        ("fd-scarce-tanks-d10", "--pack-a arms-modern --formation-a arms-modern.tank-platoon --pack-b arms-modern --formation-b arms-modern.tank-platoon --squads-a 3 --squads-b 3 --frame-ms 100 --seconds 150 --distance 700 --depot-percent 10", "799557A8CA0FBCE9960921B99F21347EC88FDE7FFA692C7F9BD8808D269F719F"),
        ("fd-scarce-5v3-lines3", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 5 --squads-b 3 --frame-ms 100 --seconds 120 --distance 500 --depot-percent 8 --stock-lines 3 --seed 4", "8AF98D30E73E5A95CC060A0C283205427B98934BF05CAFF3F1ABFDDAFEF7E2C3"),
        ("fd-scarce-mixed-d20", "--pack-a arms-modern --formation-a arms-modern.rifle-company --squads-a 2 --pack-b arms-modern --formation-b arms-modern.tank-platoon --squads-b 2 --frame-ms 100 --seconds 150 --distance 600 --depot-percent 20 --seed 9", "68D78A3DD79E2AC88EA72B3F59E44840D1DFC650C600F4C509D73F0CB8CC5DF5"),
        ("fd-scarce-6v6-d5", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 6 --squads-b 6 --frame-ms 100 --seconds 150 --distance 350 --depot-percent 5 --seed 2", "EADE015635AB7B272BE1829BA6FC047927FF16EEDB1A46AC315A1B51AB81B659"),
        ("fd-scarce-4v4-r2", "--pack-a arms-modern --formation-a arms-modern.rifle-company --pack-b arms-modern --formation-b arms-modern.rifle-company --squads-a 4 --squads-b 4 --frame-ms 100 --seconds 120 --distance 450 --depot-percent 15 --round-seconds 2", "EE0B91704F776EB543135060266176E925AF96262C6C5BE85579E6991C947767"),
        ("fd-scarce-tanks-d40", "--pack-a arms-modern --formation-a arms-modern.tank-platoon --pack-b arms-modern --formation-b arms-modern.tank-platoon --squads-a 2 --squads-b 4 --frame-ms 100 --seconds 150 --distance 800 --depot-percent 40 --seed 6", "39D9AB60E8D1A80F7E4E88CD8454CD92A18147BE5A0AE446031570B331671A90"),
    ];

    public static IEnumerable<object[]> Configurations() => Games.Select(g => new object[] { g.Name });

    [Theory]
    [MemberData(nameof(Configurations))]
    public void TheAiDrivenGamePrintsWhatTheHandWrittenOneDid(string name)
    {
        var (_, arguments, expected) = Games.Single(g => g.Name == name);
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var directory = Directory.CreateTempSubdirectory("ai-equivalence-");
        try
        {
            args.AddRange(["--audit", Path.Combine(directory.FullName, "audit.jsonl")]);
            var output = new StringWriter();
            var error = new StringWriter();
            var exit = name.StartsWith("sk-", StringComparison.Ordinal) ? SkirmishHeadlessGame.Run(args, output, error) : FieldHeadlessGame.Run(args, output, error);
            Assert.True(exit == 0, "exit " + exit + ": " + error);
            var hash = output.ToString().Split('\n').Single(l => l.StartsWith("output-sha256=", StringComparison.Ordinal)).Trim()["output-sha256=".Length..];
            Assert.Equal(expected, hash);
        }
        finally { directory.Delete(true); }
    }
}