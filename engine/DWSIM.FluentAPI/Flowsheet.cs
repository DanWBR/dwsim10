using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Automation.FluentAPI.Builders.Bioprocess;
using DWSIM.Automation.FluentAPI.Builders.CleanEnergy;
using BioReactors = DWSIM.UnitOperations.Reactors;
using BioOps = DWSIM.UnitOperations.UnitOperations;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.Streams;
using DWSIM.UnitOperations.UnitOperations;
using FB = DWSIM.FlowsheetBase.FlowsheetBase;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Root of the Fluent API. Wraps an <see cref="IFlowsheet"/> and exposes builder
    /// methods for compounds, property packages, streams, unit operations, reactions,
    /// and the solver.
    /// </summary>
    public sealed class Flowsheet
    {
        private double _x = 50;
        private double _y = 50;

        /// <summary>The underlying DWSIM flowsheet. Use this only when the Fluent surface is insufficient.</summary>
        public IFlowsheet Inner { get; }

        private Flowsheet(IFlowsheet inner) { Inner = inner; }

        /// <summary>
        /// Installs the assembly resolver that probes <c>extenders</c>, <c>unitops</c>
        /// and <c>ppacks</c> next to the running assembly. Call this once before any
        /// method that statically references Plus assemblies (LCA, TEA, refining UOs,
        /// electrolyte / ThermoPack PPs) is JITted - typically in your <c>Main</c> /
        /// process startup. <see cref="Create"/> calls it implicitly.
        /// </summary>
        public static void RegisterAssemblyResolver() => Bootstrap.RegisterAssemblyResolver();

        /// <summary>Creates a new headless flowsheet.</summary>
        public static Flowsheet Create(string name = null)
        {
            Bootstrap.RegisterAssemblyResolver();
            var fs = Bootstrap.Automation.CreateFlowsheet();
            if (!string.IsNullOrEmpty(name)) fs.FlowsheetOptions.SimulationName = name;
            return new Flowsheet(fs);
        }

        /// <summary>
        /// Wraps an <see cref="IFlowsheet"/> already living in memory - for example,
        /// the flowsheet of an open DWSIM editing session, an extender plugin, or the
        /// AI assistant host - and exposes the full Fluent surface (compounds, property
        /// packages, typed UO builders, reactions, solver, LCA / TEA) on top of it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this when you don't want to allocate a new headless flowsheet but want
        /// to script edits on an existing one. The same <see cref="IFlowsheet"/>
        /// instance is reused, so subsequent calls (graphic placement, solver, save)
        /// happen on the live document the user sees.
        /// </para>
        /// <para>
        /// Adding new <see cref="DWSIM.Interfaces.IExternalUnitOperation"/> instances
        /// (bioprocess, refining, electrolyte, advanced Plus) requires the underlying
        /// type to be either <c>FlowsheetBase</c> (used by <c>Automation3.Flowsheet2</c>,
        /// classic <c>FormFlowsheet</c> and Eto <c>UI.Forms.Flowsheet</c>) or any
        /// subclass - every standard DWSIM flowsheet host already qualifies.
        /// </para>
        /// </remarks>
        /// <param name="existing">The flowsheet to wrap (typically obtained from
        /// <c>Automation.GetMainWindow()</c>, an extender callback, or DWSIM's UI host).</param>
        /// <example>
        /// <code>
        /// // Inside a DWSIM extender plugin:
        /// public void Run(IFlowsheet flowsheet) {
        ///     var fs = Flowsheet.Wrap(flowsheet);
        ///     fs.AddHeater("H-NEW")
        ///       .WithOutletTemperature(350.Kelvin())
        ///       .WithPressureDrop(0.5.Bar());
        ///     fs.Solve();
        /// }
        /// </code>
        /// </example>
        public static Flowsheet Wrap(IFlowsheet existing)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            // Ensure Plus assemblies (LCA, TEA, refining, electrolyte, ExtensionPack)
            // remain JIT-loadable from the wrapped session's working directory too.
            Bootstrap.RegisterAssemblyResolver();
            return new Flowsheet(existing);
        }

        /// <summary>Loads a flowsheet from .dwxml or .dwxmz.</summary>
        public static Flowsheet Load(string filepath)
        {
            return new Flowsheet(Bootstrap.Automation.LoadFlowsheet(filepath));
        }

        /// <summary>Saves the flowsheet (compressed .dwxmz when <paramref name="compressed"/> is true).</summary>
        public Flowsheet Save(string filepath, bool compressed = true)
        {
            Bootstrap.Automation.SaveFlowsheet(Inner, filepath, compressed);
            return this;
        }

        /// <summary>Saves a screenshot of the flowsheet.</summary>
        public Flowsheet SaveScreenshot(string filepath)
        {
            Inner.SavePFDScreenshotToPNG(filepath);
            return this;
        }

        /// <summary>Performs natural layout on the flowsheet.</summary>
        public Flowsheet NaturalLayout()
        {
            Inner.NaturalLayout();
            return this;
        }

        // ------------------------------------------------------------- Compounds

        /// <summary>Adds a single compound by its DWSIM database name (e.g. <c>"Water"</c>, <c>"Methane"</c>).</summary>
        public Flowsheet WithCompound(string compoundName)
        {
            Inner.AddCompound(compoundName);
            return this;
        }

        /// <summary>Adds multiple compounds in one call. Equivalent to calling <see cref="WithCompound"/> for each.</summary>
        public Flowsheet WithCompounds(params string[] compoundNames)
        {
            foreach (var c in compoundNames) Inner.AddCompound(c);
            return this;
        }

        // ------------------------------------------------------ Property packages

        /// <summary>Adds a property package by name (see <see cref="PropertyPackages"/>).</summary>
        /// <remarks>Plus / DWSIMPlus PPs (electrolyte, ThermoPack, Reaktoro) require an active patron key.</remarks>
        public Flowsheet WithPropertyPackage(string name)
        {
            if (PropertyPackages.RequiresPlus(name)) License.RequirePlus();
            Inner.CreateAndAddPropertyPackage(name);
            return this;
        }

        /// <summary>Returns the names of every property package registered in the flowsheet (free + Plus that loaded successfully).</summary>
        public IReadOnlyList<string> AvailablePropertyPackages => Inner.GetAvailablePropertyPackages().ToList();

        // ------------------------------------------------------ Object placement

        private (double x, double y) NextPos()
        {
            var pos = (_x, _y);
            _x += 80;
            if (_x > 800) { _x = 50; _y += 80; }
            return pos;
        }

        /// <summary>Triggers the built-in auto-layout pass.</summary>
        public Flowsheet AutoLayout() { Inner.AutoLayout(); return this; }

        // ------------------------------------------------------------- Streams

        /// <summary>Creates a new <see cref="MaterialStream"/> tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public MaterialStreamBuilder AddMaterialStream(string tag)
        {
            var (x, y) = NextPos();
            var s = (MaterialStream)Inner.AddObject(ObjectType.MaterialStream, (int)x, (int)y, tag);
            return new MaterialStreamBuilder(this, s);
        }

        /// <summary>Creates a new <see cref="EnergyStream"/> tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public EnergyStreamBuilder AddEnergyStream(string tag)
        {
            var (x, y) = NextPos();
            var s = (EnergyStream)Inner.AddObject(ObjectType.EnergyStream, (int)x, (int)y, tag);
            return new EnergyStreamBuilder(this, s);
        }

        /// <summary>Looks up an existing <see cref="MaterialStream"/> by its tag and wraps it in a builder for further configuration / read-back.</summary>
        public MaterialStreamBuilder MaterialStream(string tag)
        {
            var so = ResolveByTag(tag);
            return new MaterialStreamBuilder(this, (MaterialStream)so);
        }

        /// <summary>Looks up an existing <see cref="EnergyStream"/> by its tag and wraps it in a builder for further configuration / read-back.</summary>
        public EnergyStreamBuilder EnergyStream(string tag)
        {
            var so = ResolveByTag(tag);
            return new EnergyStreamBuilder(this, (EnergyStream)so);
        }

        internal ISimulationObject ResolveByTag(string tag)
        {
            var match = Inner.SimulationObjects.Values
                .FirstOrDefault(o => string.Equals(o.GraphicObject?.Tag, tag, StringComparison.Ordinal));
            if (match == null)
                throw new KeyNotFoundException($"No simulation object with tag '{tag}'.");
            return match;
        }

        // -------------------------------------------------------- Unit operations

        /// <summary>Adds a Mixer unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public MixerBuilder AddMixer(string tag) => Make<Mixer, MixerBuilder>(ObjectType.Mixer, tag, (f, o) => new MixerBuilder(f, o));
        /// <summary>Adds a Splitter unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public SplitterBuilder AddSplitter(string tag) => Make<Splitter, SplitterBuilder>(ObjectType.Splitter, tag, (f, o) => new SplitterBuilder(f, o));
        /// <summary>Adds a Heater unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public HeaterBuilder AddHeater(string tag) => Make<Heater, HeaterBuilder>(ObjectType.Heater, tag, (f, o) => new HeaterBuilder(f, o));
        /// <summary>Adds a Cooler unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CoolerBuilder AddCooler(string tag) => Make<Cooler, CoolerBuilder>(ObjectType.Cooler, tag, (f, o) => new CoolerBuilder(f, o));
        /// <summary>Adds a Pump unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public PumpBuilder AddPump(string tag) => Make<Pump, PumpBuilder>(ObjectType.Pump, tag, (f, o) => new PumpBuilder(f, o));
        /// <summary>Adds a Compressor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CompressorBuilder AddCompressor(string tag) => Make<Compressor, CompressorBuilder>(ObjectType.Compressor, tag, (f, o) => new CompressorBuilder(f, o));
        /// <summary>Adds a Expander unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public ExpanderBuilder AddExpander(string tag) => Make<Expander, ExpanderBuilder>(ObjectType.Expander, tag, (f, o) => new ExpanderBuilder(f, o));
        /// <summary>Adds a Valve unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public ValveBuilder AddValve(string tag) => Make<Valve, ValveBuilder>(ObjectType.Valve, tag, (f, o) => new ValveBuilder(f, o));
        /// <summary>Adds a Pipe unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public PipeBuilder AddPipe(string tag) => Make<Pipe, PipeBuilder>(ObjectType.Pipe, tag, (f, o) => new PipeBuilder(f, o));
        /// <summary>Adds a Heat Exchanger unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public HeatExchangerBuilder AddHeatExchanger(string tag) => Make<HeatExchanger, HeatExchangerBuilder>(ObjectType.HeatExchanger, tag, (f, o) => new HeatExchangerBuilder(f, o));
        /// <summary>Adds a Component Separator unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public ComponentSeparatorBuilder AddComponentSeparator(string tag) => Make<ComponentSeparator, ComponentSeparatorBuilder>(ObjectType.ComponentSeparator, tag, (f, o) => new ComponentSeparatorBuilder(f, o));
        /// <summary>Adds a Tank unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public TankBuilder AddTank(string tag) => Make<Tank, TankBuilder>(ObjectType.Tank, tag, (f, o) => new TankBuilder(f, o));
        /// <summary>Adds a Separator unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public VesselBuilder AddSeparator(string tag) => Make<Vessel, VesselBuilder>(ObjectType.Vessel, tag, (f, o) => new VesselBuilder(f, o));
        /// <summary>Adds a Orifice Plate unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public OrificePlateBuilder AddOrificePlate(string tag) => Make<OrificePlate, OrificePlateBuilder>(ObjectType.OrificePlate, tag, (f, o) => new OrificePlateBuilder(f, o));
        /// <summary>Adds a Filter unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public FilterBuilder AddFilter(string tag) => Make<Filter, FilterBuilder>(ObjectType.Filter, tag, (f, o) => new FilterBuilder(f, o));
        /// <summary>Adds a Solids Separator unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public SolidsSeparatorBuilder AddSolidsSeparator(string tag) => Make<SolidsSeparator, SolidsSeparatorBuilder>(ObjectType.SolidSeparator, tag, (f, o) => new SolidsSeparatorBuilder(f, o));

        // Columns
        /// <summary>Adds a Shortcut Column unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public ShortcutColumnBuilder AddShortcutColumn(string tag) => Make<ShortcutColumn, ShortcutColumnBuilder>(ObjectType.ShortcutColumn, tag, (f, o) => new ShortcutColumnBuilder(f, o));
        /// <summary>Adds a Distillation Column unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public DistillationColumnBuilder AddDistillationColumn(string tag) => Make<DistillationColumn, DistillationColumnBuilder>(ObjectType.DistillationColumn, tag, (f, o) => new DistillationColumnBuilder(f, o));
        /// <summary>Adds a Absorption Column unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public AbsorptionColumnBuilder AddAbsorptionColumn(string tag) => Make<AbsorptionColumn, AbsorptionColumnBuilder>(ObjectType.AbsorptionColumn, tag, (f, o) => new AbsorptionColumnBuilder(f, o));

        // Reactors
        /// <summary>Adds a Conversion Reactor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public ConversionReactorBuilder AddConversionReactor(string tag) => Make<Reactor_Conversion, ConversionReactorBuilder>(ObjectType.RCT_Conversion, tag, (f, o) => new ConversionReactorBuilder(f, o));
        /// <summary>Adds a Equilibrium Reactor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public EquilibriumReactorBuilder AddEquilibriumReactor(string tag) => Make<Reactor_Equilibrium, EquilibriumReactorBuilder>(ObjectType.RCT_Equilibrium, tag, (f, o) => new EquilibriumReactorBuilder(f, o));
        /// <summary>Adds a Gibbs Reactor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public GibbsReactorBuilder AddGibbsReactor(string tag) => Make<Reactor_Gibbs, GibbsReactorBuilder>(ObjectType.RCT_Gibbs, tag, (f, o) => new GibbsReactorBuilder(f, o));
        /// <summary>Adds a CSTR unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CSTRBuilder AddCSTR(string tag) => Make<Reactor_CSTR, CSTRBuilder>(ObjectType.RCT_CSTR, tag, (f, o) => new CSTRBuilder(f, o));
        /// <summary>Adds a PFR unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public PFRBuilder AddPFR(string tag) => Make<Reactor_PFR, PFRBuilder>(ObjectType.RCT_PFR, tag, (f, o) => new PFRBuilder(f, o));

        /// <summary>
        /// Generic escape hatch for any unit operation in the <see cref="ObjectType"/> enum that
        /// does not have a dedicated builder yet (e.g. RefluxedAbsorber, ReboiledAbsorber, Tank, etc.).
        /// </summary>
        public GenericUnitOpBuilder AddUnitOperation(ObjectType type, string tag)
        {
            var (x, y) = NextPos();
            var so = Inner.AddObject(type, (int)x, (int)y, tag);
            return new GenericUnitOpBuilder(this, so);
        }

        // -------------------------------------------------- Clean energy (typed)

        /// <summary>Adds a Wind Turbine unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public WindTurbineBuilder AddWindTurbine(string tag) => Make<WindTurbine, WindTurbineBuilder>(ObjectType.WindTurbine, tag, (f, o) => new WindTurbineBuilder(f, o));
        /// <summary>Adds a Hydroelectric Turbine unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public HydroelectricTurbineBuilder AddHydroelectricTurbine(string tag) => Make<HydroelectricTurbine, HydroelectricTurbineBuilder>(ObjectType.HydroelectricTurbine, tag, (f, o) => new HydroelectricTurbineBuilder(f, o));
        /// <summary>Adds a Solar Panel unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public SolarPanelBuilder AddSolarPanel(string tag) => Make<SolarPanel, SolarPanelBuilder>(ObjectType.SolarPanel, tag, (f, o) => new SolarPanelBuilder(f, o));
        /// <summary>Adds a Water Electrolyzer unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public WaterElectrolyzerBuilder AddWaterElectrolyzer(string tag) => Make<WaterElectrolyzer, WaterElectrolyzerBuilder>(ObjectType.WaterElectrolyzer, tag, (f, o) => new WaterElectrolyzerBuilder(f, o));
        /// <summary>Adds a PEMFuel Cell unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public PEMFuelCellBuilder AddPEMFuelCell(string tag) => Make<PEMFC_Amphlett, PEMFuelCellBuilder>(ObjectType.PEMFuelCell, tag, (f, o) => new PEMFuelCellBuilder(f, o));

        /// <summary>Adds a Reaktoro Gibbs Reactor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public ReaktoroGibbsBuilder AddReaktoroGibbsReactor(string tag) => Make<Reactor_ReaktoroGibbs, ReaktoroGibbsBuilder>(ObjectType.RCT_GibbsReaktoro, tag, (f, o) => new ReaktoroGibbsBuilder(f, o));

        // ------------------------- Bioprocess + Refining + Plus UOs (by display name)

        /// <summary>
        /// Adds an external unit operation (bioprocess, refining, advanced heat exchanger, fired heater,
        /// pipe network, etc.) by its <see cref="ISimulationObject.GetDisplayName"/> string.
        /// The flowsheet's <see cref="IFlowsheet.AvailableSimulationObjects"/> registry is searched for
        /// a template whose display name matches; that template's <c>IExternalUnitOperation.ReturnInstance</c>
        /// is called to create a fresh instance, which is then placed on the surface.
        ///
        /// Plus / DWSIMPlus components (refining, advanced HX, fired heater, etc.) require an active
        /// patron key - call <see cref="License.Activate"/> first or <see cref="ExternalCatalog.RequiresPlus"/>
        /// will throw.
        /// </summary>
        /// <param name="displayName">Display name of the UO, e.g. <c>"Anaerobic Digester"</c>, <c>"Shortcut FCC"</c>.
        /// See <see cref="ExternalCatalog"/> for the canonical constants.</param>
        /// <param name="tag">User-visible tag for the new instance.</param>
        public GenericUnitOpBuilder AddExternalUnitOperation(string displayName, string tag)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("displayName is required", nameof(displayName));
            if (ExternalCatalog.RequiresPlus(displayName)) License.RequirePlus();

            var registry = Inner.AvailableSimulationObjects;
            if (!registry.TryGetValue(displayName, out var template))
                throw new KeyNotFoundException(
                    "No external unit operation template with display name '" + displayName +
                    "'. Verify that the unit-op DLL is present in the 'unitops' folder. " +
                    "Use AvailableExternalUnitOperationNames to list what is registered.");

            if (!(template is IExternalUnitOperation external))
                throw new InvalidOperationException(
                    "Template '" + displayName + "' does not implement IExternalUnitOperation; " +
                    "use the typed AddXxx method or AddUnitOperation(ObjectType, tag) instead.");

            var fresh = (ISimulationObject)external.ReturnInstance(template.GetType().AssemblyQualifiedName);

            var fb = (FB)Inner;
            var (x, y) = NextPos();
            var id = fb.AddObjectToSurface(ObjectType.External, (int)x, (int)y, tag, "", (IExternalUnitOperation)fresh, false);
            var so = Inner.SimulationObjects[id];
            return new GenericUnitOpBuilder(this, so);
        }

        // ===================== Bioprocess (free, IExternalUnitOperation path) =====================

        /// <summary>Adds a Bio Reactor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public BioReactorBuilder AddBioReactor(string tag) => MakeExternal<BioReactors.Reactor_BioReactor, BioReactorBuilder>(ExternalCatalog.Bioprocess.BioReactor, tag, (f, o) => new BioReactorBuilder(f, o));
        /// <summary>Adds a Anaerobic Digester unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public AnaerobicDigesterBuilder AddAnaerobicDigester(string tag) => MakeExternal<BioReactors.Reactor_AnaerobicDigester, AnaerobicDigesterBuilder>(ExternalCatalog.Bioprocess.AnaerobicDigester, tag, (f, o) => new AnaerobicDigesterBuilder(f, o));
        /// <summary>Adds a CFBFast Pyrolysis Reactor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CFBFastPyrolysisBuilder AddCFBFastPyrolysisReactor(string tag) => MakeExternal<BioReactors.Reactor_CFBFastPyrolysis, CFBFastPyrolysisBuilder>(ExternalCatalog.Bioprocess.CFBFastPyrolysis, tag, (f, o) => new CFBFastPyrolysisBuilder(f, o));
        /// <summary>Adds a Pretreatment Reactor unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public PretreatmentBuilder AddPretreatmentReactor(string tag) => MakeExternal<BioReactors.Reactor_Pretreatment, PretreatmentBuilder>(ExternalCatalog.Bioprocess.PretreatmentReactor, tag, (f, o) => new PretreatmentBuilder(f, o));
        /// <summary>Adds a Biogas Upgrader unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public BiogasUpgraderBuilder AddBiogasUpgrader(string tag) => MakeExternal<BioOps.UnitOp_BiogasUpgrader, BiogasUpgraderBuilder>(ExternalCatalog.Bioprocess.BiogasUpgrader, tag, (f, o) => new BiogasUpgraderBuilder(f, o));
        /// <summary>Adds a Cell Lysis unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CellLysisBuilder AddCellLysis(string tag) => MakeExternal<BioOps.UnitOp_CellLysis, CellLysisBuilder>(ExternalCatalog.Bioprocess.CellLysis, tag, (f, o) => new CellLysisBuilder(f, o));
        /// <summary>Adds a Centrifuge unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CentrifugeBuilder AddCentrifuge(string tag) => MakeExternal<BioOps.UnitOp_Centrifuge, CentrifugeBuilder>(ExternalCatalog.Bioprocess.Centrifuge, tag, (f, o) => new CentrifugeBuilder(f, o));
        /// <summary>Adds a Chromatography Column unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public ChromatographyBuilder AddChromatographyColumn(string tag) => MakeExternal<BioOps.UnitOp_Chromatography, ChromatographyBuilder>(ExternalCatalog.Bioprocess.ChromatographyColumn, tag, (f, o) => new ChromatographyBuilder(f, o));
        /// <summary>Adds a Crossflow UF unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CrossflowUFBuilder AddCrossflowUF(string tag) => MakeExternal<BioOps.UnitOp_CrossflowUF, CrossflowUFBuilder>(ExternalCatalog.Bioprocess.CrossflowUFDF, tag, (f, o) => new CrossflowUFBuilder(f, o));
        /// <summary>Adds a Crystallizer unit operation tagged <paramref name="tag"/> and returns its fluent builder.</summary>
        public CrystallizerBuilder AddCrystallizer(string tag) => MakeExternal<BioOps.UnitOp_Crystallizer, CrystallizerBuilder>(ExternalCatalog.Bioprocess.Crystallizer, tag, (f, o) => new CrystallizerBuilder(f, o));

        /// <summary>Returns the display names of every loaded <see cref="IExternalUnitOperation"/> template.</summary>
        public IReadOnlyList<string> AvailableExternalUnitOperationNames
        {
            get
            {
                var list = new List<string>();
                foreach (var kv in Inner.AvailableSimulationObjects)
                    if (kv.Value is IExternalUnitOperation) list.Add(kv.Key);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        private TBuilder Make<TObj, TBuilder>(ObjectType type, string tag, Func<Flowsheet, TObj, TBuilder> ctor)
            where TObj : ISimulationObject
            where TBuilder : UnitOpBuilder<TObj, TBuilder>
        {
            var (x, y) = NextPos();
            var obj = (TObj)Inner.AddObject(type, (int)x, (int)y, tag);
            return ctor(this, obj);
        }

        /// <summary>
        /// Instantiates an external (IExternalUnitOperation) UO by display name and wraps
        /// the fresh instance in a typed builder. Used by the bioprocess, refining,
        /// electrolyte and other Plus typed builder methods.
        /// </summary>
        /// <remarks>
        /// Dispatches to whichever <c>AddObjectToSurface</c> overload the wrapped host exposes:
        /// <list type="bullet">
        ///   <item><description><c>FlowsheetBase.AddObjectToSurface(type, x, y, tag, id, uoobj, createConnected)</c> - used by Automation3 / DWSIM.UI.Desktop.Shared / DynamicRunner.</description></item>
        ///   <item><description><c>FormFlowsheet.FormSurface.AddObjectToSurface(type, x, y, chemsep, tag, id, uoobj, createConnected)</c> - used by the classic WinForms editor (FormFlowsheet implements IFlowsheet directly, not via FlowsheetBase).</description></item>
        /// </list>
        /// </remarks>
        internal TBuilder MakeExternal<TObj, TBuilder>(string displayName, string tag,
            Func<Flowsheet, TObj, TBuilder> ctor, bool requiresPlus = false)
            where TObj : class, IExternalUnitOperation, ISimulationObject
            where TBuilder : UnitOpBuilder<TObj, TBuilder>
        {
            if (requiresPlus) License.RequirePlus();
            if (!Inner.AvailableSimulationObjects.TryGetValue(displayName, out var template))
                throw new System.Collections.Generic.KeyNotFoundException(
                    "External UO template '" + displayName + "' not registered. " +
                    "Verify the unit-op DLL is present in 'unitops' or 'unitops2' next to the running assembly.");
            var external = (IExternalUnitOperation)template;
            var fresh = (TObj)external.ReturnInstance(template.GetType().AssemblyQualifiedName);
            var (x, y) = NextPos();
            var id = AddExternalToSurface((int)x, (int)y, tag, fresh);
            var so = (TObj)Inner.SimulationObjects[id];
            return ctor(this, so);
        }

        /// <summary>
        /// Routes <c>AddObjectToSurface(External, ..., uoobj)</c> to whichever flavour the
        /// wrapped <see cref="IFlowsheet"/> exposes. Reflection avoids a hard reference on
        /// DWSIM.exe (which would drag the WinForms editor into headless consumers) while
        /// still supporting the classic <c>FormFlowsheet</c> path.
        /// </summary>
        private string AddExternalToSurface(int x, int y, string tag, IExternalUnitOperation uoobj)
        {
            // 1) FlowsheetBase descendants - Automation3.Flowsheet2, Eto UI.Forms.Flowsheet,
            //    DWSIM.DynamicRunner.Flowsheet, anything that derives from FlowsheetBase.
            if (Inner is FB fb)
                return fb.AddObjectToSurface(ObjectType.External, x, y, tag, "", uoobj, false);

            // 2) Classic WinForms FormFlowsheet - implements IFlowsheet directly and exposes
            //    a public field FormSurface : FlowsheetSurface_SkiaSharp with its own overload
            //    AddObjectToSurface(type, x, y, chemsep, tag, id, uoobj, createConnected).
            var t = Inner.GetType();
            var surfaceMember = (System.Reflection.MemberInfo)t.GetField("FormSurface")
                                ?? t.GetProperty("FormSurface");
            if (surfaceMember != null)
            {
                var surface = surfaceMember is System.Reflection.FieldInfo fi
                    ? fi.GetValue(Inner)
                    : ((System.Reflection.PropertyInfo)surfaceMember).GetValue(Inner);
                if (surface != null)
                {
                    var m = surface.GetType().GetMethod("AddObjectToSurface",
                        new[] { typeof(ObjectType), typeof(int), typeof(int), typeof(bool),
                                typeof(string), typeof(string), typeof(IExternalUnitOperation), typeof(bool) });
                    if (m != null)
                    {
                        var result = m.Invoke(surface, new object[]
                            { ObjectType.External, x, y, false, tag, "", uoobj, false });
                        return (string)result;
                    }
                }
            }

            // 3) Custom IFlowsheet implementation that doesn't expose either path.
            throw new NotSupportedException(
                "Cannot add an external unit operation to a flowsheet of type " + t.FullName +
                ". Wrap a host that derives from DWSIM.FlowsheetBase.FlowsheetBase, or expose a " +
                "public 'FormSurface.AddObjectToSurface(ObjectType, Int32, Int32, Boolean, String, String, IExternalUnitOperation, Boolean)' member.");
        }

        // ----------------------------------------------------------- Reactions

        /// <summary>Defines a fractional-conversion reaction.</summary>
        public IReaction DefineConversionReaction(
            string name, Dictionary<string, double> stoichiometry,
            string baseCompound, string phase = "Mixture",
            string conversionExpression = "100", string description = "")
        {
            var r = Inner.CreateConversionReaction(name, description, stoichiometry, baseCompound, phase, conversionExpression);
            Inner.AddReaction(r);
            return r;
        }

        /// <summary>Defines an equilibrium reaction with a ln(Keq) expression.</summary>
        public IReaction DefineEquilibriumReaction(
            string name, Dictionary<string, double> stoichiometry,
            string baseCompound, string phase, string basis, string units,
            string lnKeqExpression, double approachT = 0.0, string description = "")
        {
            var r = Inner.CreateEquilibriumReaction(name, description, stoichiometry, baseCompound, phase, basis, units, approachT, lnKeqExpression);
            Inner.AddReaction(r);
            return r;
        }

        /// <summary>Defines a kinetic (Arrhenius) reaction.</summary>
        public IReaction DefineKineticReaction(
            string name, Dictionary<string, double> stoichiometry,
            Dictionary<string, double> directOrders, Dictionary<string, double> reverseOrders,
            string baseCompound, string phase, string basis, string amountUnits, string rateUnits,
            double aForward, double eForward, double aReverse = 0.0, double eReverse = 0.0,
            string forwardExpression = "", string reverseExpression = "", string description = "")
        {
            var r = Inner.CreateKineticReaction(name, description, stoichiometry, directOrders, reverseOrders,
                baseCompound, phase, basis, amountUnits, rateUnits,
                aForward, eForward, aReverse, eReverse, forwardExpression, reverseExpression);
            Inner.AddReaction(r);
            return r;
        }

        /// <summary>Defines a heterogeneous catalytic (Langmuir-Hinshelwood) reaction.</summary>
        public IReaction DefineHetCatReaction(
            string name, Dictionary<string, double> stoichiometry,
            string baseCompound, string phase, string basis, string amountUnits, string rateUnits,
            string numeratorExpression, string denominatorExpression, string description = "")
        {
            var r = Inner.CreateHetCatReaction(name, description, stoichiometry, baseCompound, phase, basis, amountUnits, rateUnits,
                numeratorExpression, denominatorExpression);
            Inner.AddReaction(r);
            return r;
        }

        /// <summary>Returns a builder for a reaction set; creates it if it does not exist.</summary>
        public ReactionSetBuilder ReactionSet(string id, string description = "")
        {
            if (!Inner.ReactionSets.ContainsKey(id))
            {
                var set = Inner.CreateReactionSet(id, description);
                Inner.AddReactionSet(set);
            }
            return new ReactionSetBuilder(this, id);
        }

        // -------------------------------------------------------------- Solver

        /// <summary>
        /// Creates a <see cref="DynamicsBuilder"/> for running a dynamic (time-domain) integration
        /// on this flowsheet. The flowsheet must have been loaded from a file that contains at least
        /// one dynamics schedule configured in DWSIM.
        /// </summary>
        /// <param name="scheduleName">
        /// Description of the schedule to run, as shown in the DWSIM Dynamics Manager.
        /// When null, the first schedule in the flowsheet is used automatically.
        /// </param>
        public DynamicsBuilder RunDynamics(string scheduleName = null)
            => new DynamicsBuilder(Inner, scheduleName);

        /// <summary>
        /// Solves the flowsheet synchronously. Throws <see cref="FlowsheetSolveException"/>
        /// containing all solver exceptions when one or more occur.
        /// </summary>
        public Flowsheet Solve()
        {
            var errors = SolveCore();
            if (errors != null && errors.Count > 0)
                throw new FlowsheetSolveException(errors);
            return this;
        }

        /// <summary>Solves the flowsheet without throwing; returns solver exceptions (empty when OK).</summary>
        public IReadOnlyList<Exception> TrySolve()
        {
            var errors = SolveCore() ?? new List<Exception>();
            return errors.AsReadOnly();
        }

        /// <summary>
        /// Routes to the right solver entry point depending on whether the wrapped
        /// flowsheet is the headless <c>Flowsheet2</c> (use <c>Automation3</c>'s fast path)
        /// or any other <see cref="IFlowsheet"/> (FormFlowsheet, Eto UI.Forms.Flowsheet,
        /// extender host, …) - for those, fall through to the universal
        /// <see cref="DWSIM.FlowsheetSolver.FlowsheetSolver"/>.
        /// </summary>
        private List<Exception> SolveCore()
        {
            if (Inner is global::DWSIM.Automation.Flowsheet2)
                return Bootstrap.Automation.CalculateFlowsheet4(Inner);
            DWSIM.GlobalSettings.Settings.CalculatorActivated = true;
            DWSIM.GlobalSettings.Settings.SolverBreakOnException = true;
            return DWSIM.FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(
                Inner, DWSIM.GlobalSettings.Settings.SolverMode);
        }
    }

    /// <summary>Aggregates one or more solver exceptions raised by <see cref="Flowsheet.Solve"/>.</summary>
    public sealed class FlowsheetSolveException : AggregateException
    {
        /// <summary>Wraps every solver exception raised during a single <see cref="Flowsheet.Solve"/> call.</summary>
        public FlowsheetSolveException(IList<Exception> inner)
            : base("Flowsheet solver reported " + inner.Count + " error(s).", inner) { }
    }
}
