using System.Text.Json;
using EngineCommon;

/// <summary>
/// The strict reader of the tree interchange. A file that is not the interchange (an unknown or missing field, a wrong type, a repeated id, a number out of range) is refused whole with the first
/// mistake. A file that is the interchange but uses something the engine cannot run (a float, a category set, a missing-value route, a fact that is not a host key) is read to the end
/// and each such place is added to the report, so the author sees every one at once.
/// </summary>
internal sealed class TreeReader(TreeReport report)
{
    public const int MaxNodes = 1024;
    public const int MaxFeatures = 256;
    public const int MaxActions = 64;
    public const long NumberLimit = 1_000_000_000_000;

    private readonly StrictJson _json = new(TreeCodes.ForDocument);

    public TreeSource Read(string text)
    {
        using var document = _json.Parse(text, "the tree");
        var root = _json.Object(document.RootElement, "the tree", ["format", "id", "every", "features", "actions", "root", "nodes"]);
        if (_json.Long(root, "format", "the tree", long.MinValue, long.MaxValue) != 1) throw _json.Refuse(TreeCodes.Format, "this reader reads interchange format 1 only");
        var id = _json.Id(root, "id", "the tree");
        var every = _json.Long(root, "every", "the tree", 1, 100_000);
        var features = ReadFeatures(Sized(root, "features", MaxFeatures, 0));
        var actions = ReadActions(Sized(root, "actions", MaxActions, 1));
        var rootId = _json.Id(root, "root", "the tree");
        var nodes = ReadNodes(Sized(root, "nodes", MaxNodes, 1));
        return new TreeSource(id, every, features, actions, rootId, nodes);
    }

    /// <summary>An array of at least <paramref name="minimum"/> entries; one over <paramref name="maximum"/> is too big for the engine, which is a refusal with its own code.</summary>
    private JsonElement Sized(JsonElement owner, string field, int maximum, int minimum)
    {
        var array = _json.Array(owner, field, "the tree", minimum, int.MaxValue);
        return array.GetArrayLength() <= maximum ? array : throw _json.Refuse(TreeCodes.TooBig, "'" + field + "' holds " + array.GetArrayLength() + " entries; the importer takes at most " + maximum);
    }

