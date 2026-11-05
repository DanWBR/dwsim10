// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The data sheet import wizard in the cross-platform interface (see <see cref="DataSheetImportSession"/>).</summary>
    public sealed class DataSheetImportWindow : PlusWindow
    {
        static readonly string[] Types = { "pump", "compressor", "expander", "heat_exchanger", "vessel", "control_valve", "air_cooler", "column", "relief_valve", "fired_heater", "other" };

        readonly DataSheetImportSession session;
        readonly WizardFrame frame;
        bool loading;
        CancellationTokenSource reading;

        // data sheet page
        TextBox file;
        Button read, cancelRead, saveReading;
        TextBlock readStatus;
        WorkIndicator work;
        Image preview;

        // values page
        ComboBox type;
        TextBox tag;
        Grid fieldsGrid;

        // equipment page
        ComboBox target, @case;
        Button apply;
        StackPanel changes, checks;
        TextBlock summary;

        public DataSheetImportWindow(IFlowsheet fs)
        {
            session = new DataSheetImportSession(fs);
            Title = "Set Equipment up from a Data Sheet";
            Width = 1180;
            Height = 780;
            frame = new WizardFrame(() => Close(), DataSheetImportSession.Steps, DataSheetImportSession.StepTexts,
                new Control[] { SheetPage(), ValuesPage(), EquipmentPage() },
                "The equipment changes only when the changes are made on the last step.")
            {
                Ready = () => session.Reading != null && reading == null,
                // the edits on the values page are taken when it is left, forwards or back
                Leaving = (from, to) => { if (from == 1) TakeValues(); },
                Entered = p => { if (p == 1) FillValues(); if (p == 2) FillEquipment(); },
            };
            Content = frame.Build();
            Closing += (_, _) => reading?.Cancel();
            frame.Show(0);
            frame.NextAllowed = false;
            work.ShowModel();
        }

        static TextBlock Label(string text) => new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        IStorageProvider Storage => TopLevel.GetTopLevel(this)?.StorageProvider;

        #region data sheet

        Control SheetPage()
        {
            file = new TextBox();
            file.TextChanged += (_, _) => read.IsEnabled = File.Exists((file.Text ?? "").Trim()) && reading == null;
            var browse = new Button { Content = "Browse...", Margin = new Thickness(6, 0, 0, 0) };
            browse.Click += async (_, _) => await Browse();
            read = new Button { Content = "Read the data sheet", IsEnabled = false };
            read.Click += async (_, _) => await Read();
            cancelRead = new Button { Content = "Cancel", IsEnabled = false };
            cancelRead.Click += (_, _) => CancelRead();
            var open = new Button { Content = "Open a saved reading..." };
            open.Click += async (_, _) => await OpenReading();
            saveReading = new Button { Content = "Save the reading...", IsEnabled = false };
            saveReading.Click += async (_, _) => await SaveReading();
            readStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
            work = new WorkIndicator(readStatus);
            preview = new Image { Stretch = Stretch.Uniform };

            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), RowSpacing = 6 };
            void Put(Control c, int row, int col, int span = 1) { Grid.SetRow(c, row); Grid.SetColumn(c, col); Grid.SetColumnSpan(c, span); g.Children.Add(c); }
            Put(Label("Data sheet"), 0, 0); Put(file, 0, 1); Put(browse, 0, 2);
            Put(new StackPanel { Spacing = 8, Children = { VisionNote(), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { read, cancelRead, open, saveReading, work.Bar } } } }, 1, 0, 3);
            Put(readStatus, 2, 0, 3);
            Put(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Background = Brushes.White, Child = preview }, 3, 0, 3);
            return g;
        }

        /// <summary>The note that the reading needs a vision model, with a button to the AI Assistant's settings.</summary>
        Control VisionNote()
        {
            var open = new Button { Content = "Open the AI Assistant settings", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var text = new TextBlock { Text = DrawingImportClient.VisionModelNote, TextWrapping = TextWrapping.Wrap, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center };
            open.Click += (_, _) =>
            {
                var why = DrawingImportClient.OpenAssistantSettings(session.Flowsheet, false);
                if (why != null) text.Text = why;
            };
            var d = new DockPanel();
            DockPanel.SetDock(open, Dock.Right);
            d.Children.Add(open);
            d.Children.Add(text);
            return new StackPanel { Spacing = 4, Children = { d, work.ModelText } };
        }

        async Task Browse()
        {
            if (Storage == null) return;
            var picked = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "The equipment data sheet",
                FileTypeFilter = new[] { new FilePickerFileType("Data sheets (*.pdf, *.png, *.jpg, *.dxf, *.dwg)") { Patterns = DrawingImportClient.Extensions.Select(x => "*" + x).ToArray() }, FilePickerFileTypes.All }
            });
            var path = picked.FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return;
            file.Text = path;
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                preview.Source = ext == ".pdf" || ext == ".dxf" || ext == ".dwg" ? null : new Bitmap(path);
            }
            catch (Exception) { preview.Source = null; }
        }

        async Task Read()
        {
            session.File = (file.Text ?? "").Trim();
            reading = new CancellationTokenSource();
            var token = reading.Token;
            SetBusy(true);
            work.Start("Checking the AI Assistant's model");
            try
            {
                var model = await work.Model(token, "a data sheet");
                if (model == null) { read.Content = "Read with the local model"; return; }
                read.Content = "Read the data sheet";
                await Task.Run(() => session.Read(token, s => Dispatcher.UIThread.Post(() => { if (!token.IsCancellationRequested) work.Step(s); })));
                session.Loaded();
                work.Stop();
                readStatus.Text = string.Format(CultureInfo.CurrentCulture, "A {0} data sheet ({1}), {2} values read by {4} in {3}. Check them on the next page.",
                    session.Reading.EquipmentType.Replace("_", " "), session.Reading.Tag, session.Reading.Fields.Count, DrawingImportClient.Elapsed(work.Elapsed), model);
                Loaded();
            }
            catch (Exception ex) when (token.IsCancellationRequested || ex is OperationCanceledException)
            {
                work.Stop();
                readStatus.Text = "Reading cancelled.";
            }
            catch (Exception ex)
            {
                work.Stop();
                readStatus.Text = "The data sheet could not be read: " + ex.GetBaseException().Message + " (the details are in the flowsheet's log)";
                session.Flowsheet.ShowMessage("Set Equipment up from Data Sheet: " + ex, IFlowsheet.MessageType.GeneralError);
            }
            finally
            {
                reading = null;
                SetBusy(false);
            }
        }

        void Loaded()
        {
            if (session.Reading.Overview != null)
                try { preview.Source = new Bitmap(new MemoryStream(session.Reading.Overview)); } catch (Exception) { }
            SetBusy(false);
        }

        void CancelRead()
        {
            if (reading == null) return;
            reading.Cancel();
            cancelRead.IsEnabled = false;
            work.Step("Cancelling");
        }

        void SetBusy(bool busy)
        {
            read.IsEnabled = !busy && File.Exists((file.Text ?? "").Trim());
            cancelRead.IsEnabled = busy;
            saveReading.IsEnabled = !busy && session.Reading != null;
            frame.NextAllowed = !busy && session.Reading != null;
        }

        async Task OpenReading()
        {
            if (Storage == null) return;
            var picked = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "A saved data sheet reading",
                FileTypeFilter = new[] { new FilePickerFileType("Data sheet readings (*.json)") { Patterns = new[] { "*.json" } } }
            });
            var path = picked.FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return;
            try
            {
                session.Open(File.ReadAllText(path));
                file.Text = session.File;
                readStatus.Text = session.Reading.Fields.Count + " values in the saved reading.";
                Loaded();
            }
            catch (Exception ex) { readStatus.Text = "The reading could not be opened: " + ex.Message; }
        }

        async Task SaveReading()
        {
            if (Storage == null || session.Reading == null) return;
            if (frame.Page == 1) TakeValues();
            var picked = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the data sheet reading",
                SuggestedFileName = Path.GetFileNameWithoutExtension(session.File) + " - reading.json",
                FileTypeChoices = new[] { new FilePickerFileType("Data sheet readings (*.json)") { Patterns = new[] { "*.json" } } }
            });
            var path = picked?.TryGetLocalPath();
            if (path != null) session.SaveReading(path);
        }

        #endregion

        #region values

        static string N(double? v) => v.HasValue ? v.Value.ToString("0.#####", CultureInfo.CurrentCulture) : "";

        static double? Parse(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var x)) return x;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ? x : (double?)null;
        }

        Control ValuesPage()
        {
            type = new ComboBox { ItemsSource = Types, MinWidth = 200 };
            type.SelectionChanged += (_, _) => { if (!loading && session.Reading != null) session.Reading.EquipmentType = type.SelectedItem as string ?? session.Reading.EquipmentType; };
            tag = new TextBox { MinWidth = 200 };
            tag.LostFocus += (_, _) => { if (session.Reading != null) session.Reading.Tag = (tag.Text ?? "").Trim(); };
            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Label("Equipment"), type, Label("Tag"), tag } };
            fieldsGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,2.2*,1.6*,0.9*,0.8*,1.6*,0.8*,0.6*,1.2*"), RowSpacing = 2, ColumnSpacing = 4 };
            var d = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            d.Children.Add(top);
            d.Children.Add(new ScrollViewer { Content = fieldsGrid, Margin = new Thickness(0, 8, 0, 0) });
            return d;
        }

        void FillValues()
        {
            var r = session.Reading;
            loading = true;
            type.ItemsSource = Types.Contains(r.EquipmentType) ? Types : Types.Concat(new[] { r.EquipmentType }).ToArray();
            type.SelectedItem = r.EquipmentType;
            tag.Text = r.Tag;
            loading = false;
            fieldsGrid.Children.Clear();
            fieldsGrid.RowDefinitions.Clear();
            fieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var heads = new[] { "Use", "Label on the sheet", "Value as written", "Number", "Unit", "Stands for", "Case", "Side", "Stream" };
            for (var c = 0; c < heads.Length; c++) fieldsGrid.Children.Add(new TextBlock { Text = heads[c], FontWeight = FontWeight.SemiBold, [Grid.ColumnProperty] = c });
            for (var i = 0; i < r.Fields.Count; i++)
            {
                var f = r.Fields[i];
                var row = i + 1;
                fieldsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var use = new CheckBox { IsChecked = f.Include };
                use.IsCheckedChanged += (_, _) => f.Include = use.IsChecked == true;
                TextBox Box(string text, Action<string> set)
                {
                    var t = new TextBox { Text = text };
                    t.LostFocus += (_, _) => set(t.Text ?? "");
                    return t;
                }
                var keys = DataSheetImportSession.Keys.Contains(f.Key) ? DataSheetImportSession.Keys : DataSheetImportSession.Keys.Concat(new[] { f.Key }).ToArray();
                var key = new ComboBox { ItemsSource = keys, SelectedItem = f.Key, HorizontalAlignment = HorizontalAlignment.Stretch };
                key.SelectionChanged += (_, _) => f.Key = key.SelectedItem as string ?? "";
                var cells = new Control[]
                {
                    use, Box(f.Label, v => f.Label = v), Box(f.Value, v => f.Value = v), Box(N(f.Number), v => f.Number = Parse(v)), Box(f.Unit, v => f.Unit = v),
                    key, Box(f.Case, v => f.Case = v), Box(f.Side, v => f.Side = v.ToLowerInvariant()), Box(f.Stream, v => f.Stream = v),
                };
                for (var c = 0; c < cells.Length; c++) { Grid.SetRow(cells[c], row); Grid.SetColumn(cells[c], c); fieldsGrid.Children.Add(cells[c]); }
            }
        }

        /// <summary>The sheet's type and tag as the user left them (the values take their edits as they go), and its cases.</summary>
        void TakeValues()
        {
            var r = session.Reading;
            if (r == null || type == null) return;
            r.EquipmentType = type.SelectedItem as string ?? r.EquipmentType;
            r.Tag = (tag.Text ?? "").Trim();
            r.Cases = r.Fields.Where(f => f.Key.Length > 0 && !f.Key.StartsWith("design")).Select(f => f.Case).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (session.Case.Length > 0 && !r.Cases.Contains(session.Case)) session.Case = DataSheetImport.DefaultCase(r);
        }

        #endregion

        #region equipment

        Control EquipmentPage()
        {
            target = new ComboBox { MinWidth = 240 };
            @case = new ComboBox { MinWidth = 160 };
            target.SelectionChanged += (_, _) => Chosen();
            @case.SelectionChanged += (_, _) => Chosen();
            apply = new Button { Content = "Make the ticked changes" };
            apply.Click += async (_, _) => await Apply();
            changes = new StackPanel { Spacing = 2 };
            checks = new StackPanel { Spacing = 2 };
            summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Label("Put the sheet in"), target, Label("Operating case"), @case, apply } };
            var g = new Grid { RowDefinitions = new RowDefinitions("Auto,1.2*,Auto,*"), RowSpacing = 6 };
            Grid.SetRow(summary, 2);
            g.Children.Add(top);
            g.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = new ScrollViewer { Content = changes }, [Grid.RowProperty] = 1 });
            g.Children.Add(summary);
            g.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = new ScrollViewer { Content = checks }, [Grid.RowProperty] = 3 });
            return g;
        }

        void FillEquipment()
        {
            loading = true;
            var targets = session.Targets();
            target.ItemsSource = targets;
            var current = session.Target?.GraphicObject.Tag ?? DataSheetImportSession.NewObject;
            target.SelectedItem = targets.Contains(current) ? current : targets.FirstOrDefault();
            var cases = new[] { "" }.Concat(session.Reading.Cases).ToList();
            @case.ItemsSource = cases;
            @case.SelectedItem = cases.Contains(session.Case) ? session.Case : "";
            loading = false;
            Propose();
        }

        void Chosen()
        {
            if (loading) return;
            session.Choose(target.SelectedItem as string ?? DataSheetImportSession.NewObject);
            session.Case = @case.SelectedItem as string ?? "";
            Propose();
        }

        void Propose()
        {
            session.Propose();
            changes.Children.Clear();
            foreach (var c in session.Changes)
            {
                var box = new CheckBox { IsChecked = c.Use, Content = c.What + ":  " + (c.Current.Length > 0 ? c.Current : "-") + "  >  " + c.New };
                box.IsCheckedChanged += (_, _) => c.Use = box.IsChecked == true;
                changes.Children.Add(box);
            }
            Show(session.Issues);
            var supported = DataSheetImport.Supported(session.Reading.EquipmentType);
            apply.IsEnabled = supported && (session.Target == null || session.Changes.Count > 0);
            apply.Content = session.Target == null ? "Make the new equipment" : "Make the ticked changes";
            summary.Text = !supported ? "This kind of equipment is not set up from a data sheet yet." :
                session.Target == null ? "A new " + session.Reading.EquipmentType.Replace("_", " ") + " with its inlet and outlet streams, set up from the sheet." :
                session.Changes.Count(c => c.Use) + " of " + session.Changes.Count + " changes ticked for " + session.Target.GraphicObject.Tag + ".";
        }

        void Show(IEnumerable<ImportIssue> list)
        {
            checks.Children.Clear();
            foreach (var i in list)
            {
                var t = new TextBlock
                {
                    Text = (i.Level == ImportIssue.Levels.Error ? "Error" : i.Level == ImportIssue.Levels.Warning ? "Warning" : "Note") + (i.Subject.Length > 0 ? " (" + i.Subject + ")" : "") + ": " + i.Text,
                    TextWrapping = TextWrapping.Wrap,
                };
                if (i.Level == ImportIssue.Levels.Error) t.Foreground = Brushes.IndianRed;
                else if (i.Level == ImportIssue.Levels.Warning) t.Foreground = Brushes.DarkGoldenrod;
                checks.Children.Add(t);
            }
            if (checks.Children.Count == 0) checks.Children.Add(new TextBlock { Text = "Nothing to report.", Opacity = 0.7 });
        }

        async Task Apply()
        {
            apply.IsEnabled = false;
            summary.Text = "Making the changes and solving the flowsheet...";
            await Task.Delay(50);
            try
            {
                session.Apply();
                summary.Text = session.Results.FirstOrDefault()?.Text + (session.Results.Any(i => i.Level == ImportIssue.Levels.Warning) ? "; the outlet differs from the sheet where marked below." : ".");
                Show(session.Results.Concat(session.Issues.Where(i => i.Level != ImportIssue.Levels.Info)));
                loading = true;
                var targets = session.Targets();
                target.ItemsSource = targets;
                target.SelectedItem = session.Target.GraphicObject.Tag;
                loading = false;
            }
            catch (Exception ex) { summary.Text = "The changes could not be made: " + ex.GetBaseException().Message; }
            finally
            {
                apply.IsEnabled = true;
                apply.Content = "Make the ticked changes";
            }
        }

        #endregion
    }

    /// <summary>Opens the data sheet import in the cross-platform interface.</summary>
    internal static class DataSheetImportPrompt
    {
        internal static async void Show(IFlowsheet fs)
        {
            try
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var owner = desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? desktop?.MainWindow;
                await new DataSheetImportWindow(fs).ShowDialog(owner);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Data sheet import: " + ex);
            }
        }
    }
}
