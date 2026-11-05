//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

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
    /// <summary>The drawing import wizard in the cross-platform interface (see <see cref="DrawingImportSession"/>).</summary>
    public sealed class DrawingImportWindow : PlusWindow
    {
        const string BatteryLimit = "(battery limit)";

        readonly DrawingImportSession session;
        readonly ListBox steps = new ListBox();
        readonly TextBlock stepTitle = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(15) };
        readonly TextBlock stepText = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85, Margin = new Thickness(0, 2, 0, 8) };
        readonly ContentControl host = new ContentControl();
        readonly Button back = new Button { Content = "< Back", MinWidth = 90 };
        readonly Button next = new Button { Content = "Next >", MinWidth = 90 };
        readonly Button close = new Button { Content = "Close", MinWidth = 90 };
        Control[] pages;
        int page;
        bool loading;
        CancellationTokenSource reading;
        Bitmap overview;

        // drawing page
        TextBox file;
        ComboBox kind;
        Button read, cancelRead, saveReading;
        TextBlock readStatus;
        WorkIndicator work;
        Image preview;

        // equipment and lines pages
        Grid itemsGrid, linesGrid;
        SheetView itemsSheet, linesSheet;

        // process page
        TextBlock description;
        ComboBox processClass, package;
        TextBox unitT, unitP, unitW, unitE, search;
        ListBox compounds, found;

        // build page
        StackPanel issues, results;
        CheckBox specs, useUnits, solve;
        Button build, fit;
        TextBlock summary;
        StackPanel fitRow, fitChoices;

        public DrawingImportWindow(IFlowsheet fs)
        {
            session = new DrawingImportSession(fs);
            Title = "Import a Flowsheet from a Drawing";
            Width = 1180;
            Height = 780;
            Content = Build();
            ShowPage(0);
        }

        static TextBlock Label(string text) => new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        Control Build()
        {
            steps.ItemsSource = DrawingImportSession.Steps.Select((s, i) => (i + 1) + ". " + s).ToList();
            steps.Width = 170;
            steps.SelectionChanged += (_, _) => { if (!loading && steps.SelectedIndex >= 0 && steps.SelectedIndex != page) ShowPage(steps.SelectedIndex); };
            back.Click += (_, _) => ShowPage(Math.Max(0, page - 1));
            next.Click += (_, _) => ShowPage(Math.Min(4, page + 1));
            close.Click += (_, _) => { reading?.Cancel(); Close(); };
            pages = new[] { DrawingPage(), ItemsPage(), LinesPage(), ProcessPage(), BuildPage() };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { close, back, next } };
            var right = new DockPanel { Margin = new Thickness(12, 0, 0, 0) };
            var header = new StackPanel { Children = { stepTitle, stepText } };
            DockPanel.SetDock(header, Dock.Top);
            right.Children.Add(header);
            right.Children.Add(host);
            var root = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(buttons, Dock.Bottom);
            DockPanel.SetDock(steps, Dock.Left);
            root.Children.Add(buttons);
            root.Children.Add(steps);
            root.Children.Add(right);
            return root;
        }

        void ShowPage(int p)
        {
            if (session.Reading == null) p = 0;
            if (page == 3 && p != 3) TakeProcessPage();
            page = p;
            host.Content = pages[p];
            stepTitle.Text = DrawingImportSession.Steps[p];
            stepText.Text = DrawingImportSession.StepTexts[p];
            loading = true;
            steps.SelectedIndex = p;
            loading = false;
            back.IsEnabled = p > 0;
            next.IsVisible = p < 4;
            next.IsEnabled = session.Reading != null && reading == null;
            switch (p)
            {
                case 1: FillItems(); break;
                case 2: FillLines(); break;
                case 3: FillProcess(); break;
                case 4: FillChecks(); break;
            }
        }

        IStorageProvider Storage => TopLevel.GetTopLevel(this)?.StorageProvider;

        #region drawing

        Control DrawingPage()
        {
            file = new TextBox();
            file.TextChanged += (_, _) => read.IsEnabled = File.Exists((file.Text ?? "").Trim()) && reading == null;
            var browse = new Button { Content = "Browse...", Margin = new Thickness(6, 0, 0, 0) };
            browse.Click += async (_, _) => await BrowseDrawing();
            kind = new ComboBox { ItemsSource = new[] { "Detect", "Process flow diagram (PFD)", "Piping and instrumentation diagram (P&ID)" }, SelectedIndex = 0, MinWidth = 320 };
            read = new Button { Content = "Read the drawing", IsEnabled = false };
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

            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), RowSpacing = 6 };
            void Put(Control c, int row, int col, int span = 1) { Grid.SetRow(c, row); Grid.SetColumn(c, col); Grid.SetColumnSpan(c, span); g.Children.Add(c); }
            Put(Label("Drawing"), 0, 0); Put(file, 0, 1); Put(browse, 0, 2);
            Put(Label("Type"), 1, 0); Put(kind, 1, 1);
            Put(new StackPanel { Spacing = 8, Children = { VisionNote(), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { read, cancelRead, open, saveReading, work.Bar } } } }, 2, 0, 3);
            Put(readStatus, 3, 0, 3);
            Put(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Background = Brushes.White, Child = preview }, 4, 0, 3);
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
            return d;
        }

        async Task BrowseDrawing()
        {
            if (Storage == null) return;
            var picked = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "The PFD or P&ID",
                FileTypeFilter = new[] { new FilePickerFileType(DrawingImportSession.FileFilterText) { Patterns = DrawingImportClient.Extensions.Select(x => "*" + x).ToArray() }, FilePickerFileTypes.All }
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
            session.Kind = kind.SelectedIndex == 1 ? "pfd" : kind.SelectedIndex == 2 ? "pid" : "auto";
            reading = new CancellationTokenSource();
            var token = reading.Token;
            SetBusy(true);
            work.Start("Checking the AI Assistant's model");
            try
            {
                var model = await work.Model(token, "a drawing");
                if (model == null) { read.Content = "Read with the local model"; return; }
                read.Content = "Read the drawing";
                await Task.Run(() => session.Read(token, s => Dispatcher.UIThread.Post(() => { if (!token.IsCancellationRequested) work.Step(s); })));
                work.Step("Matching the compounds and the property package");
                session.Propose();
                work.Stop();
                readStatus.Text = string.Format(CultureInfo.CurrentCulture, "{0} items and {1} lines read by {3} in {2}. Check them on the next pages.",
                    session.Reading.Items.Count, session.Reading.Lines.Count, DrawingImportClient.Elapsed(work.Elapsed), model);
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
                readStatus.Text = "The drawing could not be read: " + ex.GetBaseException().Message + " (the details are in the flowsheet's log)";
                session.Flowsheet.ShowMessage("Import Flowsheet from Drawing: " + ex, IFlowsheet.MessageType.GeneralError);
            }
            finally
            {
                reading = null;
                SetBusy(false);
            }
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
            next.IsEnabled = !busy && session.Reading != null;
        }

        async Task OpenReading()
        {
            if (Storage == null) return;
            var picked = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "A saved drawing reading",
                FileTypeFilter = new[] { new FilePickerFileType("Drawing readings") { Patterns = new[] { "*.json" } } }
            });
            var path = picked.FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return;
            try
            {
                session.Open(File.ReadAllText(path));
                file.Text = session.File;
                readStatus.Text = session.Reading.Items.Count + " items and " + session.Reading.Lines.Count + " lines in the saved reading.";
                Loaded();
            }
            catch (Exception ex) { readStatus.Text = "The reading could not be opened: " + ex.Message; }
        }

        async Task SaveReading()
        {
            if (session.Reading == null || Storage == null) return;
            TakeProcessPage();
            var picked = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the reading",
                SuggestedFileName = Path.GetFileNameWithoutExtension(session.File) + " - reading.json",
                DefaultExtension = "json",
                FileTypeChoices = new[] { new FilePickerFileType("Drawing readings") { Patterns = new[] { "*.json" } } }
            });
            var path = picked?.TryGetLocalPath();
            if (path != null) session.SaveReading(path);
        }

        void Loaded()
        {
            overview = null;
            if (session.Reading.Overview != null)
                try { overview = new Bitmap(new MemoryStream(session.Reading.Overview)); } catch (Exception) { }
            if (overview != null) preview.Source = overview;
            itemsSheet.Image = linesSheet.Image = overview;
            SetBusy(false);
        }

        #endregion

        #region equipment

        Control ItemsPage()
        {
            itemsGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,2*,1.4*,1.4*"), ColumnSpacing = 6, RowSpacing = 3 };
            itemsSheet = new SheetView(() => session.Reading, false);
            return Split(itemsGrid, itemsSheet, "1*,1.2*");
        }

        static Grid Split(Grid table, Control sheet, string columns)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 10 };
            var scroll = new ScrollViewer { Content = table, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
            var frame = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = sheet };
            Grid.SetColumn(frame, 1);
            g.Children.Add(scroll);
            g.Children.Add(frame);
            return g;
        }

        static void Header(Grid g, params string[] titles)
        {
            g.Children.Clear();
            g.RowDefinitions.Clear();
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var i = 0; i < titles.Length; i++)
            {
                var t = new TextBlock { Text = titles[i], FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(t, i);
                g.Children.Add(t);
            }
        }

        static void Cell(Grid g, Control c, int row, int col)
        {
            Grid.SetRow(c, row);
            Grid.SetColumn(c, col);
            c.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(c);
        }

        void FillItems()
        {
            Header(itemsGrid, "Build", "Tag", "Drawn as", "DWSIM object");
            var row = 0;
            foreach (var item in session.Reading.Items)
            {
                row++;
                itemsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var it = item;
                var include = new CheckBox { IsChecked = it.Include };
                include.IsCheckedChanged += (_, _) => { it.Include = include.IsChecked == true; itemsSheet.InvalidateVisual(); };
                var tag = new TextBox { Text = it.Tag };
                tag.LostFocus += (_, _) => Rename(it, (tag.Text ?? "").Trim());
                tag.GotFocus += (_, _) => { itemsSheet.Selected = it; itemsSheet.InvalidateVisual(); };
                var cats = DrawingImport.Categories.ToList();
                if (!cats.Contains(it.Category)) cats.Add(it.Category);
                var category = new ComboBox { ItemsSource = cats, SelectedItem = it.Category, HorizontalAlignment = HorizontalAlignment.Stretch };
                var type = new ComboBox { ItemsSource = Types(it), SelectedItem = it.Type, HorizontalAlignment = HorizontalAlignment.Stretch };
                category.SelectionChanged += (_, _) =>
                {
                    if (category.SelectedItem == null || (string)category.SelectedItem == it.Category) return;
                    it.Category = (string)category.SelectedItem;
                    it.Type = DrawingImport.DefaultType(it.Category);
                    type.ItemsSource = Types(it);
                    type.SelectedItem = it.Type;
                    itemsSheet.InvalidateVisual();
                };
                type.SelectionChanged += (_, _) => { if (type.SelectedItem != null) it.Type = (string)type.SelectedItem; };
                Cell(itemsGrid, include, row, 0); Cell(itemsGrid, tag, row, 1); Cell(itemsGrid, category, row, 2); Cell(itemsGrid, type, row, 3);
            }
            itemsSheet.InvalidateVisual();
        }

        static List<string> Types(ReadItem item)
        {
            var types = DrawingImport.TypesFor(item.Category).ToList();
            if (!types.Contains(item.Type)) types.Insert(0, item.Type);
            return types;
        }

        void Rename(ReadItem item, string tag)
        {
            if (tag.Length == 0 || tag == item.Tag) return;
            // the lines follow the item
            foreach (var l in session.Reading.Lines)
            {
                if (string.Equals(l.From, item.Tag, StringComparison.OrdinalIgnoreCase)) l.From = tag;
                if (string.Equals(l.To, item.Tag, StringComparison.OrdinalIgnoreCase)) l.To = tag;
            }
            item.Tag = tag;
            itemsSheet.InvalidateVisual();
        }

        #endregion

        #region lines

        Control LinesPage()
        {
            // fixed widths: the table sits in a scroller, where star columns would take the content's width
            linesGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,130,125,125,80,90,90,64"), ColumnSpacing = 4, RowSpacing = 3 };
            linesSheet = new SheetView(() => session.Reading, true);
            return Split(linesGrid, linesSheet, "2.6*,1*");
        }

        static string Num(double? v) => v.HasValue ? v.Value.ToString("G6", CultureInfo.CurrentCulture) : "";

        static double? Parse(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var d)) return d;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return null;
        }

        void FillLines()
        {
            var u = session.Reading.Units;
            Header(linesGrid, "Build", "Stream / line", "From", "To", "T (" + u.Temperature + ")", "P (" + u.Pressure + ")", "Mass flow (" + u.MassFlow + ")", "Vapor fraction");
            var ends = new List<string> { BatteryLimit };
            ends.AddRange(session.Reading.Items.Select(i => i.Tag));
            var row = 0;
            foreach (var line in session.Reading.Lines)
            {
                row++;
                linesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var l = line;
                void Select() { linesSheet.Selected = l; linesSheet.InvalidateVisual(); }
                var include = new CheckBox { IsChecked = l.Include };
                include.IsCheckedChanged += (_, _) => { l.Include = include.IsChecked == true; linesSheet.InvalidateVisual(); };
                var name = new TextBox { Text = session.Reading.StreamTag(l) };
                name.LostFocus += (_, _) => { var n = (name.Text ?? "").Trim(); if (n.Length > 0 && n != session.Reading.StreamTag(l)) { l.Name = n; l.LineNumber = ""; } };
                name.GotFocus += (_, _) => Select();
                var fromItems = ends.ToList(); if (!l.Feed && !fromItems.Contains(l.From)) fromItems.Add(l.From);
                var toItems = ends.ToList(); if (!l.Product && !toItems.Contains(l.To)) toItems.Add(l.To);
                var from = new ComboBox { ItemsSource = fromItems, SelectedItem = l.Feed ? BatteryLimit : l.From, HorizontalAlignment = HorizontalAlignment.Stretch };
                var to = new ComboBox { ItemsSource = toItems, SelectedItem = l.Product ? BatteryLimit : l.To, HorizontalAlignment = HorizontalAlignment.Stretch };
                from.SelectionChanged += (_, _) => { if (from.SelectedItem is string s) { l.From = s == BatteryLimit ? "" : s; Select(); } };
                to.SelectionChanged += (_, _) => { if (to.SelectedItem is string s) { l.To = s == BatteryLimit ? "" : s; Select(); } };
                TextBox Value(double? v, Action<double?> set)
                {
                    var box = new TextBox { Text = Num(v) };
                    box.LostFocus += (_, _) => set(Parse(box.Text));
                    box.GotFocus += (_, _) => Select();
                    return box;
                }
                Cell(linesGrid, include, row, 0); Cell(linesGrid, name, row, 1); Cell(linesGrid, from, row, 2); Cell(linesGrid, to, row, 3);
                Cell(linesGrid, Value(l.T, v => l.T = v), row, 4); Cell(linesGrid, Value(l.P, v => l.P = v), row, 5);
                Cell(linesGrid, Value(l.W, v => l.W = v), row, 6); Cell(linesGrid, Value(l.VaporFraction, v => l.VaporFraction = v), row, 7);
                if (l.Compounds.Count > 0)
                    ToolTip.SetTip(name, string.Join("\n", l.Compounds.Select(c => c.Key + ": " + c.Value.ToString("G4", CultureInfo.CurrentCulture))));
            }
            linesSheet.InvalidateVisual();
        }

        #endregion

        #region process

        Control ProcessPage()
        {
            description = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            processClass = new ComboBox { ItemsSource = new[] { "(not known)" }.Concat(DrawingImport.ProcessClasses.Select(c => c.Class + ": " + c.Description)).ToList(), HorizontalAlignment = HorizontalAlignment.Stretch };
            processClass.SelectionChanged += (_, _) =>
            {
                if (loading || processClass.SelectedIndex < 0) return;
                session.Reading.Process.Class = processClass.SelectedIndex == 0 ? "" : DrawingImport.ProcessClasses[processClass.SelectedIndex - 1].Class;
                var pp = session.Package(session.Reading.Process.Class);
                if (pp != null) { session.Setup.PropertyPackage = pp; package.SelectedItem = pp; }
            };
            package = new ComboBox { ItemsSource = session.AvailablePackages(), HorizontalAlignment = HorizontalAlignment.Stretch };
            package.SelectionChanged += (_, _) => { if (!loading && package.SelectedItem is string s) session.Setup.PropertyPackage = s; };
            unitT = new TextBox(); unitP = new TextBox(); unitW = new TextBox(); unitE = new TextBox();
            ToolTip.SetTip(unitT, string.Join(", ", DrawingImportSession.TemperatureUnits));
            ToolTip.SetTip(unitP, string.Join(", ", DrawingImportSession.PressureUnits));
            ToolTip.SetTip(unitW, string.Join(", ", DrawingImportSession.MassFlowUnits));
            ToolTip.SetTip(unitE, string.Join(", ", DrawingImportSession.EnergyUnits));
            var units = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto,*,Auto,*"), ColumnSpacing = 6 };
            var k = 0;
            foreach (var (text, box) in new[] { ("Temperature", unitT), ("Pressure", unitP), ("Mass flow", unitW), ("Energy", unitE) })
            {
                var l = Label(text);
                Grid.SetColumn(l, k++); Grid.SetColumn(box, k++);
                units.Children.Add(l); units.Children.Add(box);
            }

            compounds = new ListBox { SelectionMode = SelectionMode.Multiple };
            found = new ListBox { SelectionMode = SelectionMode.Multiple };
            found.DoubleTapped += (_, _) => AddCompounds();
            search = new TextBox { Watermark = "Search the compound database" };
            search.TextChanged += (_, _) => Search();
            var add = new Button { Content = "< Add", HorizontalAlignment = HorizontalAlignment.Stretch };
            add.Click += (_, _) => AddCompounds();
            var remove = new Button { Content = "Remove", HorizontalAlignment = HorizontalAlignment.Stretch };
            remove.Click += (_, _) =>
            {
                var list = session.Setup.Compounds.Except(compounds.SelectedItems?.Cast<string>() ?? Enumerable.Empty<string>()).ToList();
                session.Setup.Compounds = list;
                compounds.ItemsSource = list.ToList();
                Search();
            };
            var wizard = new Button { Content = "Simulation Wizard...", HorizontalAlignment = HorizontalAlignment.Stretch };
            wizard.Click += async (_, _) => await OpenSimulationWizard();
            var lists = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), RowDefinitions = new RowDefinitions("Auto,*"), ColumnSpacing = 8, RowSpacing = 4 };
            var middle = new StackPanel { Spacing = 6, Width = 170, Children = { add, remove, wizard } };
            Grid.SetRowSpan(compounds, 2); Grid.SetColumn(middle, 1); Grid.SetRowSpan(middle, 2);
            Grid.SetColumn(search, 2); Grid.SetColumn(found, 2); Grid.SetRow(found, 1);
            lists.Children.Add(compounds); lists.Children.Add(middle); lists.Children.Add(search); lists.Children.Add(found);

            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*"), RowSpacing = 6 };
            void Put(Control c, int row, int col, int span = 1) { Grid.SetRow(c, row); Grid.SetColumn(c, col); Grid.SetColumnSpan(c, span); g.Children.Add(c); }
            Put(description, 0, 0, 2);
            Put(Label("Kind of process"), 1, 0); Put(processClass, 1, 1);
            Put(Label("Property package"), 2, 0); Put(package, 2, 1);
            Put(new TextBlock { Text = "Units of the drawing's values", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) }, 3, 0, 2);
            Put(units, 4, 0, 2);
            Put(new DockPanel { Children = { new TextBlock { Text = "Compounds", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 4), [DockPanel.DockProperty] = Dock.Top }, lists } }, 5, 0, 2);
            return g;
        }

        void FillProcess()
        {
            loading = true;
            var p = session.Reading.Process;
            description.Text = (session.Reading.TitleBlock.Count > 0 ? "Drawing: " + session.Reading.DrawingName() + "\n" : "") +
                (p.Description.Length > 0 ? p.Description : "The drawing's text does not tell the process.") +
                (p.Evidence.Length > 0 ? "\nEvidence: " + p.Evidence : "") + (p.Confidence.Length > 0 ? "\nConfidence: " + p.Confidence : "");
            processClass.SelectedIndex = Array.FindIndex(DrawingImport.ProcessClasses, c => string.Equals(c.Class, p.Class, StringComparison.OrdinalIgnoreCase)) + 1;
            package.SelectedItem = session.Setup.PropertyPackage;
            var u = session.Reading.Units;
            unitT.Text = u.Temperature; unitP.Text = u.Pressure; unitW.Text = u.MassFlow; unitE.Text = u.Energy;
            compounds.ItemsSource = session.Setup.Compounds.ToList();
            loading = false;
            Search();
        }

        void TakeProcessPage()
        {
            if (session.Reading == null || string.IsNullOrWhiteSpace(unitT?.Text)) return;
            var u = session.Reading.Units;
            u.Temperature = unitT.Text.Trim(); u.Pressure = (unitP.Text ?? "").Trim(); u.MassFlow = (unitW.Text ?? "").Trim(); u.Energy = (unitE.Text ?? "").Trim();
        }

        void Search() => found.ItemsSource = session.AvailableCompounds(search.Text).Where(c => !session.Setup.Compounds.Contains(c)).ToList();

        void AddCompounds()
        {
            foreach (var c in found.SelectedItems?.Cast<string>().ToList() ?? new List<string>())
                if (!session.Setup.Compounds.Contains(c)) session.Setup.Compounds.Add(c);
            compounds.ItemsSource = session.Setup.Compounds.ToList();
            Search();
        }

        /// <summary>The application's Simulation Wizard, on the flowsheet with the proposed compounds and package in it.</summary>
        async Task OpenSimulationWizard()
        {
            TakeProcessPage();
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetTypes().FirstOrDefault(t => t.Name == "SimulationSetupWizard" && typeof(Window).IsAssignableFrom(t)); } catch (Exception) { return null; } })
                .FirstOrDefault(t => t != null);
            if (type == null) { description.Text = "The Simulation Wizard is not available here."; return; }
            try
            {
                session.ApplyCompoundsAndPackage();
                var wizard = (Window)Activator.CreateInstance(type, session.Flowsheet);
                if (NativeWindow != null) await wizard.ShowDialog(NativeWindow);
                session.TakeCompoundsAndPackage();
                FillProcess();
            }
            catch (Exception ex) { description.Text = "The Simulation Wizard could not be opened: " + ex.GetBaseException().Message; }
        }

        #endregion

        #region check and build

        Control BuildPage()
        {
            issues = new StackPanel { Spacing = 2 };
            results = new StackPanel { Spacing = 2 };
            specs = new CheckBox { Content = "Set the items up from the stream table", IsChecked = true };
            useUnits = new CheckBox { Content = "Use the drawing's units", IsChecked = session.Flowsheet.SimulationObjects.Count == 0 };
            solve = new CheckBox { Content = "Solve and compare with the table", IsChecked = true };
            build = new Button { Content = "Build the flowsheet" };
            build.Click += async (_, _) => await BuildFlowsheet();
            summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
            // the fit of what the drawing does not give (Patreon edition), once the flowsheet is solved
            fitChoices = new StackPanel { Spacing = 2 };
            fit = new Button { Content = "Fit to the table", VerticalAlignment = VerticalAlignment.Center };
            fit.Click += async (_, _) => await FitFlowsheet();
            fitRow = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 14, IsVisible = false,
                Children =
                {
                    new TextBlock { Text = "Fit what the drawing does not give\nto the stream table:", VerticalAlignment = VerticalAlignment.Center },
                    new ScrollViewer { Content = fitChoices, MaxHeight = 90, MinWidth = 320 },
                    fit,
                },
            };
            var g = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto,*"), RowSpacing = 6 };
            var top = new ScrollViewer { Content = issues };
            var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { specs, useUnits, solve, build } };
            var bottom = new ScrollViewer { Content = results };
            Grid.SetRow(options, 1); Grid.SetRow(summary, 2); Grid.SetRow(fitRow, 3); Grid.SetRow(bottom, 4);
            g.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = top });
            g.Children.Add(options); g.Children.Add(summary); g.Children.Add(fitRow);
            g.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = bottom, [Grid.RowProperty] = 4 });
            return g;
        }

        static void Show(StackPanel panel, IEnumerable<ImportIssue> list)
        {
            panel.Children.Clear();
            foreach (var i in list)
            {
                var line = new TextBlock
                {
                    Text = (i.Level == ImportIssue.Levels.Error ? "Error" : i.Level == ImportIssue.Levels.Warning ? "Warning" : "Note") + (i.Subject.Length > 0 ? " (" + i.Subject + ")" : "") + ": " + i.Text,
                    TextWrapping = TextWrapping.Wrap,
                };
                // a note keeps the theme's text colour
                if (i.Level == ImportIssue.Levels.Error) line.Foreground = Brushes.IndianRed;
                else if (i.Level == ImportIssue.Levels.Warning) line.Foreground = Brushes.DarkGoldenrod;
                panel.Children.Add(line);
            }
            if (panel.Children.Count == 0) panel.Children.Add(new TextBlock { Text = "Nothing to report.", Opacity = 0.7 });
        }

        void FillChecks()
        {
            var list = session.Check();
            Show(issues, list);
            var errors = list.Any(i => i.Level == ImportIssue.Levels.Error);
            build.IsEnabled = session.Built == null && !errors;
            if (session.Built == null)
                summary.Text = errors ? "Correct the errors on the earlier pages before building." : "Ready to build" + (list.Count > 0 ? "; the warnings above are worth a look." : ".");
        }

        async Task BuildFlowsheet()
        {
            session.Setup.SpecsFromTable = specs.IsChecked == true;
            session.Setup.UseDrawingUnits = useUnits.IsChecked == true;
            build.IsEnabled = false;
            summary.Text = "Building the flowsheet...";
            await Task.Delay(50);
            try
            {
                session.Build(solve.IsChecked == true, s => summary.Text = s + "...");
                summary.Text = session.Summary();
                Show(results, session.Built.Issues.Concat(session.Solve));
                close.Content = "Finish";
                FillFit();
            }
            catch (Exception ex)
            {
                summary.Text = "The flowsheet could not be built: " + ex.GetBaseException().Message;
            }
        }

        /// <summary>The parameters the fit may move (Patreon edition), shown once the flowsheet is built and solved.</summary>
        void FillFit()
        {
            fitRow.IsVisible = session.CanFit && solve.IsChecked == true;
            fitChoices.Children.Clear();
            foreach (var p in session.FitParameters)
            {
                var box = new CheckBox { Content = p.ToString(), IsChecked = p.Use, Tag = p };
                box.IsCheckedChanged += (_, _) => p.Use = box.IsChecked == true;
                fitChoices.Children.Add(box);
            }
        }

        async Task FitFlowsheet()
        {
            if (reading != null) { reading.Cancel(); return; }
            reading = new CancellationTokenSource();
            var token = reading.Token;
            fit.Content = "Stop";
            fitChoices.IsEnabled = back.IsEnabled = close.IsEnabled = false;
            try
            {
                await Task.Run(() => session.Fit(token, s => Dispatcher.UIThread.Post(() => summary.Text = s.Trim())));
                summary.Text = "Solving the flowsheet at the fitted values...";
                await Task.Delay(50);
                session.FinishFit();
                summary.Text = session.Summary();
                Show(results, session.Built.Issues.Concat(session.Solve));
                FillFit();
            }
            catch (Exception ex)
            {
                summary.Text = "The fit failed: " + ex.GetBaseException().Message;
            }
            finally
            {
                reading = null;
                fit.Content = "Fit to the table";
                fitChoices.IsEnabled = back.IsEnabled = close.IsEnabled = true;
            }
        }

        #endregion

        /// <summary>The sheet with the reading drawn over it: the items as dots, and with lines the connections as arrows.</summary>
        sealed class SheetView : Control
        {
            readonly Func<DrawingReading> reading;
            readonly bool lines;
            public Bitmap Image;
            public object Selected;

            public SheetView(Func<DrawingReading> reading, bool lines)
            {
                this.reading = reading;
                this.lines = lines;
                ClipToBounds = true;
            }

            public override void Render(DrawingContext ctx)
            {
                var r = reading();
                ctx.FillRectangle(Brushes.White, new Rect(Bounds.Size));
                if (r == null) return;
                double w = Image?.Size.Width ?? 1400, h = Image?.Size.Height ?? 1000;
                var k = Math.Min(Bounds.Width / w, Bounds.Height / h);
                var sheet = new Rect((Bounds.Width - w * k) / 2, (Bounds.Height - h * k) / 2, w * k, h * k);
                if (Image != null)
                {
                    ctx.DrawImage(Image, sheet);
                    ctx.FillRectangle(new SolidColorBrush(Colors.White, 0.6), sheet);
                }
                var places = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < r.Items.Count; i++)
                {
                    var it = r.Items[i];
                    if (places.ContainsKey(it.Tag)) continue;
                    places[it.Tag] = double.IsNaN(it.X) || double.IsNaN(it.Y)
                        ? new Point(sheet.X + sheet.Width * (i + 1) / (r.Items.Count + 1), sheet.Bottom - 12)
                        : new Point(sheet.X + it.X * sheet.Width, sheet.Y + it.Y * sheet.Height);
                }
                if (lines)
                {
                    foreach (var l in r.Lines.OrderBy(x => x == Selected))
                    {
                        var hasFrom = places.TryGetValue(l.From, out var a);
                        var hasTo = places.TryGetValue(l.To, out var b);
                        if (!hasFrom && !hasTo) continue;
                        if (!hasFrom) a = new Point(b.X - 40, b.Y);
                        if (!hasTo) b = new Point(a.X + 40, a.Y);
                        var color = l == Selected ? Colors.Red : !l.Include ? Colors.LightGray : l.Feed || l.Product ? Colors.SeaGreen : Colors.RoyalBlue;
                        var pen = new Pen(new SolidColorBrush(color), l == Selected ? 3 : 1.6);
                        ctx.DrawLine(pen, a, b);
                        // the arrowhead
                        var d = b - a; var len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
                        if (len > 1)
                        {
                            var u = new Point(d.X / len, d.Y / len);
                            var bs = new Point(b.X - u.X * 9, b.Y - u.Y * 9);
                            var geo = new StreamGeometry();
                            using (var g = geo.Open())
                            {
                                g.BeginFigure(b, true);
                                g.LineTo(new Point(bs.X - u.Y * 4, bs.Y + u.X * 4));
                                g.LineTo(new Point(bs.X + u.Y * 4, bs.Y - u.X * 4));
                                g.EndFigure(true);
                            }
                            ctx.DrawGeometry(new SolidColorBrush(color), null, geo);
                        }
                    }
                    foreach (var p in places.Values) ctx.DrawEllipse(Brushes.Black, null, p, 3, 3);
                    if (Selected is ReadLine sl)
                        Text(ctx, r.StreamTag(sl) + ": " + (sl.Feed ? BatteryLimit : sl.From) + " > " + (sl.Product ? BatteryLimit : sl.To), new Point(sheet.X + 4, sheet.Y + 4), Brushes.Red, true);
                }
                else
                {
                    foreach (var it in r.Items)
                    {
                        var p = places[it.Tag];
                        var color = it == Selected ? Colors.Red : !it.Include ? Colors.Gray : it.Category == "Mixer" || it.Category == "Splitter" ? Colors.DarkOrange : Colors.RoyalBlue;
                        var rad = it == Selected ? 8 : 5;
                        ctx.DrawEllipse(new SolidColorBrush(color), null, p, rad, rad);
                        Text(ctx, it.Tag, new Point(p.X + rad + 2, p.Y - 8), Brushes.Black, it == Selected);
                    }
                }
            }

            static void Text(DrawingContext ctx, string text, Point at, IBrush brush, bool bold)
            {
                var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), UiScale.Font(11), brush);
                ctx.DrawText(ft, at);
            }
        }
    }

    /// <summary>Opens the drawing import in the cross-platform interface.</summary>
    internal static class DrawingImportPrompt
    {
        internal static async void Show(IFlowsheet fs)
        {
            try
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var owner = desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? desktop?.MainWindow;
                await new DrawingImportWindow(fs).ShowDialog(owner);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Drawing import: " + ex);
            }
        }
    }
}
