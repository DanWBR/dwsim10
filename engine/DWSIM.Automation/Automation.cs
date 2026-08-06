using System;
using System.Collections.Generic;
using DWSIM.Interfaces;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using DWSIM.GlobalSettings;
using System.Linq;
using DWSIM.Interfaces.Enums;
using DWSIM.SharedClasses;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics;
using System.Threading.Tasks;
using DWSIM.Thermodynamics.AdvancedEOS;
using CapeOpen;
using DWSIM.Thermodynamics.BaseClasses;
using System.Resources;

namespace DWSIM.Automation
{

    [Guid("ed615e8f-da69-4c24-80e2-bfe342168060")]
    public interface AutomationInterface
    {
        /// <summary>Loads a DWSIM flowsheet from the specified file path.</summary>
        /// <param name="filepath">The full path to the simulation file (.dwxml or .dwxmz).</param>
        /// <returns>The loaded <see cref="Interfaces.IFlowsheet"/> instance.</returns>
        Interfaces.IFlowsheet LoadFlowsheet(string filepath);

        /// <summary>Saves the flowsheet to the specified file path.</summary>
        /// <param name="flowsheet">The flowsheet to save.</param>
        /// <param name="filepath">The destination file path.</param>
        /// <param name="compressed">If <c>true</c>, saves as a compressed .dwxmz archive; otherwise saves as plain XML.</param>
        void SaveFlowsheet(IFlowsheet flowsheet, string filepath, bool compressed);

        /// <summary>Saves the flowsheet to the specified file path using compression.</summary>
        /// <param name="flowsheet">The flowsheet to save.</param>
        /// <param name="filepath">The destination file path.</param>
        void SaveFlowsheet2(IFlowsheet flowsheet, string filepath);

        /// <summary>Calculates the flowsheet, optionally starting from a specific simulation object.</summary>
        /// <param name="flowsheet">The flowsheet to calculate.</param>
        /// <param name="sender">The simulation object to begin calculation from, or <c>null</c> to solve the entire flowsheet.</param>
        void CalculateFlowsheet(IFlowsheet flowsheet, ISimulationObject sender);

        /// <summary>Calculates the flowsheet and returns any solver exceptions.</summary>
        /// <param name="flowsheet">The flowsheet to calculate.</param>
        /// <returns>A list of exceptions that occurred during solving.</returns>
        List<Exception> CalculateFlowsheet2(IFlowsheet flowsheet);

        /// <summary>Calculates the flowsheet with a solver timeout and returns any exceptions.</summary>
        /// <param name="flowsheet">The flowsheet to calculate.</param>
        /// <param name="timeout_seconds">Maximum allowed solver time in seconds.</param>
        /// <returns>A list of exceptions that occurred during solving.</returns>
        List<Exception> CalculateFlowsheet3(IFlowsheet flowsheet, int timeout_seconds);

        /// <summary>Creates and returns a new empty flowsheet instance.</summary>
        /// <returns>A new <see cref="IFlowsheet"/> instance.</returns>
        IFlowsheet CreateFlowsheet();

        /// <summary>Releases resources held by the automation instance.</summary>
        void ReleaseResources();

        /// <summary>Returns the main application window object.</summary>
        /// <returns>The main window object, or <c>null</c> if not applicable.</returns>
        object GetMainWindow();

    }

