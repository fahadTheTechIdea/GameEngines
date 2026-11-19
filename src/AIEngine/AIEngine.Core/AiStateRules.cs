using EngineCommon;
using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>
/// The rules a state must satisfy to belong to a document: every value of the declared kind and range, every slot a value its node can hold, every percept
/// of a declared kind with the attributes it declares. The digest is not a secret, so a save passes through these as well; it cannot bring in a state the document could not have made.
/// </summary>
internal static class AiStateRules
{
    private static EngineRefusedException Refuse(string message) => new(AiCodes.SaveState, message);

    private static bool Accepts(ValueDef def, ImageValue value)
    {
        if (value.Kind != def.Kind.ToString()) return false;
        switch (def.Kind)
        {
            case FactKind.Boolean: return value.Number is 0 or 1 && value.Text is null;
            case FactKind.Integer: return value.Number >= def.Minimum && value.Number <= def.Maximum && value.Text is null;
            case FactKind.String: break;
        }
        return value.Number == 0 && value.Text is { } text && text.Length <= ValueDef.TextLimit;
    }

    private static int Bump(Dictionary<(int Kind, string Owner), int> counts, int kind, string owner)
    {
        counts.TryGetValue((kind, owner), out var count);
        return counts[(kind, owner)] = count + 1;
    }

    public static void Check(AiSet set, AiImage image)
    {
        if (image.LastTick < 0 || image.LastTick > AiRuntime.TickLimit) throw Refuse("the latest tick is outside the allowed ticks");
        foreach (var agent in image.Agents)
        {
            var where = "agent '" + agent.Id + "'";
            if (!set.Behaviors.TryGetValue(agent.Behavior, out var behavior)) throw Refuse(where + " runs the unknown behaviour '" + agent.Behavior + "'");
            if (agent.Due < 0 || agent.Due > AiRuntime.TickLimit + behavior.Every) throw Refuse(where + " is due at a tick outside the allowed ticks");
            if (agent.Roll < 0) throw Refuse(where + " has a negative roll counter");
            if (agent.Keys.Count != set.Keys.Count) throw Refuse(where + " has " + agent.Keys.Count + " keys, the document declares " + set.Keys.Count);
            for (var i = 0; i < set.Keys.Count; i++)
            {
                if (agent.Keys[i].Id != set.Keys[i].Id) throw Refuse(where + " key " + i + " is '" + agent.Keys[i].Id + "', the document has '" + set.Keys[i].Id + "'");
                if (!Accepts(set.Keys[i].Value, agent.Keys[i].Value)) throw Refuse(where + " holds a value for key '" + set.Keys[i].Id + "' that its declaration does not allow");
            }
            if (agent.Slots.Count != behavior.SlotCount) throw Refuse(where + " has " + agent.Slots.Count + " memory slots, its behaviour has " + behavior.SlotCount);
            for (var i = 0; i < agent.Slots.Count; i++)
                if (agent.Slots[i] < 0 || agent.Slots[i] > behavior.SlotMaxima[i]) throw Refuse(where + " holds " + agent.Slots[i] + " in slot " + i + ", which its node can never hold");
            var counts = new int[set.Percepts.Count];
            var perOwner = new Dictionary<(int Kind, string Owner), int>();
            foreach (var know in agent.Knowledge)
            {
                if (!set.TryPercept(know.Kind, out var kind)) throw Refuse(where + " knows the unknown kind '" + know.Kind + "'");
                if (kind.Owned ? know.Owner.Length == 0 || know.Owner.Length > 128 : know.Owner.Length != 0) throw Refuse(where + " knows '" + know.Id + "' with an owner the kind does not allow");
                if (know.Id.Length == 0 || know.Id.Length > 128) throw Refuse(where + " knows a thing with an id of the wrong length");
                if (know.Seen < 0 || know.Seen > AiRuntime.TickLimit) throw Refuse(where + " knows '" + know.Id + "' from a tick outside the allowed ticks");
                if (++counts[kind.Index] > kind.Capacity) throw Refuse(where + " knows more things of kind '" + kind.Id + "' than its capacity " + kind.Capacity);
                if (kind.Owned && Bump(perOwner, kind.Index, know.Owner) > kind.OwnerCapacity) throw Refuse(where + " knows more things of kind '" + kind.Id + "' held by '" + know.Owner + "' than the owner capacity " + kind.OwnerCapacity);
                if (know.Values.Count != kind.Attrs.Count) throw Refuse(where + " knows '" + know.Id + "' with " + know.Values.Count + " attributes, the kind has " + kind.Attrs.Count);
                for (var i = 0; i < kind.Attrs.Count; i++)
                    if (!Accepts(kind.Attrs[i], know.Values[i])) throw Refuse(where + " holds a value for attribute '" + kind.Attrs[i].Id + "' of '" + know.Id + "' that its declaration does not allow");
            }
        }
    }
}