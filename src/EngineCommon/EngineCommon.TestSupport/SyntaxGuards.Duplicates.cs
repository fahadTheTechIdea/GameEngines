using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineCommon.TestSupport;

public static partial class SyntaxGuards
{
    // ---- one owner for the locale machinery -----------------------------------------------------------------------------------------

    private static readonly Regex LocaleTypeName = new(@"^(Plural\w*|Locale(Table|Chain|Renderer|Message|Resolution)|MessageSelect|MessageArg|TextDirection|CoverageRules|CoverageReport)$", RegexOptions.CultureInvariant);

    private static readonly string[] PluralCategories = ["zero", "one", "two", "few", "many", "other"];

    /// <summary>
    /// Signs of a second locale, plural or message-format implementation in a source that is not the owner's: a type with the name of one of the owner's types (Plural..., LocaleTable, LocaleChain,
    /// LocaleRenderer, LocaleMessage, MessageSelect, TextDirection, CoverageRules...), a method or property whose name has Plural in it, the CLDR data file named, or four or more of the plural
    /// category names as string literals in one file. The caller gives it every source except the owner's folder. Not seen: a second implementation that avoids all those names and literals.
    /// </summary>
    public static List<string> SecondLocaleImplementation(string file, string source)
    {
        var found = new List<string>();
        var root = Parse(source);
        foreach (var type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            if (LocaleTypeName.IsMatch(type.Identifier.Text)) found.Add(Where(file, type) + ": a type named " + type.Identifier.Text);
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            if (method.Identifier.Text.Contains("Plural", StringComparison.Ordinal)) found.Add(Where(file, method) + ": a method named " + method.Identifier.Text);
        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            if (property.Identifier.Text.Contains("Plural", StringComparison.Ordinal)) found.Add(Where(file, property) + ": a property named " + property.Identifier.Text);
        var literals = root.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(l => l.IsKind(SyntaxKind.StringLiteralExpression)).Select(l => l.Token.ValueText).ToList();
        if (literals.Any(l => l.Contains("cldr-plurals", StringComparison.OrdinalIgnoreCase))) found.Add(file + ": names the CLDR plural data file");
        var categories = PluralCategories.Count(c => literals.Contains(c, StringComparer.Ordinal));
        if (categories >= 4) found.Add(file + ": " + categories + " plural category names as literals");
        return found;
    }

    // ---- one owner for the channel registry -----------------------------------------------------------------------------------------

    private static bool DerivesFrom(BaseTypeDeclarationSyntax type, string name) =>
        type.BaseList is not null && type.BaseList.Types.Any(t => t.Type is IdentifierNameSyntax { Identifier.Text: var text } && text == name);

    private static readonly Regex ChannelCode = new(@"_CHANNEL_(UNKNOWN|DUPLICATE)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Signs of a second channel registry in a source that is not the owner's (the owner is the <c>ChannelRegistry</c> class): a type named ...Channels or ...ChannelRegistry that does not derive from
    /// it; a class that derives from it but is not sealed or has anything beyond constants, a static protocol field and its constructor (an engine names its protocol and nothing else); a game component
    /// that declares <c>Serve</c> or a <c>_served</c> / <c>_unserved</c> field; and a string constant ending <c>_CHANNEL_UNKNOWN</c> or <c>_CHANNEL_DUPLICATE</c> in a class that does not derive from it.
    /// The caller gives it every source except the owner's. Not seen: a registry that avoids these names.
    /// </summary>
    public static List<string> SecondChannelRegistry(string file, string source)
    {
        var found = new List<string>();
        var root = Parse(source);
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var derived = DerivesFrom(type, "ChannelRegistry");
            var name = type.Identifier.Text;
            if (!derived && (name.EndsWith("Channels", StringComparison.Ordinal) || name.EndsWith("ChannelRegistry", StringComparison.Ordinal)))
                found.Add(Where(file, type) + ": " + name + " is a channel registry that does not derive from ChannelRegistry");
            if (derived)
            {
                if (!type.Modifiers.Any(SyntaxKind.SealedKeyword)) found.Add(Where(file, type) + ": " + name + " derives from ChannelRegistry and is not sealed");
                foreach (var member in type.Members)
                {
                    var thin = member switch
                    {
                        FieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.ConstKeyword) || (field.Modifiers.Any(SyntaxKind.StaticKeyword) && field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)),
                        ConstructorDeclarationSyntax => true,
                        _ => false,
                    };
                    if (!thin) found.Add(Where(file, member) + ": " + name + " has more than constants, a protocol and a constructor");
                }
            }
            else
            {
                var component = DerivesFrom(type, "IGameComponent");
                foreach (var member in type.Members)
                {
                    if (component && member is MethodDeclarationSyntax { Identifier.Text: "Serve" }) found.Add(Where(file, member) + ": a component that declares Serve");
                    if (member is FieldDeclarationSyntax field)
                        foreach (var variable in field.Declaration.Variables)
                        {
                            if (variable.Identifier.Text is "_served" or "_unserved") found.Add(Where(file, variable) + ": a field " + variable.Identifier.Text);
                            if (variable.Initializer?.Value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) && ChannelCode.IsMatch(literal.Token.ValueText))
                                found.Add(Where(file, variable) + ": the channel code " + literal.Token.ValueText + " outside a ChannelRegistry subclass");
                        }
                }
            }
        }
        return found;
    }
}