    [Guid("62486815-2330-4CDE-8962-41F576B0C2B8"), ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class Automation3 : AutomationInterface
    {

        // The property-package / compound catalog and the AppDomain-level assembly-resolve hook are
        // process-wide singletons: they hold native/COM state (CAPE-OPEN COPPM, ThermoC, CoolProp) that
        // must be initialized only once. Rebuilding the catalog on a second Automation3 instance (e.g. a
        // server recreating the runner per case) corrupts that state and freezes every subsequent solver
        // run (Solved=False, stale outputs). Keeping them static makes multiple instances safe again,
        // restoring the 9.0.5 behavior.
        private static readonly Dictionary<String, IPropertyPackage> _availablePropertyPackages = new Dictionary<string, IPropertyPackage>();
        private static readonly Dictionary<String, ICompoundConstantProperties> _availableCompounds = new Dictionary<string, ICompoundConstantProperties>();
        private static readonly object _initLock = new object();
        private static bool _catalogLoaded = false;
        private static bool _assemblyResolveHooked = false;

        /// <summary>Gets the collection of available property packages, keyed by component name.</summary>
        public Dictionary<String, IPropertyPackage> AvailablePropertyPackages => _availablePropertyPackages;

        /// <summary>Gets the collection of available compounds, keyed by compound name.</summary>
        public Dictionary<String, ICompoundConstantProperties> AvailableCompounds => _availableCompounds;

        private System.Resources.ResourceManager rm, prm;

        /// <summary>
        /// Initializes a new instance of <see cref="Automation3"/>, loading all built-in and external
        /// property packages, compounds, and extenders.
        /// </summary>
        public Automation3()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            GlobalSettings.Settings.AutomationMode = true;
            // Hook the assembly resolver only once per AppDomain. Adding it on every instance leaked a
            // handler per Automation3 created, accumulating across runner recreations.
            lock (_initLock)
            {
                if (!_assemblyResolveHooked)
                {
                    AppDomain currentDomain = AppDomain.CurrentDomain;
                    currentDomain.AssemblyResolve += new ResolveEventHandler(LoadAssembly);
                    _assemblyResolveHooked = true;
                }
            }
            //FlowsheetBase.FlowsheetBase.AddPropPacks();
            LoadItems();
            FlowsheetBase.FlowsheetBase.Extenders = LoadExtenders();
        }

        private void LoadItems()
        {

            // resources

            rm = new System.Resources.ResourceManager("DWSIM.FlowsheetBase.Strings", Assembly.GetAssembly(typeof(FlowsheetBase.FlowsheetBase)));
            prm = new System.Resources.ResourceManager("DWSIM.FlowsheetBase.Properties", Assembly.GetAssembly(typeof(FlowsheetBase.FlowsheetBase)));

            // proppacks
            //
            // Everything below builds the property-package / compound catalog and touches native/COM
            // state (CAPE-OPEN COPPM, ThermoC, CoolProp databases). It must run exactly once per process:
            // a second pass corrupts that global state and freezes subsequent solver runs. The catalog is
            // stored in static fields and shared across all Automation3 instances.

            lock (_initLock)
            {

            if (_catalogLoaded) return;

            var plist = new System.Collections.Concurrent.BlockingCollection<PropertyPackage>();

            var t1 = TaskHelper.Run(() =>
            {
                var CPPP = new CoolPropPropertyPackage();
                CPPP.ComponentName = "CoolProp";
                plist.Add(CPPP);

                var CPIPP = new CoolPropIncompressiblePurePropertyPackage();
                CPIPP.ComponentName = "CoolProp (Incompressible Fluids)";
                CPIPP.ComponentDescription = "CoolProp (Incompressible Fluids)";
                plist.Add(CPIPP);

                var CPIMPP = new CoolPropIncompressibleMixturePropertyPackage();
                CPIMPP.ComponentName = "CoolProp (Incompressible Mixtures)";
                CPIMPP.ComponentDescription = "CoolProp (Incompressible Mixtures)";
                plist.Add(CPIMPP);

                var STPP = new SteamTablesPropertyPackage();
                STPP.ComponentName = "Steam Tables (IAPWS-IF97)";
                plist.Add(STPP);

                var SEAPP = new SeawaterPropertyPackage();
                SEAPP.ComponentName = "Seawater IAPWS-08";
                plist.Add(SEAPP);
            });

            var t2 = TaskHelper.Run(() =>
            {

                var PRPP = new PengRobinsonPropertyPackage();
                PRPP.ComponentName = "Peng-Robinson (PR)";
                plist.Add(PRPP);

            });

            var t3 = TaskHelper.Run(() =>
            {

                var PRSV2PP = new PRSV2PropertyPackage();
                PRSV2PP.ComponentName = "Peng-Robinson-Stryjek-Vera 2 (PRSV2-M)";
                plist.Add(PRSV2PP);

                var PRSV2PPVL = new PRSV2VLPropertyPackage();
                PRSV2PPVL.ComponentName = "Peng-Robinson-Stryjek-Vera 2 (PRSV2-VL)";
                plist.Add(PRSV2PPVL);

            });

            var t4 = TaskHelper.Run(() =>
            {

                var SRKPP = new SRKPropertyPackage();
                SRKPP.ComponentName = "Soave-Redlich-Kwong (SRK)";
                plist.Add(SRKPP);

            });

            var t6 = TaskHelper.Run(() =>
            {

                var UPP = new UNIFACPropertyPackage();
                UPP.ComponentName = "UNIFAC";
                plist.Add(UPP);

                var ULLPP = new UNIFACLLPropertyPackage();
                ULLPP.ComponentName = "UNIFAC-LL";
                plist.Add(ULLPP);

                var MUPP = new MODFACPropertyPackage();
                MUPP.ComponentName = "Modified UNIFAC (Dortmund)";
                plist.Add(MUPP);

                var NUPP = new NISTMFACPropertyPackage();
                NUPP.ComponentName = "Modified UNIFAC (NIST)";
                plist.Add(NUPP);

            });

            var t10 = TaskHelper.Run(() =>
            {

                var WPP = new WilsonPropertyPackage();
                WPP.ComponentName = "Wilson";
                plist.Add(WPP);

                var NRTLPP = new NRTLPropertyPackage();
                NRTLPP.ComponentName = "NRTL";
                plist.Add(NRTLPP);

                var UQPP = new UNIQUACPropertyPackage();
                UQPP.ComponentName = "UNIQUAC";
                plist.Add(UQPP);

                var CSLKPP = new ChaoSeaderPropertyPackage();
                CSLKPP.ComponentName = "Chao-Seader";
                plist.Add(CSLKPP);

                var GSLKPP = new GraysonStreedPropertyPackage();
                GSLKPP.ComponentName = "Grayson-Streed";
                plist.Add(GSLKPP);

                var RPP = new RaoultPropertyPackage();
                RPP.ComponentName = "Raoult's Law";
                plist.Add(RPP);

                var LKPPP = new LKPPropertyPackage();
                LKPPP.ComponentName = "Lee-Kesler-Plöcker";
                plist.Add(LKPPP);

            });

            var t11 = TaskHelper.Run(() =>
            {

                var ISPP = new IdealElectrolytePropertyPackage();
                plist.Add(ISPP);

                var BOPP = new BlackOilPropertyPackage();
                BOPP.ComponentName = "Black Oil";
                plist.Add(BOPP);

                var GERGPP = new GERG2008PropertyPackage();
                plist.Add(GERGPP);

                var PCSAFTPP = new PCSAFT2PropertyPackage();
                plist.Add(PCSAFTPP);

                var PR78PP = new PengRobinson1978PropertyPackage();
                PR78PP.ComponentName = "Peng-Robinson 1978 (PR78)";
                plist.Add(PR78PP);

                var PR78Adv = new PengRobinson1978AdvancedPropertyPackage();
                plist.Add(PR78Adv);

                var SRKAdv = new SoaveRedlichKwongAdvancedPropertyPackage();
                plist.Add(SRKAdv);

            });

            Task.WaitAll(t1, t2, t3, t4, t6, t10, t11);

            foreach (var pp in plist)
            {
                AvailablePropertyPackages.Add(((ICapeIdentification)pp).ComponentName, pp);
            }

            var otherpps = SharedClasses.Utility.LoadAdditionalPropertyPackages();

            foreach (var pp in otherpps)
            {
                if (!AvailablePropertyPackages.ContainsKey(((ICapeIdentification)pp).ComponentName))
                    AvailablePropertyPackages.Add(((ICapeIdentification)pp).ComponentName, pp);
                else
                    Console.WriteLine(String.Format("Error adding External Property Package '{0}'. Check the 'ppacks' and 'extenders' folders for duplicate items.", ((ICapeIdentification)pp).ComponentName));
            }

            if (!Settings.IsRunningOnMono())
            {
                var COPP = new CAPEOPENPropertyPackage();
                COPP.ComponentName = "CAPE-OPEN";
                AvailablePropertyPackages.Add(COPP.ComponentName.ToString(), COPP);
            }

            // compounds

            var addedcomps = new List<String>();
            var casnumbers = new List<String>();

            var csdb = new Thermodynamics.Databases.ChemSep();
            csdb.Load();
            var cpa = csdb.Transfer();
            foreach (ConstantProperties cp in cpa)
            { if (!AvailableCompounds.ContainsKey(cp.Name)) AvailableCompounds.Add(cp.Name, cp); }

            var cpdb = new Thermodynamics.Databases.CoolProp();
            cpdb.Load();
            cpa = cpdb.Transfer();
            addedcomps = AvailableCompounds.Keys.Select((x) => x.ToLower()).ToList();
            foreach (ConstantProperties cp in cpa)
            { if (!AvailableCompounds.ContainsKey(cp.Name)) AvailableCompounds.Add(cp.Name, cp); }

            var bddb = new Thermodynamics.Databases.Biodiesel();
            bddb.Load();
            cpa = bddb.Transfer();
            addedcomps = AvailableCompounds.Keys.Select((x) => x.ToLower()).ToList();
            foreach (ConstantProperties cp in cpa)
            { if (!AvailableCompounds.ContainsKey(cp.Name)) AvailableCompounds.Add(cp.Name, cp); }

            var chedl = new Thermodynamics.Databases.ChEDL_Thermo();
            chedl.Load();
            cpa = chedl.Transfer().ToArray();

            addedcomps = AvailableCompounds.Keys.Select((x) => x.ToLower()).ToList();
            casnumbers = AvailableCompounds.Values.Select((x) => x.CAS_Number).ToList();

            foreach (ConstantProperties cp in cpa)
            {
                if (!addedcomps.Contains(cp.Name.ToLower()) && !addedcomps.Contains(cp.Name))
                    if (!casnumbers.Contains(cp.CAS_Number))
                        if (!AvailableCompounds.ContainsKey(cp.Name)) AvailableCompounds.Add(cp.Name, cp);
            }

            var elec = new Thermodynamics.Databases.Electrolyte();
            elec.Load();
            cpa = elec.Transfer().ToArray();
            addedcomps = AvailableCompounds.Keys.Select((x) => x.ToLower()).ToList();
            foreach (ConstantProperties cp in cpa)
            { if (!AvailableCompounds.ContainsKey(cp.Name)) AvailableCompounds.Add(cp.Name, cp); }

            var comps = Thermodynamics.Databases.UserDB.LoadAdditionalCompounds();
            foreach (ConstantProperties cp in comps)
            { if (!AvailableCompounds.ContainsKey(cp.Name)) AvailableCompounds.Add(cp.Name, cp); }

            using (var filestr = Assembly.GetAssembly(elec.GetType()).GetManifestResourceStream("DWSIM.Thermodynamics.FoodProp.xml"))
            {
                var fcomps = Thermodynamics.Databases.UserDB.ReadComps(filestr);
                foreach (var cp in fcomps)
                {
                    cp.CurrentDB = "FoodProp";
                    if (!AvailableCompounds.ContainsKey(cp.Name)) AvailableCompounds.Add(cp.Name, cp);
                }
            }

            csdb.Dispose();
            cpdb.Dispose();
            chedl.Dispose();

            _catalogLoaded = true;

            } // end lock (_initLock)

        }

        /// <summary>Returns the DWSIM version string and assembly build date.</summary>
        /// <returns>A formatted string containing the version number and last write time of the assembly.</returns>
        [DispId(0)]
        public string GetVersion()
        {

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var date = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location).ToString();

            return String.Format("DWSIM version {0} ({1})", version, date);

        }

