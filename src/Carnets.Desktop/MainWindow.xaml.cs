using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Carnets.Core;
using Microsoft.Win32;

namespace Carnets.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<HolderRow> rows = [];
    private readonly string localDirectory;
    private string SettingsPath => Path.Combine(localDirectory, "settings.json");
    private string ProfilePath => Path.Combine(localDirectory, "profile.json");
    private OperatorSettings settings = new();
    private CardProfile profile = CardProfile.Default();
    private CardComposer? composer;
    private ICollectionView? view;
    private string? loadedFile;
    private bool initialized, busy, syncingCrop;
    private int previewGeneration;
    private readonly string demoDirectory = Path.Combine(Path.GetTempPath(), "ChenchoNet-demo-" + Guid.NewGuid().ToString("N"));
    private CancellationTokenSource? pending;
    private readonly List<(PrintRun Run, bool Simulated)> sessionRuns = [];
    private HolderRow? Current => Records.SelectedItem as HolderRow;

    public MainWindow() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChenchoNet")) { }
    internal MainWindow(string directory)
    {
        localDirectory = directory;
        InitializeComponent();
        initialized = true;
        Records.ItemsSource = rows;
        view = CollectionViewSource.GetDefaultView(rows);
        view.Filter = value => value is HolderRow row && row.Holder.Matches(SearchBox.Text);
        Closed += (_, _) =>
        {
            previewGeneration++;
            string full = Path.GetFullPath(demoDirectory);
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (Path.GetDirectoryName(full) == parent && Path.GetFileName(full).StartsWith("ChenchoNet-demo-", StringComparison.Ordinal) && Directory.Exists(full))
                try { Directory.Delete(full, true); } catch (IOException) { /* Only synthetic files; a later OS cleanup can remove them. */ }
        };
    }
    internal IReadOnlyList<HolderRow> SessionRows => rows;
    internal void LoadDemoSession()
    {
        if (busy || composer is null || !ConfirmDiscard()) return;
        previewGeneration++; rows.Clear();
        foreach (var row in SyntheticData.Create(demoDirectory)) { row.Changed = Counts; rows.Add(row); }
        loadedFile = null; SearchBox.Clear(); view?.Refresh();
        FileLabel.Text = "DEMOSTRACIÓN · dos titulares ficticios";
        settings.Mode = PrintMode.SimulationAutomatic; Labels();
        Records.SelectedItem = rows[0];
        StatusText.Text = "Datos ficticios cargados sin selección automática. Marque los titulares que desee simular.";
    }
    private void LoadDemo(object sender, RoutedEventArgs e) { try { LoadDemoSession(); } catch (Exception ex) { Error(ex); } }
    private void DisplayZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (initialized) Preview.Width = 420 * DisplayZoom.Value; }
    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(localDirectory);
            try { settings = SettingsStore.Load(SettingsPath); }
            catch (InvalidDataException)
            {
                if (MessageBox.Show(this, "La configuración es inválida. ¿Conservar una copia y restablecerla explícitamente?", "Configuración", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { Close(); return; }
                File.Copy(SettingsPath, SettingsPath + ".corrupt-" + Guid.NewGuid().ToString("N"));
                settings = new(); SettingsStore.Save(SettingsPath, settings);
            }
            if (!File.Exists(ProfilePath)) CardProfile.SaveDefault(ProfilePath);
            try { profile = CardProfile.Load(ProfilePath); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (MessageBox.Show(this, "El perfil es inválido. ¿Conservar una copia y restablecer el perfil inicial?\n" + ex.Message, "Perfil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { Close(); return; }
                File.Copy(ProfilePath, ProfilePath + ".corrupt-" + Guid.NewGuid().ToString("N")); CardProfile.SaveDefault(ProfilePath); profile = CardProfile.Load(ProfilePath);
            }
            composer = new(profile, localDirectory);
            Labels(); RenderPreview();
        }
        catch (Exception ex) { Error(ex); Close(); }
    }
    internal Action<Exception>? ErrorHandler { get; set; }
    private void Error(Exception error)
    {
        if (ErrorHandler is not null) { ErrorHandler(error); return; }
        MessageBox.Show(this, error is InvalidDataException ? error.Message : "No se pudo completar la operación. Revise el archivo, los permisos y los recursos locales.\n" + error.Message, "Carnets", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private bool ConfirmDiscard() => !rows.Any(r => r.Crop != new Crop()) || MessageBox.Show(this, "Se perderán los encuadres de esta sesión. ¿Continuar?", "Encuadres", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; MessageBox.Show(this, "Hay una operación en curso. Detenga los envíos pendientes y espere su resumen antes de cerrar. Cerrar no cancela lo que Windows ya aceptó.", "Operación en curso"); }
        else if (!ConfirmDiscard()) e.Cancel = true;
    }
    private void SetBusy(bool value)
    {
        busy = value; LoadBar.IsEnabled = !value; RecordsPanel.IsEnabled = !value; PhotoPanel.IsEnabled = !value; SendPanel.IsEnabled = !value;
    }
    private void Labels()
    {
        FolderLabel.Text = "Carpeta de fotos: " + (settings.PhotosFolder.Length == 0 ? "sin elegir" : settings.PhotosFolder);
        ModeLabel.Text = settings.Mode switch { PrintMode.SimulationAutomatic => "SIMULACIÓN · dos caras en cola virtual", PrintMode.SimulationManual => "SIMULACIÓN · reinserción manual guiada", PrintMode.Automatic => "Dúplex real · " + settings.Printer, _ => "Manual real · " + settings.Printer };
        ArtworkLabel.Text = profile.ArtworkApproved ? "Perfil con arte aprobado." : "Diseño inicial: logo y firmas originales pendientes. Producción bloqueada; simulación disponible.";
        Counts();
    }
    private void Counts()
    {
        if (!initialized || view is null) return;
        int selected = rows.Count(r => r.Selected), visible = view.Cast<HolderRow>().Count(), hidden = rows.Count(r => r.Selected && !view.Contains(r));
        string cards;
        try { cards = (selected * Selection.ParseCopies(CopiesBox.Text)).ToString(); }
        catch (InvalidDataException) { cards = "cantidad de copias inválida"; }
        SelectionLabel.Text = $"{rows.Count} titulares · {visible} visibles · {selected} seleccionados ({hidden} ocultos) · tarjetas: {cards}";
    }
    private async void LoadExcel(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var picker = new OpenFileDialog { Filter = "Libro de Excel (*.xlsx)|*.xlsx", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) await LoadFileAsync(picker.FileName, false);
    }
    private async void Remap(object sender, RoutedEventArgs e) { if (!busy && loadedFile is not null) await LoadFileAsync(loadedFile, true); }
    internal async Task LoadFileAsync(string path, bool forceMapping)
    {
        if (!ConfirmDiscard()) return;
        SetBusy(true); StatusText.Text = "Leyendo Excel…";
        try
        {
            var names = await Task.Run(() => { using var book = new WorkbookReader(path); return book.Sheets.Select(s => s.Name).ToArray(); });
            string? name = Dialogs.Sheet(this, names); if (name is null) return;
            var sheet = await Task.Run(() => { using var book = new WorkbookReader(path); return book.Load(name); });
            var mapping = Dialogs.Mapping(this, sheet, settings, forceMapping); if (mapping is null) return;
            var holders = await Task.Run(() => HolderValidation.Load(sheet, mapping, DateOnly.FromDateTime(DateTime.Now), settings.DateFormat));
            var incoming = await Task.Run(() => holders.Select(h => { var row = new HolderRow(h); Resolve(row); return row; }).ToArray());
            var nextSettings = settings.Snapshot(); nextSettings.ColumnHeaders = mapping.ToDictionary(p => p.Key, p => sheet.Headers[p.Value]);
            SettingsStore.Save(SettingsPath, nextSettings);
            // Commit the new session only after the whole read, association and validation succeeds.
            settings = nextSettings; previewGeneration++; rows.Clear();
            foreach (var row in incoming) { row.Changed = Counts; rows.Add(row); }
            loadedFile = path; SearchBox.Clear(); view?.Refresh(); FileLabel.Text = Path.GetFileName(path) + " · " + name;
            Preview.Child = null; CurrentLabel.Text = "Elija una fila para revisarla"; Details.Clear(); RunSummary.Clear(); Counts();
            StatusText.Text = $"Carga completada: {rows.Count} titulares. Selección vacía. Revise fotos y errores antes de imprimir.";
        }
        catch (Exception ex) { Error(ex); StatusText.Text = "No se reemplazó la carga anterior."; }
        finally { SetBusy(false); }
    }
    private void Resolve(HolderRow row, bool relocate = false)
    {
        row.Photo = null; row.ReviewedPhotoHash = ""; row.PhotoValidated = false; row.LayoutErrors.Clear(); row.LowResolution = false;
        try { var resolution = PhotoResolver.Resolve(row.Holder["referenciaFoto"], row.Matricula, settings.PhotosFolder, relocate); row.PhotoPath = resolution.Path; row.PhotoError = resolution.Error; }
        catch (Exception) { row.PhotoPath = null; row.PhotoError = "No se pudo resolver la ruta de la foto. Elija un archivo PNG/JPEG accesible."; }
    }
    private async void ChooseFolder(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var picker = new OpenFolderDialog { Title = "Elegir carpeta de fotografías" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var next = settings.Snapshot(); next.PhotosFolder = picker.FolderName; SettingsStore.Save(SettingsPath, next); settings = next;
            foreach (var row in rows) { Resolve(row); row.Refresh(); }
            Labels(); await LoadCurrentPhotoAsync();
        }
        catch (Exception ex) { Error(ex); }
    }
    private static string[] Printers()
    {
        using var server = new LocalPrintServer(); using var queues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
        return queues.Select(q => q.FullName).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private void Configure(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        try
        {
            var next = Dialogs.Settings(this, settings, Printers(), ProfilePath); if (next is null) return;
            var nextProfile = CardProfile.Load(ProfilePath); SettingsStore.Save(SettingsPath, next); settings = next; profile = nextProfile; composer = new(profile, localDirectory);
            foreach (var row in rows) row.LayoutErrors.Clear(); Labels(); RenderPreview();
        }
        catch (Exception ex) { Error(ex); }
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (initialized && !busy) { view?.Refresh(); Counts(); } }
    private void CopiesChanged(object sender, TextChangedEventArgs e) => Counts();
    private void SelectFiltered(object sender, RoutedEventArgs e) { if (!busy && view is not null) foreach (HolderRow row in view) row.Selected = true; }
    private void ClearSelection(object sender, RoutedEventArgs e) { if (!busy) foreach (var row in rows) row.Selected = false; }
    private void RemoveInvalid(object sender, RoutedEventArgs e) { if (!busy) foreach (var row in rows.Where(r => !r.Valid)) row.Selected = false; }
    private async void CurrentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        syncingCrop = true;
        if (Current is { } row) { CropZoom.Value = row.Crop.Zoom; CropX.Value = row.Crop.X; CropY.Value = row.Crop.Y; }
        syncingCrop = false;
        await LoadCurrentPhotoAsync();
    }
    private async Task LoadCurrentPhotoAsync()
    {
        int generation = ++previewGeneration; var row = Current;
        Preview.Child = null;
        // Retain hashes/validation, not all decoded portraits, while browsing large books.
        foreach (var other in rows.Where(r => r != row)) other.Photo = null;
        if (row is null) { CurrentLabel.Text = "Elija una fila para revisarla"; Details.Clear(); return; }
        CurrentLabel.Text = $"Matrícula {row.Matricula} · {row.Holder.Name}"; Details.Text = row.Details;
        if (row.PhotoPath is not null && row.Photo is null)
        {
            PreparedPhoto? photo = null;
            try { photo = await Task.Run(() => PhotoLoader.Load(row.PhotoPath)).ConfigureAwait(false); }
            catch (Exception) { /* Presented on the owning dispatcher below. */ }
            if (generation != Volatile.Read(ref previewGeneration)) return;
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (generation != previewGeneration || Current != row) return;
                    row.Photo = photo; row.PhotoValidated = photo is not null;
                    if (photo is not null) { row.ReviewedPhotoHash = photo.Hash; row.PhotoError = null; }
                    else row.PhotoError = "Archivo de foto ilegible. Elija otro PNG/JPEG.";
                    RenderPreview();
                });
            }
            catch (TaskCanceledException) { /* The window's dispatcher is shutting down. */ }
            return;
        }
        if (generation == previewGeneration) RenderPreview();
    }
    private void SideChanged(object sender, RoutedEventArgs e) { if (initialized) RenderPreview(); }
    private void CropChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!initialized || syncingCrop || busy || Current is not { } row) return;
        row.Crop = new(CropZoom.Value, CropX.Value, CropY.Value); RenderPreview();
    }
    private void ResetCrop(object sender, RoutedEventArgs e)
    {
        if (busy || Current is not { } row) return;
        row.Crop = new(); syncingCrop = true; CropZoom.Value = 1; CropX.Value = CropY.Value = .5; syncingCrop = false; RenderPreview();
    }
    private void RenderPreview()
    {
        if (composer is null || Current is not { } row) return;
        try
        {
            var front = composer.Compose(row.Holder.Values, row.Photo, row.Crop, false);
            row.LayoutErrors = front.Errors;
            if (row.Photo is not null)
            {
                row.ReviewedPhotoHash = row.Photo.Hash; row.PhotoValidated = true;
                double scale = Math.Max(profile.PhotoWidth / row.Photo.Bitmap.PixelWidth, profile.PhotoHeight / row.Photo.Bitmap.PixelHeight) * row.Crop.Zoom;
                row.LowResolution = 25.4 / scale < 150;
            }
            var drawing = FrontRadio.IsChecked == true ? front.Drawing : composer.Compose(row.Holder.Values, null, new(), true).Drawing;
            Preview.Child = composer.Visual(drawing); row.Refresh(); Details.Text = row.Details;
        }
        catch (Exception ex) { Preview.Child = null; row.LayoutErrors = ["No se pudo componer el carnet: " + ex.Message]; row.Refresh(); Details.Text = row.Details; }
    }
    private async void ChoosePhoto(object sender, RoutedEventArgs e)
    {
        if (busy || Current is not { } row) return;
        var picker = new OpenFileDialog { Filter = "Fotografía PNG/JPEG|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        row.PhotoPath = picker.FileName; row.Photo = null; row.ReviewedPhotoHash = ""; row.PhotoValidated = false; row.PhotoError = null; await LoadCurrentPhotoAsync();
    }
    private async void RelocatePhoto(object sender, RoutedEventArgs e)
    {
        if (busy || Current is not { } row) return;
        if (MessageBox.Show(this, "¿Buscar el nombre de FOTOS en la carpeta elegida? Si hay varias coincidencias, elija una con ‘Elegir foto’.", "Reasociar foto", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Resolve(row, true); row.Refresh(); await LoadCurrentPhotoAsync();
    }
    private void ExportPreview(object sender, RoutedEventArgs e)
    {
        if (Preview.Child is not CardVisual visual) return;
        var picker = new SaveFileDialog { Filter = "PNG|*.png", FileName = FrontRadio.IsChecked == true ? "frente.png" : "reverso.png" };
        if (picker.ShowDialog(this) != true) return;
        try { SaveDrawing(visual, picker.FileName); StatusText.Text = "Vista exportada a 300 ppp. La exportación no acredita calibración física."; } catch (Exception ex) { Error(ex); }
    }
    internal static void SaveDrawing(CardVisual visual, string path)
    {
        var size = new Size(CardComposer.Dip(85.6), CardComposer.Dip(53.98));
        var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width * 300 / 96), (int)Math.Round(size.Height * 300 / 96), 300, 300, System.Windows.Media.PixelFormats.Pbgra32);
        var detached = new CardVisual(visual.Drawing, size); detached.Measure(size); detached.Arrange(new Rect(size)); detached.UpdateLayout(); bitmap.Render(detached);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
    private async Task<HolderRow[]> ReviewRowsAsync()
    {
        if (composer is null) throw new InvalidOperationException("El perfil todavía no está listo.");
        _ = Selection.ParseCopies(CopiesBox.Text);
        var selected = rows.Where(r => r.Selected).OrderBy(r => r.Row).ToArray();
        if (selected.Length == 0) throw new InvalidDataException("Seleccione al menos un titular.");
        foreach (var row in selected)
        {
            if (row.PhotoPath is not null)
            {
                try
                {
                    var latest = await Task.Run(() => PhotoLoader.Load(row.PhotoPath));
                    if (row.ReviewedPhotoHash.Length > 0 && row.ReviewedPhotoHash != latest.Hash) { row.Photo = null; row.ReviewedPhotoHash = ""; row.PhotoValidated = false; row.PhotoError = "La foto cambió desde la vista previa. Revise la fila de nuevo antes de enviar."; }
                    else { row.Photo = latest; row.ReviewedPhotoHash = latest.Hash; row.PhotoValidated = true; row.PhotoError = null; }
                }
                catch (Exception) { row.Photo = null; row.PhotoValidated = false; row.PhotoError = "Foto ausente o ilegible; seleccione otro archivo."; }
            }
            row.LayoutErrors = composer.Compose(row.Holder.Values, row.Photo, row.Crop, false).Errors;
            if (row.Photo is not null)
            {
                double scale = Math.Max(profile.PhotoWidth / row.Photo.Bitmap.PixelWidth, profile.PhotoHeight / row.Photo.Bitmap.PixelHeight) * row.Crop.Zoom;
                row.LowResolution = 25.4 / scale < 150;
            }
            row.Refresh();
            if (row != Current) row.Photo = null;
        }
        var invalid = selected.Where(r => !r.Valid).ToArray();
        if (invalid.Length > 0) throw new InvalidDataException("La selección contiene errores en las filas: " + string.Join(", ", invalid.Select(r => r.Row)) + ". Corrija esas filas o use ‘Quitar seleccionados con error’. No se excluyeron automáticamente.");
        return selected;
    }
    private async void ReviewSelection(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        try
        {
            profile = CardProfile.Load(ProfilePath); composer = new(profile, localDirectory); Labels();
            var selected = await ReviewRowsAsync(); int copies = Selection.ParseCopies(CopiesBox.Text);
            CardSide side = (CardSide)SideChoice.SelectedIndex;
            if (side != CardSide.Both && (selected.Length != 1 || copies != 1)) throw new InvalidDataException("Para recuperar una cara, seleccione exactamente un titular y una copia. Identifique la tarjeta incompleta antes de confirmar.");
            var cards = selected.Select(r => new CardSnapshot(r.Matricula, new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(r.Holder.Values)), r.PhotoPath!, r.ReviewedPhotoHash, r.Crop,
                r.Holder.Issues.Where(i => i.Warning).Select(i => i.Message).Concat(r.LowResolution ? new[] { "Resolución efectiva de foto inferior a 150 ppp." } : []).ToArray())).ToArray();
            await SendCardsAsync(cards, copies, side, selected.Any(r => r.Holder.IsExpired(DateOnly.FromDateTime(DateTime.Now))), false);
        }
        catch (Exception ex) { Error(ex); }
        finally { SetBusy(false); RenderPreview(); }
    }
    private async Task SendCardsAsync(CardSnapshot[] cards, int copies, CardSide side, bool expired, bool calibration)
    {
        if (composer is null) throw new InvalidOperationException();
        var snapshot = settings.Snapshot();
        bool simulated = snapshot.Mode is PrintMode.SimulationAutomatic or PrintMode.SimulationManual;
        bool manual = snapshot.Mode is PrintMode.Manual or PrintMode.SimulationManual;
        PrintSafety.ValidateMode(snapshot, profile, calibration);
        if (!simulated && !Printers().Contains(snapshot.Printer, StringComparer.Ordinal)) throw new InvalidDataException("La impresora elegida ya no está disponible. No se usará otra.");
        if (calibration && manual && !simulated && (string.IsNullOrWhiteSpace(snapshot.Reinsertion) || string.IsNullOrWhiteSpace(snapshot.Feed))) throw new InvalidDataException("Antes de la prueba manual, escriba en Configuración las instrucciones de alimentación y reinserción que va a comprobar.");
        string stamp = PrintSafety.ResourceStamp(ProfilePath, profile);
        if (!Dialogs.ConfirmPrint(this, cards, copies, ModeLabel.Text, simulated ? "Cola virtual local · sin impresora física" : snapshot.Printer, side, expired, calibration, composer))
        { StatusText.Text = "Envío cancelado antes de enviar. No se crearon trabajos."; return; }
        IPrintAdapter inner = simulated ? new SimulatedPrintAdapter(SimulationFault.SelectedIndex == 0 ? 0 : 2, SimulationFault.SelectedIndex == 2) : new WindowsPrintAdapter(snapshot, composer, calibration);
        var adapter = new PreparedPrintAdapter(inner, composer, ProfilePath, profile, stamp, calibration);
        var run = new PrintRun(); sessionRuns.Add((run, simulated));
        pending = new(); StopButton.IsEnabled = true;
        try
        {
            await run.ExecuteAsync(cards, copies, manual, side, adapter, result => Task.FromResult(MessageBox.Show(this,
                $"{(simulated ? "SIMULACIÓN · no hay una tarjeta física" : "MODO MANUAL")}\nMatrícula: {result.Card.Matricula}\nCopia: {result.Copy}\nEl frente fue aceptado por la cola. Compruebe que terminó físicamente antes de reinsertar.\nAlimentación: {snapshot.Feed}\nReinserción: {snapshot.Reinsertion}\n\n¿Confirma la reinserción de esta misma tarjeta para enviar el reverso? No continúe con otra tarjeta incompleta.",
                "Confirmar reinserción", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes), ShowRunSummary, pending.Token);
            ShowRunSummary();
            StatusText.Text = simulated ? "Simulación terminada. Consulte el resumen; no se imprimieron tarjetas físicas." : "Envíos terminados o detenidos. Revise el resumen y la cola; la salida física no está verificada.";
        }
        finally { StopButton.IsEnabled = false; pending.Dispose(); pending = null; }
    }
    private void ShowRunSummary()
    {
        static string StateText(SubmissionState state) => state switch
        {
            SubmissionState.Pending => "Pendiente", SubmissionState.Preparing => "Preparando", SubmissionState.Sending => "Enviando",
            SubmissionState.Accepted => "Aceptado por la cola", SubmissionState.Incomplete => "Incompleto", SubmissionState.Failed => "Fallido", _ => "Desconocido"
        };
        var lines = new List<string>();
        for (int i = 0; i < sessionRuns.Count; i++)
        {
            var (run, simulated) = sessionRuns[i];
            int Count(SubmissionState state) => run.Results.Where(r => r.State == state).Sum(r => r.Copies);
            lines.Add($"Lote {i + 1} · {(simulated ? "SIMULACIÓN · cola virtual" : "COLA WINDOWS")}\nTarjetas con ambas caras en cola: {Count(SubmissionState.Accepted)} · pendientes: {Count(SubmissionState.Pending)} · incompletas: {Count(SubmissionState.Incomplete)} · desconocidas: {Count(SubmissionState.Unknown)} · fallidas: {Count(SubmissionState.Failed)}");
            lines.AddRange(run.Results.Select(r => $"{r.Card.Matricula} · copia {r.Copy} · {r.Copies} tarjeta(s) · {StateText(r.State)} · {r.Message}{(r.JobIds.Count > 0 ? " · trabajos: " + string.Join(", ", r.JobIds) : "")}"));
        }
        lines.Add("La cola no acredita tarjetas físicas completadas. Detener no cancela lo ya aceptado. No hay reintento automático.");
        RunSummary.Text = string.Join("\n", lines);
    }
    private void StopPending(object sender, RoutedEventArgs e)
    {
        pending?.Cancel(); StopButton.IsEnabled = false; StatusText.Text = "Deteniendo nuevos envíos. Los trabajos ya aceptados deben revisarse en la cola; no se cancelan automáticamente.";
    }
    private void QueueStatus(object sender, RoutedEventArgs e)
    {
        try
        {
            if (settings.Mode is PrintMode.SimulationAutomatic or PrintMode.SimulationManual) { ShowRunSummary(); StatusText.Text = "Cola virtual en memoria. No representa estados del hardware Zebra."; return; }
            if (!Printers().Contains(settings.Printer, StringComparer.Ordinal)) throw new InvalidDataException("La impresora seleccionada ya no está instalada.");
            using var server = new LocalPrintServer(); using var queue = new PrintQueue(server, settings.Printer); queue.Refresh();
            using var jobs = queue.GetPrintJobInfoCollection();
            string description = string.Join("\n", jobs.Select(j => $"Trabajo {j.JobIdentifier}: {j.JobStatus}"));
            MessageBox.Show(this, $"{queue.FullName}: {queue.QueueStatus}\n{description}\n\nEstos son los estados que informa Windows, no una verificación física. Para cancelar un trabajo aceptado, abra Configuración de Windows > Impresoras y escáneres > su impresora > Abrir cola.", "Estado de la cola");
        }
        catch (Exception ex) { Error(ex); }
    }
    private async void Calibrate(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SetBusy(true);
        string path = Path.Combine(Path.GetTempPath(), "carnets-calibration-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            profile = CardProfile.Load(ProfilePath); composer = new(profile, localDirectory);
            var image = new System.Windows.Media.DrawingVisual();
            using (var dc = image.RenderOpen()) { dc.DrawRectangle(System.Windows.Media.Brushes.LightGray, null, new Rect(0, 0, 200, 250)); dc.DrawEllipse(System.Windows.Media.Brushes.SteelBlue, null, new Point(100, 100), 45, 60); }
            var bitmap = new RenderTargetBitmap(200, 250, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(image);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
            string[] values = ["PRUEBA", "CALIBRACIÓN", "000-0000000-0", "PRUEBA-001", "SANTO DOMINGO", "-", "-", "000-00", "01/01/2026", "31/12/2028", ""];
            var card = new CardSnapshot("PRUEBA-001", Fields.Keys.Zip(values).ToDictionary(p => p.First, p => p.Second), path, PhotoResolver.Fingerprint(path), new());
            await SendCardsAsync([card], 1, CardSide.Both, false, true);
        }
        catch (Exception ex) { Error(ex); }
        finally { if (File.Exists(path)) File.Delete(path); SetBusy(false); }
    }
}
