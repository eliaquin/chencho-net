using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Carnets.Core;
using Carnets.Desktop;

namespace Carnets.Tests;

public sealed class OperatorFlowTests
{
    private static void Ui(Func<Task> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                Task task = action();
                if (!task.IsCompleted) { _ = task.ContinueWith(_ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background), TaskScheduler.Default); Dispatcher.Run(); }
                task.GetAwaiter().GetResult();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "El flujo WPF no terminó en 30 segundos.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is not DependencyObject element) continue;
            if (element is T typed) yield return typed;
            foreach (var nested in Descendants<T>(element)) yield return nested;
        }
    }
    [Fact] public void ReloadKeepsAssociationsClearsSelectionAndBadFilePreservesPriorSession() => Ui(async () =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "Carnets.ReloadTests", Guid.NewGuid().ToString("N"));
        var window = new MainWindow(directory); var errors = new List<Exception>(); window.ErrorHandler = errors.Add;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            foreach (Window dialog in window.OwnedWindows.Cast<Window>().ToArray())
                if (dialog.Title == "Elegir hoja del Excel") Descendants<Button>(dialog).Single(b => b.Content as string == "Confirmar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
        try
        {
            window.Show(); timer.Start();
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            string book = Path.Combine(root, "docs", "Base de Datos Zebra ZC300.xlsx");
            await window.LoadFileAsync(book, false); Assert.Empty(errors); Assert.Equal(2, window.SessionRows.Count);
            window.SessionRows[0].Selected = true;
            ((TextBox)window.FindName("SearchBox")).Text = "ningún titular coincide";
            await window.LoadFileAsync(book, false);
            Assert.All(window.SessionRows, r => Assert.False(r.Selected)); Assert.Equal(2, ((DataGrid)window.FindName("Records")).Items.Count);
            Assert.Equal(11, SettingsStore.Load(Path.Combine(directory, "settings.json")).ColumnHeaders.Count);
            var prior = window.SessionRows[0]; prior.Selected = true;
            string bad = Path.Combine(directory, "bad.xlsx"); File.WriteAllText(bad, "not a zip");
            await window.LoadFileAsync(bad, false);
            Assert.Single(errors); Assert.Same(prior, window.SessionRows[0]); Assert.True(prior.Selected);
        }
        finally { timer.Stop(); window.Close(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    });
    [Fact] public void AmbiguousColumnsRequireExplicitChoiceInTheRealMappingDialog() => Ui(() =>
    {
        var owner = new Window(); owner.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            foreach (Window dialog in owner.OwnedWindows.Cast<Window>().ToArray())
            {
                if (dialog.Title != "Asociar columnas del Excel") continue;
                var combos = Descendants<ComboBox>(dialog).ToArray(); Assert.Equal(11, combos.Length); Assert.Equal(-1, combos[3].SelectedIndex);
                combos[3].SelectedIndex = 11; // Explicitly choose the second equivalent matrícula column.
                Descendants<Button>(dialog).Single(b => b.Content as string == "Confirmar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        };
        try
        {
            timer.Start(); var headers = Fields.Headers.Concat(new[] { "MATRICULA" }).ToArray();
            var mapping = Dialogs.Mapping(owner, new("Hoja1", headers, [], false), new(), false);
            Assert.NotNull(mapping); Assert.Equal(11, mapping["matricula"]);
        }
        finally { timer.Stop(); owner.Close(); }
        return Task.CompletedTask;
    });
    [Fact] public void StoredHeaderAssociationsFollowReorderedColumnsWithoutUsingOldIndexes() => Ui(() =>
    {
        var settings = new OperatorSettings { ColumnHeaders = Fields.Keys.Zip(Fields.Headers).ToDictionary(p => p.First, p => p.Second) };
        string[] reordered = Fields.Headers.Reverse().ToArray(); var owner = new Window();
        var mapping = Dialogs.Mapping(owner, new("Reordenada", reordered, [], false), settings, false);
        Assert.NotNull(mapping); Assert.Equal(11, mapping.Count);
        for (int i = 0; i < Fields.Keys.Length; i++) Assert.Equal(10 - i, mapping[Fields.Keys[i]]);
        return Task.CompletedTask;
    });
    [Fact] public void CancellingPrintConfirmationCreatesNoSimulatedBatch() => Ui(async () =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "Carnets.CancelTests", Guid.NewGuid().ToString("N"));
        var window = new MainWindow(directory); var errors = new List<Exception>(); window.ErrorHandler = errors.Add;
        bool confirmedListWasComplete = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            foreach (Window dialog in window.OwnedWindows.Cast<Window>().ToArray())
            {
                if (dialog.Title != "Confirmar envío de carnets") continue;
                Assert.Equal(2, Descendants<ListBox>(dialog).Single().Items.Count);
                confirmedListWasComplete = true; dialog.DialogResult = false;
            }
        };
        try
        {
            window.Show(); window.LoadDemoSession(); foreach (var row in window.SessionRows) row.Selected = true;
            timer.Start(); ((Button)window.FindName("ReviewButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (int i = 0; i < 200 && !((Button)window.FindName("ReviewButton")).IsEnabled; i++) await Task.Delay(20);
            Assert.True(confirmedListWasComplete); Assert.Empty(errors);
            Assert.Contains("cancelado antes de enviar", ((TextBlock)window.FindName("StatusText")).Text);
            Assert.Empty(((TextBox)window.FindName("RunSummary")).Text);
        }
        finally { timer.Stop(); window.Close(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    });
}
