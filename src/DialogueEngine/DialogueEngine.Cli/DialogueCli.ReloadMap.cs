using DialogueEngine;
using EngineCommon;

public static partial class DialogueCli
{
    /// <summary>
    /// <c>reload-map &lt;old document&gt; &lt;new document&gt; &lt;map&gt;</c> checks a reload map against the two documents a hot reload would move between, the way the reload itself does, without a running session:
    /// every old name exists in the old document, every new one in the new, and no two old things become one. Whether a particular session fits the new document is the reload's own check.
    /// </summary>
    private static int ReloadMapCheck(string oldDocument, string newDocument, string map, TextWriter output)
    {
        try
        {
            var parsed = ReloadMap.Parse(map);
            parsed.Check(Load(oldDocument), Load(newDocument));
            output.WriteLine($"ok map: {parsed.Conversations.Count} conversations, {parsed.Nodes.Count} nodes, {parsed.Variables.Count} variables, {parsed.Quests.Count} quests, {parsed.Stages.Count} stages renamed");
            return 0;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }
}