        static Assembly LoadAssembly(object sender, ResolveEventArgs args)
        {
            var directories = new List<string>
            {
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "avalonia"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "extenders"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ppacks"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "unitops"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "plugins"),
                Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName,
                Path.Combine(Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName, "extenders"),
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "extenders")
            };

            foreach (var dir in directories)
            {
                var fullPath = Path.Combine(dir, new AssemblyName(args.Name).Name + ".dll");
                if (File.Exists(fullPath))
                {
                    var assembly = Assembly.LoadFrom(fullPath);
                    return assembly;
                }
                else {
                    fullPath = Path.Combine(dir, new AssemblyName(args.Name).Name + ".exe");
                    if (File.Exists(fullPath))
                    {
                        var assembly = Assembly.LoadFrom(fullPath);
                        return assembly;
                    }
                }
            }

            return null;

        }

        /// <summary>Loads a flowsheet from the specified file path with an optional UI update handler.</summary>
        /// <param name="filepath">The full path to the simulation file (.dwxml, .dwxmz, or other supported format).</param>
        /// <param name="UIUpdHandler">An optional action invoked to update the UI during loading.</param>
        /// <returns>The loaded <see cref="IFlowsheet"/> instance.</returns>
        [DispId(1)]
        public IFlowsheet LoadFlowsheet(string filepath, Action UIUpdHandler = null)
        {
            Settings.AutomationMode = true;
            Settings.CultureInfo = "en";
            var fsheet = new Flowsheet2(null, UIUpdHandler);
            fsheet.SupressDataLoading = true;
            fsheet.AvailableCompounds = AvailableCompounds;
            fsheet.AvailablePropertyPackages = AvailablePropertyPackages;
            fsheet.SetResourcesManager(rm);
            fsheet.SetPropertyResourcesManager(prm);
            fsheet.Init();
            if (System.IO.Path.GetExtension(filepath).ToLower().EndsWith("z"))
            {
                fsheet.LoadZippedXML(filepath);
            }
            else
            {
                fsheet.LoadFromXML(XDocument.Load(filepath));
            }
            Settings.CalculatorActivated = true;
            return fsheet;
        }

        /// <summary>Loads a flowsheet from the specified file path without a UI update handler.</summary>
        /// <param name="filepath">The full path to the simulation file.</param>
        /// <returns>The loaded <see cref="IFlowsheet"/> instance.</returns>
        [DispId(2)]
        public IFlowsheet LoadFlowsheet2(string filepath)
        {
            Settings.AutomationMode = true;
            Settings.CultureInfo = "en";
            var fsheet = new Flowsheet2(null, null);
            fsheet.SupressDataLoading = true;
            fsheet.AvailableCompounds = AvailableCompounds;
            fsheet.AvailablePropertyPackages = AvailablePropertyPackages;
            fsheet.SetResourcesManager(rm);
            fsheet.SetPropertyResourcesManager(prm);
            fsheet.Init();
            if (System.IO.Path.GetExtension(filepath).ToLower().EndsWith("z"))
            {
                fsheet.LoadZippedXML(filepath);
            }
            else
            {
                fsheet.LoadFromXML(XDocument.Load(filepath));
            }
            Settings.CalculatorActivated = true;
            return fsheet;
        }

        /// <inheritdoc/>
        [DispId(3)]
        public void ReleaseResources()
        {
            // Intentionally non-destructive. The property-package / compound catalog and the
            // assembly-resolve hook are process-wide singletons holding native/COM state (CAPE-OPEN
            // COPPM, ThermoC). Clearing them here corrupted any Automation3 instance created afterwards
            // (frozen, Solved=False solver runs), so only the cheap per-instance resource managers are
            // released. The shared catalog stays alive for reuse across instances.
            rm = null;
            prm = null;
        }

        /// <inheritdoc/>
        [DispId(4)]
        public void SaveFlowsheet(IFlowsheet flowsheet, string filepath, bool compressed)
        {
            ((Flowsheet2)flowsheet).SaveSimulation(filepath);
        }

        /// <inheritdoc/>
        [DispId(5)]
        public void CalculateFlowsheet(IFlowsheet flowsheet, ISimulationObject sender)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            ((Flowsheet2)flowsheet).SolveFlowsheet2();
        }

        /// <summary>Calculates the flowsheet and throws any solver exception immediately (fire-and-forget variant).</summary>
        /// <param name="flowsheet">The flowsheet to calculate.</param>
        [DispId(6)]
        public void CalculateFlowsheet2(IFlowsheet flowsheet)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            ((Flowsheet2)flowsheet).SolveFlowsheet2();
        }

        /// <summary>Calculates the flowsheet with a solver timeout (fire-and-forget variant).</summary>
        /// <param name="flowsheet">The flowsheet to calculate.</param>
        /// <param name="timeout_seconds">Maximum allowed solver time in seconds.</param>
        [DispId(7)]
        public void CalculateFlowsheet3(IFlowsheet flowsheet, int timeout_seconds)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            Settings.SolverTimeoutSeconds = timeout_seconds;
            ((Flowsheet2)flowsheet).SolveFlowsheet2();
        }

        /// <summary>Calculates the flowsheet and returns any solver exceptions.</summary>
        /// <param name="flowsheet">The flowsheet to calculate.</param>
        /// <returns>A list of exceptions that occurred during solving.</returns>
        [DispId(8)]
        public List<Exception> CalculateFlowsheet4(IFlowsheet flowsheet)
        {
            Settings.CalculatorActivated = true;
            Settings.SolverBreakOnException = true;
            return ((Flowsheet2)flowsheet).SolveFlowsheet2();
        }

        /// <inheritdoc/>
        [DispId(9)]
        public void SaveFlowsheet2(IFlowsheet flowsheet, string filepath)
        {
            SaveFlowsheet(flowsheet, filepath, true);
        }

        /// <inheritdoc/>
        [DispId(10)]
        public IFlowsheet CreateFlowsheet()
        {
            Settings.AutomationMode = true;
            var f = new Flowsheet2(null, null);
            f.SupressDataLoading = true;
            f.AvailableCompounds = AvailableCompounds;
            f.AvailablePropertyPackages = AvailablePropertyPackages;
            f.SetResourcesManager(rm);
            f.SetPropertyResourcesManager(prm);
            f.Init();
            return f;
        }

        /// <inheritdoc/>
        public object GetMainWindow()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IFlowsheet LoadFlowsheet(string filepath)
        {
            try
            {
                return LoadFlowsheet(filepath, null);
            }
            catch (Exception ex)
            {
                Logging.Logger.LogError("Automation Error (LoadFlowsheet)", ex);
                throw ex;
            }
        }

        List<Exception> AutomationInterface.CalculateFlowsheet2(IFlowsheet flowsheet)
        {
            try
            {
                return CalculateFlowsheet4(flowsheet);
            }
            catch (Exception ex)
            {
                Logging.Logger.LogError("Automation Error (CalculateFlowsheet2)", ex);
                throw ex;
            }
        }

        List<Exception> AutomationInterface.CalculateFlowsheet3(IFlowsheet flowsheet, int timeout_seconds)
        {
            try
            {
                Settings.SolverTimeoutSeconds = timeout_seconds;
                return CalculateFlowsheet4(flowsheet);
            }
            catch (Exception ex)
            {
                Logging.Logger.LogError("Automation Error (CalculateFlowsheet3)", ex);
                throw ex;
            }
        }

        private List<Assembly> LoadExtenderDLLs()
        {
            List<Assembly> extenderdlls = new List<Assembly>();
            if (Directory.Exists(SharedClasses.Utility.GetExtendersRootDirectory()))
            {
                DirectoryInfo dinfo = new DirectoryInfo(SharedClasses.Utility.GetExtendersRootDirectory());
                FileInfo[] files = dinfo.GetFiles("*Extensions*.dll");
                if (!(files == null))
                {
                    foreach (FileInfo fi in files)
                    {
                        extenderdlls.Add(Assembly.LoadFrom(fi.FullName));
                    }
                }
            }
            return extenderdlls;
        }

        List<IExtenderCollection> GetExtenders(List<Assembly> alist)
        {
            List<Type> availableTypes = new List<Type>();
            foreach (var currentAssembly in alist)
            {
                try
                {
                    availableTypes.AddRange(currentAssembly.GetExportedTypes());
                }
                catch
                { }
            }
            var extList = availableTypes.FindAll(t => t.GetInterfaces().Contains(typeof(IExtenderCollection)));
            return extList.ConvertAll(t => (IExtenderCollection)Activator.CreateInstance(t));
        }

        List<IExtenderCollection> LoadExtenders()
        {

            List<IExtenderCollection> extlist = GetExtenders(LoadExtenderDLLs());

            foreach (var extender in extlist)
            {
                try
                {
                    if (extender.Level == ExtenderLevel.MainWindow)
                    {
                        foreach (var item in extender.Collection)
                        {
                            var load = false;
                            if (item is IExtender5) load = ((IExtender5)item).LoadInAutomationMode;
                            if (load)
                            {
                                item.SetMainWindow(null);
                                item.Run();
                            }
                        }
                   }
                }
                catch (Exception ex)
                {
                    Logging.Logger.LogError("Extender Initialization", ex);
                }
            }

            return extlist;

        }

    }

}
