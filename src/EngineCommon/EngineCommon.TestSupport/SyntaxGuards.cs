using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineCommon.TestSupport;

/// <summary>
/// The source rules every engine shares, checked on the compiler's own syntax trees (Roslyn, syntax only: no compilation, so no type is resolved). Each rule is a function of one source text,
/// so the guard tests can run it on synthetic sources and show it fail. What a syntax-only rule cannot see is listed on each rule.
/// </summary>
public static partial class SyntaxGuards
{
    /// <summary>The names a catch block may call to make its outcome visible: a record, an event, a printed line or a returned refusal.</summary>
    private static readonly HashSet<string> Visible = new(StringComparer.Ordinal)
    {
        "Add", "Emit", "WriteLine", "Write", "Report", "Refused", "Wrong", "Raise", "Record", "Enqueue",
    };

    /// <summary>The marker comment a catch may carry when its outcome is a state change the report shows (the guard test checks it is rare and named).</summary>
    public const string OutcomeMarker = "// outcome:";

    public static SyntaxNode Parse(string source) => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();

    public static string Where(string file, SyntaxNode node) => file + "(" + (node.GetLocation().GetLineSpan().StartLinePosition.Line + 1) + ")";

    // ---- catch ---------------------------------------------------------------------------------------------------------------------

    /// <summary>How many catch clauses the source has.</summary>
    public static int CatchCount(string source) => Parse(source).DescendantNodes().OfType<CatchClauseSyntax>().Count();

    /// <summary>
    /// The catch clauses whose block can finish without throwing, returning, or calling one of the recording / emitting / printing names, on every path of the block. A call or a throw inside a lambda or a
    /// local function, or in a nested catch, is not the outer block's. Not seen: a call to a name outside the list that does report (the list is closed on purpose), and a recording that is only an
    /// assignment, which needs the <see cref="OutcomeMarker"/> comment.
    /// </summary>
    public static List<string> SilentCatches(string file, string source)
    {
        var found = new List<string>();
        foreach (var clause in Parse(source).DescendantNodes().OfType<CatchClauseSyntax>())
            if (!Always(clause.Block) && !HasMarker(clause)) found.Add(Where(file, clause) + ": " + Shorten(clause.ToString()));
        return found;
    }

    /// <summary>The catch clauses that rely on the <see cref="OutcomeMarker"/> comment, as file and line.</summary>
    public static List<string> MarkedCatches(string file, string source) =>
        Parse(source).DescendantNodes().OfType<CatchClauseSyntax>().Where(c => HasMarker(c) && !Always(c.Block)).Select(c => Where(file, c)).ToList();

