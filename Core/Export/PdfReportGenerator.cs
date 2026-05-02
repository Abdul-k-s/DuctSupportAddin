using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DuctSupportAddin.Core.Export;

/// <summary>
/// Generates PDF reports for duct support placement.
/// </summary>
public class PdfReportGenerator
{
    static PdfReportGenerator()
    {
        // QuestPDF license configuration
        QuestPDF.Settings.License = LicenseType.Community;
    }
    
    /// <summary>
    /// Generate PDF report.
    /// </summary>
    public void Generate(ReportData data, string filePath)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));
                
                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(ComposeFooter);
            });
        });
        
        document.GeneratePdf(filePath);
    }
    
    /// <summary>
    /// Compose header section.
    /// </summary>
    private void ComposeHeader(IContainer container, ReportData data)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("DUCT SUPPORT PLACEMENT REPORT")
                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                
                col.Item().Text($"Project: {data.ProjectName}").FontSize(12);
                col.Item().Text($"Date: {data.GeneratedDate:yyyy-MM-dd}").FontSize(10);
            });
            
            row.ConstantItem(100).Column(col =>
            {
                col.Item().Text("AUS").FontSize(16).Bold();
                col.Item().Text("MEP Solutions").FontSize(10);
            });
        });
        
        container.PaddingBottom(10);
    }
    
    /// <summary>
    /// Compose main content.
    /// </summary>
    private void ComposeContent(IContainer container, ReportData data)
    {
        container.Column(col =>
        {
            // Summary section
            col.Item().Element(c => ComposeSummary(c, data));
            col.Item().PaddingVertical(10);
            
            // Support breakdown
            col.Item().Element(c => ComposeBreakdown(c, data));
            col.Item().PaddingVertical(10);
            
            // Level summary
            col.Item().Element(c => ComposeLevelSummary(c, data));
            col.Item().PaddingVertical(10);
            
            // Load summary
            col.Item().Element(c => ComposeLoadSummary(c, data));
        });
    }
    
    /// <summary>
    /// Compose summary section.
    /// </summary>
    private void ComposeSummary(IContainer container, ReportData data)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
        {
            col.Item().Text("EXECUTIVE SUMMARY").Bold().FontSize(12);
            col.Item().PaddingVertical(5);
            
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Total Supports Placed: {data.TotalSupports}").Bold();
                    c.Item().Text($"Total Calculated Load: {data.TotalLoadKg:N0} kg");
                    c.Item().Text($"Ducts Processed: {data.DuctsProcessed}");
                });
                
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Standard Used: {data.SpacingStandard}");
                    c.Item().Text($"Spacing Adjustment: {data.SpacingPercentage}%");
                    c.Item().Text($"Insulation: {(data.IncludesInsulation ? "Included" : "Not Included")}");
                });
            });
        });
    }
    
    /// <summary>
    /// Compose support type breakdown.
    /// </summary>
    private void ComposeBreakdown(IContainer container, ReportData data)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
        {
            col.Item().Text("SUPPORT BREAKDOWN BY TYPE").Bold().FontSize(12);
            col.Item().PaddingVertical(5);
            
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });
                
                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Type").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Count").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Percentage").Bold();
                });
                
                // Data rows
                AddBreakdownRow(table, "Ceiling Mounted", data.CeilingSupports, data.TotalSupports);
                AddBreakdownRow(table, "Ground Support", data.GroundSupports, data.TotalSupports);
                AddBreakdownRow(table, "Wall Support", data.WallSupports, data.TotalSupports);
                AddBreakdownRow(table, "Vertical Support", data.VerticalSupports, data.TotalSupports);
            });
        });
    }
    
    private void AddBreakdownRow(TableDescriptor table, string type, int count, int total)
    {
        double percentage = total > 0 ? (double)count / total * 100 : 0;
        
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(type);
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(count.ToString());
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{percentage:F1}%");
    }
    
    /// <summary>
    /// Compose level summary.
    /// </summary>
    private void ComposeLevelSummary(IContainer container, ReportData data)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
        {
            col.Item().Text("SUPPORTS BY LEVEL").Bold().FontSize(12);
            col.Item().PaddingVertical(5);
            
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });
                
                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Level").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Supports").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Load (kg)").Bold();
                });
                
                // Data
                foreach (var level in data.LevelSummaries.OrderBy(l => l.LevelName))
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(level.LevelName);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(level.SupportCount.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{level.TotalLoadKg:N0}");
                }
                
                // Total
                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("TOTAL").Bold();
                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(data.TotalSupports.ToString()).Bold();
                table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text($"{data.TotalLoadKg:N0}").Bold();
            });
        });
    }
    
    /// <summary>
    /// Compose load summary for structural.
    /// </summary>
    private void ComposeLoadSummary(IContainer container, ReportData data)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
        {
            col.Item().Text("STRUCTURAL LOAD SUMMARY").Bold().FontSize(12);
            col.Item().PaddingVertical(5);
            
            col.Item().Text("Point loads for structural engineer coordination:")
                .FontSize(9).Italic();
            col.Item().PaddingVertical(3);
            
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });
                
                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text("Mark").FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text("Level").FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text("X (mm)").FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text("Y (mm)").FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text("Load (kg)").FontSize(9).Bold();
                });
                
                // Data - first 20 ceiling supports
                var ceilingSupports = data.Supports
                    .Where(s => s.Type == "Ceiling")
                    .OrderBy(s => s.Mark)
                    .Take(20);
                
                foreach (var support in ceilingSupports)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(support.Mark).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(support.LevelName).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text($"{support.X:N0}").FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text($"{support.Y:N0}").FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text($"{support.LoadKg:N1}").FontSize(8);
                }
            });
            
            if (data.Supports.Count(s => s.Type == "Ceiling") > 20)
            {
                col.Item().Text($"... and {data.Supports.Count(s => s.Type == "Ceiling") - 20} more ceiling supports. See Excel export for complete list.")
                    .FontSize(8).Italic();
            }
        });
    }
    
    /// <summary>
    /// Compose footer.
    /// </summary>
    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium));
                text.Span("Generated by AUS Duct Support Add-in v1.0 | ");
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }
}
