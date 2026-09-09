using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using RxnBaseClasses = DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.UI.Desktop.Avalonia.Reactions
{
    /// <summary>
    /// The reaction-set editor, mirroring the classic WinForms FormReacSetEditor: a name and
    /// description plus a grid of the reactions in the set (read-only Reaction / Type / Equation, an
    /// Active checkbox and an editable Rank), with add and remove. Values are read on OK.
    /// </summary>
    internal sealed class ReactionSetEditorWindow : Window
    {
        private readonly IFlowsheet _fs;
        private readonly IReactionSet _existing;
        private readonly string _suggestedName;

        private TextBox _tbName = null!, _tbDesc = null!;
        private DataGrid _grid = null!;
        private readonly ObservableCollection<Row> _rows = new();

        public bool Saved { get; private set; }

        public sealed class Row
        {
            public string ReactionName { get; init; } = "";
            public string ReactionType { get; init; } = "";
            public string Equation { get; init; } = "";
            public bool Active { get; set; }
            public string Rank { get; set; } = "0";
            public string ID { get; init; } = "";
        }

        public ReactionSetEditorWindow(IFlowsheet fs, IReactionSet existing, string suggestedName)
        {
            _fs = fs;
            _existing = existing;
            _suggestedName = suggestedName;
            BuildUI();
        }

        private void BuildUI()
        {
            Title = _existing == null ? "Add Reaction Set" : "Edit Reaction Set";
            Width = 720;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = IconHelper.GetWindowIcon();

            _tbName = new TextBox { Text = _existing?.Name ?? _suggestedName };
            _tbDesc = new TextBox { Text = _existing?.Description ?? "" };

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                ItemsSource = _rows,
                Height = 300   // a DataGrid needs a bounded height to render its rows
            };
            _grid.Columns.Add(new DataGridTextColumn { Header = "Reaction", Binding = new Binding(nameof(Row.ReactionName)) { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Binding(nameof(Row.ReactionType)) { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Equation", Binding = new Binding(nameof(Row.Equation)) { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTemplateColumn
            {
                // a checkbox column needs the cell in edit mode first (two clicks, looks disabled); a
                // templated CheckBox toggles on the first click, as the compounds grid in Settings does
                Header = "Active",
                Width = new DataGridLength(0.8, DataGridLengthUnitType.Star),
                CellTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<Row>((_, _) =>
                {
                    var cb = new CheckBox { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    cb.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(Row.Active)) { Mode = BindingMode.TwoWay });
                    return cb;
                })
            });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Rank", Binding = new Binding(nameof(Row.Rank)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) });

            LoadRows();

            var btnAdd = new Button { Content = "Add Reaction ▾", Width = 150 };
            btnAdd.Classes.Add("panel");
            btnAdd.Click += (_, _) => ShowAddFlyout(btnAdd);
            var btnRemove = new Button { Content = "Remove Selected", Width = 150 };
            btnRemove.Classes.Add("panel");
            btnRemove.Click += (_, _) => { if (_grid.SelectedItem is Row r) _rows.Remove(r); };

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 6), Children = { btnAdd, btnRemove } };

            var id = new StackPanel { Spacing = 6 };
            id.Children.Add(LabelRow("Name", _tbName));
            id.Children.Add(LabelRow("Description", _tbDesc));

            var reactionsBody = new DockPanel();
            DockPanel.SetDock(toolbar, global::Avalonia.Controls.Dock.Top);
            reactionsBody.Children.Add(toolbar);
            reactionsBody.Children.Add(_grid);

            var content = new StackPanel { Spacing = 10, Margin = new Thickness(12) };
            content.Children.Add(GroupBox("Identification", id));
            content.Children.Add(GroupBox("Reactions", reactionsBody));

            var ok = new Button { Content = "OK", Width = 90, IsDefault = true };
            ok.Classes.Add("dialog");
            ok.Click += (_, _) => OnOk();
            var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
            cancel.Classes.Add("dialog");
            cancel.Click += (_, _) => Close();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 0, 16, 12), Children = { cancel, ok } };

            var body = new DockPanel();
            DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
            body.Children.Add(buttons);
            body.Children.Add(new ScrollViewer { Content = content });
            Content = body;
        }

        private void LoadRows()
        {
            _rows.Clear();
            if (_existing == null) return;
            foreach (var pair in _existing.Reactions)
            {
                if (!_fs.Reactions.TryGetValue(pair.Key, out var rxn)) continue;
                _rows.Add(new Row
                {
                    ReactionName = rxn.Name,
                    ReactionType = rxn.ReactionType.ToString(),
                    Equation = rxn.Equation ?? "",
                    Active = pair.Value.IsActive,
                    Rank = pair.Value.Rank.ToString(CultureInfo.InvariantCulture),
                    ID = pair.Key
                });
            }
        }

        private void ShowAddFlyout(Control anchor)
        {
            var inSet = _rows.Select(r => r.ID).ToHashSet();
            var candidates = _fs.Reactions.Values.Where(r => !inSet.Contains(r.ID)).ToList();
            if (candidates.Count == 0) return;

            var menu = new MenuFlyout();
            foreach (var rxn in candidates)
            {
                var item = new MenuItem { Header = $"{rxn.Name} ({rxn.ReactionType})" };
                var captured = rxn;
                item.Click += (_, _) => _rows.Add(new Row
                {
                    ReactionName = captured.Name,
                    ReactionType = captured.ReactionType.ToString(),
                    Equation = captured.Equation ?? "",
                    Active = true,
                    Rank = "0",
                    ID = captured.ID
                });
                menu.Items.Add(item);
            }
            menu.ShowAt(anchor);
        }

        private void OnOk()
        {
            if (string.IsNullOrWhiteSpace(_tbName.Text)) return;
            _fs.RegisterSnapshot(SnapshotType.ReactionSubsystem);

            var rs = _existing ?? new RxnBaseClasses.ReactionSet(Guid.NewGuid().ToString(), _tbName.Text!, "");
            rs.Name = _tbName.Text!;
            rs.Description = _tbDesc.Text ?? "";

            rs.Reactions.Clear();
            foreach (var row in _rows)
            {
                var rank = row.Rank.IsValidDoubleFlexible() ? (int)row.Rank.ToDoubleFromCurrent() : 0;
                rs.Reactions[row.ID] = new RxnBaseClasses.ReactionSetBase(row.ID, rank, row.Active);
            }

            if (_existing == null) _fs.AddReactionSet(rs);
            Saved = true;
            Close();
        }

        // ---- small UI helpers (kept local to avoid coupling to the reaction editor base) ----

        private static Control LabelRow(string label, Control control)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 1), ColumnSpacing = 8 };
            var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };
            global::Avalonia.Controls.Grid.SetColumn(control, 1);
            g.Children.Add(lbl);
            g.Children.Add(control);
            return g;
        }

        private static Control GroupBox(string header, Control content)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = header, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 2, 0, 6) });
            stack.Children.Add(content);
            return new Border { Child = stack, Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, CornerRadius = new CornerRadius(3) };
        }
    }
}
