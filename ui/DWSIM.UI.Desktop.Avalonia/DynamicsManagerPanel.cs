using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.DynamicsManager;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using static DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Avalonia port of the Eto DynamicsManagerControl.
/// Top: "Dynamic Mode Enabled" checkbox.
/// Body: TabControl with 4 tabs: Event Sets, Cause-and-Effect Matrices, Integrators, Schedules.
/// Each tab follows a master-detail layout.
/// </summary>
public sealed class DynamicsManagerPanel : DockPanel
{
    private IFlowsheet? _flowsheet;

    private readonly CheckBox _chkDynamics;

    // Event Sets tab
    private readonly ListBox _lbEventSets;
    private readonly ListBox _lbEvents;
    private readonly Panel _eventEditorHost;

    // Cause-and-Effect tab
    private readonly ListBox _lbCEM;
    private readonly ListBox _lbCEI;
    private readonly Panel _ceiEditorHost;

    // Integrators tab
    private readonly ListBox _lbIntegrators;
    private readonly Panel _integratorPropsHost;
    private readonly ListBox _lbVariables;
    private readonly Panel _mvEditorHost;

    // Schedules tab
    private readonly ListBox _lbSchedules;
    private readonly Panel _schEditorHost;

    // Lookup helpers: store the key (ID) per ListBoxItem
    // We use ListBoxItem.Tag = id string

    public DynamicsManagerPanel()
    {
        // ---- Top: Dynamic Mode checkbox ----
        _chkDynamics = new CheckBox
        {
            Content = "Dynamic Mode Enabled",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Margin = new Thickness(6, 4)
        };
        _chkDynamics.IsCheckedChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            _flowsheet.DynamicMode = _chkDynamics.IsChecked == true;
        };
        SetDock(_chkDynamics, global::Avalonia.Controls.Dock.Top);
        Children.Add(_chkDynamics);

        // ---- Tab control ----
        var tabs = new TabControl { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };

        // --- Event Sets ---
        _lbEventSets = new ListBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _lbEvents = new ListBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _eventEditorHost = new Panel();

        tabs.Items.Add(new TabItem
        {
            Header = "Event Sets",
            Content = BuildEventSetsTab()
        });

        // --- Cause-and-Effect Matrices ---
        _lbCEM = new ListBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _lbCEI = new ListBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _ceiEditorHost = new Panel();

        tabs.Items.Add(new TabItem
        {
            Header = "Cause-and-Effect Matrices",
            Content = BuildCEMTab()
        });

        // --- Integrators ---
        _lbIntegrators = new ListBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _integratorPropsHost = new Panel();
        _lbVariables = new ListBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _mvEditorHost = new Panel();

        tabs.Items.Add(new TabItem
        {
            Header = "Integrators",
            Content = BuildIntegratorsTab()
        });

        // --- Schedules ---
        _lbSchedules = new ListBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _schEditorHost = new Panel();

        tabs.Items.Add(new TabItem
        {
            Header = "Schedules",
            Content = BuildSchedulesTab()
        });

        Children.Add(tabs);