    private static bool HasMarker(CatchClauseSyntax clause) =>
        clause.Block.DescendantTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) && t.ToString().StartsWith(OutcomeMarker, StringComparison.Ordinal));

    private static bool Always(StatementSyntax statement) => statement switch
    {
        BlockSyntax block => block.Statements.Any(Always),
        ThrowStatementSyntax => true,
        ReturnStatementSyntax => true,
        ExpressionStatementSyntax expression => Reports(expression.Expression),
        IfStatementSyntax conditional => conditional.Else is not null && Always(conditional.Statement) && Always(conditional.Else.Statement),
        _ => false,
    };

    private static bool Reports(ExpressionSyntax expression) => expression switch
    {
        ThrowExpressionSyntax => true,
        InvocationExpressionSyntax call => Visible.Contains(NameOf(call.Expression)),
        AwaitExpressionSyntax awaited => Reports(awaited.Expression),
        ParenthesizedExpressionSyntax inner => Reports(inner.Expression),
        _ => false,
    };

    private static string NameOf(ExpressionSyntax callee) => callee switch
    {
        IdentifierNameSyntax name => name.Identifier.Text,
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
        _ => "",
    };

    private static string Shorten(string text)
    {
        var flat = Regex.Replace(text, @"\s+", " ");
        return flat.Length <= 120 ? flat : flat[..120] + "...";
    }

    // ---- switches ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The switches with a catch-all arm that throws: a <c>default:</c> section or a <c>case _:</c> section whose statements throw, or a <c>_ =></c> arm whose value is a throw expression. A
    /// member left out of an enum switch is a build error instead (CS8509). Not seen: a catch-all that calls a helper which throws.
    /// </summary>
    public static List<string> ThrowingCatchAlls(string file, string source)
    {
        var found = new List<string>();
        var root = Parse(source);
        foreach (var section in root.DescendantNodes().OfType<SwitchSectionSyntax>())
        {
            var catchAll = section.Labels.Any(IsCatchAll);
            if (catchAll && section.Statements.Any(Throws)) found.Add(Where(file, section) + ": a default section that throws");
        }
        foreach (var arm in root.DescendantNodes().OfType<SwitchExpressionArmSyntax>())
            if (arm.Pattern is DiscardPatternSyntax or VarPatternSyntax && arm.WhenClause is null && arm.Expression is ThrowExpressionSyntax) found.Add(Where(file, arm) + ": a discard arm that throws");
        return found;
    }

    private static bool IsCatchAll(SwitchLabelSyntax label) => label switch
    {
        DefaultSwitchLabelSyntax => true,
        CaseSwitchLabelSyntax { Value: IdentifierNameSyntax { Identifier.Text: "_" } } => true,
        CasePatternSwitchLabelSyntax { WhenClause: null, Pattern: DiscardPatternSyntax or VarPatternSyntax } => true,
        _ => false,
    };

    private static bool Throws(SyntaxNode node) =>
        node.DescendantNodesAndSelf(n => n is not (LambdaExpressionSyntax or LocalFunctionStatementSyntax or AnonymousMethodExpressionSyntax)).Any(n => n is ThrowStatementSyntax or ThrowExpressionSyntax);

    /// <summary>Every use of the type name ArgumentOutOfRangeException as code (not in a comment or a string).</summary>
    public static List<string> OutOfRangeExceptions(string file, string source) =>
        Parse(source).DescendantNodes().OfType<IdentifierNameSyntax>().Where(n => n.Identifier.Text == "ArgumentOutOfRangeException").Select(n => Where(file, n)).ToList();

    // ---- refusal codes -------------------------------------------------------------------------------------------------------------

    /// <summary>A refusal code: the type that declares it, the constant's name and its text.</summary>
    public readonly record struct Code(string Owner, string Name, string Text)
    {
        public override string ToString() => Owner + "." + Name + " (" + Text + ")";
    }


    /// <summary>The string constants of the source whose text matches <paramref name="codeText"/>, the shape of one engine's refusal codes (such as <c>^(DLG|DIALOGUE)_[A-Z0-9_]+$</c>), whichever type declares them.</summary>
    public static List<Code> DeclaredCodes(string source, Regex codeText)
    {
        var codes = new List<Code>();
        foreach (var field in Parse(source).DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!field.Modifiers.Any(SyntaxKind.ConstKeyword)) continue;
            var owner = field.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "";
            foreach (var variable in field.Declaration.Variables)
                if (variable.Initializer?.Value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) && codeText.IsMatch(literal.Token.ValueText))
                    codes.Add(new Code(owner, variable.Identifier.Text, literal.Token.ValueText));
        }
        return codes;
    }

    /// <summary>
    /// The codes that no test source refers to: by <c>Owner.Name</c> as a member access, or by a string literal with the code's text. A mention in a comment or a documentation comment does not count.
    /// Not seen: whether the reference is in an assertion that would fail; that is what each pass's mutation checks are for.
    /// </summary>
    public static List<string> UnreferencedCodes(IEnumerable<Code> codes, IEnumerable<string> testSources)
    {
        var accessed = new HashSet<(string Owner, string Name)>();
        var literals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in testSources)
        {
            var root = Parse(source);
            foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
                accessed.Add((LastName(access.Expression), access.Name.Identifier.Text));
            foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(l => l.IsKind(SyntaxKind.StringLiteralExpression)))
                literals.Add(literal.Token.ValueText);
        }
        return codes.Where(c => !accessed.Contains((c.Owner, c.Name)) && !literals.Contains(c.Text)).Select(c => c.ToString()).ToList();
    }

    private static string LastName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax name => name.Identifier.Text,
        MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
        _ => "",
    };

}
