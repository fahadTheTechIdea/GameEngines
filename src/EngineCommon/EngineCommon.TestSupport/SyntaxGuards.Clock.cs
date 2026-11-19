using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineCommon.TestSupport;

public static partial class SyntaxGuards
{
    private static readonly HashSet<string> BannedTypes = new(StringComparer.Ordinal)
    {
        "DateTime", "DateTimeOffset", "TimeProvider", "Stopwatch", "Random", "Single", "Double", "Decimal", "TickCount", "TickCount64",
    };

    private static readonly HashSet<string> BannedMath = new(StringComparer.Ordinal) { "Sin", "Cos", "Tan", "Sqrt", "Pow", "Floor", "Ceiling", "Round", "Log", "Exp", "Atan", "Atan2" };

    private static readonly HashSet<string> BannedHosts = new(StringComparer.Ordinal) { "File", "Directory", "Console", "Environment" };

    /// <summary>
    /// What an engine core must not touch, as code (never as a comment or the text of a string): a wall clock or a random source (DateTime, DateTimeOffset, TimeProvider, Stopwatch, Random, TickCount,
    /// Thread.Sleep, Task.Delay), floating point (the float, double and decimal keywords and types, a literal with a fraction or exponent, the Math functions that give one) and the host (File, Directory,
    /// Console, Environment). Not seen: the same thing reached through a name this list does not hold, or through a using alias.
    /// </summary>
    public static List<string> ClockAndFloatViolations(string file, string source)
    {
        var found = new List<string>();
        var root = Parse(source);
        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            if (BannedTypes.Contains(name.Identifier.Text)) found.Add(Where(file, name) + ": " + name.Identifier.Text);
        foreach (var type in root.DescendantNodes().OfType<PredefinedTypeSyntax>())
            if (type.Keyword.IsKind(SyntaxKind.FloatKeyword) || type.Keyword.IsKind(SyntaxKind.DoubleKeyword) || type.Keyword.IsKind(SyntaxKind.DecimalKeyword)) found.Add(Where(file, type) + ": " + type.Keyword.Text);
        foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
            if (literal.IsKind(SyntaxKind.NumericLiteralExpression) && literal.Token.Value is float or double or decimal) found.Add(Where(file, literal) + ": the literal " + literal.Token.Text);
        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var owner = access.Expression is IdentifierNameSyntax identifier ? identifier.Identifier.Text : "";
            var member = access.Name.Identifier.Text;
            if (owner == "Math" && BannedMath.Contains(member)) found.Add(Where(file, access) + ": Math." + member);
            else if (owner == "Thread" && member == "Sleep") found.Add(Where(file, access) + ": Thread.Sleep");
            else if (owner == "Task" && member == "Delay") found.Add(Where(file, access) + ": Task.Delay");
            else if (BannedHosts.Contains(owner)) found.Add(Where(file, access) + ": " + owner + "." + member);
        }
        return found;
    }
}