    private IReadOnlyDictionary<string, TreeFeature> ReadFeatures(JsonElement array)
    {
        var result = new Dictionary<string, TreeFeature>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "features[" + n++ + "]";
            _json.Object(element, where, ["id", "fact", "min", "max", "initial"]);
            var id = _json.Id(element, "id", where);
            if (result.ContainsKey(id)) throw _json.Refuse(TreeCodes.Duplicate, "feature '" + id + "' is declared twice");
            var fact = _json.Text(element, "fact", where, 140);
            var min = _json.Long(element, "min", where, -NumberLimit, NumberLimit);
            var max = _json.Long(element, "max", where, -NumberLimit, NumberLimit);
            var initial = _json.Long(element, "initial", where, -NumberLimit, NumberLimit);
            if (min > max) throw _json.Refuse(TreeCodes.Range, "feature '" + id + "' has min above max");
            if (initial < min || initial > max) throw _json.Refuse(TreeCodes.Range, "feature '" + id + "' starts at " + initial + ", outside " + min + " to " + max);
            string? key = null;
            if (fact.StartsWith("key.", StringComparison.Ordinal))
            {
                key = _json.CheckId(fact["key.".Length..], where + ".fact");
                if (!keys.Add(key)) throw _json.Refuse(TreeCodes.Duplicate, "two features read the key '" + key + "'");
            }
            else
                report.Refuse(TreeCodes.FeatureSource, "feature " + id, "its fact '" + fact + "' is not a host key; a percept attribute or the tick is not a column the importer can read, so a sensor must write it to a host key and the feature must name 'key.<id>'");
            result[id] = new TreeFeature(id, key, min, max, initial);
        }
        return result;
    }

    private List<string> ReadActions(JsonElement array)
    {
        var result = new List<string>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String) throw _json.Refuse(TreeCodes.Format, "actions[" + result.Count + "] must be a string");
            var action = _json.CheckId(element.GetString()!, "actions[" + result.Count + "]");
            if (result.Contains(action)) throw _json.Refuse(TreeCodes.Duplicate, "action '" + action + "' is listed twice");
            result.Add(action);
        }
        return result;
    }

    private List<TreeNode> ReadNodes(JsonElement array)
    {
        var result = new List<TreeNode>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var element in array.EnumerateArray())
        {
            var where = "nodes[" + n++ + "]";
            if (element.ValueKind != JsonValueKind.Object) throw _json.Refuse(TreeCodes.Format, where + " must be an object");
            var node = _json.Has(element, "action") ? ReadLeaf(element, where) : ReadSplit(element, where);
            if (!seen.Add(node.Id)) throw _json.Refuse(TreeCodes.Duplicate, "node '" + node.Id + "' is declared twice");
            result.Add(node);
        }
        return result;
    }

    /// <summary>A node id is a letter-and-digit name with no dot: the importer derives the ids of the nodes it emits by adding a dot and a suffix, and a dot in a source id could collide with one.</summary>
    private string NodeId(JsonElement element, string field, string where)
    {
        var id = _json.Id(element, field, where);
        if (id.Contains('.')) throw _json.Refuse(TreeCodes.Format, where + "." + field + " '" + id + "' may not contain a dot");
        return id.Length <= 100 ? id : throw _json.Refuse(TreeCodes.Range, where + "." + field + " '" + id + "' is longer than 100 characters");
    }

    private TreeNode ReadLeaf(JsonElement element, string where)
    {
        _json.Object(element, where, ["id", "action", "weight"]);
        var id = NodeId(element, "id", where);
        var action = _json.Id(element, "action", where);
        var usable = ReadInteger(element, "weight", where + " '" + id + "'", id, out var weight);
        return new TreeNode(id, true, action, weight, "", TreeOp.Lt, 0, "", "", usable);
    }

    private TreeNode ReadSplit(JsonElement element, string where)
    {
        _json.Object(element, where, ["id", "feature", "op", "left", "right"], "threshold", "categories", "missing");
        var id = NodeId(element, "id", where);
        var subject = "node " + id;
        var feature = _json.Id(element, "feature", where);
        var left = NodeId(element, "left", where);
        var right = NodeId(element, "right", where);
        var usable = true;
        var opName = _json.Text(element, "op", where, 16);
        var categorical = opName is "in" or "notin" || _json.Has(element, "categories");
        var op = TreeOp.Lt;
        if (categorical)
        {
            report.Refuse(TreeCodes.Categorical, subject, "a split on a set of category values ('" + opName + "' or a 'categories' list) has no integer-threshold form; one-hot encode the feature when training so each split is a single integer comparison");
            usable = false;
        }
        else if (!TreeOps.TryParse(opName, out op)) throw _json.Refuse(TreeCodes.Format, where + ".op '" + opName + "' must be lt, lte, gt, gte, eq or ne");
        if (_json.Has(element, "missing"))
        {
            report.Refuse(TreeCodes.MissingRouting, subject, "a default direction for a missing value cannot be kept: the engine's keys always hold a whole number, so the host must supply one (impute it before the tree reads it) and the tree must not carry a route for 'missing'");
            usable = false;
        }
        long threshold = 0;
        if (!categorical)
        {
            if (!_json.Has(element, "threshold")) throw _json.Refuse(TreeCodes.MissingField, where + " is missing 'threshold'");
            usable &= ReadInteger(element, "threshold", where + " '" + id + "'", id, out threshold);
        }
        return new TreeNode(id, false, "", 0, feature, op, threshold, left, right, usable);
    }

    /// <summary>
    /// A whole number in the engine's range. A number with a fraction or an exponent is a float and a string for not-a-number or infinity is not finite: each is added to the report and the
    /// node is marked unusable, because rounding a threshold would change which side a value falls on.
    /// </summary>
    private bool ReadInteger(JsonElement owner, string field, string where, string node, out long value)
    {
        value = 0;
        var element = owner.GetProperty(field);
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetInt64(out value))
                    return value >= -NumberLimit && value <= NumberLimit ? true : throw _json.Refuse(TreeCodes.Range, where + "." + field + " " + value + " is outside +-" + NumberLimit);
                var raw = element.GetRawText();
                if (raw.All(c => c is '-' or (>= '0' and <= '9'))) throw _json.Refuse(TreeCodes.Range, where + "." + field + " " + raw + " is outside +-" + NumberLimit);
                report.Refuse(TreeCodes.Float, "node " + node, field + " " + raw + " is not a whole number; the engine's thresholds and weights are integers, so scale and round the model when quantising it, offline, and re-check its accuracy");
                return false;
            case JsonValueKind.String:
                var text = element.GetString()!;
                if (text is "NaN" or "Infinity" or "-Infinity")
                {
                    report.Refuse(TreeCodes.NotFinite, "node " + node, field + " is '" + text + "'; a split or leaf that is not a finite number cannot be run");
                    return false;
                }
                throw _json.Refuse(TreeCodes.Format, where + "." + field + " must be a whole number");
            case JsonValueKind.Undefined:
            case JsonValueKind.Object:
            case JsonValueKind.Array:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                break;
        }
        throw _json.Refuse(TreeCodes.Format, where + "." + field + " must be a whole number");
    }
}