        // Wire selection-changed events
        WireEventSetsTab();
        WireCEMTab();
        WireIntegratorsTab();
        WireSchedulesTab();
    }

    public void SetFlowsheet(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _chkDynamics.IsChecked = _flowsheet.DynamicMode;
        Populate();
    }

    // =====================================================================
    //  Populate lists from the flowsheet
    // =====================================================================

    public void Populate()
    {
        if (_flowsheet == null) return;

        _chkDynamics.IsChecked = _flowsheet.DynamicMode;

        _lbEventSets.Items.Clear();
        foreach (var kvp in _flowsheet.DynamicsManager.EventSetList)
            _lbEventSets.Items.Add(MakeItem(kvp.Key, kvp.Value.Description));

        _lbCEM.Items.Clear();
        foreach (var kvp in _flowsheet.DynamicsManager.CauseAndEffectMatrixList)
            _lbCEM.Items.Add(MakeItem(kvp.Key, kvp.Value.Description));

        _lbIntegrators.Items.Clear();
        foreach (var kvp in _flowsheet.DynamicsManager.IntegratorList)
            _lbIntegrators.Items.Add(MakeItem(kvp.Key, kvp.Value.Description));

        _lbSchedules.Items.Clear();
        foreach (var kvp in _flowsheet.DynamicsManager.ScheduleList)
            _lbSchedules.Items.Add(MakeItem(kvp.Key, kvp.Value.Description));
    }

    // =====================================================================
    //  Tab builders
    // =====================================================================

    private Control BuildEventSetsTab()
    {
        // Three-pane: event sets list | events list | event editor
        var col0 = BuildListColumn("Event Sets", _lbEventSets, "Add Event Set", "Remove Event Set",
            OnAddEventSet, OnRemoveEventSet);
        var col1 = BuildListColumn("Selected Event Set", _lbEvents, "Add Event", "Remove Event",
            OnAddEvent, OnRemoveEvent);
        var col2 = BuildEditorColumn("Selected Event", _eventEditorHost);

        return MakeThreePane(col0, col1, col2);
    }

    private Control BuildCEMTab()
    {
        var col0 = BuildListColumn("Cause-and-Effect Matrices", _lbCEM, "Add Matrix", "Remove Matrix",
            OnAddCEM, OnRemoveCEM);
        var col1 = BuildListColumn("Selected Matrix", _lbCEI, "Add Item", "Remove Item",
            OnAddCEI, OnRemoveCEI);
        var col2 = BuildEditorColumn("Selected Item", _ceiEditorHost);

        return MakeThreePane(col0, col1, col2);
    }

    private Control BuildIntegratorsTab()
    {
        // Left: integrator list. Right: sub-tabs (Parameters, Monitored Variables)
        var col0 = BuildListColumn("Integrators", _lbIntegrators, "Add Integrator", "Remove Integrator",
            OnAddIntegrator, OnRemoveIntegrator);

        var subTabs = new TabControl { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        subTabs.Items.Add(new TabItem
        {
            Header = "Parameters",
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _integratorPropsHost
            }
        });

        // Monitored Variables sub-tab: list + editor
        var mvCol0 = BuildListColumn("Monitored Variables", _lbVariables, "Add Variable", "Remove Variable",
            OnAddVariable, OnRemoveVariable);
        var mvCol1 = BuildEditorColumn("Selected Variable", _mvEditorHost);

        subTabs.Items.Add(new TabItem
        {
            Header = "Monitored Variables",
            Content = MakeTwoPane(mvCol0, mvCol1)
        });

        var rightPanel = new DockPanel { Margin = new Thickness(4) };
        var rightHeader = new TextBlock
        {
            Text = "Selected Integrator",
            FontWeight = FontWeight.SemiBold,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Margin = new Thickness(4, 4, 4, 2)
        };
        SetDock(rightHeader, global::Avalonia.Controls.Dock.Top);
        rightPanel.Children.Add(rightHeader);
        rightPanel.Children.Add(subTabs);

        return MakeTwoPane(col0, rightPanel);
    }

    private Control BuildSchedulesTab()
    {
        var col0 = BuildListColumn("Schedules", _lbSchedules, "Add Schedule", "Remove Schedule",
            OnAddSchedule, OnRemoveSchedule);
        var col1 = BuildEditorColumn("Selected Schedule", _schEditorHost);

        return MakeTwoPane(col0, col1);
    }

    // =====================================================================
    //  Selection-changed wiring
    // =====================================================================

    private void WireEventSetsTab()
    {
        _lbEventSets.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            var key = SelectedKey(_lbEventSets);
            if (key == null) return;
            _lbEvents.Items.Clear();
            var es = _flowsheet.DynamicsManager.EventSetList[key];
            foreach (var kvp in es.Events)
                _lbEvents.Items.Add(MakeItem(kvp.Key, kvp.Value.Description));
        };

        _lbEvents.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            var esKey = SelectedKey(_lbEventSets);
            var evKey = SelectedKey(_lbEvents);
            if (esKey == null || evKey == null) return;
            var ev = _flowsheet.DynamicsManager.EventSetList[esKey].Events[evKey];
            PopulateEventEditor(ev);
        };
    }

    private void WireCEMTab()
    {
        _lbCEM.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            var key = SelectedKey(_lbCEM);
            if (key == null) return;
            _lbCEI.Items.Clear();
            var cem = _flowsheet.DynamicsManager.CauseAndEffectMatrixList[key];
            foreach (var kvp in cem.Items)
                _lbCEI.Items.Add(MakeItem(kvp.Key, kvp.Value.Description));
        };

        _lbCEI.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            var cemKey = SelectedKey(_lbCEM);
            var ceiKey = SelectedKey(_lbCEI);
            if (cemKey == null || ceiKey == null) return;
            var item = _flowsheet.DynamicsManager.CauseAndEffectMatrixList[cemKey].Items[ceiKey];
            PopulateCEIEditor(item);
        };
    }

    private void WireIntegratorsTab()
    {
        _lbIntegrators.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            var key = SelectedKey(_lbIntegrators);
            if (key == null) return;
            var integ = _flowsheet.DynamicsManager.IntegratorList[key];
            PopulateIntegratorProperties(integ);
            _lbVariables.Items.Clear();
            foreach (var mv in integ.MonitoredVariables)
                _lbVariables.Items.Add(MakeItem(mv.ID, mv.Description));
        };

        _lbVariables.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            var intKey = SelectedKey(_lbIntegrators);
            if (intKey == null || _lbVariables.SelectedIndex < 0) return;
            var integ = _flowsheet.DynamicsManager.IntegratorList[intKey];
            var mv = integ.MonitoredVariables[_lbVariables.SelectedIndex];
            PopulateMonitoredVariableEditor(mv);
        };
    }

    private void WireSchedulesTab()
    {
        _lbSchedules.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            var key = SelectedKey(_lbSchedules);
            if (key == null) return;
            var sch = _flowsheet.DynamicsManager.ScheduleList[key];
            PopulateScheduleEditor(sch);
        };
    }

    // =====================================================================
    //  Add / Remove handlers
    // =====================================================================

    private async void OnAddEventSet()
    {
        if (_flowsheet == null) return;
        var name = await ShowInputDialog("Enter a Name", "New Event Set");
        if (name == null) return;
        var es = new EventSet { ID = Guid.NewGuid().ToString(), Description = name };
        _flowsheet.DynamicsManager.EventSetList.Add(es.ID, es);
        _lbEventSets.Items.Add(MakeItem(es.ID, es.Description));
    }

    private void OnRemoveEventSet()
    {
        if (_flowsheet == null) return;
        var key = SelectedKey(_lbEventSets);
        if (key == null) return;
        _flowsheet.DynamicsManager.EventSetList.Remove(key);
        _lbEventSets.Items.RemoveAt(_lbEventSets.SelectedIndex);
        _lbEvents.Items.Clear();
        _eventEditorHost.Children.Clear();
    }

    private async void OnAddEvent()
    {
        if (_flowsheet == null) return;
        var esKey = SelectedKey(_lbEventSets);
        if (esKey == null) return;
        var name = await ShowInputDialog("Enter a Name", "New Event");
        if (name == null) return;
        var ev = new DynamicEvent { ID = Guid.NewGuid().ToString(), Description = name };
        _flowsheet.DynamicsManager.EventSetList[esKey].Events.Add(ev.ID, ev);
        _lbEvents.Items.Add(MakeItem(ev.ID, ev.Description));
    }

    private void OnRemoveEvent()
    {
        if (_flowsheet == null) return;
        var esKey = SelectedKey(_lbEventSets);
        var evKey = SelectedKey(_lbEvents);
        if (esKey == null || evKey == null) return;
        _flowsheet.DynamicsManager.EventSetList[esKey].Events.Remove(evKey);
        _lbEvents.Items.RemoveAt(_lbEvents.SelectedIndex);
        _eventEditorHost.Children.Clear();
    }

    private async void OnAddCEM()
    {
        if (_flowsheet == null) return;
        var name = await ShowInputDialog("Enter a Name", "New Matrix");
        if (name == null) return;
        var cem = new CauseAndEffectMatrix { ID = Guid.NewGuid().ToString(), Description = name };
        _flowsheet.DynamicsManager.CauseAndEffectMatrixList.Add(cem.ID, cem);
        _lbCEM.Items.Add(MakeItem(cem.ID, cem.Description));
    }

    private void OnRemoveCEM()
    {
        if (_flowsheet == null) return;
        var key = SelectedKey(_lbCEM);
        if (key == null) return;
        _flowsheet.DynamicsManager.CauseAndEffectMatrixList.Remove(key);
        _lbCEM.Items.RemoveAt(_lbCEM.SelectedIndex);
        _lbCEI.Items.Clear();
        _ceiEditorHost.Children.Clear();
    }

    private async void OnAddCEI()
    {
        if (_flowsheet == null) return;
        var cemKey = SelectedKey(_lbCEM);
        if (cemKey == null) return;
        var name = await ShowInputDialog("Enter a Name", "New Item");
        if (name == null) return;
        var cei = new CauseAndEffectItem { ID = Guid.NewGuid().ToString(), Description = name };
        _flowsheet.DynamicsManager.CauseAndEffectMatrixList[cemKey].Items.Add(cei.ID, cei);
        _lbCEI.Items.Add(MakeItem(cei.ID, cei.Description));
    }

    private void OnRemoveCEI()
    {
        if (_flowsheet == null) return;
        var cemKey = SelectedKey(_lbCEM);
        var ceiKey = SelectedKey(_lbCEI);
        if (cemKey == null || ceiKey == null) return;
        _flowsheet.DynamicsManager.CauseAndEffectMatrixList[cemKey].Items.Remove(ceiKey);
        _lbCEI.Items.RemoveAt(_lbCEI.SelectedIndex);
        _ceiEditorHost.Children.Clear();
    }

    private async void OnAddIntegrator()
    {
        if (_flowsheet == null) return;
        var name = await ShowInputDialog("Enter a Name", "New Integrator");
        if (name == null) return;
        var integ = new Integrator { ID = Guid.NewGuid().ToString(), Description = name };
        _flowsheet.DynamicsManager.IntegratorList.Add(integ.ID, integ);
        _lbIntegrators.Items.Add(MakeItem(integ.ID, integ.Description));
    }

    private void OnRemoveIntegrator()
    {
        if (_flowsheet == null) return;
        var key = SelectedKey(_lbIntegrators);
        if (key == null) return;
        _flowsheet.DynamicsManager.IntegratorList.Remove(key);
        _lbIntegrators.Items.RemoveAt(_lbIntegrators.SelectedIndex);
        _integratorPropsHost.Children.Clear();
        _lbVariables.Items.Clear();
        _mvEditorHost.Children.Clear();
    }

    private async void OnAddVariable()
    {
        if (_flowsheet == null) return;
        var intKey = SelectedKey(_lbIntegrators);
        if (intKey == null) return;
        var name = await ShowInputDialog("Enter a Name", "New Variable");
        if (name == null) return;
        var mv = new MonitoredVariable { ID = Guid.NewGuid().ToString(), Description = name };
        _flowsheet.DynamicsManager.IntegratorList[intKey].MonitoredVariables.Add(mv);
        _lbVariables.Items.Add(MakeItem(mv.ID, mv.Description));
    }

    private void OnRemoveVariable()
    {
        if (_flowsheet == null) return;
        var intKey = SelectedKey(_lbIntegrators);
        if (intKey == null || _lbVariables.SelectedIndex < 0) return;
        _flowsheet.DynamicsManager.IntegratorList[intKey].MonitoredVariables.RemoveAt(_lbVariables.SelectedIndex);
        _lbVariables.Items.RemoveAt(_lbVariables.SelectedIndex);
        _mvEditorHost.Children.Clear();
    }

    private async void OnAddSchedule()
    {
        if (_flowsheet == null) return;
        var name = await ShowInputDialog("Enter a Name", "New Schedule");
        if (name == null) return;
        var sch = new Schedule { ID = Guid.NewGuid().ToString(), Description = name };
        _flowsheet.DynamicsManager.ScheduleList.Add(sch.ID, sch);
        _lbSchedules.Items.Add(MakeItem(sch.ID, sch.Description));
    }

    private void OnRemoveSchedule()
    {
        if (_flowsheet == null) return;
        var key = SelectedKey(_lbSchedules);
        if (key == null) return;
        _flowsheet.DynamicsManager.ScheduleList.Remove(key);
        _lbSchedules.Items.RemoveAt(_lbSchedules.SelectedIndex);
        _schEditorHost.Children.Clear();
    }

    // =====================================================================
    //  Property editor builders
    // =====================================================================

    private void PopulateEventEditor(IDynamicsEvent ev)
    {
        var layout = new StackPanel { Spacing = 4, Margin = new Thickness(6) };

        layout.Children.Add(MakeCheckBoxRow("Active", ev.Enabled, v => ev.Enabled = v));

        layout.Children.Add(MakeReadOnlyRow("ID", ev.ID));

        layout.Children.Add(MakeTextRow("Name", ev.Description, v =>
        {
            ev.Description = v;
            UpdateSelectedItemText(_lbEvents, v);
        }));

        // Event Type
        var eventTypes = new List<string> { "Change Property", "Run Script" };
        layout.Children.Add(MakeDropDownRow("Type", eventTypes, (int)ev.EventType, idx =>
        {
            ev.EventType = (DynamicsEventType)idx;
        }));

        // Object / Property selector
        var objectNames = GetSimulationObjectNames();
        objectNames.Insert(0, "");
        var propIds = new List<string> { "" };
        var propNames = new List<string>();

        int objIdx = 0;
        if (!string.IsNullOrEmpty(ev.SimulationObjectID) &&
            _flowsheet!.SimulationObjects.ContainsKey(ev.SimulationObjectID))
        {
            objIdx = objectNames.IndexOf(_flowsheet.SimulationObjects[ev.SimulationObjectID].GraphicObject.Tag);
            propIds.AddRange(_flowsheet.SimulationObjects[ev.SimulationObjectID].GetProperties(PropertyType.WR));
            propNames.AddRange(propIds.Select(x => _flowsheet.GetTranslatedString(x)));
        }

        ComboBox? propSelector = null;
        layout.Children.Add(MakeDropDownRow("Object", objectNames, objIdx, idx =>
        {
            if (idx > 0)
            {
                ev.SimulationObjectID = _flowsheet!.GetFlowsheetSimulationObject(objectNames[idx]).Name;
                propIds.Clear();
                propIds.Add("");
                propIds.AddRange(_flowsheet.SimulationObjects[ev.SimulationObjectID].GetProperties(PropertyType.WR));
                propNames.Clear();
                propNames.AddRange(propIds.Select(x => _flowsheet.GetTranslatedString(x)));
                if (propSelector != null)
                {
                    propSelector.Items.Clear();
                    foreach (var p in propNames) propSelector.Items.Add(p);
                }
            }
            else
            {
                ev.SimulationObjectID = "";
            }
        }));

        int propIdx = propNames.Count > 0
            ? Math.Max(0, propNames.IndexOf(_flowsheet!.GetTranslatedString(ev.SimulationObjectProperty)))
            : -1;
        propSelector = MakeComboBox(propNames, propIdx, idx =>
        {
            if (idx >= 0 && idx < propIds.Count) ev.SimulationObjectProperty = propIds[idx];
        });
        layout.Children.Add(MakeLabeledRow("Property", propSelector));

        layout.Children.Add(MakeTextRow("Value", ev.SimulationObjectPropertyValue, v => ev.SimulationObjectPropertyValue = v));
        layout.Children.Add(MakeTextRow("Units", ev.SimulationObjectPropertyUnits, v => ev.SimulationObjectPropertyUnits = v));

        // Transition
        var transTypes = new List<string> { "Step", "Linear", "Log", "Inverse Log", "Random" };
        // Note: enum values are 0,1,3,4,5 - map to combo indices 0,1,2,3,4
        int transIdx = ev.TransitionType switch
        {
            DynamicsEventTransitionType.StepChange => 0,
            DynamicsEventTransitionType.LinearChange => 1,
            DynamicsEventTransitionType.LogChange => 2,
            _ when (int)ev.TransitionType == 4 => 3, // InverseLogChange
            _ when (int)ev.TransitionType == 5 => 4, // RandomChange
            _ => 0
        };
        layout.Children.Add(MakeDropDownRow("Transition Type", transTypes, transIdx, idx =>
        {
            int[] vals = { 0, 1, 3, 4, 5 };
            ev.TransitionType = (DynamicsEventTransitionType)vals[idx];
        }));

        var transRefs = new List<string> { "Initial State", "Previous Event", "Reference Event" };
        layout.Children.Add(MakeDropDownRow("Transition Reference", transRefs, (int)ev.TransitionReference, idx =>
        {
            ev.TransitionReference = (DynamicsEventTransitionReferenceType)idx;
        }));

        layout.Children.Add(MakeTextRow("Transition Reference Event ID", ev.TransitionReferenceEventID,
            v => ev.TransitionReferenceEventID = v));

        _eventEditorHost.Children.Clear();
        _eventEditorHost.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = layout
        });
    }

    private void PopulateCEIEditor(IDynamicsCauseAndEffectItem cei)
    {
        var layout = new StackPanel { Spacing = 4, Margin = new Thickness(6) };

        layout.Children.Add(MakeCheckBoxRow("Active", cei.Enabled, v => cei.Enabled = v));
        layout.Children.Add(MakeTextRow("Name", cei.Description, v =>
        {
            cei.Description = v;
            UpdateSelectedItemText(_lbCEI, v);
        }));

        // Indicator selector (only Indicator-class objects)
        var indicators = _flowsheet!.SimulationObjects.Values
            .Where(x => x.ObjectClass == SimulationObjectClass.Indicators)
            .Select(x => x.GraphicObject.Tag).ToList();
        indicators.Insert(0, "");

        int indIdx = 0;
        if (!string.IsNullOrEmpty(cei.AssociatedIndicator) &&
            _flowsheet.SimulationObjects.ContainsKey(cei.AssociatedIndicator))
        {
            indIdx = indicators.IndexOf(_flowsheet.SimulationObjects[cei.AssociatedIndicator].GraphicObject.Tag);
            if (indIdx < 0) indIdx = 0;
        }
        layout.Children.Add(MakeDropDownRow("Indicator", indicators, indIdx, idx =>
        {
            if (idx > 0)
                cei.AssociatedIndicator = _flowsheet.GetFlowsheetSimulationObject(indicators[idx]).Name;
            else
                cei.AssociatedIndicator = "";
        }));

        var alarmTypes = new List<string> { "LL", "L", "H", "HH" };
        layout.Children.Add(MakeDropDownRow("Alarm Type", alarmTypes, (int)cei.AssociatedIndicatorAlarm, idx =>
        {
            cei.AssociatedIndicatorAlarm = (DynamicsAlarmType)idx;
        }));

        // Object / Property selector
        var objectNames = GetSimulationObjectNames();
        objectNames.Insert(0, "");
        var propIds = new List<string> { "" };
        var propNames = new List<string>();

        int objIdx = 0;
        if (!string.IsNullOrEmpty(cei.SimulationObjectID) &&
            _flowsheet.SimulationObjects.ContainsKey(cei.SimulationObjectID))
        {
            objIdx = objectNames.IndexOf(_flowsheet.SimulationObjects[cei.SimulationObjectID].GraphicObject.Tag);
            propIds.AddRange(_flowsheet.SimulationObjects[cei.SimulationObjectID].GetProperties(PropertyType.WR));
            propNames.AddRange(propIds.Select(x => _flowsheet.GetTranslatedString(x)));
        }

        ComboBox? propSelector = null;
        layout.Children.Add(MakeDropDownRow("Object", objectNames, objIdx, idx =>
        {
            if (idx > 0)
            {
                cei.SimulationObjectID = _flowsheet.GetFlowsheetSimulationObject(objectNames[idx]).Name;
                propIds.Clear();
                propIds.Add("");
                propIds.AddRange(_flowsheet.SimulationObjects[cei.SimulationObjectID].GetProperties(PropertyType.WR));
                propNames.Clear();
                propNames.AddRange(propIds.Select(x => _flowsheet.GetTranslatedString(x)));
                if (propSelector != null)
                {
                    propSelector.Items.Clear();
                    foreach (var p in propNames) propSelector.Items.Add(p);
                }
            }
            else
            {
                cei.SimulationObjectID = "";
            }
        }));

        int propIdx = propNames.Count > 0
            ? Math.Max(0, propNames.IndexOf(_flowsheet.GetTranslatedString(cei.SimulationObjectProperty)))
            : -1;
        propSelector = MakeComboBox(propNames, propIdx, idx =>
        {
            if (idx >= 0 && idx < propIds.Count) cei.SimulationObjectProperty = propIds[idx];
        });
        layout.Children.Add(MakeLabeledRow("Property", propSelector));

        layout.Children.Add(MakeTextRow("Value", cei.SimulationObjectPropertyValue, v => cei.SimulationObjectPropertyValue = v));
        layout.Children.Add(MakeTextRow("Units", cei.SimulationObjectPropertyUnits, v => cei.SimulationObjectPropertyUnits = v));

        _ceiEditorHost.Children.Clear();
        _ceiEditorHost.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = layout
        });
    }

    private void PopulateIntegratorProperties(IDynamicsIntegrator integ)
    {
        var layout = new StackPanel { Spacing = 4, Margin = new Thickness(6) };

        layout.Children.Add(MakeSectionHeader("Duration"));

        double days = integ.Duration.Days;
        double hours = integ.Duration.Hours;
        double minutes = integ.Duration.Minutes;
        double seconds = integ.Duration.Seconds;

        void UpdateDuration() => integ.Duration = new TimeSpan((int)days, (int)hours, (int)minutes, (int)seconds);

        layout.Children.Add(MakeNumericRow("Days", days, 0, 1000, 0, v => { days = v; UpdateDuration(); }));
        layout.Children.Add(MakeNumericRow("Hours", hours, 0, 23, 0, v => { hours = v; UpdateDuration(); }));
        layout.Children.Add(MakeNumericRow("Minutes", minutes, 0, 59, 0, v => { minutes = v; UpdateDuration(); }));
        layout.Children.Add(MakeNumericRow("Seconds", seconds, 0, 59, 0, v => { seconds = v; UpdateDuration(); }));

        layout.Children.Add(MakeSectionHeader("Integration"));

        layout.Children.Add(MakeNumericRow("Integration Step (ms)",
            integ.IntegrationStep.TotalMilliseconds, 100, int.MaxValue, 0,
            v => integ.IntegrationStep = TimeSpan.FromMilliseconds(v)));

        layout.Children.Add(MakeNumericRow("Real-Time Step (ms)",
            integ.RealTimeStepMs, 1, int.MaxValue, 0,
            v => integ.RealTimeStepMs = (int)v));

        layout.Children.Add(MakeSectionHeader("Integration Method"));

        var methods = new List<string> { "Explicit Euler", "Runge-Kutta 4 (Step Doubling)", "Implicit Euler", "Adaptive RK4/5" };
        layout.Children.Add(MakeDropDownRow("Method", methods, (int)integ.IntegrationMethod, idx =>
        {
            integ.IntegrationMethod = (IntegrationMethod)idx;
        }));

        layout.Children.Add(MakeNumericRow("Error Tolerance",
            integ.ErrorTolerance, 1e-10, 1.0, 6,
            v => integ.ErrorTolerance = v));

        layout.Children.Add(MakeNumericRow("Max Iterations (Implicit)",
            integ.MaxIterations, 1, 200, 0,
            v => integ.MaxIterations = (int)v));

        layout.Children.Add(MakeNumericRow("Convergence Tolerance",
            integ.ConvergenceTolerance, 1e-12, 1.0, 8,
            v => integ.ConvergenceTolerance = v));

        layout.Children.Add(MakeNumericRow("Minimum Step (ms)",
            integ.MinimumStep.TotalMilliseconds, 1, int.MaxValue, 0,
            v => integ.MinimumStep = TimeSpan.FromMilliseconds(v)));

        layout.Children.Add(MakeNumericRow("Maximum Step (ms)",
            integ.MaximumStep.TotalMilliseconds, 1, int.MaxValue, 0,
            v => integ.MaximumStep = TimeSpan.FromMilliseconds(v)));

        layout.Children.Add(MakeSectionHeader("Calculation Rates"));

        layout.Children.Add(MakeNumericRow("Equilibrium Flash",
            integ.CalculationRateEquilibrium, 1, 100, 0,
            v => integ.CalculationRateEquilibrium = (int)v));

        layout.Children.Add(MakeNumericRow("Pressure-Flow Relations",
            integ.CalculationRatePressureFlow, 1, 100, 0,
            v => integ.CalculationRatePressureFlow = (int)v));

        layout.Children.Add(MakeNumericRow("Controller Updates",
            integ.CalculationRateControl, 1, 100, 0,
            v => integ.CalculationRateControl = (int)v));

        _integratorPropsHost.Children.Clear();
        _integratorPropsHost.Children.Add(layout);
    }

    private void PopulateMonitoredVariableEditor(IDynamicsMonitoredVariable mv)
    {
        var layout = new StackPanel { Spacing = 4, Margin = new Thickness(6) };

        layout.Children.Add(MakeTextRow("Name", mv.Description, v =>
        {
            mv.Description = v;
            UpdateSelectedItemText(_lbVariables, v);
        }));

        // Object selector
        var objectNames = GetSimulationObjectNames();
        objectNames.Insert(0, "");
        var propIds = new List<string> { "" };
        var propNames = new List<string>();

        int objIdx = 0;
        if (!string.IsNullOrEmpty(mv.ObjectID) &&
            _flowsheet!.SimulationObjects.ContainsKey(mv.ObjectID))
        {
            objIdx = objectNames.IndexOf(_flowsheet.SimulationObjects[mv.ObjectID].GraphicObject.Tag);
            propIds.AddRange(_flowsheet.SimulationObjects[mv.ObjectID].GetProperties(PropertyType.ALL));
            propNames.AddRange(propIds.Select(x => _flowsheet.GetTranslatedString(x)));
        }

        ComboBox? propSelector = null;
        layout.Children.Add(MakeDropDownRow("Object", objectNames, objIdx, idx =>
        {
            if (idx > 0)
            {
                mv.ObjectID = _flowsheet!.GetFlowsheetSimulationObject(objectNames[idx]).Name;
                propIds.Clear();
                propIds.Add("");
                propIds.AddRange(_flowsheet.SimulationObjects[mv.ObjectID].GetProperties(PropertyType.ALL));
                propNames.Clear();
                propNames.AddRange(propIds.Select(x => _flowsheet.GetTranslatedString(x)));
                if (propSelector != null)
                {
                    propSelector.Items.Clear();
                    foreach (var p in propNames) propSelector.Items.Add(p);
                }
            }
            else
            {
                mv.ObjectID = "";
            }
        }));

        int propIdx = propNames.Count > 0
            ? Math.Max(0, propNames.IndexOf(_flowsheet!.GetTranslatedString(mv.PropertyID)))
            : -1;
        propSelector = MakeComboBox(propNames, propIdx, idx =>
        {
            if (idx >= 0 && idx < propIds.Count) mv.PropertyID = propIds[idx];
        });
        layout.Children.Add(MakeLabeledRow("Property", propSelector));

        layout.Children.Add(MakeNumericRow("Min Chart Axis Value",
            mv.MinimumChartAxisValue, double.MinValue, double.MaxValue, 4,
            v => mv.MinimumChartAxisValue = v));

        layout.Children.Add(MakeNumericRow("Max Chart Axis Value",
            mv.MaximumChartAxisValue, double.MinValue, double.MaxValue, 4,
            v => mv.MaximumChartAxisValue = v));

        layout.Children.Add(MakeTextRow("Units", mv.PropertyUnits, v => mv.PropertyUnits = v));

        _mvEditorHost.Children.Clear();
        _mvEditorHost.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = layout
        });
    }

    private void PopulateScheduleEditor(IDynamicsSchedule sch)
    {
        var layout = new StackPanel { Spacing = 4, Margin = new Thickness(6) };

        // Integrator selector
        var integrators = _flowsheet!.DynamicsManager.IntegratorList.Values.ToList();
        var intNames = integrators.Select(x => x.Description).ToList();
        var intIds = integrators.Select(x => x.ID).ToList();
        intNames.Insert(0, "");
        intIds.Insert(0, "");

        layout.Children.Add(MakeDropDownRow("Selected Integrator", intNames, intIds.IndexOf(sch.CurrentIntegrator), idx =>
        {
            sch.CurrentIntegrator = intIds[Math.Max(0, idx)];
        }));

        layout.Children.Add(MakeSeparator());

        // Event Set
        layout.Children.Add(MakeCheckBoxRow("Use Event Set", sch.UsesEventList, v => sch.UsesEventList = v));

        var events = _flowsheet.DynamicsManager.EventSetList.Values.ToList();
        var evNames = events.Select(x => x.Description).ToList();
        var evIds = events.Select(x => x.ID).ToList();
        evNames.Insert(0, "");
        evIds.Insert(0, "");

        layout.Children.Add(MakeDropDownRow("Selected Event Set", evNames, evIds.IndexOf(sch.CurrentEventList), idx =>
        {
            sch.CurrentEventList = evIds[Math.Max(0, idx)];
        }));

        layout.Children.Add(MakeSeparator());

        // Cause-and-Effect Matrix
        layout.Children.Add(MakeCheckBoxRow("Use Cause-and-Effect Matrix", sch.UsesCauseAndEffectMatrix,
            v => sch.UsesCauseAndEffectMatrix = v));

        var cems = _flowsheet.DynamicsManager.CauseAndEffectMatrixList.Values.ToList();
        var cemNames = cems.Select(x => x.Description).ToList();
        var cemIds = cems.Select(x => x.ID).ToList();
        cemNames.Insert(0, "");
        cemIds.Insert(0, "");

        layout.Children.Add(MakeDropDownRow("Selected Cause-and-Effect Matrix", cemNames,
            cemIds.IndexOf(sch.CurrentCauseAndEffectMatrix), idx =>
            {
                sch.CurrentCauseAndEffectMatrix = cemIds[Math.Max(0, idx)];
            }));

        layout.Children.Add(MakeSeparator());

        // Flowsheet state
        var fstates = _flowsheet.StoredSolutions.Keys.ToList();
        fstates.Insert(0, "");

        layout.Children.Add(MakeDropDownRow("Initial Flowsheet State", fstates,
            fstates.IndexOf(sch.InitialFlowsheetStateID), idx =>
            {
                sch.InitialFlowsheetStateID = fstates[Math.Max(0, idx)];
            }));

        layout.Children.Add(MakeCheckBoxRow("Use Current State as Initial", sch.UseCurrentStateAsInitial,
            v => sch.UseCurrentStateAsInitial = v));

        layout.Children.Add(MakeSeparator());

        layout.Children.Add(MakeCheckBoxRow("Reset/Clear Contents of All Volume-Defined Objects Before Running",
            sch.ResetContentsOfAllObjects, v => sch.ResetContentsOfAllObjects = v));

        _schEditorHost.Children.Clear();
        _schEditorHost.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = layout
        });
    }

    // =====================================================================
    //  UI helper builders
    // =====================================================================

    /// <summary>Build a column with header, add/remove buttons, and a ListBox.</summary>
    private static DockPanel BuildListColumn(string title, ListBox listBox,
        string addTip, string removeTip, Action onAdd, Action onRemove)
    {
        var panel = new DockPanel { Margin = new Thickness(4), Width = 230 };

        var header = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Margin = new Thickness(2, 2, 2, 4)
        };
        SetDock(header, global::Avalonia.Controls.Dock.Top);
        panel.Children.Add(header);

        var btnAdd = new Button { Content = "+", FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12), Width = 28, Height = 28, Padding = new Thickness(0) };
        btnAdd.Classes.Add("compact");
        ToolTip.SetTip(btnAdd, addTip);
        btnAdd.Click += (_, _) => onAdd();

        var btnRemove = new Button { Content = "-", FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12), Width = 28, Height = 28, Padding = new Thickness(0) };
        btnRemove.Classes.Add("compact");
        ToolTip.SetTip(btnRemove, removeTip);
        btnRemove.Click += (_, _) => onRemove();

        var toolbar = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 4)
        };
        toolbar.Children.Add(btnAdd);
        toolbar.Children.Add(btnRemove);
        SetDock(toolbar, global::Avalonia.Controls.Dock.Top);
        panel.Children.Add(toolbar);

        panel.Children.Add(listBox);
        return panel;
    }

    /// <summary>Build an editor column with header + host panel.</summary>
    private static DockPanel BuildEditorColumn(string title, Panel host)
    {
        var panel = new DockPanel { Margin = new Thickness(4) };
        var header = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Margin = new Thickness(2, 2, 2, 4)
        };
        SetDock(header, global::Avalonia.Controls.Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(host);
        return panel;
    }

    /// <summary>Three-pane horizontal splitter layout.</summary>
    private static Grid MakeThreePane(Control left, Control middle, Control right)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("230,4,230,4,*")
        };
        Grid.SetColumn(left, 0);
        var splitter1 = new GridSplitter { Width = 4 };
        Grid.SetColumn(splitter1, 1);
        Grid.SetColumn(middle, 2);
        var splitter2 = new GridSplitter { Width = 4 };
        Grid.SetColumn(splitter2, 3);
        Grid.SetColumn(right, 4);

        grid.Children.Add(left);
        grid.Children.Add(splitter1);
        grid.Children.Add(middle);
        grid.Children.Add(splitter2);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>Two-pane horizontal splitter layout.</summary>
    private static Grid MakeTwoPane(Control left, Control right)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("230,4,*")
        };
        Grid.SetColumn(left, 0);
        var splitter = new GridSplitter { Width = 4 };
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(right, 2);

        grid.Children.Add(left);
        grid.Children.Add(splitter);
        grid.Children.Add(right);
        return grid;
    }

    // --- Form row helpers ---

    private static StackPanel MakeLabeledRow(string label, Control control)
    {
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = label, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(0, 2, 0, 0) });
        row.Children.Add(control);
        return row;
    }

    private static StackPanel MakeTextRow(string label, string value, Action<string> onChanged)
    {
        var tb = new TextBox { Text = value, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), MaxWidth = 400 };
        tb.LostFocus += (_, _) => onChanged(tb.Text ?? "");
        return MakeLabeledRow(label, tb);
    }

    private static StackPanel MakeReadOnlyRow(string label, string value)
    {
        var tb = new TextBox
        {
            Text = value,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            IsReadOnly = true,
            MaxWidth = 400,
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
        };
        return MakeLabeledRow(label, tb);
    }

    private static StackPanel MakeCheckBoxRow(string label, bool value, Action<bool> onChanged)
    {
        var cb = new CheckBox { Content = label, IsChecked = value, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        cb.IsCheckedChanged += (_, _) => onChanged(cb.IsChecked == true);
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(cb);
        return row;
    }

    private static StackPanel MakeDropDownRow(string label, List<string> items, int selectedIndex, Action<int> onChanged)
    {
        var cb = MakeComboBox(items, selectedIndex, onChanged);
        return MakeLabeledRow(label, cb);
    }

    private static ComboBox MakeComboBox(List<string> items, int selectedIndex, Action<int> onChanged)
    {
        var cb = new ComboBox { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), MaxWidth = 400 };
        foreach (var item in items) cb.Items.Add(item);
        cb.SelectedIndex = Math.Max(-1, Math.Min(selectedIndex, items.Count - 1));
        cb.SelectionChanged += (_, _) => { if (cb.SelectedIndex >= 0) onChanged(cb.SelectedIndex); };
        return cb;
    }

    // NumericUpDown works in decimal, whose range (about +/-7.9e28) is far narrower than double.
    // A bound of double.MinValue/MaxValue - which the chart-axis rows pass - overflows the (decimal)
    // cast and crashes the whole window, so clamp into the decimal range (and map NaN to zero) first.
    private static decimal ToDecimalSafe(double d)
    {
        if (double.IsNaN(d)) return 0m;
        if (d <= -7.9e28) return decimal.MinValue;
        if (d >= 7.9e28) return decimal.MaxValue;
        return (decimal)d;
    }

    private static StackPanel MakeNumericRow(string label, double value, double min, double max, int decimals, Action<double> onChanged)
    {
        var nud = new NumericUpDown
        {
            Value = ToDecimalSafe(value),
            Minimum = ToDecimalSafe(min),
            Maximum = ToDecimalSafe(max),
            Increment = decimals > 0 ? (decimal)Math.Pow(10, -decimals) : 1,
            FormatString = decimals > 0 ? "F" + decimals : "F0",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            MaxWidth = 250
        };
        nud.ValueChanged += (_, _) =>
        {
            if (nud.Value.HasValue) onChanged((double)nud.Value.Value);
        };
        return MakeLabeledRow(label, nud);
    }

    private static TextBlock MakeSectionHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Margin = new Thickness(0, 8, 0, 2)
        };
    }

    private static Separator MakeSeparator()
    {
        return new Separator { Margin = new Thickness(0, 4) };
    }

    // --- Helpers ---

    private static ListBoxItem MakeItem(string key, string text)
    {
        return new ListBoxItem { Content = text, Tag = key };
    }

    private static string? SelectedKey(ListBox lb)
    {
        return lb.SelectedItem is ListBoxItem item ? item.Tag as string : null;
    }

    private static void UpdateSelectedItemText(ListBox lb, string text)
    {
        if (lb.SelectedItem is ListBoxItem item)
            item.Content = text;
    }

    private List<string> GetSimulationObjectNames()
    {
        if (_flowsheet == null) return new List<string>();
        return _flowsheet.SimulationObjects.Values
            .Where(x => x.GraphicObject != null)
            .Select(x => x.GraphicObject.Tag)
            .ToList();
    }

    /// <summary>Show a simple text input dialog. Returns null if cancelled.</summary>
    private async System.Threading.Tasks.Task<string?> ShowInputDialog(string prompt, string defaultValue)
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return defaultValue;

        var dialog = new Window
        {
            Title = prompt,
            Width = 350,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Icon = IconHelper.GetWindowIcon()
        };

        var tb = new TextBox { Text = defaultValue, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(12, 12, 12, 8) };
        var btnCancel = new Button { Content = "Cancel", Width = 80, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), IsCancel = true };
        btnCancel.Classes.Add("dialog");
        var btnOk = new Button { Content = "OK", Width = 80, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), IsDefault = true };
        btnOk.Classes.Add("dialog");

        string? result = null;
        btnOk.Click += (_, _) => { result = tb.Text; dialog.Close(); };
        btnCancel.Click += (_, _) => { result = null; dialog.Close(); };

        var buttons = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(12, 0, 12, 12)
        };
        buttons.Children.Add(btnCancel);
        buttons.Children.Add(btnOk);

        var root = new DockPanel();
        SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(tb);

        dialog.Content = root;
        await dialog.ShowDialog(window);
        return result;
    }
}
