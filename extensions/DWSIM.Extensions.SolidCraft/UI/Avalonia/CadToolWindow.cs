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
        readonly List<(int Width, int Height, string Text)> Sizes;
        readonly List<(int Samples, string Text)> Samples;

        TextBox programFolder, output, owner, clearance, rackHeight, itemGap;
        TextBlock programStatus, problem, renderTime, overwriteText;
        CheckBox openInApp, labels, flowAnimation, insulation, platforms, instruments, dynamicReplay, render, keepCopy;
        StackPanel overwrite;
        Button run;
        ComboBox standard, resolution, samples;

        public CadToolWindow(CadToolRequest request)
        {
            Request = request;
            var o = request.Options;
            // a size chosen elsewhere (an edited settings file) is offered as it is
            Sizes = CadTools.RenderSizes.ToList();
            if (!Sizes.Any(s => s.Width == o.RenderWidth && s.Height == o.RenderHeight))
                Sizes.Add((o.RenderWidth, o.RenderHeight, o.RenderWidth + " x " + o.RenderHeight));
            Samples = CadTools.RenderSamples.ToList();
            if (!Samples.Any(s => s.Samples == o.RenderSamples)) Samples.Add((o.RenderSamples, o.RenderSamples.ToString(CultureInfo.CurrentCulture)));

            Title = request.Title;
            using (var icon = request.Icon())
                if (icon != null) Icon = new WindowIcon(icon);
            Width = 620;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            Content = Build();
            UpdateProgramStatus();
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

        Control Build()
        {
            var r = Request;
            var o = r.Options;
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
            var outputRows = new List<(string, Control)> { (r.OutputLabel, PathRow(output, BrowseOutput)) };
            if (r.ShowsOpenInApp)
            {
                openInApp = new CheckBox { Content = "Open in " + r.Program + " when finished", IsChecked = r.OpenInApp };
                outputRows.Add((null, openInApp));
            }
            root.Children.Add(Group("Output", Rows(outputRows.ToArray())));

            if (r.IsDrawing)
            {
                standard = new ComboBox { ItemsSource = CadTools.Standards, MinWidth = 160 };
                standard.SelectedIndex = Math.Max(0, Array.FindIndex(CadTools.Standards, s => string.Equals(s, o.DrawingStandard, StringComparison.OrdinalIgnoreCase)));
                owner = new TextBox { Text = o.DrawingOwner ?? "" };
                var rows = new List<(string, Control)> { ("Drawing standard", standard), ("Company in the title block", owner) };
                if (r.ShowsClearance)
                {
                    clearance = new TextBox { Text = o.LineClearance.ToString(CultureInfo.CurrentCulture), Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
                    rows.Add(("Line clearance (mm, 1 to 15)", clearance));
                }
                root.Children.Add(Group("Drawing", Rows(rows.ToArray())));
            }

            if (r.ShowsModelOptions)
            {
                labels = new CheckBox { Content = "Labels over the items", IsChecked = o.Labels };
                flowAnimation = new CheckBox { Content = "Flow animation", IsChecked = o.FlowAnimation };
                insulation = new CheckBox { Content = "Insulation", IsChecked = o.Insulation };
                platforms = new CheckBox { Content = "Platforms and ladders", IsChecked = o.Platforms };
                instruments = new CheckBox { Content = "Instruments", IsChecked = o.Instruments };
                dynamicReplay = new CheckBox { Content = "Replay of the last dynamic run", IsChecked = o.DynamicReplay };
                var checks = new UniformGrid { Columns = 2 };
                foreach (var c in new[] { labels, flowAnimation, insulation, platforms, instruments, dynamicReplay }) checks.Children.Add(c);
                rackHeight = new TextBox { Text = o.RackHeight.ToString(CultureInfo.CurrentCulture), Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
                itemGap = new TextBox { Text = o.ItemGap.ToString(CultureInfo.CurrentCulture), Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
                root.Children.Add(Group("3D model", Rows((null, checks), ("Top of the pipe rack (m)", rackHeight), ("Smallest gap between items (m)", itemGap))));

                render = new CheckBox { Content = "Render a photorealistic picture of the model (path tracing)", IsChecked = o.Render };
                resolution = new ComboBox { ItemsSource = Sizes.Select(s => s.Text).ToList(), MinWidth = 200 };
                resolution.SelectedIndex = Sizes.FindIndex(s => s.Width == o.RenderWidth && s.Height == o.RenderHeight);
                samples = new ComboBox { ItemsSource = Samples.Select(s => s.Text).ToList(), MinWidth = 200 };
                samples.SelectedIndex = Samples.FindIndex(s => s.Samples == o.RenderSamples);
                renderTime = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(12) };
                render.IsCheckedChanged += (_, _) =>
                {
                    resolution.IsEnabled = samples.IsEnabled = render.IsChecked == true;
                    UpdateRenderTime();
                };
                resolution.SelectionChanged += (_, _) => UpdateRenderTime();
                samples.SelectionChanged += (_, _) => UpdateRenderTime();
                resolution.IsEnabled = samples.IsEnabled = o.Render;
                UpdateRenderTime();
                root.Children.Add(Group("Photorealistic picture", Rows((null, render), ("Resolution (pixels)", resolution), ("Samples per pixel", samples), (null, renderTime))));
            }

            overwriteText = new TextBlock { Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap };
            keepCopy = new CheckBox { Content = "Keep the old files: move them to a \"Superseded\" folder first" };
            overwrite = Group("Files from an earlier export", new StackPanel { Spacing = 6, Children = { overwriteText, keepCopy } });
            overwrite.IsVisible = false;
            root.Children.Add(overwrite);
            // another output: the warning about the old files no longer applies
            output.TextChanged += (_, _) =>
            {
                if (!overwrite.IsVisible) return;
                overwrite.IsVisible = false;
                run.Content = r.RunText;
            };

            problem = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, 6, 0, 0) };
            root.Children.Add(problem);

            run = new Button { Content = r.RunText, IsDefault = true, MinWidth = 90 };
            run.Classes.Add("dialog");
            run.Click += (_, _) => Accept();
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };
            cancel.Classes.Add("dialog");
            cancel.Click += (_, _) => Close(false);
            root.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 10, 0, 0),
                Children = { cancel, run }
            });
            return root;
        }

        /// <summary>The estimate of the render time for the size and samples chosen.</summary>
        void UpdateRenderTime()
        {
            if (renderTime == null || resolution.SelectedIndex < 0 || samples.SelectedIndex < 0) return;
            var size = Sizes[resolution.SelectedIndex];
            var n = Samples[samples.SelectedIndex].Samples;
            renderTime.Opacity = render.IsChecked == true ? 1 : 0.5;
            renderTime.Foreground = CadTools.RenderSeconds(size.Width, size.Height, n) > CadTools.LongRenderSeconds ? Brushes.DarkOrange : Brushes.Gray;
            renderTime.Text = "Render time: " + CadTools.RenderTimeText(size.Width, size.Height, n) + ", longer on a slower one.";
        }

        void UpdateProgramStatus()
        {
            if (programStatus == null) return;
            var exe = CADCraftSession.Exe(Request.ProgramExe);
            var found = Request.ProgramFoundIn(programFolder.Text);
            programStatus.Foreground = found ? Brushes.SeaGreen : Brushes.Firebrick;
            programStatus.Text = found
                ? exe + " found in this folder."
                : string.IsNullOrWhiteSpace(programFolder.Text)
                    ? Request.Program + " was not found on this computer: choose the folder that holds " + exe + "."
                    : exe + " is not in this folder.";
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

        static bool TryRead(string text, out double value)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        void ShowProblem(string text)
        {
            problem.Text = text;
            problem.IsVisible = true;
        }

        void Accept()
        {
            var r = Request;
            var o = r.Options;
            if (programFolder != null) r.ProgramFolder = programFolder.Text?.Trim() ?? "";
            r.Output = output.Text?.Trim() ?? "";
            if (r.OutputIsFile && r.Output.Length > 0 && !r.Output.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) r.Output += ".dxf";
            if (openInApp != null) r.OpenInApp = openInApp.IsChecked == true;

            if (r.IsDrawing)
            {
                o.DrawingStandard = CadTools.Standards[Math.Max(0, standard.SelectedIndex)];
                o.DrawingOwner = owner.Text ?? "";
                if (clearance != null)
                {
                    if (!TryRead(clearance.Text, out var c)) { ShowProblem("The line clearance is not a number."); return; }
                    o.LineClearance = Math.Max(1, Math.Min(15, c));
                }
            }
            if (r.ShowsModelOptions)
            {
                o.Labels = labels.IsChecked == true;
                o.FlowAnimation = flowAnimation.IsChecked == true;
                o.Insulation = insulation.IsChecked == true;
                o.Platforms = platforms.IsChecked == true;
                o.Instruments = instruments.IsChecked == true;
                o.DynamicReplay = dynamicReplay.IsChecked == true;
                if (!TryRead(rackHeight.Text, out var rack)) { ShowProblem("The top of the pipe rack is not a number."); return; }
                if (!TryRead(itemGap.Text, out var gap)) { ShowProblem("The gap between items is not a number."); return; }
                o.RackHeight = Math.Max(2.5, Math.Min(30, rack));
                o.ItemGap = Math.Max(0.5, Math.Min(20, gap));
                o.Render = render.IsChecked == true;
                var size = Sizes[Math.Max(0, resolution.SelectedIndex)];
                o.RenderWidth = size.Width;
                o.RenderHeight = size.Height;
                o.RenderSamples = Samples[Math.Max(0, samples.SelectedIndex)].Samples;
            }

            var why = CadTools.Validate(r);
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

        /// <summary>Shows the window of a running tool over the main window.</summary>
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
