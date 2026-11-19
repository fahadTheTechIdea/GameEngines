namespace GameContent.Catalog;

/// <summary>A pack was refused. The report holds every finding; the message is the first.</summary>
public sealed class CatalogRefusedException(CatalogReport report) : Exception(report.Items.Count == 0 ? "catalog refused" : report.Items[0].Code + ": " + report.Items[0].Message)
{
    public CatalogReport Report { get; } = report;
    public string Code => Report.Items.Count == 0 ? "" : Report.Items[0].Code;
}
