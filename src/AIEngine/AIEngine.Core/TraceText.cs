using System.Globalization;
using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>How a value is written in a trace: strings in single quotes, booleans as true and false, whole numbers plainly.</summary>
internal static class TraceText
{
    public static string Describe(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return value.Boolean ? "true" : "false";
            case FactKind.Integer: return value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.String: break;
        }
        return "'" + value.Text + "'";
    }
}