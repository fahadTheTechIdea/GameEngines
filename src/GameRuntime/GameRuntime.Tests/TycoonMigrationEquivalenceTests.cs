using GameRuntime.Headless;
using Xunit;

namespace GameRuntime.Tests;

/// <summary>
/// The tycoon bots used to be two hand-written policy classes; they are now behaviours of an AI document. These are the output hashes the hand-written classes produced for the
/// configurations below, recorded before they were replaced. The AI-driven bots must print exactly the same output, byte for byte: every decision, every refusal, every tick.
/// </summary>
public sealed class TycoonMigrationEquivalenceTests
{
    private const string ScriptOne = "at 0.2 autosell 10\nat 0.8 autosell off\n";
    private const string ScriptTwo = "at 0.5 sell 9\nat 0.6 sell 9\nat 0.7 speed 3/1\n";

    private static readonly (string Name, string Args, string Script, string Hash)[] Games =
    [
        ("ty-greedy-s1", "--player greedy --seed 1 --frame-ms 100 --seconds 120", "", "0FBAEFA37147EC47F5DB95ED83CBE65F51138F51C0946C451B63A9FA9E10835A"),
        ("ty-cautious-s1", "--player cautious --seed 1 --frame-ms 100 --seconds 120", "", "95CE95B3E22E7ACBAC3E590D4CB7089925EBC4157BE80782F131177AD61B8041"),
        ("ty-greedy-s2", "--player greedy --seed 2 --frame-ms 100 --seconds 120", "", "92260362D76EF79533B10733D058E210294FFBDFA08106618BF2A44030A52A51"),
        ("ty-cautious-s2", "--player cautious --seed 2 --frame-ms 100 --seconds 120", "", "98C5D3B440912FCFBE1A999002EEB2098FF610AB8617EC062F80182B71D37566"),
        ("ty-greedy-s3", "--player greedy --seed 3 --frame-ms 100 --seconds 120", "", "BFF1760C4D58DD93CFEA09F54F87E321CEE73CADCE46573B4963F341B3466528"),
        ("ty-cautious-s3", "--player cautious --seed 3 --frame-ms 100 --seconds 120", "", "77E35F11BE878A875B90727953FBCFC55A208DED0A58625436919C5BBA1BDBA4"),
        ("ty-greedy-s4", "--player greedy --seed 4 --frame-ms 100 --seconds 120", "", "3C72C6912445A0F3DEE9C842E30B345F76559A8350C14296E9D4033456D4B20B"),
        ("ty-cautious-s4", "--player cautious --seed 4 --frame-ms 100 --seconds 120", "", "728562FC49281929297EFD1E82394CF4AFAA89A3A8F3A569F1F4A64807E25FCB"),
        ("ty-greedy-s5", "--player greedy --seed 5 --frame-ms 100 --seconds 120", "", "D8E2373BD2EDBF311F1487ACC43DA34E620FA97FB444AAF80FAFC51F204ADAFF"),
        ("ty-cautious-s5", "--player cautious --seed 5 --frame-ms 100 --seconds 120", "", "952726E6490944EA95FECBAA416E10165B3738993D1CC844E0A638E671CD9AEF"),
        ("ty-greedy-s6", "--player greedy --seed 6 --frame-ms 100 --seconds 120", "", "766C81AF26506DCC10273DB31FB13EAC3E31462B472DF57A2CFD5BEF9C12FDAF"),
        ("ty-cautious-s6", "--player cautious --seed 6 --frame-ms 100 --seconds 120", "", "77640A5382FE64BDDA8FD6400E9D25E2C5347BEF1BDCC5C75FEA5D287AA35EFD"),
        ("ty-greedy-s7", "--player greedy --seed 7 --frame-ms 100 --seconds 120", "", "07D4C3B4BA8FF9398CBAF6EA3D0D40E8E905B12C69C078E7F604DB18B7ED21F6"),
        ("ty-cautious-s7", "--player cautious --seed 7 --frame-ms 100 --seconds 120", "", "AE544247E18D404B9CAECC253EA909D0F355C2AE46E45106FF4FD90A75803BF5"),
        ("ty-greedy-s8", "--player greedy --seed 8 --frame-ms 100 --seconds 120", "", "F7672BB1AB329AEABF87CD496401D618A482448B9A2FB5B9102F546CF53729DD"),
        ("ty-cautious-s8", "--player cautious --seed 8 --frame-ms 100 --seconds 120", "", "088ED7B533073CEF599221E0D8C23D2A20400C33C84CF9BC5769D2C0E5363175"),
        ("ty-greedy-s9", "--player greedy --seed 9 --frame-ms 100 --seconds 120", "", "4E3730DDE55700D945BF46479232F42824FE818EBEB552212BC4732AA98F8767"),
        ("ty-cautious-s9", "--player cautious --seed 9 --frame-ms 100 --seconds 120", "", "FBDB632B9EBF0A0907CE82202B53A0D327AF73092169881C8047A4471C461504"),
        ("ty-greedy-s10", "--player greedy --seed 10 --frame-ms 100 --seconds 120", "", "49AD6ED891439E02D24E3395A7000A235D2640AB4DCD05BE22523D163FF05546"),
        ("ty-cautious-s10", "--player cautious --seed 10 --frame-ms 100 --seconds 120", "", "EAEE49DA161AD53C56510B319461947D54B2482CF2061A5CBAEF8E4D20388DFE"),
        ("ty-greedy-s11", "--player greedy --seed 11 --frame-ms 100 --seconds 120", "", "30B0B93B1D1454597C7AF3868D2B84AEE807703A588DFC68C61074A36CB882DF"),
        ("ty-cautious-s11", "--player cautious --seed 11 --frame-ms 100 --seconds 120", "", "9D1CBF044C96F71D917D372B0DA5CDFCD2E0793B135C6CFD1801D7A486491107"),
        ("ty-greedy-s12", "--player greedy --seed 12 --frame-ms 100 --seconds 120", "", "1FBD2E62D75E7FCCB550A920965824B49B387DD2ADB504759EDA992136E42DEA"),
        ("ty-cautious-s12", "--player cautious --seed 12 --frame-ms 100 --seconds 120", "", "BEC2F1D3CE387AB24A1F9687299AC240EEBD0DEAE17A7575ECA2A21B15BDFCB6"),
        ("ty-greedy-script1", "--player greedy --seed 4 --frame-ms 100 --seconds 60", "s1", "25FAE312DB49176312AEDB8F567E626726F0A4E631959CA5290F787B6A99EFA3"),
        ("ty-greedy-script2", "--player greedy --seed 7 --frame-ms 50 --seconds 60", "s2", "AAE4491B703A86145A0D9806E8FFAECEE55333AED3F8A4002B61D4AAA418EDE5"),
        ("ty-greedy-slowframe", "--player greedy --seed 9 --frame-ms 333 --seconds 90", "", "743519DDCBBF8FF2DB069318DC91BAB19D291989BEB2C14B40555FC61D392D1E"),
        ("ty-cautious-script1", "--player cautious --seed 4 --frame-ms 100 --seconds 60", "s1", "556E7ACACFACE57F8A42794793A74F640D0AACD53D51676CE17B740460FA8CD9"),
        ("ty-cautious-script2", "--player cautious --seed 7 --frame-ms 50 --seconds 60", "s2", "3DFD15EA5A75C4EEBA0753E65B86D7B68DC1F2A6DA9D1CD8AC8CB4D19F59939E"),
        ("ty-cautious-slowframe", "--player cautious --seed 9 --frame-ms 333 --seconds 90", "", "1FD8C8BD9167D9778C1551187B379EACA5D65211964F41C6C4731DF1B876AE24"),
        ("ty-none-script", "--seed 2 --frame-ms 100 --seconds 30", "s2", "F37635B2DCBDDA2A5C5715CF4EDE62C7FACF5623B7B24C3A2C01E2A2733FAB74"),
    ];

    public static IEnumerable<object[]> Configurations() => Games.Select(g => new object[] { g.Name });

    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task TheAiDrivenBotPrintsWhatTheHandWrittenOneDid(string name)
    {
        var (_, arguments, script, expected) = Games.Single(g => g.Name == name);
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var directory = Directory.CreateTempSubdirectory("tycoon-equivalence-");
        try
        {
            if (script.Length > 0)
            {
                var path = Path.Combine(directory.FullName, "script.txt");
                File.WriteAllText(path, script == "s1" ? ScriptOne : ScriptTwo);
                args.AddRange(["--script", path]);
            }
            args.AddRange(["--audit", Path.Combine(directory.FullName, "audit.jsonl")]);
            var output = new StringWriter();
            var exit = await HeadlessGame.RunAsync(args, output, new StringWriter());
            Assert.Equal(0, exit);
            Assert.Equal(expected, output.ToString().Split('\n').Single(l => l.StartsWith("output-sha256=", StringComparison.Ordinal)).Trim()["output-sha256=".Length..]);
        }
        finally { directory.Delete(true); }
    }
}