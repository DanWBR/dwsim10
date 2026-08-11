using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// Creates Avalonia editor content for DWSIM simulation objects.
    ///
    /// Use this from a .NET 4.7.2 host to set FlowsheetWindow.EditorDescriptorFactory:
    ///
    ///   var factory = new AvaloniaEditorFactory(flowsheet);
    ///   flwWin.EditorDescriptorFactory = factory.CreateDescriptor;
    ///
    /// The factory looks up objects by name from the provided flowsheet and populates an
    /// AvaloniaEditorPanel using the same logic as the Classic UI's GeneralEditors.
    /// </summary>
    public sealed class AvaloniaEditorFactory
    {
        private readonly IFlowsheet _flowsheet;

        public AvaloniaEditorFactory(IFlowsheet flowsheet)
        {
            _flowsheet = flowsheet ?? throw new ArgumentNullException(nameof(flowsheet));
        }

        /// <summary>
        /// Creates an ObjectEditorDescriptor for the object with the given name.
        /// Returns a descriptor with only the Properties tab populated; other tabs remain
        /// as placeholders until their editors are ported.
        /// </summary>
        /// <summary>
        /// The appearance settings of an object on their own, for hosts that show them outside
        /// the editor: the material stream editor reaches them from the context menu.
        /// </summary>
        public static global::Avalonia.Controls.Control BuildAppearanceEditor(ISimulationObject simobj)
        {
            return AvaloniaTabBuilders.BuildAppearance(simobj);
        }

        /// <summary>
        /// Repaints the flowsheet after an annotation is edited. The host sets this; without it an
        /// edit only shows on the next refresh.
        /// </summary>
        public Action? RedrawRequested { get; set; }

        public ObjectEditorDescriptor CreateDescriptor(string objectName)
        {
            if (!_flowsheet.SimulationObjects.TryGetValue(objectName, out var simobj))
            {
                // tables, charts, text, pictures, rectangles and buttons are graphics with no
                // simulation object behind them, so they are not in SimulationObjects
                var annotation = FindGraphicObject(objectName);
                if (annotation != null)
                {
                    var apanel = AnnotationEditors.Build(annotation, _flowsheet, RedrawRequested);
                    if (apanel != null)
                        return new ObjectEditorDescriptor { PropertiesContent = apanel };
                }

                return new ObjectEditorDescriptor
                {
                    PropertiesContent = BuildNotFoundPanel(objectName)
                };
            }

            // the material stream brings the whole WinForms form, tab strip included, so it
            // does not get wrapped in the standard Connections / Properties / Results tabs.
            // Appearance is reached from the object's context menu, as in the WinForms UI.
            if (simobj is MaterialStream mstream)
            {
                return new ObjectEditorDescriptor
                {
                    FullContent = MaterialStreamTabbedEditor.Build(mstream)
                };
            }

            // the energy stream editor is laid out like its Windows counterpart
            if (simobj is DWSIM.UnitOperations.Streams.EnergyStream estream)
            {
                return new ObjectEditorDescriptor
                {
                    FullContent = EnergyStreamEditor.Build(estream)
                };
            }

            var windowsStyle = BuildWindowsStyleEditor(simobj);
            if (windowsStyle != null)
                return new ObjectEditorDescriptor { FullContent = windowsStyle };

            var panel = new AvaloniaEditorPanel();

            // The panel's OnAfterEdit getter returns null until ArmAfterEdit() is called.
            // This prevents deferred Avalonia TextChanged/SelectionChanged events (fired
            // when controls enter the visual tree) from triggering RequestCalculation.
            // OpenEditorFor calls ArmAfterEdit() after the panel is in the tree.
            GeneralEditorsAvalonia.Populate(simobj, panel);

            return new ObjectEditorDescriptor
            {
                ShowConnections      = true,
                ShowCustomProperties = true,
                ShowDynamics         = simobj.SupportsDynamicMode,
                ShowAppearance       = true,
                ShowUtilities        = true,
                PropertiesContent       = panel,
                ConnectionsContent      = AvaloniaTabBuilders.BuildConnections(simobj),
                CustomPropertiesContent = AvaloniaTabBuilders.BuildCustomProperties(simobj),
                DynamicsContent         = simobj.SupportsDynamicMode ? AvaloniaTabBuilders.BuildDynamics(simobj) : null,
                ResultsContent          = AvaloniaTabBuilders.BuildResults(simobj),
                AppearanceContent       = AvaloniaTabBuilders.BuildAppearance(simobj),
                UtilitiesContent        = AttachedUtilitiesEditor.Build(simobj)
            };
        }

        /// <summary>
        /// The editors already laid out like their Windows counterparts. Anything not listed
        /// here still gets the generic property panel until it is converted.
        /// </summary>
        private static global::Avalonia.Controls.Control BuildWindowsStyleEditor(ISimulationObject simobj)
        {
            switch (simobj)
            {
                case DWSIM.UnitOperations.UnitOperations.Heater heater:
                    return HeaterCoolerEditor.Build(heater);
                case DWSIM.UnitOperations.UnitOperations.Cooler cooler:
                    return HeaterCoolerEditor.Build(cooler);
                case DWSIM.UnitOperations.UnitOperations.Pump pump:
                    return PumpEditor.Build(pump);
                case DWSIM.UnitOperations.UnitOperations.Valve valve:
                    return ValveEditor.Build(valve);
                case DWSIM.UnitOperations.UnitOperations.Mixer mixer:
                    return MixerEditor.Build(mixer);
                case DWSIM.UnitOperations.UnitOperations.Splitter splitter:
                    return SplitterEditor.Build(splitter);
                case DWSIM.UnitOperations.UnitOperations.Tank tank:
                    return TankEditor.Build(tank);
                case DWSIM.UnitOperations.UnitOperations.HeatExchanger hx:
                    return HeatExchangerEditor.Build(hx);
                case DWSIM.UnitOperations.UnitOperations.Compressor compressor:
                    return CompressorExpanderEditor.Build(compressor);
                case DWSIM.UnitOperations.UnitOperations.Expander expander:
                    return CompressorExpanderEditor.Build(expander);
                case DWSIM.UnitOperations.UnitOperations.Pipe pipe:
                    return PipeEditor.Build(pipe);
                case DWSIM.UnitOperations.Reactors.Reactor_Conversion conversion:
                    return ReactorEditors.Build(conversion);
                case DWSIM.UnitOperations.Reactors.Reactor_Equilibrium equilibrium:
                    return ReactorEditors.Build(equilibrium);
                case DWSIM.UnitOperations.Reactors.Reactor_Gibbs gibbs:
                    return ReactorEditors.Build(gibbs);
                case DWSIM.UnitOperations.Reactors.Reactor_CSTR cstr:
                    return ReactorEditors.Build(cstr);
                case DWSIM.UnitOperations.Reactors.Reactor_PFR pfr:
                    return ReactorEditors.Build(pfr);
                case DWSIM.UnitOperations.UnitOperations.ShortcutColumn shortcut:
                    return ShortcutColumnEditor.Build(shortcut);
                case DWSIM.UnitOperations.UnitOperations.Column column:
                    return ColumnEditor.Build(column);
                case DWSIM.UnitOperations.SpecialOps.Adjust adjust:
                    return AdjustEditor.Build(adjust);
                case DWSIM.UnitOperations.SpecialOps.Spec spec:
                    return SpecEditor.Build(spec);
                case DWSIM.UnitOperations.SpecialOps.Recycle recycle:
                    return RecycleEditor.Build(recycle);
                case DWSIM.UnitOperations.SpecialOps.EnergyRecycle energyRecycle:
                    return EnergyRecycleEditor.Build(energyRecycle);
                case DWSIM.UnitOperations.SpecialOps.PIDController pid:
                    return PIDControllerEditor.Build(pid);
                case DWSIM.UnitOperations.SpecialOps.PythonController python:
                    return PythonControllerEditor.Build(python);
                case DWSIM.UnitOperations.SpecialOps.MPCController mpc:
                    return MPCControllerEditor.Build(mpc);
                case DWSIM.UnitOperations.UnitOperations.Vessel vessel:
                    return VesselEditor.Build(vessel);
                case DWSIM.UnitOperations.UnitOperations.ComponentSeparator csep:
                    return CompoundSeparatorEditor.Build(csep);
                case DWSIM.UnitOperations.UnitOperations.SolidsSeparator ssep:
                    return SolidsSeparatorEditor.Build(ssep);
                case DWSIM.UnitOperations.UnitOperations.Filter filter:
                    return FilterEditor.Build(filter);
                case DWSIM.UnitOperations.UnitOperations.OrificePlate plate:
                    return OrificePlateEditor.Build(plate);
                case DWSIM.UnitOperations.UnitOperations.ReliefValve reliefValve:
                    return ReliefValveEditor.Build(reliefValve);
                case DWSIM.UnitOperations.UnitOperations.AnalogGauge analog:
                    return GaugeEditors.Build(analog);
                case DWSIM.UnitOperations.UnitOperations.LevelGauge level:
                    return GaugeEditors.Build(level);
                case DWSIM.UnitOperations.UnitOperations.DigitalGauge digital:
                    return GaugeEditors.Build(digital);
                case DWSIM.UnitOperations.UnitOperations.Switch switchBlock:
                    return SwitchEditor.Build(switchBlock);
                case DWSIM.UnitOperations.UnitOperations.Input input:
                    return InputEditor.Build(input);
                case DWSIM.UnitOperations.SpecialOps.InformationCarrier carrier:
                    return InfoCarrierEditor.Build(carrier);
                case DWSIM.UnitOperations.UnitOperations.Flowsheet subflowsheet:
                    return FlowsheetUOEditor.Build(subflowsheet);
                case DWSIM.UnitOperations.UnitOperations.CustomUO script:
                    return ScriptUOEditor.Build(script);
                case DWSIM.UnitOperations.UnitOperations.CapeOpenUO capeOpen:
                    return CapeOpenUOEditor.Build(capeOpen);
                case DWSIM.UnitOperations.UnitOperations.SolarPanel solar:
                    return CleanEnergyEditors.Build(solar);
                case DWSIM.UnitOperations.UnitOperations.WindTurbine wind:
                    return CleanEnergyEditors.Build(wind);
                case DWSIM.UnitOperations.UnitOperations.HydroelectricTurbine hydro:
                    return CleanEnergyEditors.Build(hydro);
                case DWSIM.UnitOperations.UnitOperations.WaterElectrolyzer electrolyzer:
                    return CleanEnergyEditors.Build(electrolyzer);
                case DWSIM.UnitOperations.UnitOperations.PEMFuelCellUnitOpBase fuelCell:
                    return FuelCellEditor.Build(fuelCell);
                case DWSIM.UnitOperations.Reactors.Reactor_AnaerobicDigester digester:
                    return AnaerobicDigesterEditor.Build(digester);
                case DWSIM.UnitOperations.UnitOperations.UnitOp_BiogasUpgrader upgrader:
                    return BiogasUpgraderEditor.Build(upgrader);
                case DWSIM.UnitOperations.UnitOperations.UnitOp_CellLysis lysis:
                    return CellLysisEditor.Build(lysis);
                case DWSIM.UnitOperations.UnitOperations.UnitOp_Centrifuge centrifuge:
                    return CentrifugeEditor.Build(centrifuge);
                case DWSIM.UnitOperations.UnitOperations.UnitOp_Chromatography chromatography:
                    return ChromatographyEditor.Build(chromatography);
                case DWSIM.UnitOperations.UnitOperations.UnitOp_CrossflowUF ultrafiltration:
                    return CrossflowUFEditor.Build(ultrafiltration);
                case DWSIM.UnitOperations.UnitOperations.UnitOp_Crystallizer crystallizer:
                    return CrystallizerEditor.Build(crystallizer);
                case DWSIM.UnitOperations.Reactors.Reactor_Pretreatment pretreatment:
                    return PretreatmentEditor.Build(pretreatment);
                case DWSIM.UnitOperations.Reactors.Reactor_BioReactor bioreactor:
                    return BioReactorEditor.Build(bioreactor);
                case DWSIM.UnitOperations.Reactors.Reactor_CFBFastPyrolysis pyrolysis:
                    return CFBPyrolysisEditor.Build(pyrolysis);
                case DWSIM.UnitOperations.Reactors.Reactor_ReaktoroGibbs reaktoro:
                    return ReaktoroGibbsEditor.Build(reaktoro);
                default:
                    return null;
            }
        }

        /// <summary>Looks an annotation up on the drawing surface by its internal name.</summary>
        private IGraphicObject? FindGraphicObject(string objectName)
        {
            if (_flowsheet.GetSurface() is not DWSIM.Drawing.SkiaSharp.GraphicsSurface surface) return null;

            return surface.DrawingObjects.FirstOrDefault(o => o.Name == objectName);
        }

        private static AvaloniaEditorPanel BuildNotFoundPanel(string name)
        {
            var p = new AvaloniaEditorPanel();
            p.CreateAndAddLabelRow("Object not found");
            p.CreateAndAddDescriptionRow($"No simulation object named '{name}' exists in the flowsheet.");
            return p;
        }
    }
}
