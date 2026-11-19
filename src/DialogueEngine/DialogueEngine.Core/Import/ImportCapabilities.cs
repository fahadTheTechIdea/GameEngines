using System.Text;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>What a converter does with one feature of its source format.</summary>
public enum CapabilityStatus { Converted, Approximated, Dropped, Refused }

/// <summary>
/// One row of a converter's capability matrix. <paramref name="Key"/> is the feature name the conversion report uses for it (null for a refused feature, which stops the conversion), and
/// <paramref name="Sample"/> is a complete small source that exercises exactly this row, so the table is checked by running it and cannot drift from the code. A refused row says which code it
/// is refused with (<see cref="Code"/>); a row whose sample needs allowed functions or several starts says so (<see cref="Functions"/>, <see cref="Starts"/>).
/// </summary>
public sealed record Capability(string Feature, string? Key, CapabilityStatus Status, string Note, string Sample)
{
    /// <summary>For a refused row: the code the conversion stops with.</summary>
    public string Code { get; init; } = DialogueCodes.ImportUnsupported;

    /// <summary>The allowed functions the sample is converted with.</summary>
    public IReadOnlyList<ImportFunction> Functions { get; init; } = [];

    /// <summary>The start nodes the sample is converted with (Yarn); null for the one start.</summary>
    public IReadOnlyList<string>? Starts { get; init; }

    /// <summary>The conversion options the sample runs with.</summary>
    public ImportOptions OptionsFor(ImportOptions basis) => basis with { Functions = Functions, Starts = Starts };
}

/// <summary>The capability matrices of the converters: what each source feature becomes, or why it is refused.</summary>
public static class ImportCapabilities
{
    private const string Head = "title: Start\n---\n";
    private const string Tail = "\n===\n";

    private static string Yarn(string body, string? title = null) => (title is null ? Head : "title: " + title + "\n---\n") + body + Tail;

    private static readonly ImportFunction HasKey = ImportFunction.Parse("has_key()=has-key:bool");

