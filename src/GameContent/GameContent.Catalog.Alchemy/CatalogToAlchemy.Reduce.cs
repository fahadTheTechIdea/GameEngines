using System.Text;
using System.Text.Json;

namespace GameContent.Catalog;

public static partial class CatalogToAlchemy
{
    /// <summary>
    /// Alchemy answers one reaction per combination, so alternate recipes for the same vessel need something that tells them apart.
    /// Where their environments already differ (a station, a skill, a tag) nothing changes. Where they would collide, each colliding
    /// recipe gets its own <c>method-&lt;recipe&gt;</c> environment tag that the game offers to choose that route; nothing is refused.
    /// </summary>
    private static List<Candidate> Disambiguate(List<Candidate> candidates, ConversionReport report)
    {
        var result = candidates.ToList();
        foreach (var group in candidates.GroupBy(c => string.Join('+', c.Vessel.Order(StringComparer.Ordinal)), StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var members = group.ToList();
            var parent = Enumerable.Range(0, members.Count).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
            for (var a = 0; a < members.Count; a++)
                for (var b = a + 1; b < members.Count; b++)
                    if (Collide(members[a], members[b])) parent[Find(a)] = Find(b);
            var collided = Enumerable.Range(0, members.Count).Where(i => Enumerable.Range(0, members.Count).Count(j => Find(j) == Find(i)) > 1).ToList();
            foreach (var i in collided)
            {
                var member = members[i];
                var tag = "method-" + Local(member.Id);
                var index = result.IndexOf(member);
                result[index] = member with { Environment = member.Environment.Append(tag).Order(StringComparer.Ordinal).ToArray() };
                report.Reduce(AlchemyConversionCodes.MethodTag, member.Id, "shares its combination with other recipes, so the game offers the environment tag '" + tag + "' to choose this route");
            }
        }
        return result;
    }

    private static bool Collide(Candidate first, Candidate second) =>
        first.Environment.All(second.Environment.Contains) || second.Environment.All(first.Environment.Contains);

    /// <summary>
    /// Removes every element no remaining recipe chain can make from the natural ones, and every recipe that touches such an element,
    /// until nothing more falls out. A recipe refused for another reason can leave a crafted resource with no path.
    /// </summary>
    private static List<Candidate> DropUnreachable(List<Candidate> candidates, Dictionary<string, CatalogResource> elements, ConversionReport report)
    {
        while (true)
        {
            var reachable = elements.Values.Where(e => e.Origin == ResourceOrigin.Natural).Select(e => e.Id).Append(Hand).ToHashSet(StringComparer.Ordinal);
            bool grew;
            do
            {
                grew = false;
                foreach (var candidate in candidates.Where(c => c.Vessel.All(reachable.Contains)))
                    foreach (var output in candidate.Outputs) grew |= reachable.Add(output);
            }
            while (grew);

            var lost = elements.Keys.Where(id => !reachable.Contains(id)).ToList();
            if (lost.Count == 0) return candidates;
            foreach (var id in lost)
            {
                elements.Remove(id);
                report.Refuse(AlchemyConversionCodes.Unreachable, id, "no alchemy recipe chain makes it from the natural resources");
            }
            var gone = lost.ToHashSet(StringComparer.Ordinal);
            var remaining = new List<Candidate>();
            foreach (var candidate in candidates)
            {
                if (candidate.Vessel.Concat(candidate.Outputs).Any(gone.Contains))
                    report.Refuse(AlchemyConversionCodes.Unreachable, candidate.Id, "uses a resource that alchemy cannot reach");
                else remaining.Add(candidate);
            }
            candidates = remaining;
        }
    }

    /// <summary>The alchemy content document for the surviving elements and recipes.</summary>
    private static string AlchemyDocument(IEnumerable<CatalogResource> elements, List<Candidate> candidates, CatalogSet set)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("format", 1);
            json.WriteStartArray("elements");
            if (candidates.Any(c => c.Catalysts.Contains(Hand)))
            {
                json.WriteStartObject();
                json.WriteString("id", Hand);
                json.WriteString("name", "Bare hands");
                json.WriteStartArray("properties");
                json.WriteStringValue("tool");
                json.WriteEndArray();
                json.WriteBoolean("starting", true);
                json.WriteEndObject();
            }
            foreach (var element in elements)
            {
                json.WriteStartObject();
                json.WriteString("id", element.Id);
                json.WriteString("name", element.Name);
                json.WriteStartArray("properties");
                foreach (var property in new[] { Names<ResourceClass>.Of(element.Class) }.Concat(element.Tags).Concat(element.Cpc is null ? [] : ["cpc-" + element.Cpc]).Concat(set.TryItem(element.Id, out var item) ? ["item-" + Names<ItemClass>.Of(item.Class)] : []).Distinct(StringComparer.Ordinal)) json.WriteStringValue(property);
                json.WriteEndArray();
                json.WriteBoolean("starting", element.Origin == ResourceOrigin.Natural);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("recipes");
            foreach (var candidate in candidates)
            {
                json.WriteStartObject();
                json.WriteString("id", candidate.Id);
                Strings(json, "inputs", candidate.Inputs);
                Strings(json, "catalysts", candidate.Catalysts);
                Strings(json, "environment", candidate.Environment);
                Strings(json, "outputs", candidate.Outputs);
                json.WriteBoolean("ordered", false);
                json.WriteStartObject("chance");
                json.WriteNumber("numerator", candidate.Numerator);
                json.WriteNumber("denominator", candidate.Denominator);
                json.WriteEndObject();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Strings(Utf8JsonWriter json, string name, IEnumerable<string> values)
    {
        json.WriteStartArray(name);
        foreach (var value in values) json.WriteStringValue(value);
        json.WriteEndArray();
    }
}
