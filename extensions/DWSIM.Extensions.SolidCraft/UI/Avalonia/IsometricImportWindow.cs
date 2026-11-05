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
    /// <summary>The isometric import wizard in the cross-platform interface (see <see cref="IsometricImportSession"/>).</summary>
    public sealed class IsometricImportWindow : PlusWindow
    {
        static readonly string[] Directions = { "N", "S", "E", "W", "NE", "NW", "SE", "SW", "up", "down" };

        readonly IsometricImportSession session;
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

        // isometric page
        TextBox file;
        Button read, cancelRead, saveReading;
        TextBlock readStatus;
        WorkIndicator work;
        Image preview;

        // route page
        TextBox line, size, schedule, od, wall, material;
        TextBlock ends;
        Grid legsGrid;
        RouteView sketch;

        // pipe segment page
        StackPanel checks, sections;
        ComboBox target;
        Button apply;
        TextBlock summary;

        public IsometricImportWindow(IFlowsheet fs)
        {
            session = new IsometricImportSession(fs);
            Title = "Import a Pipe Segment from a Piping Isometric";
            Width = 1180;
            Height = 780;
            Content = Build();
            ShowPage(0);
        }

        static TextBlock Label(string text) => new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        Control Build()
        {
            steps.ItemsSource = IsometricImportSession.Steps.Select((s, i) => (i + 1) + ". " + s).ToList();
            steps.Width = 170;
            steps.SelectionChanged += (_, _) => { if (!loading && steps.SelectedIndex >= 0 && steps.SelectedIndex != page) ShowPage(steps.SelectedIndex); };
            back.Click += (_, _) => ShowPage(Math.Max(0, page - 1));
            next.Click += (_, _) => ShowPage(Math.Min(2, page + 1));
            close.Click += (_, _) => { reading?.Cancel(); Close(); };
            pages = new[] { IsometricPage(), RoutePage(), PipePage() };

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
            if (page == 1 && p != 1) TakeRoute();
            page = p;
            host.Content = pages[p];
            stepTitle.Text = IsometricImportSession.Steps[p];
            stepText.Text = IsometricImportSession.StepTexts[p];
            loading = true;
            steps.SelectedIndex = p;
            loading = false;
            back.IsEnabled = p > 0;
            next.IsVisible = p < 2;
            next.IsEnabled = session.Reading != null && reading == null;
            if (p == 1) FillRoute();
            if (p == 2) FillPipe();
        }

        IStorageProvider Storage => TopLevel.GetTopLevel(this)?.StorageProvider;

        #region isometric

        Control IsometricPage()
        {
            file = new TextBox();
            file.TextChanged += (_, _) => read.IsEnabled = File.Exists((file.Text ?? "").Trim()) && reading == null;
            var browse = new Button { Content = "Browse...", Margin = new Thickness(6, 0, 0, 0) };
            browse.Click += async (_, _) => await Browse();
            read = new Button { Content = "Read the isometric", IsEnabled = false };
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
            Put(Label("Isometric"), 0, 0); Put(file, 0, 1); Put(browse, 0, 2);
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
            return d;
        }

        async Task Browse()
        {
            if (Storage == null) return;
            var picked = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "The piping isometric",
                FileTypeFilter = new[] { new FilePickerFileType("Isometrics (*.png, *.jpg, *.pdf, *.dxf, *.dwg)") { Patterns = DrawingImportClient.Extensions.Select(x => "*" + x).ToArray() }, FilePickerFileTypes.All }
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
                var model = await work.Model(token, "an isometric");
                if (model == null) { read.Content = "Read with the local model"; return; }
                read.Content = "Read the isometric";
                await Task.Run(() => session.Read(token, s => Dispatcher.UIThread.Post(() => { if (!token.IsCancellationRequested) work.Step(s); })));
                session.Propose();
                work.Stop();
                readStatus.Text = string.Format(CultureInfo.CurrentCulture, "{0} legs read by {2} in {1}. Check them on the next page.", session.Reading.Legs.Count, DrawingImportClient.Elapsed(work.Elapsed), model);
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
                readStatus.Text = "The isometric could not be read: " + ex.GetBaseException().Message + " (the details are in the flowsheet's log)";
                session.Flowsheet.ShowMessage("Import Pipe Segment from Piping Isometric: " + ex, IFlowsheet.MessageType.GeneralError);
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
            next.IsEnabled = !busy && session.Reading != null;
        }

        async Task OpenReading()
        {
            if (Storage == null) return;
            var picked = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "A saved isometric reading",
                FileTypeFilter = new[] { new FilePickerFileType("Isometric readings (*.json)") { Patterns = new[] { "*.json" } } }
            });
            var path = picked.FirstOrDefault()?.TryGetLocalPath();
            if (path == null) return;
            try
            {
                session.Open(File.ReadAllText(path));
                file.Text = session.File;
                readStatus.Text = session.Reading.Legs.Count + " legs in the saved reading.";
                Loaded();
            }
            catch (Exception ex) { readStatus.Text = "The reading could not be opened: " + ex.Message; }
        }

        async Task SaveReading()
        {
            if (Storage == null || session.Reading == null) return;
            if (page == 1) TakeRoute();
            var picked = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the isometric reading",
                SuggestedFileName = Path.GetFileNameWithoutExtension(session.File) + " - reading.json",
                FileTypeChoices = new[] { new FilePickerFileType("Isometric readings (*.json)") { Patterns = new[] { "*.json" } } }
            });
            var path = picked?.TryGetLocalPath();
            if (path != null) session.SaveReading(path);
        }

        #endregion

        #region route

        static string N(double? v) => v.HasValue ? v.Value.ToString("0.###", CultureInfo.CurrentCulture) : "";

        static double? Parse(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var x)) return x;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ? x : (double?)null;
        }

        Control RoutePage()
        {
            line = new TextBox(); size = new TextBox(); schedule = new TextBox(); od = new TextBox(); wall = new TextBox(); material = new TextBox();
            foreach (var t in new[] { line, size, schedule, od, wall, material }) t.LostFocus += (_, _) => { if (!loading) TakeRoute(); };
            ends = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            var data = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), RowSpacing = 4, ColumnSpacing = 6 };
            void Put(Control c, int row, int col, int span = 1) { Grid.SetRow(c, row); Grid.SetColumn(c, col); Grid.SetColumnSpan(c, span); data.Children.Add(c); }
            Put(Label("Line"), 0, 0); Put(line, 0, 1); Put(Label("Material"), 0, 2); Put(material, 0, 3);
            Put(Label("Nominal size"), 1, 0); Put(size, 1, 1); Put(Label("Schedule"), 1, 2); Put(schedule, 1, 3);
            Put(Label("Outside diameter (mm)"), 2, 0); Put(od, 2, 1); Put(Label("Wall (mm)"), 2, 2); Put(wall, 2, 3);
            Put(Label("From / to"), 3, 0); Put(ends, 3, 1, 3);

            legsGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,40,100,100,100,*,Auto"), RowSpacing = 2, ColumnSpacing = 4 };
            var add = new Button { Content = "Add a leg", Margin = new Thickness(0, 6, 0, 0) };
            add.Click += (_, _) => { TakeRoute(); session.Reading.Legs.Add(new IsoLeg { Direction = "E" }); FillLegs(); };
            var left = new DockPanel();
            DockPanel.SetDock(data, Dock.Top);
            DockPanel.SetDock(add, Dock.Bottom);
            left.Children.Add(data);
            left.Children.Add(add);
            left.Children.Add(new ScrollViewer { Content = legsGrid, Margin = new Thickness(0, 8, 0, 0) });

            sketch = new RouteView { Reading = () => session.Reading };
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("1.3*,1*"), ColumnSpacing = 10 };
            Grid.SetColumn(sketch, 1);
            g.Children.Add(left);
            g.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = sketch, [Grid.ColumnProperty] = 1 });
            return g;
        }

        void FillRoute()
        {
            var r = session.Reading;
            loading = true;
            line.Text = r.LineNumber; size.Text = r.Size; schedule.Text = r.Schedule;
            od.Text = N(r.OuterDiameter); wall.Text = N(r.Wall); material.Text = r.Material;
            ends.Text = (r.From.Length > 0 ? r.From : "?") + "  >  " + (r.To.Length > 0 ? r.To : "?");
            loading = false;
            FillLegs();
        }

        void FillLegs()
        {
            var r = session.Reading;
            legsGrid.Children.Clear();
            legsGrid.RowDefinitions.Clear();
            legsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var heads = new[] { "Use", "Leg", "Direction", "Length (mm)", "Rise (mm)", "Fittings at the end", "" };
            for (var c = 0; c < heads.Length; c++) legsGrid.Children.Add(new TextBlock { Text = heads[c], FontWeight = FontWeight.SemiBold, [Grid.ColumnProperty] = c });
            for (var i = 0; i < r.Legs.Count; i++)
            {
                var leg = r.Legs[i];
                var row = i + 1;
                legsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var use = new CheckBox { IsChecked = leg.Include };
                use.IsCheckedChanged += (_, _) => { leg.Include = use.IsChecked == true; sketch.InvalidateVisual(); };
                var options = Directions.Contains(leg.Direction, StringComparer.OrdinalIgnoreCase) || leg.Direction.Length == 0 ? Directions : Directions.Concat(new[] { leg.Direction }).ToArray();
                var dir = new ComboBox { ItemsSource = options, SelectedItem = options.FirstOrDefault(d => string.Equals(d, leg.Direction, StringComparison.OrdinalIgnoreCase)), HorizontalAlignment = HorizontalAlignment.Stretch };
                dir.SelectionChanged += (_, _) => { leg.Direction = dir.SelectedItem as string ?? ""; sketch.InvalidateVisual(); };
                var length = new TextBox { Text = N(leg.Length) };
                length.LostFocus += (_, _) => { leg.Length = Parse(length.Text); sketch.InvalidateVisual(); };
                var rise = new TextBox { Text = N(leg.Rise) };
                rise.LostFocus += (_, _) => { leg.Rise = Parse(rise.Text); sketch.InvalidateVisual(); };
                var fittings = new TextBox { Text = string.Join(", ", leg.Fittings) };
                fittings.LostFocus += (_, _) =>
                {
                    leg.Fittings = (fittings.Text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                    sketch.InvalidateVisual();
                };
                var remove = new Button { Content = "x", Padding = new Thickness(8, 2) };
                remove.Click += (_, _) => { r.Legs.Remove(leg); FillLegs(); };
                var cells = new Control[] { use, new TextBlock { Text = row.ToString(CultureInfo.CurrentCulture), VerticalAlignment = VerticalAlignment.Center }, dir, length, rise, fittings, remove };
                for (var c = 0; c < cells.Length; c++) { Grid.SetRow(cells[c], row); Grid.SetColumn(cells[c], c); legsGrid.Children.Add(cells[c]); }
            }
            sketch.InvalidateVisual();
        }

        /// <summary>The line data as the user left it (the legs take their edits as they go).</summary>
        void TakeRoute()
        {
            var r = session.Reading;
            if (r == null || line == null) return;
            r.LineNumber = (line.Text ?? "").Trim();
            r.Size = (size.Text ?? "").Trim();
            r.Schedule = (schedule.Text ?? "").Trim();
            r.OuterDiameter = Parse(od.Text);
            r.Wall = Parse(wall.Text);
            r.Material = (material.Text ?? "").Trim();
        }

        /// <summary>The route in isometric projection, scaled to the box: the legs, the start and the end, the fittings as dots.</summary>
        sealed class RouteView : Control
        {
            public Func<IsoReading> Reading;

            public override void Render(DrawingContext ctx)
            {
                ctx.FillRectangle(Brushes.White, new Rect(Bounds.Size));
                var r = Reading?.Invoke();
                if (r == null) return;
                var points = IsometricImport.Route(r).Select(IsometricImport.Project).ToList();
                if (points.Count < 2) return;
                double x0 = points.Min(p => p[0]), x1 = points.Max(p => p[0]), y0 = points.Min(p => p[1]), y1 = points.Max(p => p[1]);
                const double margin = 30;
                var scale = Math.Min((Bounds.Width - 2 * margin) / Math.Max(1, x1 - x0), (Bounds.Height - 2 * margin) / Math.Max(1, y1 - y0));
                Point At(double[] p) => new Point(margin + (p[0] - x0) * scale + (Bounds.Width - 2 * margin - (x1 - x0) * scale) / 2,
                    Bounds.Height - margin - (p[1] - y0) * scale - (Bounds.Height - 2 * margin - (y1 - y0) * scale) / 2);
                var o = At(points[0]);
                var faint = new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 1, DashStyle.Dot);
                foreach (var axis in new[] { new[] { 1000.0, 0, 0 }, new[] { 0, 1000.0, 0 }, new[] { 0, 0, 1000.0 } })
                {
                    var a = IsometricImport.Project(axis);
                    ctx.DrawLine(faint, o, new Point(o.X + a[0] * scale, o.Y - a[1] * scale));
                }
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 2.5);
                var legs = r.Legs.Where(l => l.Include).ToList();
                for (var i = 1; i < points.Count; i++)
                {
                    var a = At(points[i - 1]);
                    var b = At(points[i]);
                    ctx.DrawLine(pen, a, b);
                    var leg = legs[i - 1];
                    Text(ctx, i + (leg.Length.HasValue ? ": " + leg.Length.Value.ToString("0", CultureInfo.CurrentCulture) : ""), new Point((a.X + b.X) / 2 + 4, (a.Y + b.Y) / 2 - 14), Brushes.SteelBlue);
                    if (leg.Fittings.Any(f => IsometricImport.PipeFitting(f) != null)) ctx.DrawEllipse(Brushes.DarkOrange, null, b, 4, 4);
                }
                var s = At(points[0]);
                var t = At(points[points.Count - 1]);
                ctx.DrawEllipse(Brushes.SeaGreen, null, s, 5, 5);
                ctx.DrawEllipse(Brushes.IndianRed, null, t, 5, 5);
                Text(ctx, r.From.Length > 0 ? r.From : "start", new Point(s.X + 6, s.Y + 2), Brushes.SeaGreen);
                Text(ctx, r.To.Length > 0 ? r.To : "end", new Point(t.X + 6, t.Y + 2), Brushes.IndianRed);
                Text(ctx, "N", new Point(8, 8), Brushes.Gray);
                ctx.DrawLine(new Pen(Brushes.Gray, 1), new Point(10, 40), new Point(30, 28));
            }

            static void Text(DrawingContext ctx, string text, Point at, IBrush brush)
            {
                var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default), UiScale.Font(11), brush);
                ctx.DrawText(ft, at);
            }
        }

        #endregion

        #region pipe segment

        Control PipePage()
        {
            checks = new StackPanel { Spacing = 2 };
            sections = new StackPanel { Spacing = 2 };
            target = new ComboBox { MinWidth = 300 };
            apply = new Button { Content = "Make the hydraulic profile" };
            apply.Click += (_, _) => Apply();
            summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
            var g = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,1.4*"), RowSpacing = 6 };
            var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { Label("Put the route in"), target, apply } };
            Grid.SetRow(options, 1); Grid.SetRow(summary, 2);
            g.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = new ScrollViewer { Content = checks } });
            g.Children.Add(options); g.Children.Add(summary);
            g.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6), Child = new ScrollViewer { Content = sections }, [Grid.RowProperty] = 3 });
            return g;
        }

        void FillPipe()
        {
            session.Propose();
            checks.Children.Clear();
            foreach (var i in session.Issues)
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
            var targets = session.Targets();
            var keep = session.Applied?.GraphicObject.Tag ?? target.SelectedItem as string;
            target.ItemsSource = targets;
            target.SelectedItem = keep != null && targets.Contains(keep) ? keep : targets[0];
            sections.Children.Clear();
            sections.Children.Add(new TextBlock { Text = "Section | quantity | length (m) | rise (m) | OD / ID (in) | material", FontWeight = FontWeight.SemiBold });
            foreach (var s in session.Sections)
            {
                var straight = s.TipoSegmento == "Tubulaosimples";
                sections.Children.Add(new TextBlock
                {
                    Text = (straight ? "Straight pipe" : s.TipoSegmento) + " | " + s.Quantidade + " | " +
                        (straight ? s.Comprimento.ToString("0.000", CultureInfo.CurrentCulture) + " | " + s.Elevacao.ToString("0.000", CultureInfo.CurrentCulture) : " | ") + " | " +
                        s.DE.ToString("0.###", CultureInfo.CurrentCulture) + " / " + s.DI.ToString("0.###", CultureInfo.CurrentCulture) + " | " + s.Material,
                });
            }
            var errors = session.Issues.Any(i => i.Level == ImportIssue.Levels.Error);
            apply.IsEnabled = session.Sections.Count > 0 && !errors;
            summary.Text = session.Sections.Count == 0 ? "No sections: correct the route first." : IsometricImport.Summary(session.Sections) + (errors ? ". Correct the errors above first." : ".");
        }

        void Apply()
        {
            try
            {
                var obj = session.Apply(target.SelectedItem as string ?? IsometricImportSession.NewPipe);
                FillPipe();
                summary.Text = "The hydraulic profile of " + obj.GraphicObject.Tag + " now has " + session.Sections.Count + " sections: " + IsometricImport.Summary(session.Sections) + ".";
                close.Content = "Finish";
            }
            catch (Exception ex) { summary.Text = "The profile could not be made: " + ex.GetBaseException().Message; }
        }

        #endregion
    }

    /// <summary>Opens the isometric import in the cross-platform interface.</summary>
    internal static class IsometricImportPrompt
    {
        internal static async void Show(IFlowsheet fs)
        {
            try
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var owner = desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? desktop?.MainWindow;
                await new IsometricImportWindow(fs).ShowDialog(owner);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Isometric import: " + ex);
            }
        }
    }
}
