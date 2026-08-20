using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using Reports = DWSIM.SharedClasses.Reports;

namespace DWSIM.UI.Desktop.Avalonia
{
    /// <summary>
    /// The cross-platform equivalent of the classic FormReportConfig: pick which objects and which
    /// per-phase property groups go into the results report, order them, and either view the HTML
    /// report in the system browser or export the data. The report itself is built by the shared
    /// <see cref="Reports.SimulationReportBuilder"/>, exactly as the Windows edition does.
    /// </summary>
    public sealed class ReportConfigWindow : Window
    {
        private readonly IFlowsheet _flowsheet;

        // selected objects, in the order they appear in the report
        private sealed class SelObj
        {
            public string Name = "";
            public string Tag = "";
            public override string ToString() => Tag;
        }

        private readonly ObservableCollection<SelObj> _selected = new();
        private readonly ListBox _selectedList = new() { Height = 300 };
        private readonly Dictionary<string, CheckBox> _objChecks = new();

        // the nine per-phase toggles, matching FormReportConfig's CheckBox1..9
        private readonly CheckBox _conditions   = new() { Content = "Conditions", IsChecked = true };
        private readonly CheckBox _compositions  = new() { Content = "Compositions", IsChecked = true };
        private readonly CheckBox _mixture       = new() { Content = "Overall Mixture Properties", IsChecked = true };
        private readonly CheckBox _vapor         = new() { Content = "Vapor Phase Properties", IsChecked = true };
        private readonly CheckBox _liquidMix     = new() { Content = "Overall Liquid Phase Properties", IsChecked = true };
        private readonly CheckBox _liquid1       = new() { Content = "Liquid Phase 1 Properties", IsChecked = true };
        private readonly CheckBox _liquid2       = new() { Content = "Liquid Phase 2 Properties", IsChecked = true };
        private readonly CheckBox _aqueous       = new() { Content = "Aqueous Phase Properties", IsChecked = true };
        private readonly CheckBox _solid         = new() { Content = "Solid Phase Properties", IsChecked = true };

        public ReportConfigWindow(IFlowsheet flowsheet, string title)
        {
            _flowsheet = flowsheet;
            Title = title;
            Width = 900;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            IconHelper.ApplyWindowIcon(this);
            _selectedList.ItemsSource = _selected;
            Content = BuildContent();
        }

        private Control BuildContent()
        {
            var root = new Grid
            {
                Margin = new Thickness(10),
                ColumnDefinitions = new ColumnDefinitions("300,8,240,8,*"),
                RowDefinitions = new RowDefinitions("*,Auto")
            };

            // --- left: available objects grouped by category, with per-category select-all ---
            var objPanel = new StackPanel { Spacing = 2 };
            foreach (var group in GroupedObjects())
            {
                var catBox = new CheckBox { Content = group.Key, FontWeight = FontWeight.Bold };
                var children = new List<CheckBox>();
                var childPanel = new StackPanel { Spacing = 1, Margin = new Thickness(16, 0, 0, 6) };
                foreach (var (name, tag) in group.Value)
                {
                    var cb = new CheckBox { Content = tag, Tag = name };
                    cb.IsCheckedChanged += (_, _) => OnObjectToggled(name, tag, cb.IsChecked == true);
                    _objChecks[name] = cb;
                    children.Add(cb);
                    childPanel.Children.Add(cb);
                }
                catBox.IsCheckedChanged += (_, _) =>
                {
                    foreach (var cb in children) cb.IsChecked = catBox.IsChecked;
                };
                objPanel.Children.Add(catBox);
                objPanel.Children.Add(childPanel);
            }
            var objScroll = new ScrollViewer { Content = objPanel };
            var left = new StackPanel { Spacing = 4 };
            left.Children.Add(new TextBlock { Text = "Available Objects", FontWeight = FontWeight.Bold });
            left.Children.Add(objScroll);
            Grid.SetColumn(left, 0); Grid.SetRow(left, 0);
            root.Children.Add(left);

            // --- middle: selected objects, in report order, with up/down ---
            var btnUp = new Button { Content = "Move Up", Width = 100 };
            var btnDown = new Button { Content = "Move Down", Width = 100 };
            btnUp.Click += (_, _) => Move(-1);
            btnDown.Click += (_, _) => Move(+1);
            var order = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            order.Children.Add(btnUp);
            order.Children.Add(btnDown);
            var middle = new StackPanel { Spacing = 4 };
            middle.Children.Add(new TextBlock { Text = "Report Objects (in order)", FontWeight = FontWeight.Bold });
            middle.Children.Add(_selectedList);
            middle.Children.Add(order);
            Grid.SetColumn(middle, 2); Grid.SetRow(middle, 0);
            root.Children.Add(middle);

            // --- right: per-phase include options ---
            var opts = new StackPanel { Spacing = 3 };
            opts.Children.Add(new TextBlock { Text = "Include in Report", FontWeight = FontWeight.Bold });
            foreach (var cb in new[] { _conditions, _compositions, _mixture, _vapor, _liquidMix, _liquid1, _liquid2, _aqueous, _solid })
                opts.Children.Add(cb);
            var right = new ScrollViewer { Content = opts };
            Grid.SetColumn(right, 4); Grid.SetRow(right, 0);
            root.Children.Add(right);

            // --- bottom: actions ---
            var btnView = new Button { Content = "View Report", Width = 130 };
            var btnCsv = new Button { Content = "Export CSV...", Width = 120 };
            var btnTxt = new Button { Content = "Export Text...", Width = 120 };
            var btnClose = new Button { Content = "Close", Width = 90 };
            btnView.Click += (_, _) => ViewReport();
            btnCsv.Click += async (_, _) => await Export("csv");
            btnTxt.Click += async (_, _) => await Export("txt");
            btnClose.Click += (_, _) => Close();
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            actions.Children.Add(btnView);
            actions.Children.Add(btnCsv);
            actions.Children.Add(btnTxt);
            actions.Children.Add(btnClose);
            Grid.SetColumn(actions, 0); Grid.SetColumnSpan(actions, 5); Grid.SetRow(actions, 1);
            root.Children.Add(actions);

            return root;
        }

