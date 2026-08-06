using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UI.Shared.Avalonia;
using Column = DWSIM.UnitOperations.UnitOperations.Column;
using StreamInformation = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The connections of a rigorous column, as the Windows connections editor arranges them: the
    /// connectors on one side, grouped into feeds, products, side draws and duties, and the stage
    /// each connected stream belongs to on the other, with the side draw specifications.
    /// </summary>
    public static class ColumnConnectionsEditor
    {

        /// <summary>A connector of the column and whatever is attached to it.</summary>
        private sealed class ConnectorRow : INotifyPropertyChanged
        {
            private readonly Column _column;
            private readonly bool _input;
            private readonly int _index;
            private readonly Action _changed;

            public ConnectorRow(Column column, bool input, int index, Action changed)
            {
                _column = column;
                _input = input;
                _index = index;
                _changed = changed;
            }

            private IConnectionPoint Connector
            {
                get
                {
                    return _input
                        ? _column.GraphicObject.InputConnectors[_index]
                        : _column.GraphicObject.OutputConnectors[_index];
                }
            }

            public string Name { get { return Connector.ConnectorName; } }

            public string Stream
            {
                get
                {
                    var connector = Connector;
                    if (!connector.IsAttached) return "";
                    return _input
                        ? connector.AttachedConnector.AttachedFrom.Tag
                        : connector.AttachedConnector.AttachedTo.Tag;
                }
                set
                {
                    Connect(_column, Connector, _input, value);
                    Raise("Stream");
                    _changed();
                }
            }

            public void Refresh()
            {
                Raise("Stream");
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>A connected stream and the stage it feeds or leaves from.</summary>
        private sealed class AssociationRow : INotifyPropertyChanged
        {
            private readonly Column _column;
            private readonly StreamInformation _info;
            private readonly List<string> _stageNames;
            private readonly List<string> _stageIDs;

            public AssociationRow(Column column, StreamInformation info, string kind,
                                  List<string> stageNames, List<string> stageIDs)
            {
                _column = column;
                _info = info;
                _stageNames = stageNames;
                _stageIDs = stageIDs;
                Kind = kind;
            }

            public string Kind { get; private set; }

            public string Stream
            {
                get
                {
                    var objects = _column.GetFlowsheet().SimulationObjects;
                    if (_info.StreamID == null || !objects.ContainsKey(_info.StreamID)) return "";
                    return objects[_info.StreamID].GraphicObject.Tag;
                }
            }

            public string Stage
            {
                get
                {
                    var index = StageIndexOf(_info.AssociatedStage, _stageNames, _stageIDs);
                    return _stageNames[index];
                }
                set
                {
                    var index = _stageNames.IndexOf(value);
                    if (index < 0) return;
                    _info.AssociatedStage = _stageIDs[index];
                    Raise("Stage");
                }
            }

            public string Position
            {
                get { return _info.StreamPosition == StreamInformation.Position.Above ? "Above" : "Below"; }
                set
                {
                    _info.StreamPosition = value == "Above"
                        ? StreamInformation.Position.Above
                        : StreamInformation.Position.Below;
                    Raise("Position");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>The phase and the flow rate of a side draw.</summary>
        private sealed class SideDrawRow : INotifyPropertyChanged
        {
            private readonly Column _column;
            private readonly StreamInformation _info;
            private readonly IUnitsOfMeasure _su;
            private readonly string _nf;

            public SideDrawRow(Column column, StreamInformation info, IUnitsOfMeasure su, string nf)
            {
                _column = column;
                _info = info;
                _su = su;
                _nf = nf;
            }

            public string Stream
            {
                get
                {
                    var objects = _column.GetFlowsheet().SimulationObjects;
                    if (_info.StreamID == null || !objects.ContainsKey(_info.StreamID)) return "";
                    return objects[_info.StreamID].GraphicObject.Tag;
                }
            }

            public string PhaseName
            {
                get { return _info.StreamPhase == StreamInformation.Phase.V ? "Vapor" : "Liquid"; }
                set
                {
                    _info.StreamPhase = value == "Vapor"
                        ? StreamInformation.Phase.V
                        : StreamInformation.Phase.L;
                    Raise("PhaseName");
                }
            }

            public string FlowRate
            {
                get
                {
                    return cv.ConvertFromSI(_su.molarflow, _info.FlowRate.Value)
                             .ToString(_nf, CultureInfo.CurrentCulture);
                }
                set
                {
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _info.FlowRate.Value = cv.ConvertToSI(_su.molarflow, v);
                    Raise("FlowRate");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public static Control Build(Column column)
        {
            var flowsheet = column.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            column.SyncConnectedStreams();

            var host = new StackPanel();
            var tabs = new TabControl { Height = 380 };
            host.Children.Add(tabs);

            var streamTags = Tags(flowsheet, ObjectType.MaterialStream);
            var energyTags = Tags(flowsheet, ObjectType.EnergyStream);

            var feeds = new ObservableCollection<ConnectorRow>();
            var products = new ObservableCollection<ConnectorRow>();
            var sideDraws = new ObservableCollection<ConnectorRow>();
            var duties = new ObservableCollection<ConnectorRow>();

            // rebuilt whenever a connection changes, since that is what adds and drops streams
            Action rebuild = null;

            var associations = new ObservableCollection<AssociationRow>();
            var sideDrawSpecs = new ObservableCollection<SideDrawRow>();

            rebuild = () =>
            {
                column.SyncConnectedStreams();
                FillAssociations(column, associations, sideDrawSpecs, su, nf);
                flowsheet.UpdateInterface();
            };

            for (int i = 0; i < column.GraphicObject.InputConnectors.Count; i++)
            {
                var connector = column.GraphicObject.InputConnectors[i];
                var row = new ConnectorRow(column, true, i, () => rebuild());
                if (connector.IsEnergyConnector || connector.Type == ConType.ConEn) duties.Add(row);
                else feeds.Add(row);
            }

            for (int i = 0; i < column.GraphicObject.OutputConnectors.Count; i++)
            {
                var connector = column.GraphicObject.OutputConnectors[i];
                var row = new ConnectorRow(column, false, i, () => rebuild());
                if (connector.IsEnergyConnector || connector.Type == ConType.ConEn) duties.Add(row);
                else if (connector.ConnectorName.ToLower().Contains("side")) sideDraws.Add(row);
                else products.Add(row);
            }

            tabs.Items.Add(new TabItem { Header = "Feeds", Content = ConnectorGrid(feeds, streamTags) });
            tabs.Items.Add(new TabItem { Header = "Products", Content = ConnectorGrid(products, streamTags) });
            tabs.Items.Add(new TabItem { Header = "Side Draws", Content = ConnectorGrid(sideDraws, streamTags) });
            tabs.Items.Add(new TabItem { Header = "Duties", Content = ConnectorGrid(duties, energyTags) });

            FillAssociations(column, associations, sideDrawSpecs, su, nf);

            var stageNames = StageNames(column);

            var associationGrid = Grid();
            associationGrid.ItemsSource = associations;
            associationGrid.Columns.Add(TextColumn("Type", "Kind", 1.0, readOnly: true));
            associationGrid.Columns.Add(TextColumn("Stream", "Stream", 1.4, readOnly: true));
            associationGrid.Columns.Add(ComboColumn("Stage", "Stage", stageNames, 1.4));
            associationGrid.Columns.Add(ComboColumn("Position", "Position",
                new List<string> { "Above", "Below" }, 1.0));

            tabs.Items.Add(new TabItem { Header = "Stage Associations", Content = associationGrid });

            var sideDrawGrid = Grid();
            sideDrawGrid.ItemsSource = sideDrawSpecs;
            sideDrawGrid.Columns.Add(TextColumn("Stream", "Stream", 1.4, readOnly: true));
            sideDrawGrid.Columns.Add(ComboColumn("Phase", "PhaseName",
                new List<string> { "Liquid", "Vapor" }, 1.0));
            sideDrawGrid.Columns.Add(TextColumn("Molar Flow (" + su.molarflow + ")", "FlowRate", 1.2));

            tabs.Items.Add(new TabItem { Header = "Side Draw Specs", Content = sideDrawGrid });

            return host;
        }

        /// <summary>Refills the association and side draw rows from the column's stream information.</summary>
        private static void FillAssociations(Column column,
                                             ObservableCollection<AssociationRow> associations,
                                             ObservableCollection<SideDrawRow> sideDraws,
                                             IUnitsOfMeasure su, string nf)
        {
            associations.Clear();
            sideDraws.Clear();

            var names = StageNames(column);
            var ids = StageIDs(column);

            foreach (var si in column.MaterialStreams.Values)
            {
                var kind = KindOf(si.StreamBehavior);
                if (kind == null) continue;

                associations.Add(new AssociationRow(column, si, kind, names, ids));

                if (si.StreamBehavior == StreamInformation.Behavior.Sidedraw)
                    sideDraws.Add(new SideDrawRow(column, si, su, nf));
            }

            foreach (var si in column.EnergyStreams.Values)
            {
                var kind = si.StreamBehavior == StreamInformation.Behavior.Distillate
                    ? "Condenser Duty"
                    : "Reboiler Duty";

                associations.Add(new AssociationRow(column, si, kind, names, ids));
            }
        }

        private static string KindOf(StreamInformation.Behavior behavior)
        {
            switch (behavior)
            {
                case StreamInformation.Behavior.Feed: return "Feed";
                case StreamInformation.Behavior.Sidedraw: return "Side Draw";
                case StreamInformation.Behavior.Distillate: return "Distillate";
                case StreamInformation.Behavior.OverheadVapor: return "Overhead Vapor";
                case StreamInformation.Behavior.BottomsLiquid: return "Bottoms Product";
                default: return null;
            }
        }

        private static List<string> StageNames(Column column)
        {
            var names = column.Stages.Select(x => x.Name).ToList();
            names.Insert(0, "");
            return names;
        }

        private static List<string> StageIDs(Column column)
        {
            var ids = column.Stages.Select(x => x.ID).ToList();
            ids.Insert(0, "");
            return ids;
        }

        /// <summary>
        /// The stage a stream is associated with. The engine writes the stage ID from its setters
        /// and the stage name from the constructors, so both are matched.
        /// </summary>
        private static int StageIndexOf(string associated, List<string> names, List<string> ids)
        {
            if (string.IsNullOrEmpty(associated)) return 0;

            for (int i = 1; i < ids.Count; i++)
                if (ids[i] == associated || names[i] == associated) return i;

            // saved flowsheets also carry a plain stage number
            if (int.TryParse(associated, out var number) && number + 1 < names.Count) return number + 1;

            return 0;
        }

        /// <summary>Attaches or detaches a stream, as the Windows editor does from its combos.</summary>
        private static void Connect(Column column, IConnectionPoint connector, bool input, string tag)
        {
            var flowsheet = column.GetFlowsheet();
            var gobj = column.GraphicObject;

            try
            {
                if (connector.IsAttached)
                {
                    if (input) flowsheet.DisconnectObjects(connector.AttachedConnector.AttachedFrom, gobj);
                    else flowsheet.DisconnectObjects(gobj, connector.AttachedConnector.AttachedTo);
                }

                if (string.IsNullOrEmpty(tag)) return;

                var other = flowsheet.GetFlowsheetSimulationObject(tag).GraphicObject;

                if (input)
                {
                    if (connector.IsEnergyConnector)
                    {
                        if (other.InputConnectors[0].IsAttached)
                        {
                            flowsheet.ShowMessage("Selected object already connected to another object.",
                                IFlowsheet.MessageType.GeneralError);
                            return;
                        }
                        flowsheet.ConnectObjects(gobj, other, 0, 0);
                    }
                    else
                    {
                        if (other.OutputConnectors[0].IsAttached)
                        {
                            flowsheet.ShowMessage("Selected object already connected to another object.",
                                IFlowsheet.MessageType.GeneralError);
                            return;
                        }
                        flowsheet.ConnectObjects(other, gobj, 0, gobj.InputConnectors.IndexOf(connector));
                    }
                }
                else
                {
                    if (other.InputConnectors[0].IsAttached)
                    {
                        flowsheet.ShowMessage("Selected object already connected to another object.",
                            IFlowsheet.MessageType.GeneralError);
                        return;
                    }
                    flowsheet.ConnectObjects(gobj, other, gobj.OutputConnectors.IndexOf(connector), 0);
                }
            }
            catch (Exception ex)
            {
                flowsheet.ShowMessage(ex.Message, IFlowsheet.MessageType.GeneralError);
            }
        }

        private static List<string> Tags(IFlowsheet flowsheet, ObjectType type)
        {
            var tags = flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null && x.GraphicObject.ObjectType == type)
                .Select(x => x.GraphicObject.Tag)
                .OrderBy(x => x)
                .ToList();

            tags.Insert(0, "");
            return tags;
        }

        private static Control ConnectorGrid(ObservableCollection<ConnectorRow> rows, List<string> tags)
        {
            var grid = Grid();
            grid.ItemsSource = rows;
            grid.Columns.Add(TextColumn("Connector", "Name", 1.6, readOnly: true));
            grid.Columns.Add(ComboColumn("Connected Stream", "Stream", tags, 1.6));
            return grid;
        }

        private static DataGrid Grid()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };
        }

        private static DataGridTextColumn TextColumn(string header, string path, double width,
                                                     bool readOnly = false)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay },
                IsReadOnly = readOnly,
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            };
        }

        /// <summary>
        /// A column of pickers. The cell template carries the combo itself, so a click lands on
        /// the list straight away rather than having to put the cell into edit mode first.
        /// </summary>
        private static DataGridTemplateColumn ComboColumn(string header, string path,
                                                          List<string> items, double width)
        {
            return new DataGridTemplateColumn
            {
                Header = header,
                Width = new DataGridLength(width, DataGridLengthUnitType.Star),
                CellTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<object>(
                    (item, scope) =>
                    {
                        var combo = new ComboBox { ItemsSource = items, MinWidth = 80 };
                        combo.Bind(ComboBox.SelectedItemProperty,
                                   new Binding(path) { Mode = BindingMode.TwoWay });
                        return combo;
                    },
                    supportsRecycling: true)
            };
        }

    }

}
