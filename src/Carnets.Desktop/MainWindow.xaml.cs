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
    }
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
    private void Error(Exception error) => MessageBox.Show(this, error is InvalidDataException ? error.Message : "No se pudo completar la operación. Revise el archivo, los permisos y los recursos locales.\n" + error.Message, "Carnets", MessageBoxButton.OK, MessageBoxImage.Warning);
    private bool ConfirmDiscard() => !rows.Any(r => r.Crop != new Crop()) || MessageBox.Show(this, "Se perderán los encuadres de esta sesión. ¿Continuar?", "Encuadres", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void WindowClosing(object? sender, CancelEventArgs e) { if (busy || !ConfirmDiscard()) e.Cancel = true; }
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
    private async Task LoadFileAsync(string path, bool forceMapping)
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
            loadedFile = path; SearchBox.Clear(); FileLabel.Text = Path.GetFileName(path) + " · " + name;
            Preview.Child = null; CurrentLabel.Text = "Elija una fila para revisarla"; Details.Clear(); RunSummary.Clear(); Counts();
            StatusText.Text = $"Carga completada: {rows.Count} titulares. Selección vacía. Revise fotos y errores antes de imprimir.";
        }
        catch (Exception ex) { Error(ex); StatusText.Text = "No se reemplazó la carga anterior."; }
        finally { SetBusy(false); }
    }
    private void Resolve(HolderRow row, bool relocate = false)
    {
        row.Photo = null; row.LayoutErrors.Clear(); row.LowResolution = false;
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
        if (row is null) { CurrentLabel.Text = "Elija una fila para revisarla"; Details.Clear(); return; }
        CurrentLabel.Text = $"Matrícula {row.Matricula} · {row.Holder.Name}"; Details.Text = row.Details;
        if (row.PhotoPath is not null && row.Photo is null)
        {
            try
            {
                var photo = await Task.Run(() => PhotoLoader.Load(row.PhotoPath));
                if (generation != previewGeneration || Current != row) return;
                row.Photo = photo; row.PhotoError = null;
            }
            catch (Exception) { if (generation != previewGeneration || Current != row) return; row.Photo = null; row.PhotoError = "Archivo de foto ilegible. Elija otro PNG/JPEG."; }
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
        row.PhotoPath = picker.FileName; row.Photo = null; row.PhotoError = null; await LoadCurrentPhotoAsync();
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
                try { row.Photo = await Task.Run(() => PhotoLoader.Load(row.PhotoPath)); row.PhotoError = null; }
                catch (Exception) { row.Photo = null; row.PhotoError = "Foto ausente o ilegible; seleccione otro archivo."; }
            }
            row.LayoutErrors = composer.Compose(row.Holder.Values, row.Photo, row.Crop, false).Errors;
            if (row.Photo is not null)
            {
                double scale = Math.Max(profile.PhotoWidth / row.Photo.Bitmap.PixelWidth, profile.PhotoHeight / row.Photo.Bitmap.PixelHeight) * row.Crop.Zoom;
                row.LowResolution = 25.4 / scale < 150;
            }
            row.Refresh();
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
            var selected = await ReviewRowsAsync(); int copies = Selection.ParseCopies(CopiesBox.Text);
            var text = string.Join("\n", selected.Select(r => $"{r.Matricula} · {r.Holder.Name}"));
            RunSummary.Text = $"{selected.Length} titulares · {copies} copias · {selected.Length * copies} tarjetas\n{ModeLabel.Text}\n\n{text}\n\nSelección revisada. El envío se integrará en la siguiente tarea.";
            StatusText.Text = "Selección revisada; no se envió ningún trabajo.";
        }
        catch (Exception ex) { Error(ex); }
        finally { SetBusy(false); RenderPreview(); }
    }
}