    public static IReadOnlyList<Capability> YarnSpinner { get; } =
    [
        new("nodes (title, ---, ===)", "nodes", CapabilityStatus.Converted, "all nodes reachable from the start by jumps become nodes of one conversation", Yarn("Ann: Hi.")),
        new("a line with a speaker (Name: text)", "line", CapabilityStatus.Converted, "a line node; the speaker is the lower-cased name and must be a character of the game catalog", Yarn("Ann: Hi.")),
        new("a line without a speaker", "line without a speaker", CapabilityStatus.Approximated, "spoken by 'narrator'", Yarn("Hello there.")),
        new("options (->) with indented bodies", "options", CapabilityStatus.Converted, "a choice node; the flow goes on after the group", Yarn("-> Yes\n    Ann: Good.\n-> No\nAnn: Bye.")),
        new("an option's <<if>> condition", "option condition", CapabilityStatus.Approximated, "a false condition hides the option; with none left the conversation goes on after the group", Yarn("<<declare $k = true>>\n-> Yes <<if $k>>\n-> No")),
        new("<<jump Node>>", "jump", CapabilityStatus.Converted, "an edge of the conversation", Yarn("<<jump Other>>") + Yarn("Ann: Other.", "Other")),
        new("<<detour Node>>", "detour", CapabilityStatus.Converted, "a call of a conversation made from the detoured node, which comes back to the next statement; a jump inside it leaves for good, as in Yarn Spinner", Yarn("<<detour Other>>\nAnn: After.") + Yarn("Ann: Other.", "Other")),
        new("<<return>>", "return", CapabilityStatus.Converted, "ends a detour early; outside a detour it stops the dialogue", Yarn("<<detour Other>>\nAnn: After.") + Yarn("Ann: One.\n<<return>>\nAnn: Never.", "Other")),
        new("<<stop>>", "stop", CapabilityStatus.Converted, "an end node", Yarn("Ann: Hi.\n<<stop>>")),
        new("<<once>> ... <<else>> ... <<endonce>>, <<once if cond>>", "once", CapabilityStatus.Converted, "a branch on a true/false variable of the document that is set when the content first runs", Yarn("<<once>>\nAnn: Hi.\n<<else>>\nAnn: Again.\n<<endonce>>")),
        new("a line ending in <<once>> or <<once if cond>>", "once", CapabilityStatus.Converted, "the same, around that one line", Yarn("Ann: Who are you? <<once>>\nAnn: Bye.")),
        new("an option ending in <<once>> or <<once if cond>>", "once (option)", CapabilityStatus.Converted, "an answer that is hidden once it was taken", Yarn("-> Ask <<once>>\n    Ann: Hi.\n-> Leave")),
        new("<<enum Name>> with <<case X>> and <<endenum>>", "enum", CapabilityStatus.Converted, "a text variable that may hold only the cases (Name.Case and .Case shorthand in set and conditions)", Yarn("<<enum Mood>>\n<<case Calm>>\n<<case Angry>>\n<<endenum>>\n<<declare $m = Mood.Calm>>\n<<set $m to .Angry>>\n<<if $m == Mood.Angry>>\nAnn: Grr.\n<<endif>>")),
        new("<<declare $x = value>> (whole number, true/false, text, enum case)", "declare", CapabilityStatus.Converted, "a variable of the document (a number is -1,000,000,000 to 1,000,000,000)", Yarn("<<declare $gold = 5>>\nAnn: Hi.")),
        new("<<set $x to value>>, += and -=, $x = $x + n", "set", CapabilityStatus.Converted, "a set or add effect", Yarn("<<declare $gold = 5>>\n<<set $gold to $gold + 2>>\nAnn: Hi.")),
        new("<<if>>, <<elseif>>, <<else>>, <<endif>>", "if / elseif / else", CapabilityStatus.Converted, "a branch node (at most 16 branches)", Yarn("<<declare $k = true>>\n<<if $k>>\nAnn: Yes.\n<<else>>\nAnn: No.\n<<endif>>")),
        new("conditions: and, or, not, == != < <= > >=, a variable against a value", "conditions", CapabilityStatus.Converted, "the condition grammar of the document", Yarn("<<declare $g = 5>>\n<<if $g >= 3 and not $g == 9>>\nAnn: Yes.\n<<endif>>")),
        new("{$variable} in text", "{$variable} in text", CapabilityStatus.Converted, "a placeholder filled from the variable when the line is reached", Yarn("<<declare $gold = 5>>\nAnn: I have {$gold}.")),
        new("#tags at the end of a line", "#tags", CapabilityStatus.Converted, "tags of the line (letters, digits, - . _)", Yarn("Ann: Hi. #happy")),
        new("a command with arguments (<<wave Bob 3 \"to you\" {$gold}>>)", "command with arguments", CapabilityStatus.Converted, "a signal that carries the arguments: whole numbers, true/false, text (quoted or a bare word) and variables read when the signal is raised", Yarn("<<declare $gold = 5>>\n<<wave Bob 3 \"to you\" {$gold}>>\nAnn: Hi.")),
        new("a command with no arguments (<<name>>)", "command without arguments", CapabilityStatus.Approximated, "a signal of that name for the game to act on", Yarn("<<shake>>\nAnn: Hi.")),
        new("a function the caller allows, in a condition or in text", "function (allowed)", CapabilityStatus.Converted, "the external fact the host supplies for that exact call (--function 'has_key()=has-key:bool')", Yarn("<<if has_key()>>\nAnn: Open.\n<<endif>>")) { Functions = [HasKey] },
        new("visited(\"Node\") and visited_count(\"Node\")", "visited / visited_count", CapabilityStatus.Approximated, "a number variable counts entries into the node (Yarn Spinner counts a node entered and left)", Yarn("<<if visited(\"Other\")>>\nAnn: Back.\n<<endif>>\n<<jump Other>>") + Yarn("Ann: Other.", "Other")),
        new("several start nodes", "start nodes", CapabilityStatus.Converted, "one conversation per start node (talk-<title>), as StartDialogue(node) picks the entry; visit counts are shared: a node entered through any start counts for all of them", Yarn("Ann: One.") + Yarn("Ann: Two.", "Other")) { Starts = ["start", "other"] },
        new("<<set $x to dice(n)>>, <<set $x to random_range(a, b)>>", "set (roll)", CapabilityStatus.Converted, "a seeded roll of the document (the roll counter is saved), every value equally likely; at most 100 values; whole numbers", Yarn("<<declare $r = 0>>\n<<set $r to dice(6)>>\nAnn: Rolled.")),
        new("a roll compared with a whole number in a condition (dice(6) >= 5, random_range(1, 10) < 4)", "random in conditions", CapabilityStatus.Approximated, "one seeded roll made before the statement, into a hidden true/false variable the condition reads; the rolls of an if / elseif chain are all made first", Yarn("<<if dice(6) >= 5>>\nAnn: Lucky.\n<<endif>>")),
        new("random() compared with a decimal (random() < 0.25)", "random in conditions", CapabilityStatus.Approximated, "the same, with the probability kept to the thousandth", Yarn("<<if random() < 0.25>>\nAnn: Lucky.\n<<endif>>")),
        new("#line:id tags", "#line: tags", CapabilityStatus.Dropped, "Yarn Spinner's localisation ids; keys are made new", Yarn("Ann: Hi. #line:abc123")),
        new("headers other than title", "headers other than title", CapabilityStatus.Dropped, "editor metadata (color, group, position)", "title: Start\ncolor: red\n---\nAnn: Hi.\n===\n"),
        new("nodes no jump from the start leads to", "nodes not reachable from the start", CapabilityStatus.Dropped, "left out; choose another start (or all starts) to convert them", Yarn("Ann: Hi.") + Yarn("Ann: Alone.", "Alone")),
        new("[markup] in text", "markup", CapabilityStatus.Approximated, "kept in the text for the game's renderer", Yarn("Ann: [b]Hi[/b].")),
        new("a function that is not on the allowed list", null, CapabilityStatus.Refused, "a document reads declared facts, not functions; allow a call with --function and the host supplies its answer", Yarn("<<if roll_it() == 6>>\nAnn: Hi.\n<<endif>>")) { Code = DialogueCodes.ImportFunction },
        new("random() stored in a variable", null, CapabilityStatus.Refused, "random() is a decimal between 0 and 1 and variables are whole numbers; compare it in a condition or use dice(n)", Yarn("<<declare $r = 0>>\n<<set $r to random()>>\nAnn: Hi.")),
        new("random() compared with == or !=", null, CapabilityStatus.Refused, "a number in [0, 1) is never exactly a value; use < or >", Yarn("<<if random() == 0.5>>\nAnn: Hi.\n<<endif>>")),
        new("dice or random_range over more than 100 values in <<set>>", null, CapabilityStatus.Refused, "an assignment rolls in two stages of at most 16 outcomes", Yarn("<<declare $r = 0>>\n<<set $r to dice(101)>>\nAnn: Hi.")),
        new("a roll compared with a variable", null, CapabilityStatus.Refused, "a roll is compared with a number written in the script", Yarn("<<declare $n = 3>>\n<<if dice(6) > $n>>\nAnn: Hi.\n<<endif>>")),
        new("<<call function(...)>>", null, CapabilityStatus.Refused, "a function run for its effect; an allowed function only answers a fact", Yarn("<<call do_it()>>\nAnn: Hi.")) { Code = DialogueCodes.ImportFunction },
        new("a detour that leads back to itself", null, CapabilityStatus.Refused, "conversations may not call each other in a circle", Yarn("<<detour Start>>\nAnn: Hi.")) { Code = DialogueCodes.ImportInvalid },
        new("an enum case with a value", null, CapabilityStatus.Refused, "a case is a name", Yarn("<<enum Mood>>\n<<case Calm = 1>>\n<<endenum>>\nAnn: Hi.")),
        new("decimal numbers", null, CapabilityStatus.Refused, "variables are whole numbers", Yarn("<<declare $x = 1.5>>\nAnn: Hi.")),
        new("arithmetic between variables in <<set>>", null, CapabilityStatus.Refused, "an add takes a whole number", Yarn("<<declare $a = 1>>\n<<declare $b = 1>>\n<<set $a to $a + $b>>\nAnn: Hi.")),
        new("an expression in {braces}", null, CapabilityStatus.Refused, "a placeholder shows one variable or one allowed call", Yarn("<<declare $a = 1>>\nAnn: {$a + 1}")),
        new("an expression as a command argument", null, CapabilityStatus.Refused, "an argument is a value or a variable", Yarn("<<declare $a = 1>>\n<<give {$a + 1}>>\nAnn: Hi.")),
        new("a variable in an option's text", null, CapabilityStatus.Refused, "an answer takes no placeholder", Yarn("<<declare $a = 1>>\n-> Pay {$a}")),
        new("<<jump {$expression}>>", null, CapabilityStatus.Refused, "a jump names its node", Yarn("<<declare $a = 1>>\n<<jump {$a}>>")),
        new("comparing two variables", null, CapabilityStatus.Refused, "a condition compares a variable with a value", Yarn("<<declare $a = 1>>\n<<declare $b = 1>>\n<<if $a == $b>>\nAnn: Hi.\n<<endif>>")),
        new("a command inside a line of text", null, CapabilityStatus.Refused, "commands stand on their own line", Yarn("Ann: Hi <<shake>> there.")),
    ];

