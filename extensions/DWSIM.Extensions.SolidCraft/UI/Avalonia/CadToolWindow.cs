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
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The dialog of a CAD Tools item in the cross-platform interface (see <see cref="CadToolRequest"/>).</summary>
    public sealed class CadToolWindow : PlusWindow
    {
        readonly CadToolRequest Request;
        readonly List<(int Samples, string Text)> Samples;

        TextBox programFolder, output, owner, clearance, rackHeight, itemGap;
        TextBox projectNumber, department, preparedBy, area, checkedBy, revision, approvedBy, revisionText, logo;
        TextBlock programStatus, problem, renderTime, overwriteText;
        CheckBox openInApp, labels, flowAnimation, insulation, platforms, instruments, dynamicReplay, render, keepCopy;
        CheckBox dwg, pdf, png, drawingSet, readMoves;
        List<CheckBox> itemChecks;
        StackPanel overwrite;
        Button run, preview;
        ComboBox standard, status, resolution, samples, view;

        public CadToolWindow(CadToolRequest request)
        {
            Request = request;
            var o = request.Options;
            // a count chosen elsewhere (an edited settings file) is offered as it is
            Samples = CadTools.RenderSamples.ToList();
            if (!Samples.Any(s => s.Samples == o.RenderSamples)) Samples.Add((o.RenderSamples, o.RenderSamples.ToString(CultureInfo.CurrentCulture)));

            Title = request.Title;
            using (var icon = request.Icon())
                if (icon != null) Icon = new WindowIcon(icon);
            Width = 640;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Content = Build();
            ShowOptions(o);
            UpdateProgramStatus();
            UpdatePreview();
        }

        static TextBlock Label(string text) => new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        /// <summary>A titled section: the title over a rule, the content below it.</summary>
        static StackPanel Group(string header, Control content)
            => new StackPanel
            {
                Margin = new Thickness(0, 6, 0, 4),
                Children =
                {
                    new TextBlock { Text = header, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) },
                    new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 6, 0, 0), Child = content }
                }
            };

        /// <summary>A grid of label and field rows.</summary>
        static Grid Rows(params (string Label, Control Field)[] rows)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 6 };
            for (var i = 0; i < rows.Length; i++)
            {
                g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                if (rows[i].Label != null)
                {
                    var l = Label(rows[i].Label);
                    Grid.SetRow(l, i);
                    g.Children.Add(l);
                    Grid.SetColumn(rows[i].Field, 1);
                }
                else Grid.SetColumnSpan(rows[i].Field, 2);
                Grid.SetRow(rows[i].Field, i);
                g.Children.Add(rows[i].Field);
            }
            return g;
        }

        /// <summary>Label and field pairs two to a row; a pair whose field is wide takes the whole row.</summary>
        static Grid Pairs(params (string Label, Control Field, bool Wide)[] pairs)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"), RowSpacing = 6 };
            int row = -1, col = 2;
            foreach (var p in pairs)
            {
                if (p.Wide || col == 2) { row++; col = 0; g.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); }
                else col = 2;
                var l = Label(p.Label);
                if (col == 2) l.Margin = new Thickness(12, 0, 10, 0);
                Grid.SetRow(l, row); Grid.SetColumn(l, col);
                Grid.SetRow(p.Field, row); Grid.SetColumn(p.Field, col + 1);
                if (p.Wide) { Grid.SetColumnSpan(p.Field, 3); col = 2; }
                g.Children.Add(l);
                g.Children.Add(p.Field);
            }
            return g;
        }

        /// <summary>A path field with its Browse button.</summary>
        static Grid PathRow(TextBox box, Func<Task> browse)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var button = new Button { Content = "Browse...", Margin = new Thickness(6, 0, 0, 0) };
            button.Click += async (_, _) => await browse();
            Grid.SetColumn(button, 1);
            g.Children.Add(box);
            g.Children.Add(button);
            return g;
        }

        static TextBox Short() => new TextBox { Width = 90, HorizontalAlignment = HorizontalAlignment.Left };

        Control Build()
        {
            var r = Request;
            var root = new StackPanel { Margin = new Thickness(14, 10, 14, 12), Spacing = 2 };
            root.Children.Add(new TextBlock { Text = r.Description, TextWrapping = TextWrapping.Wrap, Opacity = 0.85, Margin = new Thickness(0, 0, 0, 4) });

            if (r.HasProgram)
            {
                programFolder = new TextBox { Text = r.ProgramFolder ?? "" };
                programFolder.TextChanged += (_, _) => UpdateProgramStatus();
                programStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(12) };
                root.Children.Add(Group(r.Program, Rows(("Installed in", PathRow(programFolder, BrowseProgram)), (null, programStatus))));
            }

            output = new TextBox { Text = r.Output ?? "" };
            // another output: the warning about the old files no longer applies
            output.TextChanged += (_, _) => { HideOverwrite(); UpdatePreview(); };
            var outputRows = new List<(string, Control)> { (r.OutputLabel, PathRow(output, BrowseOutput)) };
            if (r.ShowsOpenInApp)
            {
                openInApp = new CheckBox { Content = r.OpenInAppText, IsChecked = r.OpenInApp };
                outputRows.Add((null, openInApp));
            }
            root.Children.Add(Group("Output", Rows(outputRows.ToArray())));

            if (r.IsDrawing)
            {
                standard = new ComboBox { ItemsSource = CadTools.Standards, MinWidth = 160 };
                owner = new TextBox();
                var rows = new List<(string, Control)> { ("Drawing standard", standard), ("Company in the title block", owner) };
                if (r.ShowsClearance)
                {
                    clearance = Short();
                    rows.Add(("Line clearance (mm, 1 to 15)", clearance));
                }
                dwg = new CheckBox { Content = "DWG" };
                pdf = new CheckBox { Content = "PDF" };
                png = new CheckBox { Content = "PNG" };
                rows.Add(("Also save (DXF always)", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { dwg, pdf, png } }));
                if (r.ShowsDrawingSet)
                {
                    drawingSet = new CheckBox { Content = "One PDF with a drawing list" };
                    rows.Add(("Drawing set", drawingSet));
                }
                root.Children.Add(Group("Drawing", Rows(rows.ToArray())));

                projectNumber = new TextBox(); department = new TextBox(); preparedBy = new TextBox(); area = new TextBox();
                checkedBy = new TextBox(); revision = new TextBox(); approvedBy = new TextBox(); revisionText = new TextBox(); logo = new TextBox();
                status = new ComboBox { ItemsSource = CadTools.Statuses, IsEditable = true, HorizontalAlignment = HorizontalAlignment.Stretch };
                root.Children.Add(Group("Title block", Pairs(
                    ("Project number", projectNumber, false), ("Department", department, false),
                    ("Prepared by", preparedBy, false), ("Area / unit", area, false),
                    ("Checked by", checkedBy, false), ("Revision", revision, false),
                    ("Approved by", approvedBy, false), ("Status", status, false),
                    ("Revision description", revisionText, true),
                    ("Logo (DXF or DWG)", PathRow(logo, BrowseLogo), true))));
            }

            if (r.ShowsItems)
            {
                itemChecks = r.Items.Select(i => new CheckBox { Content = i.Text, IsChecked = i.Checked, Tag = i }).ToList();
                foreach (var c in itemChecks) c.IsCheckedChanged += (_, _) => HideOverwrite();
                var list = new UniformGrid { Columns = 2 };
                foreach (var c in itemChecks) list.Children.Add(c);
                Button Pick(string text, bool value)
                {
                    var b = new Button { Content = text, MinWidth = 70 };
                    b.Click += (_, _) => { foreach (var c in itemChecks) c.IsChecked = value; };
                    return b;
                }
                root.Children.Add(Group(r.Tool == CadTool.PipingIsometrics ? "Lines to draw" : r.Tool == CadTool.DataSheets ? "Data sheets to write" : "Items to draw", new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = "Ticked: the ones selected in the flowsheet, or all of them when none is selected.", Opacity = 0.7, FontSize = UiScale.Font(12), TextWrapping = TextWrapping.Wrap },
                        new Border
                        {
                            BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(6, 4),
                            Child = new ScrollViewer { MaxHeight = 170, Content = list }
                        },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Pick("All", true), Pick("None", false) } }
                    }
                }));
            }

            if (r.Design != null)
            {
                readMoves = new CheckBox { Content = "Read the places of the items moved in SolidCraft first", IsChecked = r.ReadMoves };
                root.Children.Add(Group("3D model", new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = r.DesignText, TextWrapping = TextWrapping.Wrap, Foreground = r.DesignChanged ? Brushes.DarkOrange : null },
                        readMoves
                    }
                }));
            }

            if (r.ShowsModelOptions)
            {
                labels = new CheckBox { Content = "Labels over the items" };
                flowAnimation = new CheckBox { Content = "Flow animation" };
                insulation = new CheckBox { Content = "Insulation" };
                platforms = new CheckBox { Content = "Platforms and ladders" };
                instruments = new CheckBox { Content = "Instruments" };
                dynamicReplay = new CheckBox { Content = "Replay of the last dynamic run" };
                var checks = new UniformGrid { Columns = 2 };
                foreach (var c in new[] { labels, flowAnimation, insulation, platforms, instruments, dynamicReplay }) checks.Children.Add(c);
                rackHeight = Short();
                itemGap = Short();
                root.Children.Add(Group("3D model", Rows((null, checks),
                    ("Top of the pipe rack (" + r.LengthUnits + ")", rackHeight), ("Smallest gap between items (" + r.LengthUnits + ")", itemGap))));

                render = new CheckBox { Content = "Render a photorealistic picture of the model (path tracing)" };
                // a preset, or a size typed as "width x height"
                resolution = new ComboBox { ItemsSource = CadTools.RenderSizes.Select(s => s.Text).ToList(), IsEditable = true, MinWidth = 200 };
                samples = new ComboBox { ItemsSource = Samples.Select(s => s.Text).ToList(), MinWidth = 200 };
                view = new ComboBox { ItemsSource = CadTools.RenderViews.Select(v => v.Text).ToList(), MinWidth = 200 };
                renderTime = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(12) };
                preview = new Button { Content = "Preview the view (the 3D model saved last)" };
                preview.Click += (_, _) =>
                {
                    var why = Collect();
                    if (why != null) ShowProblem(why);
                    else CadTools.Preview(Request);
                };
                render.IsCheckedChanged += (_, _) =>
                {
                    resolution.IsEnabled = samples.IsEnabled = view.IsEnabled = render.IsChecked == true;
                    UpdateRenderTime();
                    UpdatePreview();
                };
                resolution.PropertyChanged += (_, e) => { if (e.Property == ComboBox.TextProperty || e.Property == SelectingItemsControl.SelectedIndexProperty) UpdateRenderTime(); };
                samples.SelectionChanged += (_, _) => UpdateRenderTime();
                root.Children.Add(Group("Photorealistic picture", Rows((null, render), ("Resolution (pixels)", resolution), ("Samples per pixel", samples),
                    ("View", view), (null, renderTime), (null, preview))));
            }

            overwriteText = new TextBlock { Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap };
            keepCopy = new CheckBox { Content = "Keep the old files: move them to a \"Superseded\" folder first" };
            overwrite = Group("Files from an earlier export", new StackPanel { Spacing = 6, Children = { overwriteText, keepCopy } });
            overwrite.IsVisible = false;
            root.Children.Add(overwrite);

            problem = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, 6, 0, 0) };
            root.Children.Add(problem);

            run = new Button { Content = r.RunText, IsDefault = true, MinWidth = 90 };
            run.Classes.Add("dialog");
            run.Click += (_, _) => Accept();
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };
            cancel.Classes.Add("dialog");
            cancel.Click += (_, _) => Close(false);
            var defaults = new Button { Content = "Defaults", MinWidth = 90, Margin = new Thickness(0, 0, 16, 0) };
            defaults.Classes.Add("dialog");
            defaults.Click += (_, _) =>
            {
                ShowOptions(new ExportOptions { CADCraftPath = Request.Options.CADCraftPath });
                if (!string.IsNullOrEmpty(Request.DefaultOutput)) output.Text = Request.DefaultOutput;
            };
            root.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 10, 0, 0),
                Children = { defaults, cancel, run }
            });
            // a tall dialog (many items, the title block) scrolls on a small screen
            return new ScrollViewer { Content = root, MaxHeight = 900, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        /// <summary>Puts the options in the controls (on opening, and for the Defaults button).</summary>
        void ShowOptions(ExportOptions o)
        {
            var r = Request;
            if (standard != null)
            {
                standard.SelectedIndex = Math.Max(0, Array.FindIndex(CadTools.Standards, s => string.Equals(s, o.DrawingStandard, StringComparison.OrdinalIgnoreCase)));
                owner.Text = o.DrawingOwner ?? "";
                if (clearance != null) clearance.Text = o.LineClearance.ToString(CultureInfo.CurrentCulture);
                dwg.IsChecked = o.SaveDwg;
                pdf.IsChecked = o.SavePdf;
                png.IsChecked = o.SavePng;
                if (drawingSet != null) drawingSet.IsChecked = o.DrawingSet;
                projectNumber.Text = o.ProjectNumber ?? "";
                department.Text = o.Department ?? "";
                preparedBy.Text = o.PreparedBy ?? "";
                area.Text = o.Area ?? "";
                checkedBy.Text = o.CheckedBy ?? "";
                revision.Text = o.Revision ?? "";
                approvedBy.Text = o.ApprovedBy ?? "";
                status.Text = o.Status ?? "";
                revisionText.Text = o.RevisionText ?? "";
                logo.Text = o.Logo ?? "";
            }
            if (render != null)
            {
                labels.IsChecked = o.Labels;
                flowAnimation.IsChecked = o.FlowAnimation;
                insulation.IsChecked = o.Insulation;
                platforms.IsChecked = o.Platforms;
                instruments.IsChecked = o.Instruments;
                dynamicReplay.IsChecked = o.DynamicReplay;
                rackHeight.Text = r.ToLength(o.RackHeight).ToString("0.###", CultureInfo.CurrentCulture);
                itemGap.Text = r.ToLength(o.ItemGap).ToString("0.###", CultureInfo.CurrentCulture);
                render.IsChecked = o.Render;
                var preset = Array.FindIndex(CadTools.RenderSizes, s => s.Width == o.RenderWidth && s.Height == o.RenderHeight);
                if (preset >= 0) resolution.SelectedIndex = preset;
                else { resolution.SelectedIndex = -1; resolution.Text = CadTools.SizeText(o.RenderWidth, o.RenderHeight); }
                samples.SelectedIndex = Math.Max(0, Samples.FindIndex(s => s.Samples == o.RenderSamples));
                view.SelectedIndex = Math.Max(0, Array.FindIndex(CadTools.RenderViews, v => v.Id == o.RenderView));
                resolution.IsEnabled = samples.IsEnabled = view.IsEnabled = o.Render;
                UpdateRenderTime();
                UpdatePreview();
            }
        }

        /// <summary>The size typed or picked in the resolution box.</summary>
        string ResolutionText => resolution.SelectedIndex >= 0 ? CadTools.RenderSizes[resolution.SelectedIndex].Text : resolution.Text;

        /// <summary>The estimate of the render time for the size and samples chosen.</summary>
        void UpdateRenderTime()
        {
            if (renderTime == null || samples.SelectedIndex < 0 || !CadTools.TryReadSize(ResolutionText, out var width, out var height))
            {
                if (renderTime != null) renderTime.Text = "";
                return;
            }
            var n = Samples[samples.SelectedIndex].Samples;
            renderTime.Opacity = render.IsChecked == true ? 1 : 0.5;
            renderTime.Foreground = CadTools.RenderSeconds(width, height, n) > CadTools.LongRenderSeconds ? Brushes.DarkOrange : Brushes.Gray;
            renderTime.Text = "Render time: " + CadTools.RenderTimeText(width, height, n) + ", longer on a slower one.";
        }

        /// <summary>The preview needs the design the 3D model saved last, in the output folder.</summary>
        void UpdatePreview()
        {
            if (preview != null) preview.IsEnabled = render.IsChecked == true && Request.CanPreview(output.Text);
        }

        /// <summary>Another output or choice of items: the warning about the old files is worked out again.</summary>
        void HideOverwrite()
        {
            if (overwrite == null || !overwrite.IsVisible) return;
            overwrite.IsVisible = false;
            run.Content = Request.RunText;
        }

        void UpdateProgramStatus()
        {
            if (programStatus == null) return;
            programStatus.Text = Request.ProgramStatus(programFolder.Text, out var level);
            programStatus.Foreground = level == 0 ? Brushes.SeaGreen : level == 1 ? Brushes.DarkOrange : Brushes.Firebrick;
        }

        IStorageProvider Storage => TopLevel.GetTopLevel(this)?.StorageProvider;

        async Task<IStorageFolder> Folder(string path)
        {
            try { return Directory.Exists(path) ? await Storage.TryGetFolderFromPathAsync(path) : null; }
            catch (Exception) { return null; }
        }

        async Task BrowseProgram()
        {
            if (Storage == null) return;
            var picked = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "The folder that holds " + CADCraftSession.Exe(Request.ProgramExe),
                SuggestedStartLocation = await Folder(programFolder.Text)
            });
            var path = picked.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) programFolder.Text = path;
        }

        async Task BrowseOutput()
        {
            if (Storage == null) return;
            string path;
            if (Request.OutputIsFile)
            {
                string folder = null, file = null;
                try { folder = Path.GetDirectoryName(output.Text); file = Path.GetFileName(output.Text); } catch (Exception) { }
                var picked = await Storage.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Request.Title,
                    SuggestedFileName = file,
                    SuggestedStartLocation = await Folder(folder),
                    DefaultExtension = "dxf",
                    ShowOverwritePrompt = false,
                    FileTypeChoices = new[] { new FilePickerFileType("DXF drawing (DWG, PDF and PNG are saved beside it)") { Patterns = new[] { "*.dxf" } } }
                });
                path = picked?.TryGetLocalPath();
            }
            else
            {
                var picked = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = Request.OutputLabel,
                    SuggestedStartLocation = await Folder(output.Text)
                });
                path = picked.FirstOrDefault()?.TryGetLocalPath();
            }
            if (path != null) output.Text = path;
        }

        async Task BrowseLogo()
        {
            if (Storage == null) return;
            string folder = null;
            try { folder = Path.GetDirectoryName(logo.Text); } catch (Exception) { }
            var picked = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "The logo for the title block",
                SuggestedStartLocation = await Folder(folder),
                FileTypeFilter = new[] { new FilePickerFileType("Drawings (DXF, DWG)") { Patterns = new[] { "*.dxf", "*.dwg" } } }
            });
            var path = picked.FirstOrDefault()?.TryGetLocalPath();
            if (path != null) logo.Text = path;
        }

        static bool TryRead(string text, out double value)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        void ShowProblem(string text)
        {
            problem.Text = text;
            problem.IsVisible = true;
        }

        /// <summary>Takes what the controls hold into the request and its options; returns the problem, or null.</summary>
        string Collect()
        {
            var r = Request;
            var o = r.Options;
            if (programFolder != null) r.ProgramFolder = programFolder.Text?.Trim() ?? "";
            r.Output = output.Text?.Trim() ?? "";
            if (r.OutputIsFile && r.Output.Length > 0 && !r.Output.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) r.Output += ".dxf";
            if (openInApp != null) r.OpenInApp = openInApp.IsChecked == true;
            if (itemChecks != null)
                foreach (var c in itemChecks) ((CadToolItem)c.Tag).Checked = c.IsChecked == true;
            r.ReadMoves = readMoves != null && readMoves.IsChecked == true;

            if (r.IsDrawing)
            {
                o.SaveDwg = dwg.IsChecked == true;
                o.SavePdf = pdf.IsChecked == true;
                o.SavePng = png.IsChecked == true;
                if (drawingSet != null) o.DrawingSet = drawingSet.IsChecked == true;
                o.DrawingStandard = CadTools.Standards[Math.Max(0, standard.SelectedIndex)];
                o.DrawingOwner = owner.Text ?? "";
                if (clearance != null)
                {
                    if (!TryRead(clearance.Text, out var c)) return "The line clearance is not a number.";
                    o.LineClearance = Math.Max(1, Math.Min(15, c));
                }
                o.ProjectNumber = projectNumber.Text?.Trim() ?? "";
                o.Department = department.Text?.Trim() ?? "";
                o.PreparedBy = preparedBy.Text?.Trim() ?? "";
                o.Area = area.Text?.Trim() ?? "";
                o.CheckedBy = checkedBy.Text?.Trim() ?? "";
                o.Revision = revision.Text?.Trim() ?? "";
                o.ApprovedBy = approvedBy.Text?.Trim() ?? "";
                o.Status = (status.SelectedIndex >= 0 ? CadTools.Statuses[status.SelectedIndex] : status.Text)?.Trim() ?? "";
                o.RevisionText = revisionText.Text?.Trim() ?? "";
                o.Logo = logo.Text?.Trim() ?? "";
                if (o.Logo.Length > 0 && !File.Exists(o.Logo)) return "The logo was not found: " + o.Logo;
            }
            if (r.ShowsModelOptions)
            {
                o.Labels = labels.IsChecked == true;
                o.FlowAnimation = flowAnimation.IsChecked == true;
                o.Insulation = insulation.IsChecked == true;
                o.Platforms = platforms.IsChecked == true;
                o.Instruments = instruments.IsChecked == true;
                o.DynamicReplay = dynamicReplay.IsChecked == true;
                if (!TryRead(rackHeight.Text, out var rack)) return "The top of the pipe rack is not a number.";
                if (!TryRead(itemGap.Text, out var gap)) return "The gap between items is not a number.";
                o.RackHeight = Math.Max(2.5, Math.Min(30, r.FromLength(rack)));
                o.ItemGap = Math.Max(0.5, Math.Min(20, r.FromLength(gap)));
                o.Render = render.IsChecked == true;
                if (!CadTools.TryReadSize(ResolutionText, out var width, out var height)) return "The resolution is the width and the height in pixels, such as 2000 x 1250.";
                o.RenderWidth = width;
                o.RenderHeight = height;
                o.RenderSamples = Samples[Math.Max(0, samples.SelectedIndex)].Samples;
                o.RenderView = CadTools.RenderViews[Math.Max(0, view.SelectedIndex)].Id;
            }
            return null;
        }

        void Accept()
        {
            var r = Request;
            var why = Collect() ?? CadTools.Validate(r);
            if (why != null) { ShowProblem(why); return; }

            // files of an earlier export: say so first, and go on only on the second click
            var existing = CadTools.ExistingOutputs(r);
            if (existing.Count > 0 && !overwrite.IsVisible)
            {
                problem.IsVisible = false;
                overwriteText.Text = CadTools.OverwriteWarning(existing);
                overwrite.IsVisible = true;
                run.Content = "Replace";
                return;
            }
            r.KeepOldCopies = existing.Count > 0 && keepCopy.IsChecked == true;
            Close(true);
        }
    }

    /// <summary>Shows the dialog of a CAD tool and runs the tool when the user confirms it.</summary>
    internal static class CadToolPrompt
    {
        internal static async void Ask(CadToolRequest request, Action<CadToolRequest> run)
        {
            try
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var owner = desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? desktop?.MainWindow;
                if (await new CadToolWindow(request).ShowDialog<bool>(owner)) run(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CAD Tools: " + ex);
            }
        }

        /// <summary>Shows the window of a running tool over the window in front (a tool's dialog for its preview, else the main window).</summary>
        internal static void Watch(CadJob job)
        {
            try
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var owner = desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? desktop?.MainWindow;
                new CadToolProgressWindow(job).Show(owner);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CAD Tools: " + ex);
            }
        }
    }
}
