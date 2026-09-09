using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Extensions.ReactionFinder.Logic;
using DWSIM.Interfaces;
using RxnBaseClasses = DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.UI.Desktop.Avalonia.Reactions
{
    /// <summary>
    /// The Avalonia port of the classic ReactionFinderControl used in the Simulation Wizard: it lists
    /// the reactions DWSIM can suggest from the selected compounds (a checkbox list), shows details of
    /// the focused one, and adds the checked ones to the flowsheet's default reaction set. The
    /// suggestion logic itself is the shared DWSIM.ReactionFinder.Logic assembly.
    /// </summary>
    public sealed class ReactionFinderPanel : UserControl
    {
        private readonly IFlowsheet _fs;
        private readonly bool _wizard;
        private List<SuggestedReaction> _suggestions = new();
        private readonly Dictionary<CheckBox, SuggestedReaction> _map = new();
        private readonly StackPanel _list = new() { Spacing = 2 };
        private TextBox _details = null!;

        public ReactionFinderPanel(IFlowsheet flowsheet, bool wizardMode)
        {
            _fs = flowsheet;
            _wizard = wizardMode;
            Build();
            RefreshSuggestions();
        }

        public int SuggestionsCount => _suggestions.Count;

        private void Build()
        {
            _details = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var intro = new TextBlock
            {
                Text = "DWSIM detected the following possible reactions based on the compounds you selected. " +
                       "Check the ones you want to add to the flowsheet.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var split = new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,*") };
            var leftScroll = new ScrollViewer { Content = _list };
            global::Avalonia.Controls.Grid.SetColumn(leftScroll, 0);
            var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, HorizontalAlignment = HorizontalAlignment.Stretch };
            global::Avalonia.Controls.Grid.SetColumn(splitter, 1);
            global::Avalonia.Controls.Grid.SetColumn(_details, 2);
            split.Children.Add(leftScroll);
            split.Children.Add(splitter);
            split.Children.Add(_details);

            var root = new DockPanel { Margin = new Thickness(8) };
            DockPanel.SetDock(intro, global::Avalonia.Controls.Dock.Top);
            root.Children.Add(intro);

            if (!_wizard)
            {
                // outside the wizard: an Add button that commits the checked suggestions immediately
                var add = new Button { Content = "Add Checked to Default Set", HorizontalAlignment = HorizontalAlignment.Left };
                add.Classes.Add("panel");
                add.Click += (_, _) => { AddCheckedToDefaultSet(); RefreshSuggestions(); };
                var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Children = { add } };
                DockPanel.SetDock(bar, global::Avalonia.Controls.Dock.Bottom);
                root.Children.Add(bar);
            }

            root.Children.Add(split);
            Content = root;
        }

        public void RefreshSuggestions()
        {
            _suggestions = _fs != null ? ReactionSuggester.SuggestAll(_fs) : new List<SuggestedReaction>();
            _list.Children.Clear();
            _map.Clear();
            foreach (var s in _suggestions)
            {
                var cb = new CheckBox { Content = s.DisplayName };
                var captured = s;
                cb.GotFocus += (_, _) => ShowDetails(captured);
                cb.PointerPressed += (_, _) => ShowDetails(captured);
                _map[cb] = s;
                _list.Children.Add(cb);
            }
            _details.Text = _suggestions.Count == 0 ? "No reactions were suggested for the current compounds." : "";
        }

        private void ShowDetails(SuggestedReaction s)
            => _details.Text = $"{s.DisplayName}\r\nCategory: {s.Category}\r\n\r\n{s.Equation}";

        /// <summary>Adds every checked suggestion to the flowsheet and its first (default) reaction set,
        /// matching the classic ReactionFinderControl.</summary>
        public void AddCheckedToDefaultSet()
        {
            if (_fs == null) return;
            var target = _fs.ReactionSets.Values.FirstOrDefault();
            foreach (var kv in _map)
            {
                if (kv.Key.IsChecked != true) continue;
                if (kv.Value.Reaction is not RxnBaseClasses.Reaction concrete) continue;
                if (!_fs.Reactions.ContainsKey(concrete.ID)) _fs.Reactions.Add(concrete.ID, concrete);
                if (target != null && !target.Reactions.ContainsKey(concrete.ID))
                    target.Reactions.Add(concrete.ID, new RxnBaseClasses.ReactionSetBase(concrete.ID, 0, true));
            }
        }
    }
}