    private const string InkHead = "{\"inkVersion\":21,\"root\":[";
    private const string InkTail = "],\"listDefs\":{}}";

    private static string Ink(string flow, string? knots = null, bool closed = false) => InkHead + "[" + flow + (closed ? "" : ",null") + "],\"done\"," + (knots ?? "null") + InkTail;

    /// <summary>A story with a list definition: <c>mood</c> has the members calm (1) and angry (2).</summary>
    private static string InkLists(string flow, string knots) => InkHead + "[" + flow + ",null],\"done\"," + knots + "],\"listDefs\":{\"mood\":{\"calm\":1,\"angry\":2}}}";

    private static string Globals(string declarations) => "\"global decl\":[\"ev\"," + declarations + ",\"/ev\",\"end\",null]";

    private const string ChoiceOnly = "\"ev\",\"str\",\"^Take the road\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^You walk.\",\"\\n\",\"end\",{\"#f\":5}]}";
    private const string OnceOnly = "\"ev\",\"str\",\"^Take the road\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":20},{\"c-0\":[\"\\n\",\"^You walk.\",\"\\n\",\"end\",{\"#f\":5}]}";
    private const string StartContent = "\"ev\",{\"^->\":\"0.0.$r1\"},{\"temp=\":\"$r\"},\"str\",{\"->\":\".^.s\"},[{\"#n\":\"$r1\"}],\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":18},"
        + "{\"s\":[\"^Hello\",{\"->\":\"$r\",\"var\":true},null],\"c-0\":[\"ev\",{\"^->\":\"0.0.c-0.$r2\"},\"/ev\",{\"temp=\":\"$r\"},{\"->\":\".^.^.s\"},[{\"#n\":\"$r2\"}],\"^ there.\",\"\\n\",\"end\",{\"#f\":5}]}";

    /// <summary>
    /// A sequence container the way the compiler is understood to write it: <c>visit</c>, a limit and <c>MIN</c> (stopping, once), <c>%</c> (cycle) or <c>seq</c> (shuffle), then for each branch a conditional
    /// divert on <c>du N ==</c> to the named branch (which pops the position, says its text and diverts to the <c>nop</c> after the dispatch).
    /// </summary>
    public static string InkSequence(string selector, params string[] branchTexts)
    {
        var items = new StringBuilder("\"ev\",\"visit\",").Append(selector).Append(",\"/ev\"");
        for (var i = 0; i < branchTexts.Length; i++) items.Append(",\"ev\",\"du\",").Append(i).Append(",\"==\",\"/ev\",{\"->\":\".^.s").Append(i).Append("\",\"c\":true}");
        var nop = 5 + 6 * branchTexts.Length;   // "ev" "visit" N op "/ev" are five items, each dispatch is six, and the nop follows
        items.Append(",\"nop\",{");
        for (var i = 0; i < branchTexts.Length; i++)
        {
            if (i > 0) items.Append(',');
            items.Append("\"s").Append(i).Append("\":[\"pop\",");
            if (branchTexts[i].Length > 0) items.Append("\"^").Append(branchTexts[i]).Append("\",\"\\n\",");
            items.Append("{\"->\":\".^.^.").Append(nop).Append("\"},null]");
        }
        return items.Append(",\"#f\":5}").ToString();
    }

    /// <summary>
    /// A shuffle once / shuffle stopping container the way the compiler is understood to write it (from memory of the compiler, not from the format document, which does not describe it): the position is
    /// <c>MIN(visit, last)</c>, a conditional divert skips the shuffle when the position already is the last branch, otherwise <c>last seq</c> shuffles the branches before it, then the same dispatch as any
    /// sequence. The last branch is empty for shuffle once, the final text for shuffle stopping.
    /// </summary>
    public static string InkShuffleLimited(int last, params string[] branchTexts)
    {
        var items = new StringBuilder("\"ev\",\"visit\",").Append(last).Append(",\"MIN\",\"du\",").Append(last).Append(",\"==\",{\"->\":\".^.10\",\"c\":true},").Append(last).Append(",\"seq\",\"nop\",\"/ev\"");
        for (var i = 0; i < branchTexts.Length; i++) items.Append(",\"ev\",\"du\",").Append(i).Append(",\"==\",\"/ev\",{\"->\":\".^.s").Append(i).Append("\",\"c\":true}");
        var nop = 12 + 6 * branchTexts.Length;   // twelve items of selector, then six for each dispatch, then the nop
        items.Append(",\"nop\",{");
        for (var i = 0; i < branchTexts.Length; i++)
        {
            if (i > 0) items.Append(',');
            items.Append("\"s").Append(i).Append("\":[\"pop\",");
            if (branchTexts[i].Length > 0) items.Append("\"^").Append(branchTexts[i]).Append("\",\"\\n\",");
            items.Append("{\"->\":\".^.^.").Append(nop).Append("\"},null]");
        }
        return items.Append(",\"#f\":5}").ToString();
    }

    public static IReadOnlyList<Capability> InkJson { get; } =
    [
        new("text and newlines", "text", CapabilityStatus.Converted, "a line spoken by 'narrator'; Ink has no speakers", Ink("\"^Hello there.\",\"\\n\",\"done\"")),
        new("tags (# ... /#)", "tags", CapabilityStatus.Converted, "tags of the line", Ink("\"^Hello.\",\"#\",\"^happy\",\"/#\",\"\\n\",\"done\"")),
        new("tags that are not ids", "tags that are not ids", CapabilityStatus.Dropped, "a tag may use letters, digits, - . _ only", Ink("\"^Hello.\",\"#\",\"^speaker: Bob\",\"/#\",\"\\n\",\"done\"")),
        new("diverts to knots and stitches, gathers", "divert", CapabilityStatus.Converted, "an edge of the conversation", Ink("\"^Start\",\"\\n\",{\"->\":\"hall\"}", "{\"hall\":[\"^In the hall.\",\"\\n\",\"end\",null]}")),
        new("-> DONE and -> END", "done / end", CapabilityStatus.Converted, "an end node", Ink("\"^Bye.\",\"\\n\",\"end\"")),
        new("glue (<>)", "glue (<>)", CapabilityStatus.Converted, "the line before the glue carries a glue flag; the host joins it to the next line", Ink("\"^Hello\",\"<>\",\"\\n\",\"^there.\",\"\\n\",\"done\"")),
        new("sticky choices (+) with choice-only content [..]", "sticky choices (+)", CapabilityStatus.Converted, "a choice node", Ink(ChoiceOnly, null, true)),
        new("choices with start content (text before [ ] or no brackets)", "choices", CapabilityStatus.Converted, "the answer shows the start content followed by the choice-only content, and the branch echoes it", Ink(StartContent, null, true)),
        new("once-only choices (*)", "once-only choices (*)", CapabilityStatus.Approximated, "spent by a true/false variable set when taken; with all spent the conversation ends", Ink(OnceOnly, null, true)),
        new("choices with a condition", "conditional choices", CapabilityStatus.Converted, "an answer that is hidden while its condition is false", Ink("\"ev\",{\"VAR?\":\"gold\"},0,\">\",\"/ev\",\"ev\",\"str\",\"^Buy\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":5},{\"c-0\":[\"\\n\",\"^Bought.\",\"\\n\",\"end\",{\"#f\":5}]}", "{" + Globals("5,{\"VAR=\":\"gold\"}") + "}", true)),
        new("VAR declarations and printing a variable ({x})", "variables", CapabilityStatus.Converted, "a variable of the document with the type of its first value; printing becomes a placeholder", Ink("\"^Gold: \",\"ev\",{\"VAR?\":\"gold\"},\"out\",\"/ev\",\"\\n\",\"done\"", "{" + Globals("5,{\"VAR=\":\"gold\"}") + "}")),
        new("assignments (~ x = x + 1, ~ x = 3)", "assignments", CapabilityStatus.Converted, "a set or add effect", Ink("\"ev\",{\"VAR?\":\"gold\"},3,\"+\",{\"VAR=\":\"gold\",\"re\":true},\"/ev\",\"^Gold: \",\"ev\",{\"VAR?\":\"gold\"},\"out\",\"/ev\",\"\\n\",\"done\"", "{" + Globals("5,{\"VAR=\":\"gold\"}") + "}")),
        new("temporary variables (~ temp x)", "temp variables", CapabilityStatus.Approximated, "one variable of the document for each knot or stitch that declares the name, set again each time its declaration runs (Ink drops a temporary when its knot ends)", Ink("\"ev\",7,{\"temp=\":\"t\"},\"/ev\",\"^T: \",\"ev\",{\"VAR?\":\"t\"},\"out\",\"/ev\",\"\\n\",\"done\"")),

        new("conditional diverts", "conditional diverts", CapabilityStatus.Converted, "a branch node on the condition", Ink("\"ev\",{\"VAR?\":\"gold\"},3,\">\",\"/ev\",{\"->\":\"rich\",\"c\":true},\"^Poor.\",\"\\n\",\"done\"", "{\"rich\":[\"^Rich.\",\"\\n\",\"done\",null]," + Globals("5,{\"VAR=\":\"gold\"}") + "}")),
        new("conditional text and if / else blocks", "conditional diverts", CapabilityStatus.Converted, "a branch node whose branches come back to the content after them", Ink("\"ev\",{\"VAR?\":\"gold\"},3,\">\",\"/ev\",[{\"->\":\".^.b\",\"c\":true},{\"b\":[\"\\n\",\"^Rich.\",\"\\n\",{\"->\":\"0.7\"},null]}],[{\"->\":\".^.b\"},{\"b\":[\"\\n\",\"^Poor.\",\"\\n\",{\"->\":\"0.7\"},null]}],\"nop\",\"\\n\",\"done\"", "{" + Globals("5,{\"VAR=\":\"gold\"}") + "}")),
        new("visit counts ({knot}, CNT?)", "visit counts (CNT?)", CapabilityStatus.Approximated, "a number variable counts entries into the container at its start", Ink("{\"->\":\"hall\"}", "{\"hall\":[\"ev\",{\"CNT?\":\"hall\"},1,\"==\",\"/ev\",{\"->\":\".^.first\",\"c\":true},\"^Again.\",\"\\n\",\"done\",{\"first\":[\"^First visit.\",\"\\n\",\"done\",null]}]}")),
        new("sequences {a|b|c} (stopping)", "sequences, cycles, once and shuffle", CapabilityStatus.Converted, "a variant node in sequence mode", Ink("[" + InkSequence("2,\"MIN\"", "A", "B", "C") + "],\"done\"")),
        new("cycles {&a|b}", "sequences, cycles, once and shuffle", CapabilityStatus.Converted, "a variant node in cycle mode", Ink("[" + InkSequence("2,\"%\"", "A", "B") + "],\"done\"")),
        new("once-only content {!a|b}", "sequences, cycles, once and shuffle", CapabilityStatus.Converted, "a variant node in once mode: nothing after the last branch", Ink("[" + InkSequence("2,\"MIN\"", "A", "B", "") + "],\"done\"")),
        new("shuffles {~a|b}", "shuffle", CapabilityStatus.Approximated, "a variant node in shuffle mode: seeded rounds, not Ink's random pick", Ink("[" + InkSequence("2,\"seq\"", "A", "B") + "],\"done\"")),
        new("tunnels (-> knot -> and ->->)", "tunnels", CapabilityStatus.Converted, "a conversation that is called and returns", Ink("{\"->t->\":\"scene\"},\"^Back.\",\"\\n\",\"done\"", "{\"scene\":[\"^In the scene.\",\"\\n\",\"->->\",null]}")),
        new("threads that only offer choices (<- knot)", "threads (choices only)", CapabilityStatus.Converted, "the thread's choices join the choices of the place it is pulled into", Ink("\"^Pick.\",\"\\n\",\"thread\",{\"->\":\"opts\"},\"done\"", "{\"opts\":[\"ev\",\"str\",\"^Left\",\"/str\",\"/ev\",{\"*\":\".^.c-0\",\"flg\":4},{\"c-0\":[\"\\n\",\"^Went left.\",\"\\n\",\"end\",{\"#f\":5}]}]}")),
        new("LIST definitions and list operations (+=, -=, ?)", "lists (LIST)", CapabilityStatus.Approximated, "one true/false variable per member", InkLists("\"ev\",{\"VAR?\":\"m\"},{\"list\":{\"mood.calm\":1},\"origins\":[\"mood\"]},\"?\",\"/ev\",{\"->\":\"calmly\",\"c\":true},\"^Edgy.\",\"\\n\",\"done\"", "{\"calmly\":[\"^Calm.\",\"\\n\",\"done\",null]," + Globals("{\"list\":{\"mood.calm\":1},\"origins\":[\"mood\"]},{\"VAR=\":\"m\"}") + "}")),
        new("external functions the caller allows", "external functions (allowed)", CapabilityStatus.Converted, "the external fact the host supplies for that exact call (--function 'has_item(\"sword\")=has-sword:bool')", Ink("\"ev\",\"str\",\"^sword\",\"/str\",{\"x()\":\"has_item\",\"exArgs\":1},\"/ev\",{\"->\":\"yes\",\"c\":true},\"^No sword.\",\"\\n\",\"done\"", "{\"yes\":[\"^Sword!\",\"\\n\",\"done\",null]}")) { Functions = [ImportFunction.Parse("has_item(\"sword\")=has-sword:bool")] },
        new("an external function that is not allowed", null, CapabilityStatus.Refused, "the host answers declared facts only", Ink("\"ev\",{\"x()\":\"ping\",\"exArgs\":0},\"/ev\",{\"->\":\"a\",\"c\":true},\"done\"", "{\"a\":[\"done\",null]}")) { Code = DialogueCodes.ImportFunction },
        new("invisible default (fallback) choices", null, CapabilityStatus.Refused, "a choice without text", Ink("{\"*\":\".^.c-0\",\"flg\":24},{\"c-0\":[\"end\",null]}", null, true)),
        new("functions defined in Ink (~ return)", null, CapabilityStatus.Refused, "a function computes a value", Ink("{\"f()\":\"fn\"},\"done\"", "{\"fn\":[\"^x\",\"\\n\",\"end\",null]}")),
        new("diverts to a variable", null, CapabilityStatus.Refused, "a divert names its place", Ink("{\"->\":\"x\",\"var\":true},\"done\"")),
        new("decimal numbers", null, CapabilityStatus.Refused, "variables are whole numbers", Ink("\"ev\",1.5,\"out\",\"/ev\",\"done\"")),
        new("POW, FLOOR, random numbers and the other arithmetic functions", null, CapabilityStatus.Refused, "only + - and comparisons are converted", Ink("\"ev\",2,3,\"POW\",\"out\",\"/ev\",\"done\"")),
        new("LIST_COUNT and the other list functions", null, CapabilityStatus.Refused, "only add, remove and has are converted", Ink("\"ev\",{\"list\":{\"mood.calm\":1}},\"LIST_COUNT\",\"out\",\"/ev\",\"done\"")),
        new("a tunnel return outside a tunnel", null, CapabilityStatus.Refused, "there is nowhere to return to", Ink("\"->->\"")),
        new("threads that say text or set variables (<- knot)", "threads (text and choices)", CapabilityStatus.Converted, "the thread runs where it is pulled in: its text and variable changes come in order, then the flow goes on; its choices join the choices of the place", Ink("\"^Pick.\",\"\\n\",\"thread\",{\"->\":\"chat\"},\"^After.\",\"\\n\",\"done\"", "{\"chat\":[\"^Hello.\",\"\\n\",\"done\",null]}")),
        new("a thread whose content goes on with a divert", null, CapabilityStatus.Refused, "the content after the divert is part of the thread and may leave the place it was pulled in at", Ink("\"thread\",{\"->\":\"chat\"},\"done\"", "{\"chat\":[\"^Hello.\",\"\\n\",{\"->\":\"other\"},null],\"other\":[\"^Elsewhere.\",\"\\n\",\"end\",null]}")),

        new("glue at the start of content reached by a divert that follows a said line", "glue (<>)", CapabilityStatus.Converted, "the line said before the divert carries the glue flag, so the host joins it to the content the divert reaches", Ink("\"^Hello\",\"\\n\",{\"->\":\"k\"}", "{\"k\":[\"<>\",\"^there.\",\"\\n\",\"done\",null]}")),
        new("glue at the start of content reached by a divert that does not follow a said line", null, CapabilityStatus.Refused, "which line it joins depends on the path taken", Ink("{\"->\":\"k\"}", "{\"k\":[\"<>\",\"^x\",\"\\n\",\"done\",null]}")),

        new("shuffle stopping ({shuffle stopping: a|b|c})", "shuffle once / stopping", CapabilityStatus.Approximated, "a shuffled pass over the branches before the last, then the last for ever; the order is this engine's seeded one", Ink("[" + InkShuffleLimited(2, "A", "B", "C") + "],\"done\"")),
        new("shuffle once ({shuffle once: a|b})", "shuffle once / stopping", CapabilityStatus.Approximated, "a shuffled pass over the branches, then nothing", Ink("[" + InkShuffleLimited(2, "A", "B", "") + "],\"done\"")),
        new("a shuffle in a shape the compiler is not understood to write", null, CapabilityStatus.Refused, "only {~a|b}, shuffle once and shuffle stopping in the shape described are converted", Ink("\"ev\",\"visit\",1,\"MIN\",2,\"seq\",\"/ev\",\"done\"")),

        new("comparing two variables", null, CapabilityStatus.Refused, "a condition compares a variable with a value", Ink("\"ev\",{\"VAR?\":\"a\"},{\"VAR?\":\"b\"},\"==\",\"/ev\",{\"->\":\"x\",\"c\":true},\"done\"", "{\"x\":[\"done\",null]," + Globals("1,{\"VAR=\":\"a\"},1,{\"VAR=\":\"b\"}") + "}")),
    ];

    /// <summary>The matrix as text, one row per feature, for the tool and the docs.</summary>
    public static string Render(IReadOnlyList<Capability> matrix)
    {
        var text = new StringBuilder();
        foreach (var row in matrix) text.Append(row.Status.ToString().PadRight(12)).Append(row.Feature).Append(": ").AppendLine(row.Note);
        return text.ToString();
    }
}
