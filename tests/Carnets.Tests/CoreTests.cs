using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using Carnets.Core;

namespace Carnets.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "Carnets.Tests", Guid.NewGuid().ToString("N"));
    private static readonly DateOnly Today = new(2026, 10, 23);
    public CoreTests() => Directory.CreateDirectory(temp);
    public void Dispose() => Directory.Delete(temp, true);
    private static SheetData Sheet(params Dictionary<int, CellData>[] rows) => new("Hoja1", Fields.Headers, rows.Select((r, i) => new SheetRow(i + 2, r)).ToList(), false);
    private static Dictionary<int, CellData> ValidRow(string id = "000123")
    {
        string[] values = [" José ", "Muñoz", "001-0000000-1", id, "Santo Domingo", "", "-", "001-24", "09/03/2026", "31/12/2028", ""];
        return values.Select((s, i) => (s, i)).ToDictionary(x => x.i, x => new CellData(x.s, x.s));
    }
    [Fact] public void HeaderNormalizationRejectsAmbiguousSuggestions()
    {
        string[] headers = [.. Fields.Headers.Select(h => " " + h.ToLowerInvariant() + " "), "MATRICULA"];
        var mapping = Fields.Suggest(headers);
        Assert.Equal(10, mapping.Count); Assert.False(mapping.ContainsKey("matricula")); Assert.Equal(8, mapping["fechaAdmision"]);
    }
    [Fact] public void ValuesPreserveIdentifiersAndAccentsAndSeparateOptionalNames()
    {
        var h = Assert.Single(HolderValidation.Load(Sheet(ValidRow()), Fields.Suggest(Fields.Headers), Today));
        Assert.True(h.IsValid); Assert.Equal("000123", h.Matricula); Assert.Equal("José", h["nombre"]); Assert.Equal("-", h["segundoNombre"]);
        Assert.Equal("001-0000000-1", h["cedula"]); Assert.True(h.Matches("JOSE")); Assert.True(h.Matches("munoz"));
    }
    [Fact] public void AllDuplicateRowsAreBlocked()
    {
        var holders = HolderValidation.Load(Sheet(ValidRow(" 001 "), ValidRow("001"), ValidRow("1")), Fields.Suggest(Fields.Headers), Today);
        Assert.All(holders.Take(2), h => Assert.Contains(h.Issues, i => i.Field == "matricula" && !i.Warning)); Assert.True(holders[2].IsValid);
    }
    [Fact] public void MissingOrReusedMappingColumnsAreRejected()
    {
        var map = Fields.Suggest(Fields.Headers); map["nombre"] = map["apellido"];
        Assert.Throws<InvalidDataException>(() => HolderValidation.Load(Sheet(ValidRow()), map, Today));
        map.Remove("nombre"); Assert.Throws<InvalidDataException>(() => HolderValidation.Load(Sheet(ValidRow()), map, Today));
    }
    [Fact] public void ExpiryBeforeAdmissionBlocksAndExpiredOnlyWarns()
    {
        var invalid = ValidRow(); invalid[9] = new("01/01/2020", "01/01/2020");
        Assert.False(Assert.Single(HolderValidation.Load(Sheet(invalid), Fields.Suggest(Fields.Headers), Today)).IsValid);
        invalid[8] = new("01/01/2019", "01/01/2019");
        var h = Assert.Single(HolderValidation.Load(Sheet(invalid), Fields.Suggest(Fields.Headers), Today));
        Assert.True(h.IsValid); Assert.True(h.IsExpired(Today)); Assert.Contains(h.Issues, i => i.Warning && i.Field == "fechaExpiracion");
    }
    [Theory][InlineData("02/30/2026")][InlineData("2026-03-09")][InlineData("31/04/2026")]
    public void DatesUseOnlyConfiguredFormat(string date)
    {
        var row = ValidRow(); row[8] = new(date, date);
        Assert.False(Assert.Single(HolderValidation.Load(Sheet(row), Fields.Suggest(Fields.Headers), Today)).IsValid);
    }
    [Fact] public void BothExcelCalendarsAndLeapBugAreHandled()
    {
        var row = ValidRow(); row[8] = new("1", "1", true);
        var sheet = Sheet(row);
        Assert.Equal(new DateOnly(1900, 1, 1), Assert.Single(HolderValidation.Load(sheet, Fields.Suggest(Fields.Headers), Today)).Admission);
        Assert.Equal(new DateOnly(1904, 1, 2), Assert.Single(HolderValidation.Load(sheet with { Date1904 = true }, Fields.Suggest(Fields.Headers), Today)).Admission);
        row[8] = new("60", "60", true); Assert.Null(Assert.Single(HolderValidation.Load(sheet, Fields.Suggest(Fields.Headers), Today)).Admission);
    }
    [Fact] public void SelectionSurvivesSortingAndFiltering()
    {
        var rows = HolderValidation.Load(Sheet(ValidRow("2"), ValidRow("1")), Fields.Suggest(Fields.Headers), Today);
        var selection = new Selection(); selection.Set(rows[0], true);
        _ = rows.OrderBy(h => h.Matricula).Where(h => h.Matches("1")).ToArray();
        Assert.Same(rows[0], Assert.Single(selection.Items)); selection.Clear(); Assert.Empty(selection.Items);
    }
    [Theory][InlineData("0")][InlineData("101")][InlineData("1.5")][InlineData("-1")][InlineData(" 2")][InlineData("")]
    public void CopiesRejectInvalidValues(string value) => Assert.Throws<InvalidDataException>(() => Selection.ParseCopies(value));
    [Theory][InlineData("1", 1)][InlineData("100", 100)][InlineData("2", 2)]
    public void CopiesAcceptValidIntegers(string text, int expected) => Assert.Equal(expected, Selection.ParseCopies(text));

    private string Workbook(bool cached = false)
    {
        string path = Path.Combine(temp, "test.xlsx");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Put(string part, string xml) { using var writer = new StreamWriter(zip.CreateEntry(part).Open()); writer.Write(xml); }
        Put("xl/workbook.xml", "<workbook xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Hoja1' r:id='rId1'/></sheets></workbook>");
        Put("xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id='rId1' Target='worksheets/sheet1.xml' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet'/></Relationships>");
        Put("xl/styles.xml", "<styleSheet><numFmts><numFmt numFmtId='164' formatCode='000000'/></numFmts><cellXfs><xf numFmtId='0'/><xf numFmtId='164'/></cellXfs></styleSheet>");
        Put("xl/worksheets/sheet1.xml", $"<worksheet><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>MATRÍCULA</t></is></c></row><row r='2'/><row r='5'><c r='A5' s='1'><v>123</v></c><c r='B5'><f>1+1</f>{(cached ? "<v>2</v>" : "")}</c></row></sheetData></worksheet>");
        return path;
    }
    [Fact] public void ReaderPreservesFormattedZeroesSourceRowAndUncachedFormulaError()
    {
        using var book = new WorkbookReader(Workbook()); var sheet = book.Load("Hoja1"); var row = Assert.Single(sheet.Rows);
        Assert.Equal(5, row.Number); Assert.Equal("000123", row.Cells[0].Display); Assert.True(row.Cells[0].Padded); Assert.NotNull(row.Cells[1].Error);
    }
    [Fact] public void ReaderUsesCachedFormulaWithoutCalculating()
    {
        using var book = new WorkbookReader(Workbook(true)); var cell = Assert.Single(book.Load("Hoja1").Rows).Cells[1]; Assert.Equal("2", cell.Display); Assert.Null(cell.Error);
    }
    [Fact] public void ReaderRejectsInvalidFileAndDoesNotWriteOriginal()
    {
        var path = Workbook(); string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        using (var book = new WorkbookReader(path)) _ = book.Load("Hoja1");
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        File.WriteAllText(Path.Combine(temp, "bad.xlsx"), "bad"); Assert.Throws<InvalidDataException>(() => new WorkbookReader(Path.Combine(temp, "bad.xlsx")));
    }
    [Fact] public void IncludedWorkbookHasElevenColumnsAndTwoRows()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string path = Path.Combine(root, "docs", "Base de Datos Zebra ZC300.xlsx");
        var before = File.ReadAllBytes(path);
        using var reader = new WorkbookReader(path); var sheet = reader.Load("Hoja1");
        Assert.Equal(11, sheet.Headers.Count); Assert.Equal(2, sheet.Rows.Count); Assert.Equal(11, Fields.Suggest(sheet.Headers).Count);
        Assert.All(HolderValidation.Load(sheet, Fields.Suggest(sheet.Headers), Today), h => { Assert.NotNull(h.Admission); Assert.NotNull(h.Expiration); Assert.NotEmpty(h.Matricula); });
        Assert.Equal(before, File.ReadAllBytes(path));
    }
    [Fact] public void ExplicitPhotoFailureDoesNotFallbackAndRelocationIsExplicit()
    {
        File.WriteAllText(Path.Combine(temp, "001.png"), "fixture");
        Assert.Null(PhotoResolver.Resolve("C:\\old\\001.png", "001", temp).Path);
        Assert.NotNull(PhotoResolver.Resolve("C:\\old\\001.png", "001", temp, true).Path);
        File.WriteAllText(Path.Combine(temp, "001.jpg"), "fixture");
        var result = PhotoResolver.Resolve("", "001", temp); Assert.Null(result.Path); Assert.Equal(2, result.Candidates.Count);
    }
    [Fact] public void SettingsAreVersionedAtomicAndNeverContainSessionData()
    {
        string path = Path.Combine(temp, "settings.json");
        var settings = new OperatorSettings { PhotosFolder = temp, FrontX = 1.25 };
        SettingsStore.Save(path, settings); Assert.Equal(1.25, SettingsStore.Load(path).FrontX); Assert.Empty(Directory.GetFiles(temp, "*.tmp"));
        File.WriteAllText(path, "{broken"); Assert.Throws<InvalidDataException>(() => SettingsStore.Load(path)); Assert.Equal("{broken", File.ReadAllText(path));
        settings.SchemaVersion = 99; Assert.Throws<InvalidDataException>(() => SettingsStore.Save(path, settings));
    }
    [Fact] public void ThousandRowsValidateWithinFiveSeconds()
    {
        var rows = Enumerable.Range(0, 1000).Select(i => ValidRow(i.ToString("D6"))).ToArray();
        var timer = Stopwatch.StartNew(); var loaded = HolderValidation.Load(Sheet(rows), Fields.Suggest(Fields.Headers), Today);
        Assert.Equal(1000, loaded.Count); Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5));
    }

    private CardSnapshot Card(string id = "001")
    {
        string path = Path.Combine(temp, id + ".png"); File.WriteAllText(path, "synthetic photo bytes");
        return new(id, new Dictionary<string, string>(), path, PhotoResolver.Fingerprint(path), new());
    }
    [Fact] public async Task AutomaticPlanKeepsBothFacesTogetherAndCopiesPerHolder()
    {
        var adapter = new SimulatedPrintAdapter(); var run = new PrintRun();
        await run.ExecuteAsync([Card(), Card("002")], 2, false, CardSide.Both, adapter, _ => Task.FromResult(true), () => { }, default);
        Assert.Equal(2, adapter.Jobs.Count); Assert.All(adapter.Jobs, j => { Assert.Equal(CardSide.Both, j.Side); Assert.Equal(2, j.Copies); });
        Assert.Equal(4, run.Results.Sum(r => r.Copies)); Assert.All(run.Results, r => Assert.Equal(SubmissionState.Accepted, r.State));
    }
    [Fact] public async Task ManualCopiesRequireReinsertionAndCompleteEachCardBeforeNext()
    {
        var adapter = new SimulatedPrintAdapter(); int prompts = 0; var run = new PrintRun();
        await run.ExecuteAsync([Card(), Card("002")], 2, true, CardSide.Both, adapter, r => { prompts++; Assert.Equal(CardSide.Front, adapter.Jobs.Last().Side); Assert.Equal(r.Card.Matricula, adapter.Jobs.Last().Card.Matricula); return Task.FromResult(true); }, () => { }, default);
        Assert.Equal(4, prompts); Assert.Equal(8, adapter.Jobs.Count);
        Assert.Equal(new[] { CardSide.Front, CardSide.Back, CardSide.Front, CardSide.Back, CardSide.Front, CardSide.Back, CardSide.Front, CardSide.Back }, adapter.Jobs.Select(j => j.Side));
        Assert.All(adapter.Jobs, j => Assert.Equal(1, j.Copies));
    }
    [Fact] public async Task RejectingReinsertionStopsWithIncompleteCardAndPendingNext()
    {
        var run = new PrintRun(); var adapter = new SimulatedPrintAdapter();
        await run.ExecuteAsync([Card(), Card("002")], 1, true, CardSide.Both, adapter, _ => Task.FromResult(false), () => { }, default);
        Assert.Single(adapter.Jobs); Assert.Equal(SubmissionState.Incomplete, run.Results[0].State); Assert.Equal(SubmissionState.Pending, run.Results[1].State);
    }
    [Theory][InlineData(false, SubmissionState.Failed)][InlineData(true, SubmissionState.Unknown)]
    public async Task FailureKeepsPartialResultsAndDoesNotRetry(bool uncertain, SubmissionState expected)
    {
        var run = new PrintRun(); var adapter = new SimulatedPrintAdapter(2, uncertain);
        await run.ExecuteAsync([Card(), Card("002"), Card("003")], 1, false, CardSide.Both, adapter, _ => Task.FromResult(true), () => { }, default);
        Assert.Single(adapter.Jobs); Assert.Equal(SubmissionState.Accepted, run.Results[0].State); Assert.Equal(expected, run.Results[1].State); Assert.Equal(SubmissionState.Pending, run.Results[2].State);
    }
    [Fact] public async Task PhotoChangeBlocksBeforeSending()
    {
        var card = Card(); File.WriteAllText(card.PhotoPath, "changed"); var adapter = new SimulatedPrintAdapter(); var run = new PrintRun();
        await run.ExecuteAsync([card], 1, false, CardSide.Both, adapter, _ => Task.FromResult(true), () => { }, default);
        Assert.Empty(adapter.Jobs); Assert.Equal(SubmissionState.Failed, Assert.Single(run.Results).State);
    }
    [Fact] public async Task StopDoesNotCancelAlreadyAcceptedFaces()
    {
        var run = new PrintRun(); var adapter = new SimulatedPrintAdapter(); using var cancel = new CancellationTokenSource();
        await run.ExecuteAsync([Card(), Card("002")], 1, false, CardSide.Both, adapter, _ => Task.FromResult(true), () => { if (run.Results.Any(r => r.State == SubmissionState.Accepted)) cancel.Cancel(); }, cancel.Token);
        Assert.Single(adapter.Jobs); Assert.Equal(SubmissionState.Pending, run.Results[1].State);
    }
    [Fact] public async Task ConcurrentSendIsRejected()
    {
        var run = new PrintRun(); var adapter = new SimulatedPrintAdapter(); var cards = new[] { Card() };
        var first = run.ExecuteAsync(cards, 1, false, CardSide.Both, adapter, _ => Task.FromResult(true), () => { }, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => run.ExecuteAsync(cards, 1, false, CardSide.Both, adapter, _ => Task.FromResult(true), () => { }, default));
        await first; Assert.Single(adapter.Jobs);
    }
}
