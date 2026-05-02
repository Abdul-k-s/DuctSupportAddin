using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DuctSupportAddin.Core.Analysis;
using DuctSupportAddin.Core.Export;
using DuctSupportAddin.Core.Placement;
using DuctSupportAddin.Core.Structural;
using DuctSupportAddin.Core.Validation;
using DuctSupportAddin.Models;
using DuctSupportAddin.UI.Views;
using DuctSupportAddin.Utilities;

namespace DuctSupportAddin.Commands;

/// <summary>
/// Main command for placing duct supports.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class PlaceDuctSupportsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uiDoc = commandData.Application.ActiveUIDocument;
        Document doc = uiDoc.Document;
        
        try
        {
            Logger.Info("PlaceDuctSupportsCommand started");
            
            // Load configuration
            var config = Configuration.Load();
            
            // Show main window
            var mainWindow = new MainWindow(config);
            mainWindow.ShowDialog();
            
            if (mainWindow.DialogResult != true || mainWindow.ResultConfiguration == null)
            {
                Logger.Info("User cancelled");
                return Result.Cancelled;
            }
            
            config = mainWindow.ResultConfiguration;
            
            // Execute placement
            var result = ExecutePlacement(doc, uiDoc, config, mainWindow.ShowPreview);
            
            if (result.TotalSupportsPlaced > 0)
            {
                TaskDialog.Show("Duct Support Placement", result.GetSummary());
            }
            else if (result.Errors.Count > 0)
            {
                TaskDialog.Show("Duct Support Placement", 
                    $"Placement completed with errors:\n{string.Join("\n", result.Errors.Take(5))}");
            }
            else
            {
                TaskDialog.Show("Duct Support Placement", "No supports were placed. Check that ducts exist and settings are correct.");
            }
            
            Logger.Info($"Placement complete: {result.TotalSupportsPlaced} supports placed");
            
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Logger.Error("PlaceDuctSupportsCommand failed", ex);
            message = ex.Message;
            return Result.Failed;
        }
    }
    
    private PlacementResults ExecutePlacement(Document doc, UIDocument uiDoc, Configuration config, bool showPreview)
    {
        var results = new PlacementResults();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Step 1: Collect ducts
        Logger.Info("Collecting ducts...");
        var collector = new DuctCollector(doc);
        var ducts = collector.Collect(config, uiDoc);
        results.TotalDuctsProcessed = ducts.Count;
        
        if (ducts.Count == 0)
        {
            results.Warnings.Add("No ducts found matching criteria");
            return results;
        }
        
        Logger.Info($"Found {ducts.Count} ducts to process");
        
        // Step 2: Calculate placements
        Logger.Info("Calculating placements...");
        var calculator = new PlacementCalculator(doc, config);
        var allPlacements = new List<SupportPlacement>();
        
        foreach (var duct in ducts)
        {
            var placements = calculator.CalculatePlacements(duct);
            allPlacements.AddRange(placements);
        }
        
        Logger.Info($"Calculated {allPlacements.Count} support positions");
        
        // Show diagnostic info
        if (!string.IsNullOrEmpty(PlacementCalculator.LastDiagnostic))
        {
            Logger.Info($"Raycast diagnostic:\n{PlacementCalculator.LastDiagnostic}");
        }
        
        // Count support types for debugging
        int ceilingCount = allPlacements.Count(p => p.SupportType == SupportType.Ceiling);
        int groundCount = allPlacements.Count(p => p.SupportType == SupportType.Ground);
        int wallCount = allPlacements.Count(p => p.SupportType == SupportType.Wall);
        int verticalCount = allPlacements.Count(p => p.SupportType == SupportType.Vertical);
        Logger.Info($"Support types: Ceiling={ceilingCount}, Ground={groundCount}, Wall={wallCount}, Vertical={verticalCount}");
        
        // Show diagnostic dialog
        string diagnosticMsg = $"Ducts: {ducts.Count}\nSupport positions: {allPlacements.Count}\n\n";
        diagnosticMsg += $"Support Types:\n- Ceiling: {ceilingCount}\n- Ground: {groundCount}\n- Wall: {wallCount}\n- Vertical: {verticalCount}\n\n";
        diagnosticMsg += $"Element Cache:\n{PlacementCalculator.LastDiagnostic}\n\n";
        diagnosticMsg += $"Beam Alignment:\n{Core.Hosting.BeamAligner.LastDiagnostic}";
        TaskDialog.Show("Placement Diagnostic", diagnosticMsg);
        
        if (allPlacements.Count == 0)
        {
            results.Warnings.Add("No valid support positions calculated");
            return results;
        }
        
        // Step 3: Calculate loads if enabled
        if (config.CalculateLoads)
        {
            Logger.Info("Calculating loads...");
            var loadCalculator = new LoadCalculator(config);
            var insulationDetector = new InsulationDetector(doc, config.DefaultInsulationType, config.CustomInsulationDensity);
            
            foreach (var duct in ducts)
            {
                var ductPlacements = allPlacements.Where(p => p.DuctId == duct.ElementId).ToList();
                var insulation = insulationDetector.GetInsulation(duct.ElementId);
                loadCalculator.CalculateLoads(duct, ductPlacements, insulation);
            }
        }
        
        // Step 4: Validate placements
        Logger.Info("Validating placements...");
        var clashDetector = new ClashDetector(doc);
        var existingChecker = new ExistingSupportChecker(doc);
        var validator = new PlacementValidator(clashDetector, existingChecker, config.SkipExistingSupports);
        
        var validation = validator.Validate(allPlacements);
        results.SkippedDueToExisting = validation.SkippedDueToExisting;
        results.ClashesDetected = validation.TotalClashes;
        
        // Auto-resolve clashes
        validator.AutoResolveClashes(validation.ValidPlacements);
        
        // Step 5: Place supports
        Logger.Info("Placing supports...");
        
        using (var trans = new Transaction(doc, "Place Duct Supports"))
        {
            trans.Start();
            
            try
            {
                var placer = new SupportPlacer(doc, config);
                
                // Validate families
                var (familiesValid, missingFamilies, searchPath) = placer.ValidateFamilies();
                if (!familiesValid)
                {
                    results.Errors.Add($"Missing families (searched in: {searchPath}):");
                    results.Errors.AddRange(missingFamilies);
                    trans.RollBack();
                    return results;
                }
                
                // Place supports
                var placementResults = placer.PlaceSupports(validation.ValidPlacements);
                
                results.TotalSupportsPlaced = placementResults.TotalSupportsPlaced;
                results.CeilingSupports = placementResults.CeilingSupports;
                results.GroundSupports = placementResults.GroundSupports;
                results.WallSupports = placementResults.WallSupports;
                results.VerticalSupports = placementResults.VerticalSupports;
                results.TotalLoadKg = placementResults.TotalLoadKg;
                results.Errors.AddRange(placementResults.Errors);
                
                trans.Commit();
            }
            catch (Exception ex)
            {
                trans.RollBack();
                results.Errors.Add($"Placement failed: {ex.Message}");
                Logger.Error("Transaction failed", ex);
            }
        }
        
        // Step 6: Export if enabled
        if (results.TotalSupportsPlaced > 0 && (config.ExportExcel || config.ExportPdf))
        {
            Logger.Info("Exporting reports...");
            ExportReports(doc, config, allPlacements, results);
        }
        
        stopwatch.Stop();
        results.ElapsedTime = stopwatch.Elapsed;
        
        return results;
    }
    
    private void ExportReports(Document doc, Configuration config, List<SupportPlacement> placements, PlacementResults results)
    {
        try
        {
            var (projectName, projectNumber) = RevitUtils.GetProjectInfo(doc);
            
            var reportData = new ReportDataBuilder()
                .WithProjectInfo(projectName, projectNumber)
                .WithConfiguration(config)
                .WithPlacements(placements)
                .WithResults(results)
                .Build();
            
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            if (config.ExportExcel)
            {
                string excelPath = Path.Combine(config.OutputFolder, $"DuctSupports_{timestamp}.xlsx");
                var excelExporter = new ExcelExporter();
                excelExporter.Export(reportData, excelPath);
                Logger.Info($"Excel exported to {excelPath}");
            }
            
            if (config.ExportPdf)
            {
                string pdfPath = Path.Combine(config.OutputFolder, $"DuctSupports_{timestamp}.pdf");
                var pdfGenerator = new PdfReportGenerator();
                pdfGenerator.Generate(reportData, pdfPath);
                Logger.Info($"PDF exported to {pdfPath}");
            }
        }
        catch (Exception ex)
        {
            results.Warnings.Add($"Export failed: {ex.Message}");
            Logger.Error("Export failed", ex);
        }
    }
}

/// <summary>
/// Settings command.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class SettingsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var config = Configuration.Load();
        var window = new MainWindow(config);
        window.ShowDialog();
        return Result.Succeeded;
    }
}
