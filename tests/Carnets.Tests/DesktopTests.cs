using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Carnets.Core;
using Carnets.Desktop;

namespace Carnets.Tests;

public sealed class DesktopTests
{
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static Dictionary<string, string> Values()
    {
        string[] fields = ["JOSÉ", "MUÑOZ", "001-0000000-1", "000123", "SANTO DOMINGO", "-", "PÉREZ", "001-24", "09/03/2026", "31/12/2028", ""];
        return Fields.Keys.Zip(fields).ToDictionary(p => p.First, p => p.Second);
    }
    private static PreparedPhoto SyntheticPhoto()
    {
        // A deliberately non-square, synthetic image tests crop without retaining a real portrait.
        byte[] pixels = new byte[156 * 191 * 4];
        for (int y = 0; y < 191; y++) for (int x = 0; x < 156; x++)
        {
            int i = (y * 156 + x) * 4; pixels[i] = (byte)(x * 255 / 156); pixels[i + 1] = (byte)(y * 255 / 191); pixels[i + 2] = 100; pixels[i + 3] = 255;
        }
        var bitmap = BitmapSource.Create(156, 191, 96, 96, PixelFormats.Bgra32, null, pixels, 156 * 4); bitmap.Freeze();
        return new(bitmap, "synthetic");
    }
    [Fact] public void CardCompositionPreservesPhysicalSizeAndAllTenFields() => Sta(() =>
    {
        var profile = CardProfile.Default(); profile.Validate(System.IO.Path.GetTempPath());
        var composer = new CardComposer(profile, System.IO.Path.GetTempPath());
        var composed = composer.Compose(Values(), SyntheticPhoto(), new(), false);
        Assert.Empty(composed.Errors); Assert.Equal(85.6 * 96 / 25.4, composer.Size.Width, 7); Assert.Equal(53.98 * 96 / 25.4, composer.Size.Height, 7);
        Assert.Equal(10, profile.Fields.Count); Assert.Contains(profile.Fields, f => f.Key == "filial" && f.Label == "Provincia");
        Assert.True(composed.Drawing.IsFrozen);
    });
    [Fact] public void LongTextBlocksInsteadOfSilentlyClippingAtMinimumFont() => Sta(() =>
    {
        var values = Values(); values["nombre"] = new string('W', 200);
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath());
        Assert.Contains(composer.Compose(values, SyntheticPhoto(), new(), false).Errors, e => e.StartsWith("Nombre:"));
    });
    [Fact] public void BackContainsNoVariableDataAndRemainsSameAcrossHolders() => Sta(() =>
    {
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath());
        var values = Values(); var first = composer.Compose(values, SyntheticPhoto(), new(), true).Drawing;
        values["nombre"] = "OTRO TITULAR"; values["matricula"] = "999";
        var second = composer.Compose(values, null, new(4, 0, 0), true).Drawing;
        Assert.Equal(Pixels(composer.Visual(first)), Pixels(composer.Visual(second)));
    });
    private static byte[] Pixels(CardVisual visual)
    {
        var size = new Size(CardComposer.Dip(85.6), CardComposer.Dip(53.98));
        visual.Measure(size); visual.Arrange(new Rect(size)); visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap(324, 205, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var bytes = new byte[324 * 205 * 4]; bitmap.CopyPixels(bytes, 324 * 4, 0); return bytes;
    }
    [Fact] public void DifferentCropsChangeOnlyTheRenderedPhotoWithoutMutatingSource() => Sta(() =>
    {
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath()); var photo = SyntheticPhoto();
        byte[] a = Pixels(composer.Visual(composer.Compose(Values(), photo, new(), false).Drawing));
        byte[] b = Pixels(composer.Visual(composer.Compose(Values(), photo, new(2, 0, 1), false).Drawing));
        Assert.False(a.SequenceEqual(b)); Assert.Equal(156, photo.Bitmap.PixelWidth); Assert.Equal(191, photo.Bitmap.PixelHeight);
    });
    [Fact] public void UnapprovedProfileDoesNotPretendToHaveOriginalArtwork() => Sta(() =>
    {
        var profile = CardProfile.Default(); Assert.False(profile.ArtworkApproved); profile.ArtworkApproved = true;
        Assert.Throws<System.IO.InvalidDataException>(() => profile.Validate(System.IO.Path.GetTempPath()));
    });
    [Fact] public void RealProductionRequiresArtworkAndExactValidatedPrinter()
    {
        var profile = CardProfile.Default(); var settings = new OperatorSettings { Mode = PrintMode.Automatic, Printer = "ZC300" };
        Assert.Throws<System.IO.InvalidDataException>(() => PrintSafety.ValidateMode(settings, profile, false));
        PrintSafety.ValidateMode(settings, profile, true); // Calibration deliberately precedes hardware acceptance.
        profile.ArtworkApproved = true;
        Assert.Throws<System.IO.InvalidDataException>(() => PrintSafety.ValidateMode(settings, profile, false));
        settings.AutomaticValidated = true; settings.ValidatedPrinter = "ZC300"; PrintSafety.ValidateMode(settings, profile, false);
        settings.Printer = "Other"; Assert.Throws<System.IO.InvalidDataException>(() => PrintSafety.ValidateMode(settings, profile, false));
        settings.Mode = PrintMode.SimulationAutomatic; PrintSafety.ValidateMode(settings, CardProfile.Default(), false);
    }
    [Fact] public void FixedLayoutIsConfigurableAndOverlappingVariableFieldsAreRejected() => Sta(() =>
    {
        var profile = CardProfile.Default(); var values = Values();
        var original = Pixels(new CardComposer(profile, System.IO.Path.GetTempPath()).Visual(new CardComposer(profile, System.IO.Path.GetTempPath()).Compose(values, SyntheticPhoto(), new(), true).Drawing));
        profile.Fixed["backTitle"] = new(4, 3, 77.6, 14, 28); profile.Validate(System.IO.Path.GetTempPath());
        var changedComposer = new CardComposer(profile, System.IO.Path.GetTempPath());
        Assert.False(original.SequenceEqual(Pixels(changedComposer.Visual(changedComposer.Compose(values, null, new(), true).Drawing))));
        profile.Fields[1].X = profile.Fields[0].X; profile.Fields[1].Y = profile.Fields[0].Y;
        Assert.Throws<System.IO.InvalidDataException>(() => profile.Validate(System.IO.Path.GetTempPath()));
    });
    [Fact] public void ManualProductionRequiresBothFeedAndReinsertionInstructions()
    {
        var profile = CardProfile.Default(); profile.ArtworkApproved = true;
        var settings = new OperatorSettings { Mode = PrintMode.Manual, Printer = "ZC300", ValidatedPrinter = "ZC300", ManualValidated = true };
        Assert.Throws<System.IO.InvalidDataException>(() => PrintSafety.ValidateMode(settings, profile, false));
        settings.Feed = "Una tarjeta por ciclo"; settings.Reinsertion = "Orientación comprobada"; PrintSafety.ValidateMode(settings, profile, false);
    }
    [Fact] public void PreparedSimulationUsesRealCompositionAndBlocksChangedResources() => Sta(() =>
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Carnets.PreparedTests", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(directory);
        try
        {
            string profilePath = System.IO.Path.Combine(directory, "profile.json"); CardProfile.SaveDefault(profilePath); var profile = CardProfile.Load(profilePath);
            string photoPath = System.IO.Path.Combine(directory, "photo.png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(SyntheticPhoto().Bitmap)); using (var stream = System.IO.File.Create(photoPath)) encoder.Save(stream);
            var card = new CardSnapshot("000123", Values(), photoPath, PhotoResolver.Fingerprint(photoPath), new());
            var inner = new SimulatedPrintAdapter(); var composer = new CardComposer(profile, directory);
            var adapter = new PreparedPrintAdapter(inner, composer, profilePath, profile, PrintSafety.ResourceStamp(profilePath, profile), false);
            adapter.PrepareAsync(card, default).GetAwaiter().GetResult();
            var receipt = adapter.SendAsync(new(card, 1, 2, CardSide.Both), default).GetAwaiter().GetResult(); Assert.StartsWith("SIM-", receipt.JobId); Assert.Equal(2, Assert.Single(inner.Jobs).Copies);
            System.IO.File.AppendAllText(profilePath, " ");
            Assert.Throws<SubmissionException>(() => adapter.PrepareAsync(card, default).GetAwaiter().GetResult());
        }
        finally { System.IO.Directory.Delete(directory, true); }
    });
    [Fact] public void CorruptPhotosAreRejectedWithoutReusingPreviousPixels() => Sta(() =>
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "carnets-corrupt-" + Guid.NewGuid().ToString("N") + ".png");
        try { System.IO.File.WriteAllText(path, "not an image"); Assert.ThrowsAny<Exception>(() => PhotoLoader.Load(path)); }
        finally { System.IO.File.Delete(path); }
    });
    [Fact] public void WorkerDecodedPhotoCanBeComposedOnTheUiThread() => Sta(() =>
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "carnets-thread-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(SyntheticPhoto().Bitmap)); using (var stream = System.IO.File.Create(path)) encoder.Save(stream);
            var loaded = Task.Run(() => PhotoLoader.Load(path)).GetAwaiter().GetResult();
            Assert.True(loaded.Bitmap.IsFrozen);
            var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath());
            Assert.Empty(composer.Compose(Values(), loaded, new(), false).Errors);
        }
        finally { System.IO.File.Delete(path); }
    });
    [Fact] public void IncludedWorkbookPhotosCanBeExplicitlyRelocatedAndDecoded() => Sta(() =>
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Carnets.ReferenceTests", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(directory);
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("tar") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (string arg in new[] { "-xf", System.IO.Path.Combine(root, "docs", "Fotos prueba.rar"), "-C", directory }) start.ArgumentList.Add(arg);
            using var process = System.Diagnostics.Process.Start(start)!; process.WaitForExit(); Assert.Equal(0, process.ExitCode);
            using var workbook = new WorkbookReader(System.IO.Path.Combine(root, "docs", "Base de Datos Zebra ZC300.xlsx")); var sheet = workbook.Load("Hoja1");
            var holders = HolderValidation.Load(sheet, Fields.Suggest(sheet.Headers), new DateOnly(2026, 10, 23));
            foreach (var holder in holders)
            {
                var resolved = PhotoResolver.Resolve(holder["referenciaFoto"], holder.Matricula, System.IO.Path.Combine(directory, "Fotos prueba"), true);
                Assert.NotNull(resolved.Path); Assert.Null(resolved.Error);
                var photo = Task.Run(() => PhotoLoader.Load(resolved.Path!)).GetAwaiter().GetResult(); Assert.Equal(156, photo.Bitmap.PixelWidth); Assert.Equal(191, photo.Bitmap.PixelHeight);
                Assert.Empty(new CardComposer(CardProfile.Default(), directory).Compose(holder.Values, photo, new(), false).Errors);
            }
        }
        finally { System.IO.Directory.Delete(directory, true); }
    });
    [Fact] public void OperatorTableKeepsSelectionWhenFilteringAndSortingAndCropDoesNotChangePhoto() => Sta(() =>
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Carnets.UiTests", Guid.NewGuid().ToString("N"));
        var window = new MainWindow(directory);
        try
        {
            window.Show(); window.LoadDemoSession(); window.UpdateLayout();
            var selected = window.SessionRows[0]; Assert.All(window.SessionRows, r => Assert.False(r.Selected));
            var grid = (System.Windows.Controls.DataGrid)window.FindName("Records");
            var container = (System.Windows.Controls.DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(selected);
            System.Windows.Controls.CheckBox? FindCheck(System.Windows.DependencyObject parent)
            {
                if (parent is System.Windows.Controls.CheckBox check) return check;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) if (FindCheck(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
                return null;
            }
            var checkbox = FindCheck(container); Assert.NotNull(checkbox); checkbox.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
            Assert.True(selected.Selected);
            var search = (System.Windows.Controls.TextBox)window.FindName("SearchBox"); search.Text = "ANA";
            Assert.Single(grid.Items.Cast<object>()); Assert.True(selected.Selected);
            Assert.Contains("1 seleccionados (1 ocultos)", ((System.Windows.Controls.TextBlock)window.FindName("SelectionLabel")).Text);
            grid.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(HolderRow.Matricula), System.ComponentModel.ListSortDirection.Descending));
            search.Clear(); grid.SelectedItem = selected; window.UpdateLayout();
            string hash = PhotoResolver.Fingerprint(selected.PhotoPath!);
            ((System.Windows.Controls.Slider)window.FindName("CropZoom")).Value = 2;
            Assert.Equal(2, selected.Crop.Zoom); Assert.Equal(hash, PhotoResolver.Fingerprint(selected.PhotoPath!));
            ((System.Windows.Controls.Slider)window.FindName("CropZoom")).Value = 1;
        }
        finally { window.Close(); if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true); }
    });
    [Fact] public void RenderSyntheticCardsForVisualVerification() => Sta(() =>
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string output = System.IO.Path.Combine(root, "artifacts", "verification"); System.IO.Directory.CreateDirectory(output);
        var composer = new CardComposer(CardProfile.Default(), System.IO.Path.GetTempPath());
        foreach (bool back in new[] { false, true }) MainWindow.SaveDrawing(composer.Visual(composer.Compose(Values(), SyntheticPhoto(), new(), back).Drawing), System.IO.Path.Combine(output, back ? "synthetic-back.png" : "synthetic-front.png"));
        var window = new MainWindow(System.IO.Path.Combine(output, "isolated-settings"));
        window.Show(); window.Measure(new Size(1440, 900)); window.Arrange(new Rect(0, 0, 1440, 900)); window.UpdateLayout();
        Assert.Contains("Carnets", window.Title); Assert.True(window.IsLoaded);
        var grid = (System.Windows.Controls.DataGrid)window.FindName("Records");
        var source = (System.Collections.ObjectModel.ObservableCollection<HolderRow>)grid.ItemsSource;
        var holder = new Holder { Row = 2, Sheet = "Prueba sintética", Values = Values() };
        var row = new HolderRow(holder) { Photo = SyntheticPhoto(), PhotoPath = "synthetic.png" };
        source.Add(row); grid.SelectedItem = row;
        window.UpdateLayout();
        Assert.Single(grid.Items.Cast<object>()); Assert.False(row.Selected); Assert.Equal(85.6 * 96 / 25.4, ((CardVisual)((System.Windows.Controls.Viewbox)window.FindName("Preview")).Child).DesiredSize.Width, 7);
        var bitmap = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = System.IO.File.Create(System.IO.Path.Combine(output, "window.png"))) png.Save(stream);
        window.Close();
    });
}
