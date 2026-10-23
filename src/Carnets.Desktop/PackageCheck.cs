using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Carnets.Core;

namespace Carnets.Desktop;

internal static class PackageCheck
{
    public static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>(); var metrics = new Dictionary<string, double>();
        var timer = Stopwatch.StartNew();
        var window = new MainWindow(Path.Combine(output, "isolated-settings")) { ShowActivated = false };
        try
        {
            window.Show(); window.LoadDemoSession(); window.UpdateLayout(); metrics["WindowStartupMilliseconds"] = timer.Elapsed.TotalMilliseconds;
            Require(window.SessionRows.Count == 2 && window.SessionRows.All(r => !r.Selected), "WPF abre con dos titulares ficticios sin selección automática");
            var profilePath = Path.Combine(output, "isolated-settings", "profile.json");
            var profile = CardProfile.Load(profilePath); var composer = new CardComposer(profile, Path.GetDirectoryName(profilePath)!);
            var cards = window.SessionRows.Select(r => new CardSnapshot(r.Matricula, new Dictionary<string, string>(r.Holder.Values), r.PhotoPath!, r.ReviewedPhotoHash, new())).ToArray();
            timer.Restart();
            foreach (bool back in new[] { false, true })
            {
                var composed = composer.Compose(cards[0].Values, window.SessionRows[0].Photo, new(), back);
                Require(composed.Errors.Count == 0, back ? "Reverso compuesto sin desbordamientos" : "Frente compuesto sin desbordamientos");
                MainWindow.SaveDrawing(composer.Visual(composed.Drawing), Path.Combine(output, back ? "demo-reverso.png" : "demo-frente.png"));
            }
            metrics["BothPreviewsMilliseconds"] = timer.Elapsed.TotalMilliseconds;
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(output, "demo-ventana.png"))) png.Save(stream);
            var simulator = new SimulatedPrintAdapter(); var run = new PrintRun();
            await Prepared(simulator).PrepareAsync(cards[0], default);
            await run.ExecuteAsync(cards, 2, false, CardSide.Both, Prepared(simulator), _ => Task.FromResult(true), () => { }, default);
            File.WriteAllText(Path.Combine(output, "simulated-run.json"), JsonSerializer.Serialize(run.Results.Select(r => new { r.Card.Matricula, r.State, r.Message }), new JsonSerializerOptions { WriteIndented = true }));
            Require(simulator.Jobs.Count == 2 && run.Results.All(r => r.State == SubmissionState.Accepted) && run.Results.Sum(r => r.Copies) == 4, "Automático: dos trabajos de dos caras y cuatro tarjetas simuladas");
            var manual = new SimulatedPrintAdapter(); int prompts = 0;
            await new PrintRun().ExecuteAsync(cards, 2, true, CardSide.Both, Prepared(manual), _ => { prompts++; return Task.FromResult(true); }, () => { }, default);
            Require(manual.Jobs.Count == 8 && prompts == 4 && manual.Jobs.Select(j => j.Side).SequenceEqual(Enumerable.Range(0, 8).Select(i => i % 2 == 0 ? CardSide.Front : CardSide.Back)), "Manual: cuatro ciclos completos, con pausa entre caras y sin cruzar titulares");
            var failure = new PrintRun();
            await failure.ExecuteAsync(cards, 1, false, CardSide.Both, Prepared(new SimulatedPrintAdapter(2, true)), _ => Task.FromResult(true), () => { }, default);
            Require(failure.Results[0].State == SubmissionState.Accepted && failure.Results[1].State == SubmissionState.Unknown, "Fallo parcial: primer titular aceptado, segundo incierto, sin reintento");
            string workbook = Path.Combine(output, "synthetic-1000.xlsx"); CreateWorkbook(workbook, 1000);
            timer.Restart(); List<Holder> loaded;
            using (var reader = new WorkbookReader(workbook)) { var sheet = reader.Load("Hoja1"); loaded = HolderValidation.Load(sheet, Fields.Suggest(sheet.Headers), DateOnly.FromDateTime(DateTime.Now)); }
            metrics["ReadAndValidate1000Milliseconds"] = timer.Elapsed.TotalMilliseconds;
            Require(loaded.Count == 1000 && loaded.All(h => h.IsValid) && metrics["ReadAndValidate1000Milliseconds"] < 5000, "Lectura y validación de XLSX con 1.000 filas en menos de 5 segundos");
            Require(metrics["WindowStartupMilliseconds"] < 5000 && metrics["BothPreviewsMilliseconds"] < 1000, "Ventana en menos de 5 segundos y vistas en menos de 1 segundo");
            File.WriteAllText(Path.Combine(output, "self-check.json"), JsonSerializer.Serialize(new { Passed = true, Runtime = Environment.Version.ToString(), OS = Environment.OSVersion.VersionString, Checks = checks, Metrics = metrics, PhysicalZebraVerified = false, ArtworkApproved = profile.ArtworkApproved }, new JsonSerializerOptions { WriteIndented = true }));

            IPrintAdapter Prepared(IPrintAdapter inner) => new PreparedPrintAdapter(inner, composer, profilePath, profile, PrintSafety.ResourceStamp(profilePath, profile), false);
        }
        finally { window.Close(); }
        void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
    }
    private static void CreateWorkbook(string path, int count)
    {
        // Diagnostic fixture only: standard SpreadsheetML, synthetic records, no external connections.
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Put(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(xml); }
        Put("[Content_Types].xml", "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/><Default Extension='xml' ContentType='application/xml'/><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/><Override PartName='/xl/worksheets/sheet1.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml'/></Types>");
        Put("_rels/.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='xl/workbook.xml'/></Relationships>");
        Put("xl/workbook.xml", "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Hoja1' sheetId='1' r:id='rId1'/></sheets></workbook>");
        Put("xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var data = new XElement(ns + "sheetData");
        void Row(int number, string[] values) => data.Add(new XElement(ns + "row", new XAttribute("r", number), values.Select((value, index) => new XElement(ns + "c", new XAttribute("r", ((char)('A' + index)).ToString() + number), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value))))));
        Row(1, Fields.Headers);
        for (int i = 0; i < count; i++) Row(i + 2, ["José", "Demostración", "000-0000000-0", i.ToString("D6"), "Santo Domingo", "-", "-", "PRUEBA", "01/01/2026", "31/12/2028", ""]);
        Put("xl/worksheets/sheet1.xml", new XElement(ns + "worksheet", data).ToString(SaveOptions.DisableFormatting));
    }
}
