namespace GameRuntime.Tutorial.Headless;

/// <summary>
/// The scripted players of the demo. The first is a newcomer: she chooses a language, goes back and chooses another, reads the story, presses keys the tutorial hears, stalls for a long time on one step so the
/// hints escalate, skips a step, talks to the mayor, ends the tutorial and then asks to see it again and skips it. The second knows the game: his first presses are quick and many, so the steps he has no need of
/// are never taught. Every action is a press on the input or a request on the session at a tick, so the same script gives the same run.
/// </summary>
internal sealed class ValleyPlayer
{
    private readonly SortedDictionary<long, List<Action>> _script = new();

    public ValleyPlayer(RuntimeSession session, InputState input, ValleyGame game)
    {
        void At(long tick, Action action)
        {
            if (!_script.TryGetValue(tick, out var list)) _script[tick] = list = new List<Action>();
            list.Add(action);
        }
        void Request(long tick, string kind, string text = "", long number = 1, string channel = "") => At(tick, () => session.Submit(new TutorialRequested("p" + tick + "-" + kind, kind, text, number, channel)));

        // The newcomer. The first-run journey: language, story, entry; the language is saved the moment it is chosen.
        Request(1, "language", "fr");
        Request(3, "story-back");
        Request(5, "language", "en");
        Request(7, "intent", "join:room.7");
        Request(9, "story-next"); Request(11, "story-next"); Request(13, "story-next"); Request(15, "story-next");
        Request(17, "entry", "host");
        Request(19, "start");
        At(40, () => input.Press("move"));
        At(41, () => input.Release("move"));
        // She stalls on looking around: the hint comes after 600 steps without doing anything.
        At(700, () => input.Press("camera.pan"));
        At(701, () => input.Release("camera.pan"));
        At(720, () => { game.MenuOpen = true; input.Press("menu.open"); });
        At(721, () => input.Release("menu.open"));
        At(900, () => game.Paused = true);
        Request(950, "dismiss-tip", "paused");              // "got it": the tip never shows again
        At(1000, () => game.Paused = false);
        // She stalls for a long time on building: three hints, each stronger.
        At(3500, () => input.Press("build.place"));
        At(3501, () => input.Release("build.place"));
        At(3510, () => game.MenuOpen = false);
        Request(3600, "skip-step", "road");
        Request(3650, "observe", "worker_hired");
        // The mayor speaks when "talk" begins (the bridge asks the dialogue engine); she reads each line for a while.
        Request(4000, "observe", "trade_made");
        Request(4020, "observe", "bond_made");
        At(4040, () => input.Press("save"));
        At(4041, () => input.Release("save"));
        At(4100, () => game.Population = 12);
        Request(4310, "observe", "levitated");                  // an event the document does not have: refused, and the tutorial is as it was
        Request(4320, "touch", "", 0, "ghost");               // a channel nobody serves: refused by the registry in the same tick
        Request(4400, "replay");
        Request(4450, "text-scale", "", 2000);
        Request(4451, "reduced-motion", "", 1);
        Request(4600, "skip-tutorial");
        Request(4650, "replay-story");
        Request(4655, "story-skip");

        // The player who knows the game, on his own channel: many quick presses, so what he already does is never taught.
        Request(2, "language", "en", 0, "expert");
        Request(3, "story-skip", "", 0, "expert");
        Request(4, "entry", "sign-in", 0, "expert");
        Request(5, "start", "", 0, "expert");
        for (var n = 0; n < 24; n++) Request(10 + n, "observe", "moved", 1, "expert");
        for (var n = 0; n < 3; n++) Request(40 + n, "observe", "menu_opened", 1, "expert");
        Request(60, "observe", "structure_built", 2, "expert");
        Request(70, "observe", "camera_moved", 1, "expert");
    }

    /// <summary>Does something later, at a tick after the current one (the player reads a line, then goes on).</summary>
    public void Later(long tick, Action action)
    {
        if (!_script.TryGetValue(tick, out var list)) _script[tick] = list = new List<Action>();
        list.Add(action);
    }

    /// <summary>Does what the script says for this tick, before the session runs it.</summary>
    public void Act(long tick)
    {
        if (!_script.TryGetValue(tick, out var actions)) return;
        foreach (var action in actions) action();
    }
}
