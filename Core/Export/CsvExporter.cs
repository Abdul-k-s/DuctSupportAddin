using System.IO;
using System.Text;

namespace DuctSupportAddin.Core.Export;

/// <summary>
/// Exports support data to CSV format.
/// </summary>
public class CsvExporter
{
    /// <summary>
    /// Export report data to CSV file.
    /// </summary>
    public void Export(ReportData data, string filePath)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("Mark,Type,Level,Grid,X (mm),Y (mm),Z (mm),Duct Width (mm),Support Width (mm),Rod Length (mm),Support Height (mm),Load (kg),Recommended Rod,Duct System,Host Element,Clash Status");
        
        // Data
        foreach (var support in data.Supports.OrderBy(s => s.Mark))
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(support.Mark),
                EscapeCsv(support.Type),
                EscapeCsv(support.LevelName),
                EscapeCsv(support.GridLocation),
                Math.Round(support.X, 0),
                Math.Round(support.Y, 0),
                Math.Round(support.Z, 0),
                Math.Round(support.DuctWidth, 0),
                Math.Round(support.SupportWidth, 0),
                Math.Round(support.RodLength, 0),
                Math.Round(support.SupportHeight, 0),
                Math.Round(support.LoadKg, 1),
                EscapeCsv(support.RecommendedRod),
                EscapeCsv(support.DuctSystem),
                EscapeCsv(support.HostElement),
                EscapeCsv(support.ClashStatus)
            ));
        }
        
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
    
    /// <summary>
    /// Escape CSV value.
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }
}