        // Material Streams, Energy Streams, then all unit operations, each sorted by tag.
        private SortedDictionary<string, List<(string Name, string Tag)>> GroupedObjects()
        {
            var groups = new SortedDictionary<string, List<(string, string)>>(StringComparer.Ordinal);
            foreach (var so in _flowsheet.SimulationObjects.Values)
            {
                var go = so.GraphicObject;
                if (go == null) continue;
                var cat = go.ObjectType switch
                {
                    ObjectType.MaterialStream => "1. Material Streams",
                    ObjectType.EnergyStream => "2. Energy Streams",
                    _ => "3. Unit Operations"
                };
                if (!groups.TryGetValue(cat, out var list)) { list = new(); groups[cat] = list; }
                list.Add((so.Name, string.IsNullOrEmpty(go.Tag) ? so.Name : go.Tag));
            }
            foreach (var list in groups.Values) list.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));
            return groups;
        }

        private void OnObjectToggled(string name, string tag, bool on)
        {
            var existing = _selected.FirstOrDefault(s => s.Name == name);
            if (on && existing == null) _selected.Add(new SelObj { Name = name, Tag = tag });
            else if (!on && existing != null) _selected.Remove(existing);
        }

        private void Move(int delta)
        {
            var i = _selectedList.SelectedIndex;
            if (i < 0) return;
            var j = i + delta;
            if (j < 0 || j >= _selected.Count) return;
            _selected.Move(i, j);
            _selectedList.SelectedIndex = j;
        }

        private Reports.ReportOptions BuildOptions() => new()
        {
            ObjectNames = _selected.Select(s => s.Name).ToList(),
            IncludeConditions = _conditions.IsChecked == true,
            IncludeCompositions = _compositions.IsChecked == true,
            IncludeMixtureProps = _mixture.IsChecked == true,
            IncludeVaporProps = _vapor.IsChecked == true,
            IncludeLiquidMixtureProps = _liquidMix.IsChecked == true,
            IncludeLiquid1Props = _liquid1.IsChecked == true,
            IncludeLiquid2Props = _liquid2.IsChecked == true,
            IncludeAqueousProps = _aqueous.IsChecked == true,
            IncludeSolidProps = _solid.IsChecked == true,
            ProductVersion = "DWSIM 10"
        };

        private void ViewReport()
        {
            if (_selected.Count == 0) return;
            try
            {
                var html = new Reports.SimulationReportBuilder(_flowsheet).GenerateHTML(BuildOptions());
                var path = Path.Combine(Path.GetTempPath(), $"dwsim_report_{Guid.NewGuid():N}.html");
                File.WriteAllText(path, html);
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _flowsheet.ShowMessage("Could not generate the report: " + ex.Message, IFlowsheet.MessageType.GeneralError);
            }
        }

        private async System.Threading.Tasks.Task Export(string kind)
        {
            if (_selected.Count == 0) return;
            var sp = StorageProvider;
            if (sp == null) return;
            var ext = kind == "csv" ? "csv" : "txt";
            var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Report",
                SuggestedFileName = "DWSIM_Report." + ext,
                DefaultExtension = ext,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(kind == "csv" ? "CSV File" : "Text File") { Patterns = new[] { "*." + ext } }
                }
            });
            if (file == null) return;

            try
            {
                var dt = new Reports.SimulationReportBuilder(_flowsheet).BuildDataTable(BuildOptions());
                var sep = kind == "csv" ? "," : "\t";
                var sb = new StringBuilder();
                string prev = null;
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    var obj = row[0].ToString();
                    if (obj != prev)
                    {
                        if (prev != null) sb.AppendLine();
                        sb.AppendLine("Object: " + obj);
                        prev = obj;
                    }
                    sb.AppendLine($"{Csv(row[2], sep)}{sep}{Csv(row[3], sep)}{sep}{Csv(row[4], sep)}");
                }
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(sb.ToString());
            }
            catch (Exception ex)
            {
                _flowsheet.ShowMessage("Could not export the report: " + ex.Message, IFlowsheet.MessageType.GeneralError);
            }
        }

        private static string Csv(object value, string sep)
        {
            var s = value?.ToString() ?? "";
            if (sep == "," && (s.Contains(',') || s.Contains('"') || s.Contains('\n')))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
