using System.Globalization;
using DialogueEngine;
using EngineCommon;
using GameContent.Catalog;
using GameRuntime.Field.Headless;
using ScenarioEngine.Contracts;

namespace GameRuntime.Briefing.Headless;

/// <summary>
/// One headless game that uses both new engines. A commander briefs the squads (a conversation of the dialogue engine, its text from a locale table, its speaker a character of the
/// catalog formation); the answer decides whether the squads may fire and starts a quest. The squads are then led by AI agents (the commander and quartermaster behaviours of the
/// field document). The quest completes when no foe is left standing, which the dialogue engine learns from a fact the host supplies. The output is hashed; two runs print the same.
/// </summary>
public static class BriefingHeadlessGame
{
    public const int Ok = 0, Refused = 2;

    private const string Usage = "usage: GameRuntime.Briefing.Headless [--answer go|wait] <the options of GameRuntime.Field.Headless>";

    private sealed class Reporter : IRuntimeReporter
    {
        public void Report(string code, string message, Exception? exception) => Console.Error.WriteLine(code + ": " + message);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(BriefingHeadlessGame).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("the program does not carry " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var answer = "go";
            var rest = new List<string>();
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] != "--answer") { rest.Add(args[i]); continue; }
                answer = i + 1 < args.Count && args[i + 1] is "go" or "wait" ? args[++i] : throw new FieldUsageException("--answer must be go or wait");
            }
            var options = FieldOptions.Parse(rest);
            if (options.Help) { output.WriteLine(Usage); output.WriteLine(FieldOptions.Usage); return Ok; }
            return Play(options, answer, output);
        }
        catch (FieldUsageException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine(Usage);
            return Refused;
        }
        catch (EngineRefusedException exception)
        {
            error.WriteLine(exception.Code + ": " + exception.Message);
            return Refused;
        }
    }

    private static int Play(FieldOptions options, string answer, TextWriter output)
    {
        var forces = FieldForceSource.Load(options);
        var speakers = CatalogSpeakers.FromCharacters(forces.CharactersDocument);
        var speaker = CatalogCharacters.ArchetypeId(forces.FormationA);
        var set = DialogueLoader.Load(Resource("briefing.json").Replace("{SPEAKER}", speaker, StringComparison.Ordinal), speakers);
        var locale = LocaleTable.Load(Resource("briefing-en.json"));
        LocaleCoverage.Check(set, locale);

        FieldSession? session = null;
        long Foes() => forces.SideB.Count(g => { var group = session!.Combat.Groups[g]; return !group.Destroyed && !group.Routed; });
        var director = new BriefingDirector(answer, Foes);
        var world = FieldWorld.Create(options, forces, null);
        session = FieldSession.Create(options, forces, world, new Reporter(), authorised: () => director.Authorised);
        var channels = new DialogueChannels();
        channels.Install(session.Runtime);
        IReadOnlyDictionary<string, FactValue> Facts() => new Dictionary<string, FactValue>
        {
            ["ext.foes"] = FactValue.From(Foes()),
            ["ext.squads"] = FactValue.From((long)forces.SideA.Count(g => { var group = session.Combat.Groups[g]; return !group.Destroyed && !group.Routed; })),
        };
        var dialogue = new DialogueComponent(new DialogueRuntime(set, options.Seed), speakers, Facts, channels);
        session.Runtime.AddComponent(dialogue, 300);
        session.Runtime.AddComponent(director, 290);

        var log = new OutputLog(output);
        log.Line($"game briefing {forces.Source} seed={options.Seed} answer={answer}");
        session.Runtime.Subscribe<DialogueLineShown>(e => log.Line($"t{e.Tick} says {e.Speaker}/{e.Portrait}: " + LocaleRenderer.Render(locale, e.Key, Args(e.Args))));
        session.Runtime.Subscribe<DialogueChoicesShown>(e => log.Line($"t{e.Tick} choices " + string.Join(" | ", e.Options.Split(';').Select(o => o.Split('|')).Select(o => $"{o[0]}: " + LocaleRenderer.Render(locale, o[1], [])))));
        session.Runtime.Subscribe<DialogueSignalRaised>(e => log.Line($"t{e.Tick} signal {e.Name}"));
        session.Runtime.Subscribe<DialogueQuestChanged>(e => log.Line($"t{e.Tick} quest {e.Quest} {e.Status} {e.Stage}"));
        session.Runtime.Subscribe<DialogueEnded>(e => log.Line($"t{e.Tick} briefing ended {e.Result}"));
        session.Runtime.Subscribe<DialogueRefused>(e => log.Line($"t{e.Tick} dialogue refused {e.Code}"));
        session.Runtime.Subscribe<FieldProgress>(e => log.Line($"t{e.Tick} {e.Kind} {e.Detail}"));
        session.Runtime.Subscribe<SessionEnded>(e => log.Line($"t{e.Tick} ended {e.EndingId}"));
        session.Runtime.Init();

        EngineMainLoop.Run(session.Runtime, options.TotalMicroseconds, options.RealTime ? null : options.FrameMicroseconds, _ => { });

        var state = session.Commander.State;
        log.Line($"final a={Head(session, forces.SideA)}/{state.StrengthA * forces.SquadsA} b={Head(session, forces.SideB)}/{state.StrengthB * forces.SquadsB} rounds={state.Round}");
        log.Line($"quest hold={QuestStatuses.Name(dialogue.Runtime.State.Quests["hold"].Status)} stage={dialogue.Runtime.State.Quests["hold"].Stage} answer={dialogue.Runtime.State.Variables["answer"].Text}");
        log.Line($"result state={session.Runtime.State} tick={session.Runtime.Clock.Tick} {session.Combat.Fingerprint()}");
        log.Finish();
        return session.Runtime.State == RuntimeState.Faulted ? 1 : Ok;
    }

    private static int Head(FieldSession session, IReadOnlyList<string> side) =>
        side.Sum(g => session.Commander.State.SquadOf.TryGetValue(g, out var squad) && session.Commander.State.Headcount.TryGetValue(squad, out var left) ? left : 0);

    private static IReadOnlyList<MessageArg> Args(string text) =>
        text.Length == 0 ? [] : text.Split(';').Select(e => e.Split('=')).Select(p => new MessageArg(p[0], long.TryParse(p[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? FactValue.From(n) : FactValue.From(p[1]))).ToList();
}