using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineCommon.TestSupport;

public static partial class SyntaxGuards
{
    /// <summary>
    /// The places a failure is thrown from an expression instead of being handled by name: <c>x ?? throw ...</c> and <c>x ??= throw ...</c>. A throw is a statement a reader can see; hiding it at the end of a
    /// coalesce is how a closed set of known values turns into a catch-all failure, so the engines state their one named failure path as an <c>if</c> that refuses. Not seen: a throw reached through a
    /// helper method, and a throw expression in a conditional (<c>a ? b : throw ...</c>), which is a different rule.
    /// </summary>
    public static List<string> ThrowingCoalesces(string file, string source)
    {
        var found = new List<string>();
        foreach (var node in Parse(source).DescendantNodes())
        {
            if (node is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.CoalesceExpression) && binary.Right is ThrowExpressionSyntax) found.Add(Where(file, binary) + ": ?? throw");
            else if (node is AssignmentExpressionSyntax assignment && assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression) && assignment.Right is ThrowExpressionSyntax) found.Add(Where(file, assignment) + ": ??= throw");
        }
        return found;
    }

    /// <summary>
    /// Every use, as code (never in a comment or the text of a string), of one of the <paramref name="names"/>: a type (generic ones such as <c>HashSet&lt;int&gt;</c> included), a member, a method or a variable. Used to keep a layer away from what it must not touch (the
    /// economy bridge never calls the inventory's own transaction interface: the ledger is written through its one effect). Not seen: the same thing reached through a name this list does not hold.
    /// </summary>
    public static List<string> ForbiddenNames(string file, string source, params string[] names)
    {
        var wanted = new HashSet<string>(names, StringComparer.Ordinal);
        return Parse(source).DescendantNodes().OfType<SimpleNameSyntax>().Where(n => wanted.Contains(n.Identifier.Text)).Select(n => Where(file, n) + ": " + n.Identifier.Text).ToList();
    }
}
