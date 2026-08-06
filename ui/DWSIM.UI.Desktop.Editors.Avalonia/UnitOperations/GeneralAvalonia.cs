using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UI.Shared.Avalonia;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.UnitOperations.Streams;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using StringResources = DWSIM.UI.Shared.Avalonia.StringArrays;
using Exp = DWSIM.UnitOperations.UnitOperations.Expander;
using DWSIM.Interfaces.Enums;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// Avalonia-side counterpart of GeneralEditors.Initialize().
    /// Called when the container object passed to the GeneralEditors constructor
    /// is an AvaloniaEditorPanel instead of an Eto DynamicLayout.
    /// </summary>
    internal static class GeneralEditorsAvalonia
    {
        private static readonly NumberStyles NS = NumberStyles.Any;
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        private static bool TryVal(string text, out double v) =>
            double.TryParse(text, NS, IC, out v);

        private static string[] SplitLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            return text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Object + property pickers writing into a <see cref="DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo"/>,
        /// the record the Spec and Adjust blocks use to point at a variable. Shows the current
        /// value so the user can tell whether the pick landed where they meant.
        /// </summary>
        /// <summary>
        /// Element matrix editor of a Gibbs reactor: one row per chemical element, one coefficient
        /// per reactive compound. The engine seeds it from the compound formulas in
        /// CreateElementMatrix, but it is editable: elements can be added, removed and rebalanced,
        /// which is what the Windows editor's grid does through SaveMatrix.
        /// </summary>
        /// <summary>Opens the element matrix editor; the reactor editor reuses it.</summary>
        internal static void ShowGibbsElementMatrix(Reactor_Gibbs rg)
        {
            ShowGibbsElementMatrixEditor(rg);
        }

        private static void ShowGibbsElementMatrixEditor(Reactor_Gibbs rg)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm("Element Matrix", 700, 600, panel);

            Action rebuild = null;

            rebuild = () =>
            {
                panel.Children.Clear();

                var compounds = rg.ComponentIDs ?? new List<string>();
                var elements = (rg.Elements ?? new string[0]).ToList();
                var matrix = rg.ElementMatrix;

                panel.CreateAndAddLabelRow("Element Matrix");
                panel.CreateAndAddDescriptionRow("One row per element, one coefficient per reactive compound: " +
                    (compounds.Count > 0 ? string.Join(", ", compounds) : "no reactive compounds selected"));

                for (int i2 = 0; i2 < elements.Count; i2++)
                {
                    var row = i2;

                    panel.CreateAndAddStringEditorRow(string.Format("Element {0}", row + 1), elements[row],
                        (tb, e) =>
                        {
                            var els = rg.Elements;
                            if (row < els.Length) els[row] = tb.Text ?? "";
                        });

                    for (int j2 = 0; j2 < compounds.Count; j2++)
                    {
                        var col = j2;
                        var value = matrix != null && row <= matrix.GetUpperBound(0) && col <= matrix.GetUpperBound(1)
                            ? matrix[row, col] : 0.0;

                        panel.CreateAndAddTextBoxRow("G4", "  " + compounds[col], value, (tb, e) =>
                        {
                            if (!double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v)) return;
                            var m = rg.ElementMatrix;
                            if (m != null && row <= m.GetUpperBound(0) && col <= m.GetUpperBound(1)) m[row, col] = v;
                        });
                    }
                }

                if (elements.Count == 0)
                    panel.CreateAndAddDescriptionRow("The matrix is empty. Rebuild it from the compound formulas below.");

                panel.CreateAndAddButtonRow("Add Element", null, (btn, e) =>
                {
                    ResizeGibbsMatrix(rg, (rg.Elements?.Length ?? 0) + 1, rg.ComponentIDs?.Count ?? 0);
                    rebuild();
                });

                panel.CreateAndAddButtonRow("Remove Last Element", null, (btn, e) =>
                {
                    var n = rg.Elements?.Length ?? 0;
                    if (n == 0) return;
                    ResizeGibbsMatrix(rg, n - 1, rg.ComponentIDs?.Count ?? 0);
                    rebuild();
                });

                panel.CreateAndAddButtonRow("Rebuild from Compound Formulas", null, (btn, e) =>
                {
                    try
                    {
                        rg.CreateElementMatrix();
                    }
                    catch (Exception ex)
                    {
                        rg.FlowSheet?.ShowMessage("Could not rebuild the element matrix: " + ex.Message,
                            IFlowsheet.MessageType.GeneralError);
                    }
                    rebuild();
                });

                if (rg.TotalElements != null && rg.TotalElements.Length == elements.Count && elements.Count > 0)
                {
                    panel.CreateAndAddLabelRow("Element Amounts at the Inlet (mol/s)");
                    for (int i2 = 0; i2 < elements.Count; i2++)
                        panel.CreateAndAddTwoLabelsRow(elements[i2], rg.TotalElements[i2].ToString("G6", IC));
                }
            };

            rebuild();
            window.Show();
        }

        /// <summary>Grows or shrinks the element matrix, keeping the coefficients already entered.</summary>
        private static void ResizeGibbsMatrix(Reactor_Gibbs rg, int elementCount, int compoundCount)
        {
            var oldElements = rg.Elements ?? new string[0];
            var oldMatrix = rg.ElementMatrix;

            var elements = new string[elementCount];
            var matrix = new double[Math.Max(elementCount, 1), Math.Max(compoundCount, 1)];

            for (int i2 = 0; i2 < elementCount; i2++)
            {
                elements[i2] = i2 < oldElements.Length ? oldElements[i2] : "New";
                if (oldMatrix == null) continue;
                for (int j2 = 0; j2 < compoundCount; j2++)
                {
                    if (i2 <= oldMatrix.GetUpperBound(0) && j2 <= oldMatrix.GetUpperBound(1))
                        matrix[i2, j2] = oldMatrix[i2, j2];
                }
            }

            rg.Elements = elements;
            rg.ElementMatrix = matrix;

            var totals = new double[Math.Max(elementCount, 1)];
            var oldTotals = rg.TotalElements;
            if (oldTotals != null)
            {
                for (int i2 = 0; i2 < Math.Min(elementCount, oldTotals.Length); i2++) totals[i2] = oldTotals[i2];
            }
            rg.TotalElements = totals;
        }


        private static void AddSpecialOpVariablePicker(AvaloniaEditorPanel panel, ISimulationObject simobj,
            DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo info, IUnitsOfMeasure su)
        {
            var fs = simobj.GetFlowsheet();

            var objs = fs.SimulationObjects.Values
                .Where(x => x.GraphicObject != null && x.Name != simobj.Name)
                .OrderBy(x => x.GraphicObject.Tag).ToList();
            if (objs.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("No other objects on the flowsheet yet.");
                return;
            }

            var tags = objs.Select(x => x.GraphicObject.Tag).ToList();
            var props = new List<string>();
            global::Avalonia.Controls.ComboBox propDD = null;
            global::Avalonia.Controls.TextBlock valueLabel = null;

            var selIdx = objs.FindIndex(x => x.Name == info.ID);
            var objDD = panel.CreateAndAddDropDownRow("Object", tags, Math.Max(0, selIdx), null);

            void ReloadProps()
            {
                props.Clear();
                var o = objs.ElementAtOrDefault(objDD.SelectedIndex);
                if (o == null) return;
                props.AddRange((o.GetProperties(PropertyType.ALL) ?? Array.Empty<string>()).OrderBy(x => x));
            }
            ReloadProps();

            void Store()
            {
                var o = objs.ElementAtOrDefault(objDD.SelectedIndex);
                if (o == null || propDD == null) return;
                var idx = propDD.SelectedIndex;
                if (idx < 0 || idx >= props.Count) return;

                info.ID = o.Name;
                info.Name = o.GraphicObject.Tag;
                info.ObjectType = o.GraphicObject.ObjectType.ToString();
                info.PropertyName = props[idx];
                info.Units = o.GetPropertyUnit(props[idx], su);
                info.UnitsType = su.GetUnitType(info.Units);

                if (valueLabel == null) return;
                try
                {
                    var v = Convert.ToDouble(o.GetPropertyValue(props[idx], su));
                    valueLabel.Text = v.ToString("G6", IC) +
                        (string.IsNullOrEmpty(info.Units) ? "" : " " + info.Units);
                }
                catch { valueLabel.Text = "—"; }
            }

            propDD = panel.CreateAndAddDropDownRow("Property", props.ToList(),
                Math.Max(0, props.IndexOf(info.PropertyName ?? "")), (dd, e) => Store());

            valueLabel = panel.CreateAndAddTwoLabelsRow("Current Value", "—");

            objDD.SelectionChanged += (s, e) =>
            {
                ReloadProps();
                propDD.Items.Clear();
                foreach (var p in props) propDD.Items.Add(p);
                if (propDD.Items.Count > 0) propDD.SelectedIndex = 0;
            };

            Store();
        }

        private static bool IsWindows() =>
            Environment.OSVersion.Platform == PlatformID.Win32NT;

        private static void Notify(ISimulationObject simobj, string message)
        {
            var fs = simobj.GetFlowsheet();
            if (fs != null) fs.ShowMessage(message, IFlowsheet.MessageType.Information);
        }

        private static void OpenUrl(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target)
                {
                    UseShellExecute = true
                });
            }
            catch { /* nothing sensible to do if the shell refuses to open it */ }
        }

        /// <summary>
        /// Opens the Avalonia file picker owned by the window hosting <paramref name="panel"/> and
        /// invokes <paramref name="onPicked"/> with the local path of the chosen file.
        /// </summary>
        private static async void PickFile(AvaloniaEditorPanel panel, string title,
            string[] patterns, Action<string> onPicked)
        {
            try
            {
                var top = global::Avalonia.Controls.TopLevel.GetTopLevel(panel);
                if (top == null || top.StorageProvider == null) return;

                var options = new global::Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                };
                if (patterns != null && patterns.Length > 0)
                {
                    options.FileTypeFilter = new[]
                    {
                        new global::Avalonia.Platform.Storage.FilePickerFileType("Supported Files")
                        {
                            Patterns = patterns.ToList()
                        }
                    };
                }

                var files = await top.StorageProvider.OpenFilePickerAsync(options);
                if (files == null || files.Count == 0) return;

                var path = files[0].Path == null ? null : files[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(path)) onPicked(path);
            }
            catch { /* picker unavailable or cancelled */ }
        }

        private static bool _unitRegistryWired;

        private static void EnsureUnitRegistryWired(IFlowsheet fs)
        {
            if (_unitRegistryWired) return;
            UnitConversionRegistry.Convert = (from, to, val) => cv.Convert(from, to, val);
            UnitConversionRegistry.GetAlternatives = unit =>
            {
                var su = fs.FlowsheetOptions.SelectedUnitSystem;
                var measure = su.GetUnitType(unit);
                if (measure == DWSIM.Interfaces.Enums.UnitOfMeasure.none) return Array.Empty<string>();
                return su.GetUnitSet(measure);
            };
            _unitRegistryWired = true;
        }

        internal static void Populate(ISimulationObject simobj, AvaloniaEditorPanel panel)
        {
            var su = simobj.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
            var nf = simobj.GetFlowsheet().FlowsheetOptions.NumberFormat;

            EnsureUnitRegistryWired(simobj.GetFlowsheet());

            panel.CreateAndAddLabelRow("Object Property Editor");
            panel.CreateAndAddDescriptionRow("Property values are updated/stored as they are changed/edited. There's no need to press ENTER to commit the changes.");

            panel.CreateAndAddLabelRow("Object Details");
            panel.CreateAndAddTwoLabelsRow("Type", simobj.GetDisplayName());
            panel.CreateAndAddTwoLabelsRow("Status", simobj.GraphicObject.Active ? "Active" : "Inactive");

            panel.CreateAndAddStringEditorRow("Name", simobj.GraphicObject.Tag, (tb, e) =>
            {
                simobj.GraphicObject.Tag = tb.Text;
                simobj.GetFlowsheet().UpdateInterface();
            });

            var proppacks = simobj.GetFlowsheet().PropertyPackages.Values.Select(x => x.Tag).ToList();
            if (proppacks.Count > 0)
            {
                panel.CreateAndAddLabelRow("Property Package");
                var selPP = simobj.PropertyPackage?.Tag ?? "";
                panel.CreateAndAddDropDownRow("Property Package", proppacks, proppacks.IndexOf(selPP), (dd, e) =>
                {
                    var tag = dd.SelectedItem?.ToString();
                    simobj.PropertyPackage = (IPropertyPackage)simobj.GetFlowsheet().PropertyPackages.Values.FirstOrDefault(x => x.Tag == tag);
                });
            }

            panel.CreateAndAddLabelRow("Object Properties");

            switch (simobj.GraphicObject.ObjectType)
            {
                case ObjectType.External:
                    ((IExternalUnitOperation)simobj)?.PopulateEditorPanel(panel);
                    break;

                case ObjectType.CapeOpenUO:
                {
                    var couo = (CapeOpenUO)simobj;

                    if (!IsWindows())
                    {
                        panel.CreateAndAddDescriptionRow(
                            "CAPE-OPEN is a Windows COM standard. On this platform the unit operation runs in read-only bypass mode.");
                        break;
                    }

                    panel.CreateAndAddLabelRow("CAPE-OPEN Unit Operation");
                    if (couo._seluo == null || string.IsNullOrEmpty(couo._seluo.Name))
                    {
                        panel.CreateAndAddDescriptionRow("No CAPE-OPEN unit operation is assigned yet.");
                    }
                    else
                    {
                        panel.CreateAndAddTwoLabelsRow("Name", couo._seluo.Name);
                        panel.CreateAndAddTwoLabelsRow("Version", couo._seluo.Version ?? "-");
                        panel.CreateAndAddTwoLabelsRow("CAPE-OPEN Version", couo._seluo.CapeVersion ?? "-");
                        panel.CreateAndAddTwoLabelsRow("Type Name", couo._seluo.TypeName ?? "-");
                        if (!string.IsNullOrEmpty(couo._seluo.Description))
                            panel.CreateAndAddDescriptionRow(couo._seluo.Description);
                    }

                    panel.CreateAndAddButtonRow("Edit Unit Operation...", null, (btn, e) =>
                    {
                        try { couo.Edit(); }
                        catch (Exception ex) { Notify(simobj, "CAPE-OPEN edit failed: " + ex.Message); }
                    });
                    panel.CreateAndAddDescriptionRow("Opens the unit operation's own configuration dialog, provided by its vendor.");

                    panel.CreateAndAddButtonRow("Select / Change Unit Operation...", null, (btn, e) =>
                    {
                        try
                        {
                            // ShowForm goes through CapeOpenUO.SelectorOverride, which the
                            // Avalonia host points at its own picker.
                            couo.ShowForm();
                            if (couo._seluo != null) couo.InstantiateSelected();
                            Notify(simobj, "CAPE-OPEN unit operation updated. Re-open the editor to see its ports and parameters.");
                        }
                        catch (Exception ex) { Notify(simobj, "CAPE-OPEN selection failed: " + ex.Message); }
                    });

                    if (couo._ports != null && couo._ports.Count > 0)
                    {
                        panel.CreateAndAddLabelRow("Ports");
                        foreach (var port in couo._ports)
                        {
                            try
                            {
                                var id = (CapeOpen.ICapeIdentification)port;
                                panel.CreateAndAddTwoLabelsRow(id.ComponentName,
                                    port.direction + " / " + port.portType);
                            }
                            catch { /* a port that refuses to describe itself is not worth a row */ }
                        }
                    }

                    if (couo._params != null && couo._params.Count > 0)
                    {
                        panel.CreateAndAddLabelRow("Parameters");
                        foreach (var par in couo._params)
                        {
                            try
                            {
                                var id = (CapeOpen.ICapeIdentification)par;
                                panel.CreateAndAddTwoLabelsRow(id.ComponentName,
                                    Convert.ToString(par.value, IC) ?? "-");
                            }
                            catch { /* same */ }
                        }
                        panel.CreateAndAddDescriptionRow("Parameter values are edited in the vendor's own dialog above.");
                    }
                    break;
                }

                case ObjectType.EnergyStream:
                    var es = (EnergyStream)simobj;
                    panel.CreateAndAddTextBoxRow(nf, "Heat Flow (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, es.EnergyFlow.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) es.EnergyFlow = cv.ConvertToSI(su.heatflow, v); });
                    break;

                case ObjectType.SolidSeparator:
                    var ss = (SolidsSeparator)simobj;
                    panel.CreateAndAddTextBoxRow(nf, "Solids Separation Efficiency", ss.SeparationEfficiency,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ss.SeparationEfficiency = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Liquids Separation Efficiency", ss.LiquidSeparationEfficiency,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ss.LiquidSeparationEfficiency = v; });
                    break;

                case ObjectType.NodeIn:
                    var mix = (Mixer)simobj;
                    panel.CreateAndAddDropDownRow("Pressure Calculation Mode",
                        StringResources.mixercalcmode().ToList(), (int)mix.PressureCalculation,
                        (dd, e) => mix.PressureCalculation = (Mixer.PressureBehavior)dd.SelectedIndex);
                    break;

                case ObjectType.NodeOut:
                {
                    var sp = (Splitter)simobj;
                    var spModeNames = new[]
                    {
                        "Split Ratios", "Stream 1 + 2 Mass Flow", "Stream 1 + 2 Mole Flow", "Stream 1 + 2 Volume Flow"
                    };
                    int spModeIdx = Math.Min(spModeNames.Length - 1, Math.Max(0, (int)sp.OperationMode));
                    panel.CreateAndAddDropDownRow("Calculation Mode", spModeNames.ToList(), spModeIdx,
                        (dd, e) => { if (dd.SelectedIndex >= 0) sp.OperationMode = (Splitter.OpMode)dd.SelectedIndex; });

                    // Ensure 3 slots so the editor doesn't index out of range on legacy files
                    while (sp.Ratios.Count < 3) sp.Ratios.Add(0.0);

                    panel.CreateAndAddLabelRow("Outlet 1");
                    panel.CreateAndAddTextBoxRow(nf, "Split Ratio (fraction, 0-1)", Convert.ToDouble(sp.Ratios[0]),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sp.Ratios[0] = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Mass Flow Spec (" + su.massflow + ")",
                        cv.ConvertFromSI(su.massflow, sp.StreamFlowSpec),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sp.StreamFlowSpec = cv.ConvertToSI(su.massflow, v); });

                    panel.CreateAndAddLabelRow("Outlet 2");
                    panel.CreateAndAddTextBoxRow(nf, "Split Ratio (fraction, 0-1)", Convert.ToDouble(sp.Ratios[1]),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sp.Ratios[1] = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Mass Flow Spec (" + su.massflow + ")",
                        cv.ConvertFromSI(su.massflow, sp.Stream2FlowSpec),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sp.Stream2FlowSpec = cv.ConvertToSI(su.massflow, v); });

                    panel.CreateAndAddLabelRow("Outlet 3 (derived)");
                    panel.CreateAndAddTextBoxRow(nf, "Split Ratio (fraction, 0-1)", Convert.ToDouble(sp.Ratios[2]),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sp.Ratios[2] = v; });
                    panel.CreateAndAddDescriptionRow("Outlet 3 ratio is normally 1 - r1 - r2. Edit the upper rows in Split Ratios mode; outlet 3 is recomputed on solve.");
                    break;
                }

                case ObjectType.Valve:
                    var valve = (Valve)simobj;
                    var valveModes = new[] {
                        Valve.CalculationMode.OutletPressure, Valve.CalculationMode.DeltaP,
                        Valve.CalculationMode.Kv_Liquid, Valve.CalculationMode.Kv_Gas,
                        Valve.CalculationMode.Kv_Steam, Valve.CalculationMode.Kv_General
                    };
                    int vpos = Math.Max(0, Array.IndexOf(valveModes, valve.CalcMode));
                    panel.CreateAndAddDropDownRow("Calculation Mode", StringResources.valvecalcmode().ToList(), vpos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < valveModes.Length) valve.CalcMode = valveModes[dd.SelectedIndex]; });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Pressure (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, valve.OutletPressure.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) valve.OutletPressure = cv.ConvertToSI(su.pressure, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, valve.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) valve.DeltaP = cv.ConvertToSI(su.deltaP, v); });
                    break;

                case ObjectType.Pump:
                    var pump = (Pump)simobj;
                    var pumpModes = new[] {
                        Pump.CalculationMode.OutletPressure, Pump.CalculationMode.Delta_P,
                        Pump.CalculationMode.Power, Pump.CalculationMode.EnergyStream,
                        Pump.CalculationMode.Curves
                    };
                    int ppos = Math.Max(0, Array.IndexOf(pumpModes, pump.CalcMode));
                    panel.CreateAndAddDropDownRow("Calculation Mode", StringResources.pumpcalcmode().ToList(), ppos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < pumpModes.Length) pump.CalcMode = pumpModes[dd.SelectedIndex]; });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Pressure (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, pump.Pout),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pump.Pout = cv.ConvertToSI(su.pressure, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Increase (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, pump.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pump.DeltaP = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Efficiency (%)", pump.Eficiencia.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pump.Eficiencia = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Power (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, pump.DeltaQ.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pump.DeltaQ = cv.ConvertToSI(su.heatflow, v); });
                    break;

                case ObjectType.Compressor:
                    var ce = (Compressor)simobj;
                    var ceModes = new[] {
                        Compressor.CalculationMode.OutletPressure, Compressor.CalculationMode.Delta_P,
                        Compressor.CalculationMode.PowerRequired, Compressor.CalculationMode.EnergyStream,
                        Compressor.CalculationMode.Head, Compressor.CalculationMode.Curves,
                        Compressor.CalculationMode.PressureRatio
                    };
                    int cePos = Math.Max(0, Array.IndexOf(ceModes, ce.CalcMode));
                    panel.CreateAndAddDropDownRow("Calculation Mode", StringResources.comprcalcmode().ToList(), cePos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < ceModes.Length) ce.CalcMode = ceModes[dd.SelectedIndex]; });
                    panel.CreateAndAddDropDownRow("Thermodynamic Path",
                        new List<string> { "Adiabatic", "Polytropic" }, (int)ce.ProcessPath,
                        (dd, e) => ce.ProcessPath = (Compressor.ProcessPathType)dd.SelectedIndex);
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Pressure (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, ce.POut),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ce.POut = cv.ConvertToSI(su.pressure, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Increase (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, ce.DeltaP),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ce.DeltaP = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Ratio", ce.PressureRatio,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ce.PressureRatio = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Power Required (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, ce.DeltaQ),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ce.DeltaQ = cv.ConvertToSI(su.heatflow, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Adiabatic Efficiency (%)", ce.AdiabaticEfficiency,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ce.AdiabaticEfficiency = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Polytropic Efficiency (%)", ce.PolytropicEfficiency,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) ce.PolytropicEfficiency = v; });
                    break;

                case ObjectType.Expander:
                    var xe = (Exp)simobj;
                    var xeModes = new[] {
                        Exp.CalculationMode.OutletPressure, Exp.CalculationMode.Delta_P,
                        Exp.CalculationMode.PowerGenerated, Exp.CalculationMode.Head,
                        Exp.CalculationMode.Curves, Exp.CalculationMode.PressureRatio
                    };
                    int xePos = Math.Max(0, Array.IndexOf(xeModes, xe.CalcMode));
                    panel.CreateAndAddDropDownRow("Calculation Mode", StringResources.expndrcalcmode().ToList(), xePos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < xeModes.Length) xe.CalcMode = xeModes[dd.SelectedIndex]; });
                    panel.CreateAndAddDropDownRow("Thermodynamic Path",
                        new List<string> { "Adiabatic", "Polytropic" }, (int)xe.ProcessPath,
                        (dd, e) => xe.ProcessPath = (Exp.ProcessPathType)dd.SelectedIndex);
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Pressure (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, xe.POut),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) xe.POut = cv.ConvertToSI(su.pressure, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, xe.DeltaP),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) xe.DeltaP = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Power Generated (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, xe.DeltaQ),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) xe.DeltaQ = cv.ConvertToSI(su.heatflow, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Adiabatic Efficiency (%)", xe.AdiabaticEfficiency,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) xe.AdiabaticEfficiency = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Polytropic Efficiency (%)", xe.PolytropicEfficiency,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) xe.PolytropicEfficiency = v; });
                    break;

                case ObjectType.Heater:
                    var hc = (Heater)simobj;
                    var hcModes = new[] {
                        Heater.CalculationMode.HeatAdded, Heater.CalculationMode.OutletTemperature,
                        Heater.CalculationMode.OutletVaporFraction, Heater.CalculationMode.EnergyStream
                    };
                    int hcPos = Math.Max(0, Array.IndexOf(hcModes, hc.CalcMode));
                    panel.CreateAndAddDropDownRow("Calculation Mode", StringResources.heatercalcmode().ToList(), hcPos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < hcModes.Length) hc.CalcMode = hcModes[dd.SelectedIndex]; });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, hc.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hc.DeltaP = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, hc.OutletTemperature.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hc.OutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Heat Added (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, hc.DeltaQ.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hc.DeltaQ = cv.ConvertToSI(su.heatflow, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Efficiency (%)", hc.Eficiencia.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hc.Eficiencia = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Vapor Fraction", hc.OutletVaporFraction.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hc.OutletVaporFraction = v; });
                    break;

                case ObjectType.Cooler:
                    var cc = (Cooler)simobj;
                    var ccModes = new[] {
                        Cooler.CalculationMode.HeatRemoved, Cooler.CalculationMode.OutletTemperature,
                        Cooler.CalculationMode.OutletVaporFraction, Cooler.CalculationMode.EnergyStream
                    };
                    int ccPos = Math.Max(0, Array.IndexOf(ccModes, cc.CalcMode));
                    panel.CreateAndAddDropDownRow("Calculation Mode", StringResources.heatercalcmode().ToList(), ccPos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < ccModes.Length) cc.CalcMode = ccModes[dd.SelectedIndex]; });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, cc.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cc.DeltaP = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, cc.OutletTemperature.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cc.OutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Heat Removed (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, cc.DeltaQ.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cc.DeltaQ = cv.ConvertToSI(su.heatflow, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Efficiency (%)", cc.Eficiencia.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cc.Eficiencia = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Vapor Fraction", cc.OutletVaporFraction.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cc.OutletVaporFraction = v; });
                    break;

                case ObjectType.Vessel:
                    var vessel = (Vessel)simobj;
                    panel.CreateAndAddDropDownRow("Calculation Mode",
                        new List<string> { "Adiabatic", "Legacy", "Heating/Cooling Isothermic", "Heating/Cooling Isobaric" },
                        (int)vessel.CalculationMode,
                        (dd, e) => vessel.CalculationMode = (Vessel.CalculationModes)dd.SelectedIndex);
                    panel.CreateAndAddCheckBoxRow("Override Separation Pressure", vessel.OverrideP,
                        (cb, e) => vessel.OverrideP = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow(nf, "Separation Pressure (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, vessel.FlashPressure),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) vessel.FlashPressure = cv.ConvertToSI(su.pressure, v); });
                    panel.CreateAndAddCheckBoxRow("Override Separation Temperature", vessel.OverrideT,
                        (cb, e) => vessel.OverrideT = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow(nf, "Separation Temperature (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, vessel.FlashTemperature),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) vessel.FlashTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Heating/Cooling Amount (" + su.heatflow + ")",
                        vessel.HeatingCoolingAmount.HasValue ? cv.ConvertFromSI(su.heatflow, vessel.HeatingCoolingAmount.Value) : double.NaN,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) vessel.HeatingCoolingAmount = cv.ConvertToSI(su.heatflow, v); });
                    break;

                case ObjectType.HeatExchanger:
                    var hx = (HeatExchanger)simobj;
                    panel.CreateAndAddDropDownRow("Calculation Mode",
                        StringResources.hxcalcmode().ToList(), (int)hx.CalculationMode,
                        (dd, e) => hx.CalculationMode = (HeatExchangerCalcMode)dd.SelectedIndex);
                    int hxFlowPos = hx.FlowDir == FlowDirection.CoCurrent ? 0 : 1;
                    panel.CreateAndAddDropDownRow("Flow Direction",
                        StringResources.hxflowdir().ToList(), hxFlowPos,
                        (dd, e) => hx.FlowDir = dd.SelectedIndex == 0 ? FlowDirection.CoCurrent : FlowDirection.CounterCurrent);
                    int hxTempPos = hx.DefinedTemperature == SpecifiedTemperature.Cold_Fluid ? 0 : 1;
                    panel.CreateAndAddDropDownRow("Defined Temperature (Calc Area Mode)",
                        StringResources.hxspectemp().ToList(), hxTempPos,
                        (dd, e) => hx.DefinedTemperature = dd.SelectedIndex == 0 ? SpecifiedTemperature.Cold_Fluid : SpecifiedTemperature.Hot_Fluid);
                    panel.CreateAndAddTextBoxRow(nf, "Overall HTC (" + su.heat_transf_coeff + ")",
                        cv.ConvertFromSI(su.heat_transf_coeff, hx.OverallCoefficient.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.OverallCoefficient = cv.ConvertToSI(su.heat_transf_coeff, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Heat Exchange Area (" + su.area + ")",
                        cv.ConvertFromSI(su.area, hx.Area.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.Area = cv.ConvertToSI(su.area, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Heat Exchanged (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, hx.Q.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.Q = cv.ConvertToSI(su.heatflow, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (Hot) (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, hx.HotSideOutletTemperature),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.HotSideOutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (Cold) (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, hx.ColdSideOutletTemperature),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.ColdSideOutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (Hot) (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, hx.HotSidePressureDrop),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.HotSidePressureDrop = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (Cold) (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, hx.ColdSidePressureDrop),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.ColdSidePressureDrop = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Min Temperature Approach (" + su.deltaT + ")",
                        cv.ConvertFromSI(su.deltaT, hx.MITA),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.MITA = cv.ConvertToSI(su.deltaT, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Thermal Efficiency (%)", hx.ThermalEfficiency,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.ThermalEfficiency = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Heat Loss (" + su.heatflow + ")",
                        cv.ConvertFromSI(su.heatflow, hx.HeatLoss),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.HeatLoss = cv.ConvertToSI(su.heatflow, v); });
                    panel.CreateAndAddCheckBoxRow("Force Pinch at Outlets", hx.PinchPointAtOutlets,
                        (cb, e) => hx.PinchPointAtOutlets = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Vapor Fraction (Stream 1)", hx.OutletVaporFraction1,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.OutletVaporFraction1 = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Vapor Fraction (Stream 2)", hx.OutletVaporFraction2,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) hx.OutletVaporFraction2 = v; });
                    break;

                case ObjectType.RCT_CSTR:
                    var cstr = (Reactor_CSTR)simobj;
                    var rsetsCstr = simobj.GetFlowsheet().ReactionSets.Values.Select(x => x.Name).ToList();
                    if (!simobj.GetFlowsheet().ReactionSets.ContainsKey(cstr.ReactionSetID))
                        cstr.ReactionSetID = simobj.GetFlowsheet().ReactionSets.Keys.First();
                    var selnameCstr = simobj.GetFlowsheet().ReactionSets[cstr.ReactionSetID].Name;
                    panel.CreateAndAddDropDownRow("Reaction Set", rsetsCstr, rsetsCstr.IndexOf(selnameCstr), (dd, e) =>
                    {
                        var rs = simobj.GetFlowsheet().ReactionSets.Values.FirstOrDefault(x => x.Name == dd.SelectedItem?.ToString());
                        if (rs != null) cstr.ReactionSetID = rs.ID;
                    });
                    var cstrModeList = new List<string>(StringResources.rctcalcmode()) { "Heat Exchange" };
                    int cstrModePos;
                    switch (cstr.ReactorOperationMode)
                    {
                        case OperationMode.Isothermic: cstrModePos = 1; break;
                        case OperationMode.OutletTemperature: cstrModePos = 2; break;
                        case OperationMode.HeatExchange: cstrModePos = 3; break;
                        default: cstrModePos = 0; break;
                    }
                    panel.CreateAndAddDropDownRow("Calculation Mode", cstrModeList, cstrModePos, (dd, e) =>
                    {
                        switch (dd.SelectedIndex)
                        {
                            case 1: cstr.ReactorOperationMode = OperationMode.Isothermic; break;
                            case 2: cstr.ReactorOperationMode = OperationMode.OutletTemperature; break;
                            case 3: cstr.ReactorOperationMode = OperationMode.HeatExchange; break;
                            default: cstr.ReactorOperationMode = OperationMode.Adiabatic; break;
                        }
                    });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, cstr.OutletTemperature),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.OutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Reactor Volume (" + su.volume + ")",
                        cv.ConvertFromSI(su.volume, cstr.Volume),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.Volume = cv.ConvertToSI(su.volume, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Headspace Volume (" + su.volume + ")",
                        cv.ConvertFromSI(su.volume, cstr.Headspace),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.Headspace = cv.ConvertToSI(su.volume, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Catalyst Amount (" + su.mass + ")",
                        cv.ConvertFromSI(su.mass, cstr.CatalystAmount),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.CatalystAmount = cv.ConvertToSI(su.mass, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, cstr.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.DeltaP = cv.ConvertToSI(su.deltaP, v); });
                    if (cstr.ReactorOperationMode == OperationMode.HeatExchange)
                    {
                        panel.CreateAndAddLabelRow("Heat Exchange (Utility Fluid)");
                        panel.CreateAndAddDropDownRow("Heat Exchange Area Mode",
                            new List<string> { "Auto (from geometry)", "User-Specified" },
                            (int)cstr.HeatExchangeAreaCalculationMode,
                            (dd, e) => cstr.HeatExchangeAreaCalculationMode = (HeatExchangeAreaMode)dd.SelectedIndex);
                        panel.CreateAndAddTextBoxRow(nf, "Heat Exchange Area (" + su.area + ")",
                            cv.ConvertFromSI(su.area, cstr.HeatExchangeArea),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.HeatExchangeArea = cv.ConvertToSI(su.area, v); });
                        panel.CreateAndAddTextBoxRow(nf, "Overall HTC (" + su.heat_transf_coeff + ")",
                            cv.ConvertFromSI(su.heat_transf_coeff, cstr.OverallHeatTransferCoefficient),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.OverallHeatTransferCoefficient = cv.ConvertToSI(su.heat_transf_coeff, v); });
                        panel.CreateAndAddDropDownRow("Coolant Flow Direction",
                            new List<string> { "Constant Temperature", "Co-current", "Counter-current" },
                            (int)cstr.HeatExchangeCoolantFlowDirection,
                            (dd, e) => cstr.HeatExchangeCoolantFlowDirection = (HeatExchangeCoolantMode)dd.SelectedIndex);
                        panel.CreateAndAddTextBoxRow(nf, "Coolant Inlet Temperature (" + su.temperature + ")",
                            cv.ConvertFromSI(su.temperature, cstr.CoolantInletTemperature),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.CoolantInletTemperature = cv.ConvertToSI(su.temperature, v); });
                        panel.CreateAndAddTextBoxRow(nf, "Coolant Mass Flow (" + su.massflow + ")",
                            cv.ConvertFromSI(su.massflow, cstr.CoolantMassFlowRate),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.CoolantMassFlowRate = cv.ConvertToSI(su.massflow, v); });
                        panel.CreateAndAddTextBoxRow(nf, "Coolant Specific Heat (" + su.heatCapacityCp + ")",
                            cv.ConvertFromSI(su.heatCapacityCp, cstr.CoolantSpecificHeat),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) cstr.CoolantSpecificHeat = cv.ConvertToSI(su.heatCapacityCp, v); });
                    }
                    break;

                case ObjectType.RCT_PFR:
                    var pfr = (Reactor_PFR)simobj;
                    var rsetsPfr = simobj.GetFlowsheet().ReactionSets.Values.Select(x => x.Name).ToList();
                    if (!simobj.GetFlowsheet().ReactionSets.ContainsKey(pfr.ReactionSetID))
                        pfr.ReactionSetID = simobj.GetFlowsheet().ReactionSets.Keys.First();
                    var selnamePfr = simobj.GetFlowsheet().ReactionSets[pfr.ReactionSetID].Name;
                    panel.CreateAndAddDropDownRow("Reaction Set", rsetsPfr, rsetsPfr.IndexOf(selnamePfr), (dd, e) =>
                    {
                        var rs = simobj.GetFlowsheet().ReactionSets.Values.FirstOrDefault(x => x.Name == dd.SelectedItem?.ToString());
                        if (rs != null) pfr.ReactionSetID = rs.ID;
                    });
                    var pfrModeList = new List<string>(StringResources.rctcalcmode2()) { "Heat Exchange" };
                    int pfrModePos;
                    switch (pfr.ReactorOperationMode)
                    {
                        case OperationMode.Isothermic: pfrModePos = 1; break;
                        case OperationMode.OutletTemperature: pfrModePos = 2; break;
                        case OperationMode.NonIsothermalNonAdiabatic: pfrModePos = 3; break;
                        case OperationMode.HeatExchange: pfrModePos = 4; break;
                        default: pfrModePos = 0; break;
                    }
                    panel.CreateAndAddDropDownRow("Calculation Mode", pfrModeList, pfrModePos, (dd, e) =>
                    {
                        switch (dd.SelectedIndex)
                        {
                            case 1: pfr.ReactorOperationMode = OperationMode.Isothermic; break;
                            case 2: pfr.ReactorOperationMode = OperationMode.OutletTemperature; break;
                            case 3: pfr.ReactorOperationMode = OperationMode.NonIsothermalNonAdiabatic; break;
                            case 4: pfr.ReactorOperationMode = OperationMode.HeatExchange; break;
                            default: pfr.ReactorOperationMode = OperationMode.Adiabatic; break;
                        }
                    });
                    panel.CreateAndAddDropDownRow("ODE Solver",
                        new List<string> { "Implicit Runge-Kutta", "Explicit Runge-Kutta", "Adams-Moulton", "Gear's BDF" },
                        pfr.InternalSolver,
                        (dd, e) => pfr.InternalSolver = dd.SelectedIndex);
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, pfr.OutletTemperature),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.OutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddLabelRow("Sizing");
                    panel.CreateAndAddDropDownRow("Sizing Mode",
                        new List<string> { "Specify Length", "Specify Diameter" },
                        pfr.ReactorSizingType == Reactor_PFR.SizingType.Length ? 0 : 1,
                        (dd, e) => pfr.ReactorSizingType = dd.SelectedIndex == 0 ? Reactor_PFR.SizingType.Length : Reactor_PFR.SizingType.Diameter);
                    panel.CreateAndAddTextBoxRow(nf, "Reactive Volume (" + su.volume + ")",
                        cv.ConvertFromSI(su.volume, pfr.Volume),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.Volume = cv.ConvertToSI(su.volume, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Tube Length (" + su.distance + ")",
                        cv.ConvertFromSI(su.distance, pfr.Length),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.Length = cv.ConvertToSI(su.distance, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Tube Diameter (" + su.diameter + ")",
                        cv.ConvertFromSI(su.diameter, pfr.Diameter),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.Diameter = cv.ConvertToSI(su.diameter, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Number of Tubes", (double)pfr.NumberOfTubes,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.NumberOfTubes = (int)v; });
                    panel.CreateAndAddTextBoxRow(nf, "ODE Volume Step (0-1)", pfr.dV,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.dV = v; });
                    panel.CreateAndAddLabelRow("Catalyst");
                    panel.CreateAndAddTextBoxRow(nf, "Catalyst Loading (" + su.density + ")",
                        cv.ConvertFromSI(su.density, pfr.CatalystLoading),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.CatalystLoading = cv.ConvertToSI(su.density, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Catalyst Particle Diameter (" + su.diameter + ")",
                        cv.ConvertFromSI(su.diameter, pfr.CatalystParticleDiameter),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.CatalystParticleDiameter = cv.ConvertToSI(su.diameter, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Catalyst Void Fraction", pfr.CatalystVoidFraction,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.CatalystVoidFraction = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Catalyst Particle Sphericity", pfr.CatalystParticleSphericity,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.CatalystParticleSphericity = v; });
                    panel.CreateAndAddCheckBoxRow("Use User-Defined Pressure Drop", pfr.UseUserDefinedPressureDrop,
                        (cb, e) => pfr.UseUserDefinedPressureDrop = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.deltaP + ")",
                        cv.ConvertFromSI(su.deltaP, pfr.UserDefinedPressureDrop),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.UserDefinedPressureDrop = cv.ConvertToSI(su.deltaP, v); });
                    panel.CreateAndAddDropDownRow("Slurry Viscosity Correction",
                        new List<string> { "Disabled", "Yoshida et al" },
                        pfr.SlurryViscosityMode,
                        (dd, e) => pfr.SlurryViscosityMode = dd.SelectedIndex);
                    if (pfr.ReactorOperationMode == OperationMode.HeatExchange)
                    {
                        panel.CreateAndAddLabelRow("Heat Exchange (Utility Fluid)");
                        panel.CreateAndAddDropDownRow("Heat Exchange Area Mode",
                            new List<string> { "Auto (from geometry)", "User-Specified" },
                            (int)pfr.HeatExchangeAreaCalculationMode,
                            (dd, e) => pfr.HeatExchangeAreaCalculationMode = (HeatExchangeAreaMode)dd.SelectedIndex);
                        panel.CreateAndAddTextBoxRow(nf, "Heat Exchange Area (" + su.area + ")",
                            cv.ConvertFromSI(su.area, pfr.HeatExchangeArea),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.HeatExchangeArea = cv.ConvertToSI(su.area, v); });
                        panel.CreateAndAddTextBoxRow(nf, "Overall HTC (" + su.heat_transf_coeff + ")",
                            cv.ConvertFromSI(su.heat_transf_coeff, pfr.OverallHeatTransferCoefficient),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.OverallHeatTransferCoefficient = cv.ConvertToSI(su.heat_transf_coeff, v); });
                        panel.CreateAndAddDropDownRow("Coolant Flow Direction",
                            new List<string> { "Constant Temperature", "Co-current", "Counter-current" },
                            (int)pfr.HeatExchangeCoolantFlowDirection,
                            (dd, e) => pfr.HeatExchangeCoolantFlowDirection = (HeatExchangeCoolantMode)dd.SelectedIndex);
                        panel.CreateAndAddTextBoxRow(nf, "Coolant Inlet Temperature (" + su.temperature + ")",
                            cv.ConvertFromSI(su.temperature, pfr.CoolantInletTemperature),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.CoolantInletTemperature = cv.ConvertToSI(su.temperature, v); });
                        panel.CreateAndAddTextBoxRow(nf, "Coolant Mass Flow (" + su.massflow + ")",
                            cv.ConvertFromSI(su.massflow, pfr.CoolantMassFlowRate),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.CoolantMassFlowRate = cv.ConvertToSI(su.massflow, v); });
                        panel.CreateAndAddTextBoxRow(nf, "Coolant Specific Heat (" + su.heatCapacityCp + ")",
                            cv.ConvertFromSI(su.heatCapacityCp, pfr.CoolantSpecificHeat),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) pfr.CoolantSpecificHeat = cv.ConvertToSI(su.heatCapacityCp, v); });
                    }
                    break;

                case ObjectType.DistillationColumn:
                case ObjectType.RefluxedAbsorber:
                case ObjectType.ReboiledAbsorber:
                {
                    var dc = (DistillationColumn)simobj;
                    PopulateColumnEditor(dc, panel, su, nf);
                    break;
                }

                case ObjectType.AbsorptionColumn:
                {
                    var ac = (AbsorptionColumn)simobj;
                    string[] absModes = { "Absorber", "Liquid-Liquid Extraction" };
                    int modeIdx = (int)ac._opmode;
                    panel.CreateAndAddDropDownRow("Operating Mode", absModes.ToList(), modeIdx, (dd, e) =>
                        { if (dd.SelectedIndex >= 0) ac._opmode = (AbsorptionColumn.OpMode)dd.SelectedIndex; });
                    PopulateColumnEditor(ac, panel, su, nf);
                    break;
                }

                case ObjectType.ShortcutColumn:
                {
                    var sc = (ShortcutColumn)simobj;
                    var compounds = simobj.GetFlowsheet().SelectedCompounds.Keys.ToList();

                    panel.CreateAndAddDropDownRow("Light Key Compound", compounds,
                        Math.Max(0, compounds.IndexOf(sc.m_lightkey)), (dd, e) =>
                        { if (dd.SelectedItem is string s) sc.m_lightkey = s; });
                    panel.CreateAndAddDropDownRow("Heavy Key Compound", compounds,
                        Math.Max(0, compounds.IndexOf(sc.m_heavykey)), (dd, e) =>
                        { if (dd.SelectedItem is string s) sc.m_heavykey = s; });

                    panel.CreateAndAddTextBoxRow(nf, "LK Mole Fraction in Bottoms", sc.m_lightkeymolarfrac,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sc.m_lightkeymolarfrac = v; });
                    panel.CreateAndAddTextBoxRow(nf, "HK Mole Fraction in Distillate", sc.m_heavykeymolarfrac,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sc.m_heavykeymolarfrac = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Reflux Ratio", sc.m_refluxratio,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sc.m_refluxratio = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Condenser Pressure (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, sc.m_condenserpressure),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sc.m_condenserpressure = cv.ConvertToSI(su.pressure, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Reboiler Pressure (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, sc.m_boilerpressure),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sc.m_boilerpressure = cv.ConvertToSI(su.pressure, v); });
                    break;
                }

                case ObjectType.Filter:
                {
                    var f = (Filter)simobj;
                    panel.CreateAndAddTwoLabelsRow("Calculated Pressure Drop (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, f.PressureDrop).ToString(nf, IC));
                    panel.CreateAndAddNumericEditorRow("Total Filter Area (m²)", f.TotalFilterArea, 0.001, 100000, 4,
                        (sp, e) => f.TotalFilterArea = (double)sp.Value.GetValueOrDefault());
                    panel.CreateAndAddNumericEditorRow("Submerged Area Fraction", f.SubmergedAreaFraction, 0, 1, 4,
                        (sp, e) => f.SubmergedAreaFraction =  (double)sp.Value.GetValueOrDefault());
                    panel.CreateAndAddNumericEditorRow("Cake Relative Humidity (fraction)", f.CakeRelativeHumidity, 0, 1, 4,
                        (sp, e) => f.CakeRelativeHumidity =  (double)sp.Value.GetValueOrDefault());
                    panel.CreateAndAddNumericEditorRow("Filter Cycle Time (s)", f.FilterCycleTime, 1, 86400, 0,
                        (sp, e) => f.FilterCycleTime =  (double)sp.Value.GetValueOrDefault());
                    break;
                }

                case ObjectType.Pipe:
                {
                    var p = (Pipe)simobj;
                    panel.CreateAndAddTwoLabelsRow("Calculated Pressure Drop (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, p.DeltaP.GetValueOrDefault()).ToString(nf, IC));

                    // Load section type list from embedded resource. If anything goes wrong,
                    // surface it via the message bus so the user sees why only "Straight Tube
                    // Section" is offered instead of letting the failure disappear silently.
                    var secTypes = new List<string> { "Straight Tube Section" };
                    try
                    {
                        var asm = Assembly.GetAssembly(typeof(PipeSection));
                        using (var stream = asm != null ? asm.GetManifestResourceStream("DWSIM.UnitOperations.fittings.dat") : null)
                        {
                            if (stream != null)
                            {
                                using (var reader = new StreamReader(stream))
                                    while (!reader.EndOfStream)
                                    {
                                        var line = reader.ReadLine();
                                        if (line != null) secTypes.Add(line.Split(';')[0]);
                                    }
                            }
                            else
                            {
                                simobj.GetFlowsheet().ShowMessage(
                                    "Pipe editor: embedded resource 'fittings.dat' not found. Only straight-tube sections will be selectable.",
                                    IFlowsheet.MessageType.Warning);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        simobj.GetFlowsheet().ShowMessage(
                            "Pipe editor: failed to read fittings.dat - " + ex.Message,
                            IFlowsheet.MessageType.GeneralError);
                    }

                    var materials = new List<string>
                    {
                        "Carbon Steel", "Common Steel", "Cast Iron", "Stainless Steel",
                        "PVC", "PVC+PFRV", "Commercial Copper", "User-Defined"
                    };

                    void AddSectionEditor(PipeSection sec, int idx)
                    {
                        panel.CreateAndAddLabelRow($"Section {idx + 1}");
                        panel.CreateAndAddTwoLabelsRow("Segment Index", sec.Indice.ToString());

                        int typeIdx = secTypes.IndexOf(sec.TipoSegmento);
                        if (typeIdx < 0) typeIdx = 0;
                        panel.CreateAndAddDropDownRow("Type", secTypes, typeIdx,
                            (dd, e) => { if (dd.SelectedIndex >= 0) sec.TipoSegmento = secTypes[dd.SelectedIndex]; });

                        int matIdx = materials.IndexOf(sec.Material);
                        if (matIdx < 0) matIdx = 0;
                        panel.CreateAndAddDropDownRow("Material", materials, matIdx,
                            (dd, e) => { if (dd.SelectedIndex >= 0) sec.Material = materials[dd.SelectedIndex]; });

                        panel.CreateAndAddTextBoxRow(nf, "Length (" + su.distance + ")",
                            cv.ConvertFromSI(su.distance, sec.Comprimento),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) sec.Comprimento = cv.ConvertToSI(su.distance, v); });

                        panel.CreateAndAddTextBoxRow(nf, "Elevation (" + su.distance + ")",
                            cv.ConvertFromSI(su.distance, sec.Elevacao),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) sec.Elevacao = cv.ConvertToSI(su.distance, v); });

                        panel.CreateAndAddTextBoxRow(nf, "Internal Diameter (" + su.diameter + ")",
                            cv.Convert("in", su.diameter, sec.DI),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) sec.DI = cv.Convert(su.diameter, "in", v); });

                        panel.CreateAndAddTextBoxRow(nf, "External Diameter (" + su.diameter + ")",
                            cv.Convert("in", su.diameter, sec.DE),
                            (tb, e) => { if (TryVal(tb.Text, out var v)) sec.DE = cv.Convert(su.diameter, "in", v); });

                        panel.CreateAndAddTextBoxRow("N0", "Increments",
                            sec.Incrementos,
                            (tb, e) => { if (int.TryParse(tb.Text, out var v)) sec.Incrementos = v; });

                        panel.CreateAndAddTextBoxRow("N0", "Quantity",
                            sec.Quantidade,
                            (tb, e) => { if (int.TryParse(tb.Text, out var v)) sec.Quantidade = v; });
                    }

                    int secIndex = 0;
                    foreach (var sec in p.Profile.Sections.Values)
                        AddSectionEditor(sec, secIndex++);

                    panel.CreateAndAddButtonRow("Add Section", null, (btn, e) =>
                    {
                        var newSec = new PipeSection
                        {
                            Indice     = p.Profile.Sections.Count + 1,
                            TipoSegmento = "Straight Tube Section",
                            Material   = "Carbon Steel",
                            Comprimento = 1.0,
                            Incrementos = 5,
                            Quantidade  = 1,
                            DI          = 2.067,
                            DE          = 2.375
                        };
                        p.Profile.Sections[newSec.Indice] = newSec;
                        // Structural change: tell the host to rebuild the editor so the new
                        // section's fields appear without forcing the user to reselect the pipe.
                        simobj.GetFlowsheet().UpdateOpenEditForms();
                    });

                    panel.CreateAndAddButtonRow("Remove Last Section", null, (btn, e) =>
                    {
                        if (p.Profile.Sections.Count == 0) return;
                        var lastKey = p.Profile.Sections.Keys.Max();
                        p.Profile.Sections.Remove(lastKey);
                        simobj.GetFlowsheet().UpdateOpenEditForms();
                    });

                    break;
                }

                case ObjectType.RCT_Conversion:
                {
                    var rc = (Reactor_Conversion)simobj;
                    var rcSets = rc.GetFlowsheet().ReactionSets.Values.Select(x => x.Name).ToList();
                    var rcSelRS = rc.GetFlowsheet().ReactionSets.Values.FirstOrDefault(x => x.ID == rc.ReactionSetID);
                    if (rcSets.Count > 0)
                        panel.CreateAndAddDropDownRow("Reaction Set", rcSets, rcSelRS != null ? rcSets.IndexOf(rcSelRS.Name) : 0,
                            (dd, e) => { var rs = rc.GetFlowsheet().ReactionSets.Values.ElementAtOrDefault(dd.SelectedIndex); if (rs != null) rc.ReactionSetID = rs.ID; });
                    string[] rcModeNames = { "Adiabatic", "Isothermal", "Outlet Temperature" };
                    OperationMode[] rcModeVals = { OperationMode.Adiabatic, OperationMode.Isothermic, OperationMode.OutletTemperature };
                    int rcModePos = Array.IndexOf(rcModeVals, rc.ReactorOperationMode); if (rcModePos < 0) rcModePos = 0;
                    panel.CreateAndAddDropDownRow("Calculation Mode", rcModeNames.ToList(), rcModePos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < rcModeVals.Length) rc.ReactorOperationMode = rcModeVals[dd.SelectedIndex]; });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, rc.OutletTemperature),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) rc.OutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, rc.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) rc.DeltaP = cv.ConvertToSI(su.pressure, v); });
                    break;
                }

                case ObjectType.RCT_Equilibrium:
                {
                    var req = (Reactor_Equilibrium)simobj;
                    var reqSets = req.GetFlowsheet().ReactionSets.Values.Select(x => x.Name).ToList();
                    var reqSelRS = req.GetFlowsheet().ReactionSets.Values.FirstOrDefault(x => x.ID == req.ReactionSetID);
                    if (reqSets.Count > 0)
                        panel.CreateAndAddDropDownRow("Reaction Set", reqSets, reqSelRS != null ? reqSets.IndexOf(reqSelRS.Name) : 0,
                            (dd, e) => { var rs = req.GetFlowsheet().ReactionSets.Values.ElementAtOrDefault(dd.SelectedIndex); if (rs != null) req.ReactionSetID = rs.ID; });
                    string[] reqModeNames = { "Adiabatic", "Isothermal", "Outlet Temperature" };
                    OperationMode[] reqModeVals = { OperationMode.Adiabatic, OperationMode.Isothermic, OperationMode.OutletTemperature };
                    int reqModePos = Array.IndexOf(reqModeVals, req.ReactorOperationMode); if (reqModePos < 0) reqModePos = 0;
                    panel.CreateAndAddDropDownRow("Calculation Mode", reqModeNames.ToList(), reqModePos,
                        (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < reqModeVals.Length) req.ReactorOperationMode = reqModeVals[dd.SelectedIndex]; });
                    panel.CreateAndAddTextBoxRow(nf, "Outlet Temperature (" + su.temperature + ")",
                        cv.ConvertFromSI(su.temperature, req.OutletTemperature),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) req.OutletTemperature = cv.ConvertToSI(su.temperature, v); });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, req.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) req.DeltaP = cv.ConvertToSI(su.pressure, v); });
                    break;
                }

                case ObjectType.RCT_Gibbs:
                {
                    var rg = (Reactor_Gibbs)simobj;
                    panel.CreateAndAddDropDownRow("Reactive Phase Behavior",
                        new List<string> { "Calculate Equilibrium", "Vapor", "Liquid", "Solid" },
                        (int)rg.ReactivePhaseBehavior,
                        (dd, e) => { if (dd.SelectedIndex >= 0) rg.ReactivePhaseBehavior = (Reactor_Gibbs.ReactivePhaseType)dd.SelectedIndex; });
                    panel.CreateAndAddTextBoxRow(nf, "Pressure Drop (" + su.pressure + ")",
                        cv.ConvertFromSI(su.pressure, rg.DeltaP.GetValueOrDefault()),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) rg.DeltaP = cv.ConvertToSI(su.pressure, v); });

                    panel.CreateAndAddLabelRow("Reactive Compounds");
                    panel.CreateAndAddDescriptionRow(
                        "Compounds taken into account by the Gibbs energy minimization. Leaving all of them unchecked makes the reactor treat every compound as reactive.");
                    if (rg.ComponentIDs == null) rg.ComponentIDs = new List<string>();
                    foreach (var comp in simobj.GetFlowsheet().SelectedCompounds.Values)
                    {
                        var cname = comp.Name;
                        panel.CreateAndAddCheckBoxRow(cname, rg.ComponentIDs.Contains(cname), (cb, e) =>
                        {
                            if (cb.IsChecked.GetValueOrDefault())
                            {
                                if (!rg.ComponentIDs.Contains(cname)) rg.ComponentIDs.Add(cname);
                            }
                            else rg.ComponentIDs.Remove(cname);
                        });
                    }

                    panel.CreateAndAddLabelRow("Solver Settings");
                    panel.CreateAndAddCheckBoxRow("Use IPOPT Solver", rg.UseIPOPTSolver,
                        (cb, e) => rg.UseIPOPTSolver = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Use Alternate Solving Method", rg.AlternateSolvingMethod,
                        (cb, e) => rg.AlternateSolvingMethod = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Initialize from Previous Solution", rg.InitializeFromPreviousSolution,
                        (cb, e) => rg.InitializeFromPreviousSolution = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Enable Damping", rg.EnableDamping,
                        (cb, e) => rg.EnableDamping = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow("N0", "Maximum Internal Iterations", rg.MaximumInternalIterations,
                        (tb, e) => { if (TryVal(tb.Text, out var v) && v >= 1) rg.MaximumInternalIterations = (int)v; });
                    panel.CreateAndAddTextBoxRow("G", "Internal Tolerance", rg.InternalTolerance,
                        (tb, e) => { if (TryVal(tb.Text, out var v) && v > 0) rg.InternalTolerance = v; });
                    panel.CreateAndAddTextBoxRow("G", "Derivative Perturbation", rg.DerivativePerturbation,
                        (tb, e) => { if (TryVal(tb.Text, out var v) && v > 0) rg.DerivativePerturbation = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Lagrange Coeff. Estimation Temperature (K)",
                        rg.LagrangeCoeffsEstimationTemperature,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) rg.LagrangeCoeffsEstimationTemperature = v; });

                    // External non-linear minimization solvers registered by extensions.
                    var extSolvers = simobj.GetFlowsheet().ExternalSolvers.Values
                        .Where(x => x.Category == DWSIM.Interfaces.Enums.ExternalSolverCategory.NonLinearMinimization)
                        .ToList();
                    if (extSolvers.Count > 0)
                    {
                        var solverNames = new List<string> { "(built-in)" };
                        solverNames.AddRange(extSolvers.Select(x => x.DisplayText));
                        var selIdx = extSolvers.FindIndex(x => x.ID == rg.ExternalSolverID) + 1;
                        panel.CreateAndAddDropDownRow("External Solver", solverNames, Math.Max(0, selIdx), (dd, e) =>
                        {
                            rg.ExternalSolverID = dd.SelectedIndex <= 0
                                ? ""
                                : extSolvers[dd.SelectedIndex - 1].ID;
                        });
                    }

                    panel.CreateAndAddLabelRow("Results");
                    panel.CreateAndAddTwoLabelsRow("Initial Gibbs Energy", rg.InitialGibbsEnergy.ToString(nf, IC));
                    panel.CreateAndAddTwoLabelsRow("Final Gibbs Energy", rg.FinalGibbsEnergy.ToString(nf, IC));
                    panel.CreateAndAddTwoLabelsRow("Element Balance", rg.ElementBalance.ToString(nf, IC));

                    panel.CreateAndAddButtonRow("Edit Element Matrix...", null,
                        (btn, e) => ShowGibbsElementMatrixEditor(rg));
                    panel.CreateAndAddDescriptionRow("The matrix starts from the compound formulas and can be overridden, the same way the Windows editor allows.");
                    break;
                }

                case ObjectType.OT_Recycle:
                {
                    var rcl = (Recycle)simobj;
                    panel.CreateAndAddTwoLabelsRow("Converged", rcl.Converged ? "Yes" : "No");
                    panel.CreateAndAddTwoLabelsRow("Iterations Taken", rcl.IterationsTaken.ToString());
                    panel.CreateAndAddNumericEditorRow("Maximum Iterations", rcl.MaximumIterations, 1, 10000, 0,
                        (sp, e) => rcl.MaximumIterations = (int)sp.Value.GetValueOrDefault());
                    panel.CreateAndAddNumericEditorRow("Smoothing Factor", rcl.SmoothingFactor, 0.001, 1, 4,
                        (sp, e) => rcl.SmoothingFactor =  (double)sp.Value.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Legacy Convergence Mode", rcl.LegacyMode,
                        (cb, e) => rcl.LegacyMode = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddDropDownRow("Acceleration Method",
                        new List<string> { "None", "Wegstein", "Dominant Eigenvalue", "Global Broyden" },
                        (int)rcl.AccelerationMethod,
                        (dd, e) => { if (dd.SelectedIndex >= 0) rcl.AccelerationMethod = (AccelMethod)dd.SelectedIndex; });
                    break;
                }

                case ObjectType.OT_EnergyRecycle:
                {
                    var er = (EnergyRecycle)simobj;
                    panel.CreateAndAddTwoLabelsRow("Iterations Taken", er.IterationsTaken.ToString());
                    panel.CreateAndAddNumericEditorRow("Maximum Iterations", er.MaximumIterations, 1, 10000, 0,
                        (sp, e) => er.MaximumIterations = (int)sp.Value.GetValueOrDefault());
                    panel.CreateAndAddDropDownRow("Acceleration Method",
                        new List<string> { "None", "Wegstein", "Dominant Eigenvalue", "Global Broyden" },
                        (int)er.AccelerationMethod,
                        (dd, e) => { if (dd.SelectedIndex >= 0) er.AccelerationMethod = (AccelMethod)dd.SelectedIndex; });
                    break;
                }

                case ObjectType.OT_Adjust:
                {
                    var adj = (Adjust)simobj;
                    panel.CreateAndAddTwoLabelsRow("Manipulated Object", adj.ManipulatedObjectData?.Name ?? "—");
                    panel.CreateAndAddTwoLabelsRow("Manipulated Property", adj.ManipulatedObjectData?.PropertyName ?? "—");
                    panel.CreateAndAddTwoLabelsRow("Controlled Object", adj.ControlledObjectData?.Name ?? "—");
                    panel.CreateAndAddTwoLabelsRow("Controlled Property", adj.ControlledObjectData?.PropertyName ?? "—");
                    panel.CreateAndAddDropDownRow("Solving Method",
                        new List<string> { "Bisection", "Secant Method" }, adj.SolvingMethodSelf,
                        (dd, e) => adj.SolvingMethodSelf = dd.SelectedIndex);
                    panel.CreateAndAddTextBoxRow(nf, "Minimum Value", adj.MinVal.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) adj.MinVal = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Maximum Value", adj.MaxVal.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) adj.MaxVal = v; });
                    panel.CreateAndAddCheckBoxRow("Simultaneous Adjust", adj.SimultaneousAdjust,
                        (cb, e) => adj.SimultaneousAdjust = cb.IsChecked.GetValueOrDefault());
                    break;
                }

                case ObjectType.OT_Spec:
                {
                    var spec = (Spec)simobj;
                    panel.CreateAndAddTwoLabelsRow("Status", spec.Status ?? "—");
                    panel.CreateAndAddCheckBoxRow("Recalculate Target Object", spec.CalculateTargetObject,
                        (cb, e) => spec.CalculateTargetObject = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow(nf, "Minimum Clamp Value", spec.MinVal.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) spec.MinVal = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Maximum Clamp Value", spec.MaxVal.GetValueOrDefault(),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) spec.MaxVal = v; });

                    panel.CreateAndAddLabelRow("Source Variable");
                    panel.CreateAndAddDescriptionRow("The variable the specification reads.");
                    if (spec.SourceObjectData == null)
                        spec.SourceObjectData = new DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo();
                    AddSpecialOpVariablePicker(panel, simobj, spec.SourceObjectData, su);

                    panel.CreateAndAddLabelRow("Target Variable");
                    panel.CreateAndAddDescriptionRow("The variable the specification writes, after applying the expression.");
                    if (spec.TargetObjectData == null)
                        spec.TargetObjectData = new DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo();
                    AddSpecialOpVariablePicker(panel, simobj, spec.TargetObjectData, su);

                    panel.CreateAndAddLabelRow("Expression");
                    panel.CreateAndAddDescriptionRow(
                        "Relation between the two variables, with the source value as 'X'. For example: X, 2*X, X^0.5 + 10.");
                    panel.CreateAndAddStringEditorRow("f(X)", spec.Expression ?? "X",
                        (tb, e) => spec.Expression = tb.Text ?? "");
                    break;
                }

                case ObjectType.Controller_PID:
                {
                    var pid = (PIDController)simobj;
                    panel.CreateAndAddCheckBoxRow("Active", pid.Active,
                        (cb, e) => pid.Active = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Manual Override", pid.ManualOverride,
                        (cb, e) => pid.ManualOverride = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Reverse Acting", pid.ReverseActing,
                        (cb, e) => pid.ReverseActing = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow(nf, "Proportional Gain (Kp)", pid.Kp,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pid.Kp = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Integral Gain (Ki)", pid.Ki,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pid.Ki = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Derivative Gain (Kd)", pid.Kd,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pid.Kd = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Windup Guard", pid.WindupGuard,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pid.WindupGuard = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Output Minimum", pid.OutputMin,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pid.OutputMin = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Output Maximum", pid.OutputMax,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) pid.OutputMax = v; });
                    panel.CreateAndAddTwoLabelsRow("Current Output", pid.Output.ToString(nf, IC));
                    break;
                }

                case ObjectType.Switch:
                {
                    var sw = (DWSIM.UnitOperations.UnitOperations.Switch)simobj;
                    panel.CreateAndAddCheckBoxRow("Switch On", sw.IsOn,
                        (cb, e) => sw.IsOn = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddTextBoxRow(nf, "On Value", sw.OnValue,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sw.OnValue = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Off Value", sw.OffValue,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) sw.OffValue = v; });
                    panel.CreateAndAddTwoLabelsRow("Target Object", sw.SelectedObjectID ?? "—");
                    panel.CreateAndAddTwoLabelsRow("Target Property", sw.SelectedProperty ?? "—");
                    break;
                }

                case ObjectType.Input:
                {
                    var inp = (DWSIM.UnitOperations.UnitOperations.Input)simobj;
                    var fsi = simobj.GetFlowsheet();

                    panel.CreateAndAddDescriptionRow(
                        "Writes a value into another object's property. Pick the target below, then type the value.");

                    var inpObjs = fsi.SimulationObjects.Values
                        .Where(x => x.GraphicObject != null && x.Name != simobj.Name)
                        .OrderBy(x => x.GraphicObject.Tag).ToList();
                    var inpTags = inpObjs.Select(x => x.GraphicObject.Tag).ToList();

                    var inpValueRow = new List<Action>();
                    global::Avalonia.Controls.ComboBox inpPropDD = null;

                    var inpSelIdx = inpObjs.FindIndex(x => x.Name == inp.SelectedObjectID);
                    var inpObjDD = panel.CreateAndAddDropDownRow("Target Object", inpTags,
                        Math.Max(0, inpSelIdx), null);

                    var inpProps = new List<string>();
                    void ReloadInputProps()
                    {
                        inpProps.Clear();
                        var o = inpObjs.ElementAtOrDefault(inpObjDD.SelectedIndex);
                        if (o == null) return;
                        inpProps.AddRange((o.GetProperties(PropertyType.WR) ?? Array.Empty<string>()).OrderBy(x => x));
                    }
                    ReloadInputProps();

                    inpPropDD = panel.CreateAndAddDropDownRow("Target Property", inpProps.ToList(),
                        Math.Max(0, inpProps.IndexOf(inp.SelectedProperty ?? "")), (dd, e) =>
                        {
                            var o = inpObjs.ElementAtOrDefault(inpObjDD.SelectedIndex);
                            if (o == null || dd.SelectedIndex < 0 || dd.SelectedIndex >= inpProps.Count) return;
                            inp.SelectedObjectID = o.Name;
                            inp.SelectedProperty = inpProps[dd.SelectedIndex];
                            inp.SelectedPropertyUnits = o.GetPropertyUnit(inp.SelectedProperty, su);
                            foreach (var refresh in inpValueRow) refresh();
                        });

                    var inpUnitsLabel = panel.CreateAndAddTwoLabelsRow("Units",
                        string.IsNullOrEmpty(inp.SelectedPropertyUnits) ? "—" : inp.SelectedPropertyUnits);

                    double ReadInputValue()
                    {
                        if (string.IsNullOrEmpty(inp.SelectedObjectID) || string.IsNullOrEmpty(inp.SelectedProperty))
                            return 0.0;
                        if (!fsi.SimulationObjects.ContainsKey(inp.SelectedObjectID)) return 0.0;
                        try
                        {
                            return Convert.ToDouble(
                                fsi.SimulationObjects[inp.SelectedObjectID].GetPropertyValue(inp.SelectedProperty, su));
                        }
                        catch { return 0.0; }
                    }

                    var inpValueBox = panel.CreateAndAddTextBoxRow(nf, "Value", ReadInputValue(), (tb, e) =>
                    {
                        if (!TryVal(tb.Text, out var v)) return;
                        if (string.IsNullOrEmpty(inp.SelectedObjectID) || string.IsNullOrEmpty(inp.SelectedProperty)) return;
                        if (!fsi.SimulationObjects.ContainsKey(inp.SelectedObjectID)) return;
                        fsi.SimulationObjects[inp.SelectedObjectID].SetPropertyValue(inp.SelectedProperty, v, su);
                    });

                    inpValueRow.Add(() =>
                    {
                        inpUnitsLabel.Text = string.IsNullOrEmpty(inp.SelectedPropertyUnits) ? "—" : inp.SelectedPropertyUnits;
                        inpValueBox.Text = ReadInputValue().ToString(nf, IC);
                    });

                    inpObjDD.SelectionChanged += (s, e) =>
                    {
                        ReloadInputProps();
                        inpPropDD.Items.Clear();
                        foreach (var p in inpProps) inpPropDD.Items.Add(p);
                        if (inpPropDD.Items.Count > 0) inpPropDD.SelectedIndex = 0;
                    };
                    break;
                }

                case ObjectType.ComponentSeparator:
                {
                    var csep = (ComponentSeparator)simobj;
                    panel.CreateAndAddDropDownRow("Specified Stream",
                        StringResources.csepspecstream().ToList(), csep.SpecifiedStreamIndex,
                        (dd, e) => { if (dd.SelectedIndex >= 0) csep.SpecifiedStreamIndex = (byte)dd.SelectedIndex; });
                    panel.CreateAndAddDescriptionRow(simobj.GetPropertyDescription("Specified Stream"));

                    panel.CreateAndAddLabelRow("Compound Separation Specs");

                    // The spec dictionary is populated lazily: seed an entry for every selected
                    // compound so each one gets a row, exactly like the Classic/Eto editor does.
                    foreach (ICompoundConstantProperties comp in simobj.GetFlowsheet().SelectedCompounds.Values)
                    {
                        if (!csep.ComponentSepSpecs.ContainsKey(comp.Name))
                        {
                            csep.ComponentSepSpecs.Add(comp.Name,
                                new DWSIM.UnitOperations.UnitOperations.Auxiliary.ComponentSeparationSpec(
                                    comp.Name,
                                    DWSIM.UnitOperations.UnitOperations.Auxiliary.SeparationSpec.PercentInletMassFlow,
                                    0.0f, "%"));
                        }
                    }

                    var specTypes = StringResources.csepspectype().ToList();
                    var specUnits = StringResources.cspecunit().ToList();

                    foreach (var cs in csep.ComponentSepSpecs.Values)
                    {
                        var spec = cs;
                        panel.CreateAndAddLabelRow2(spec.ComponentID);
                        panel.CreateAndAddDropDownRow("Spec Type", specTypes, (int)spec.SepSpec,
                            (dd, e) => { if (dd.SelectedIndex >= 0) spec.SepSpec = (DWSIM.UnitOperations.UnitOperations.Auxiliary.SeparationSpec)dd.SelectedIndex; });
                        panel.CreateAndAddTextBoxRow(nf, "Spec Value", spec.SpecValue,
                            (tb, e) => { if (TryVal(tb.Text, out var v)) spec.SpecValue = v; });
                        panel.CreateAndAddDropDownRow("Spec Units", specUnits,
                            Math.Max(0, specUnits.IndexOf(spec.SpecUnit)),
                            (dd, e) => { if (dd.SelectedIndex >= 0) spec.SpecUnit = specUnits[dd.SelectedIndex]; });
                    }
                    break;
                }

                case ObjectType.Tank:
                {
                    var tank = (Tank)simobj;
                    panel.CreateAndAddTextBoxRow(nf, "Volume (" + su.volume + ")",
                        cv.ConvertFromSI(su.volume, tank.Volume),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) tank.Volume = cv.ConvertToSI(su.volume, v); });
                    break;
                }

                case ObjectType.OrificePlate:
                {
                    var op = (OrificePlate)simobj;
                    panel.CreateAndAddDropDownRow("Pressure Tappings",
                        new List<string> { "Corner", "Flange", "Radius" }, (int)op.OrifType,
                        (dd, e) => { if (dd.SelectedIndex >= 0) op.OrifType = (OrificePlate.OrificeType)dd.SelectedIndex; });
                    // Diameters are stored in the display unit here, matching the Classic editor.
                    panel.CreateAndAddTextBoxRow(nf, "Orifice Diameter (" + su.diameter + ")", op.OrificeDiameter,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) op.OrificeDiameter = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Internal Pipe Diameter (" + su.diameter + ")", op.InternalPipeDiameter,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) op.InternalPipeDiameter = v; });
                    panel.CreateAndAddTextBoxRow(nf, "Correction Factor", op.CorrectionFactor,
                        (tb, e) => { if (TryVal(tb.Text, out var v)) op.CorrectionFactor = v; });
                    break;
                }

                case ObjectType.CustomUO:
                {
                    var scriptuo = (CustomUO)simobj;

                    panel.CreateAndAddLabelRow("Python Script Repositories");
                    panel.CreateAndAddButtonRow("FOSSEE's Custom Modeling Repository", null,
                        (btn, e) => OpenUrl("https://dwsim.fossee.in/custom-model"));

                    panel.CreateAndAddLabelRow("Flowsheet Object");
                    panel.CreateAndAddLabelAndButtonRow("Embedded Image Icon", "Load File", null, (btn, e) =>
                    {
                        PickFile(panel, "Open Image File", new[] { "*.png", "*.jpg", "*.jpeg" }, path =>
                        {
                            try
                            {
                                using (var img = SkiaSharp.SKImage.FromEncodedData(path))
                                {
                                    if (img == null)
                                    {
                                        Notify(simobj, "Could not decode the selected image.");
                                        return;
                                    }
                                    scriptuo.EmbeddedImageData = DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.EmbeddedImageGraphic
                                        .ImageToBase64(img, SkiaSharp.SKEncodedImageFormat.Png);
                                    Notify(simobj, "Image data read successfully.");
                                }
                            }
                            catch (Exception ex) { Notify(simobj, "Error reading image data: " + ex.Message); }
                        });
                    });
                    panel.CreateAndAddCheckBoxRow("Use Embedded Image Icon", scriptuo.UseEmbeddedImage,
                        (cb, e) => scriptuo.UseEmbeddedImage = cb.IsChecked.GetValueOrDefault());

                    panel.CreateAndAddLabelRow("Python Script");
                    panel.CreateAndAddDropDownRow("Python Interpreter",
                        new List<string> { "IronPython", "Python.NET" }, (int)scriptuo.ExecutionEngine,
                        (dd, e) => { if (dd.SelectedIndex >= 0) scriptuo.ExecutionEngine = (CustomUO.PythonExecutionEngine)dd.SelectedIndex; });
                    panel.CreateAndAddDescriptionRow("The script itself is edited in the Script Editor window (Tools menu).");

                    panel.CreateAndAddLabelRow("Script Variables");
                    panel.CreateAndAddDescriptionRow(
                        "Enter one variable per line, separating its name (no special characters or spaces) from its value with a tab or a single space.");

                    panel.CreateAndAddLabelRow2("Input Variables");
                    var inVars = new System.Text.StringBuilder();
                    foreach (var obj in scriptuo.InputVariables)
                        inVars.AppendLine(obj.Key + "\t" + obj.Value.ToString(IC));
                    panel.CreateAndAddMultilineMonoSpaceTextBoxRow(inVars.ToString(), 160, false, (tb, e) =>
                    {
                        var parsed = new Dictionary<string, double>();
                        foreach (var line in SplitLines(tb.Text))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length < 2) continue;
                            if (TryVal(parts[1], out var v)) parsed[parts[0]] = v;
                        }
                        scriptuo.InputVariables = parsed;
                    });

                    panel.CreateAndAddLabelRow2("Input String Variables");
                    var inStrs = new System.Text.StringBuilder();
                    foreach (var obj in scriptuo.InputStringVariables)
                        inStrs.AppendLine(obj.Key + "\t" + obj.Value);
                    panel.CreateAndAddMultilineMonoSpaceTextBoxRow(inStrs.ToString(), 120, false, (tb, e) =>
                    {
                        var parsed = new Dictionary<string, string>();
                        foreach (var line in SplitLines(tb.Text))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length < 2) continue;
                            parsed[parts[0]] = parts[1];
                        }
                        scriptuo.InputStringVariables = parsed;
                    });

                    panel.CreateAndAddLabelRow2("Output Variables");
                    var outVars = new System.Text.StringBuilder();
                    foreach (var obj in scriptuo.OutputVariables)
                        outVars.AppendLine(obj.Key + "\t" + obj.Value.ToString(IC));
                    panel.CreateAndAddMultilineMonoSpaceTextBoxRow(outVars.ToString(), 120, true, null);
                    break;
                }

                case ObjectType.FlowsheetUO:
                {
                    var fsuo = (DWSIM.UnitOperations.UnitOperations.Flowsheet)simobj;

                    panel.CreateAndAddDropDownRow("File Source",
                        new List<string> { "Embedded", "External" }, fsuo.FileIsEmbedded ? 0 : 1,
                        (dd, e) => fsuo.FileIsEmbedded = dd.SelectedIndex == 0);

                    var fsfiles = fsuo.GetFlowsheet().FileDatabaseProvider.GetFiles();
                    fsfiles.Insert(0, "");
                    var fsselected = fsuo.EmbeddedFileName;
                    if (!fsfiles.Contains(fsselected)) fsselected = "";
                    panel.CreateAndAddDropDownRow("Embedded File", fsfiles, fsfiles.IndexOf(fsselected),
                        (dd, e) => { if (dd.SelectedIndex >= 0) fsuo.EmbeddedFileName = fsfiles[dd.SelectedIndex]; });

                    var fsbox = panel.CreateAndAddLabelAndTextBoxAndButtonRow("Flowsheet Path",
                        fsuo.SimulationFile, "Search", null,
                        (tb, e) => fsuo.SimulationFile = tb.Text, null);
                    panel.CreateAndAddButtonRow("Browse for Simulation File...", null, (btn, e) =>
                    {
                        PickFile(panel, "Select Simulation File", new[] { "*.dwxmz", "*.dwxml", "*.xml" }, path =>
                        {
                            fsbox.Text = path;
                            fsuo.SimulationFile = path;
                        });
                    });

                    panel.CreateAndAddCheckBoxRow("Initialize on Load", fsuo.InitializeOnLoad,
                        (cb, e) => fsuo.InitializeOnLoad = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Update Process Data when Saving", fsuo.UpdateOnSave,
                        (cb, e) => fsuo.UpdateOnSave = cb.IsChecked.GetValueOrDefault());
                    panel.CreateAndAddCheckBoxRow("Redirect Flowsheet Calculator Messages", fsuo.RedirectOutput,
                        (cb, e) => fsuo.RedirectOutput = cb.IsChecked.GetValueOrDefault());

                    panel.CreateAndAddLabelRow("Linked Input Variables");
                    if (fsuo.Fsheet == null)
                    {
                        panel.CreateAndAddDescriptionRow("The linked flowsheet is not loaded yet. Solve once, or enable 'Initialize on Load'.");
                    }
                    else
                    {
                        var any = false;
                        foreach (var item in fsuo.InputParams)
                        {
                            var par = item.Value;
                            if (!fsuo.Fsheet.SimulationObjects.ContainsKey(par.ObjectID)) continue;
                            var target = fsuo.Fsheet.SimulationObjects[par.ObjectID];
                            var label = target.GraphicObject.Tag + ", " +
                                        fsuo.GetFlowsheet().GetTranslatedString(par.ObjectProperty);
                            var units = target.GetPropertyUnit(par.ObjectProperty, su);
                            var value = Convert.ToDouble(target.GetPropertyValue(par.ObjectProperty, su));
                            panel.CreateAndAddTextBoxRow(nf, label + " (" + units + ")", value, (tb, e) =>
                            {
                                if (TryVal(tb.Text, out var v))
                                    target.SetPropertyValue(par.ObjectProperty, v, su);
                            });
                            any = true;
                        }
                        if (!any) panel.CreateAndAddDescriptionRow("No linked input variables are defined.");
                    }
                    break;
                }

                default:
                    panel.CreateAndAddDescriptionRow("Full property editor for this object type is available in the Classic UI.");
                    break;
            }

            // Wire the panel-wide after-edit callback AFTER all controls have been populated,
            // so initial Text/SelectedIndex assignments don't trigger spurious solver runs.
            // Every CreateAndAdd* helper calls panel.OnAfterEdit?.Invoke() once the user's
            // per-control lambda runs, which gives us "edit -> recalc -> redraw" for free.
            panel.OnAfterEdit = () =>
            {
                var fs = simobj.GetFlowsheet();
                if (fs == null) return;
                fs.RequestCalculation(simobj);
                fs.UpdateInterface();
            };
        }

        private static void PopulateColumnEditor(DWSIM.UnitOperations.UnitOperations.Column col,
            AvaloniaEditorPanel panel, IUnitsOfMeasure su, string nf)
        {
            var fs = col.GetFlowsheet();

            panel.CreateAndAddLabelRow("Column Configuration");
            panel.CreateAndAddTwoLabelsRow("Column Type", col.ColumnType.ToString());

            panel.CreateAndAddNumericEditorRow("Number of Stages", col.NumberOfStages, 3, 200, 0,
                (sp, e) => col.SetNumberOfStages((int)sp.Value.GetValueOrDefault()));

            // CondenserType only applies to columns that have a condenser.
            if (col is DWSIM.UnitOperations.UnitOperations.DistillationColumn
                || col.ColumnType == DWSIM.UnitOperations.UnitOperations.Column.ColType.RefluxedAbsorber)
            {
                var condNames = new List<string> { "Total Condenser", "Partial Condenser", "Full Reflux" };
                panel.CreateAndAddDropDownRow("Condenser Type", condNames,
                    Math.Min(condNames.Count - 1, Math.Max(0, (int)col.CondenserType)),
                    (dd, e) => col.CondenserType = (DWSIM.UnitOperations.UnitOperations.Column.condtype)dd.SelectedIndex);
            }

            var solvingMethods = new List<string>
            {
                "Wang-Henke (Bubble Point)",
                "Modified Wang-Henke (Bubble Point)",
                "Napthali-Sandholm (Simultaneous Correction)"
            };
            panel.CreateAndAddDropDownRow("Solving Method", solvingMethods,
                Math.Max(0, solvingMethods.IndexOf(col.SolvingMethodName ?? "")),
                (dd, e) => { if (dd.SelectedIndex >= 0) col.SolvingMethodName = solvingMethods[dd.SelectedIndex]; });

            var schemeNames = new List<string>
            {
                "Ideal K Initialization",
                "Ideal Enthalpy Initialization",
                "Ideal K + Enthalpy Initialization",
                "Direct"
            };
            panel.CreateAndAddDropDownRow("Solver Scheme", schemeNames,
                Math.Min(schemeNames.Count - 1, Math.Max(0, (int)col.SolverScheme)),
                (dd, e) => col.SolverScheme = (DWSIM.UnitOperations.UnitOperations.Column.SolvingScheme)dd.SelectedIndex);

            panel.CreateAndAddNumericEditorRow("Maximum Iterations", col.MaxIterations, 1, 10000, 0,
                (sp, e) => col.MaxIterations = (int)sp.Value.GetValueOrDefault());

            panel.CreateAndAddTextBoxRow("G", "Internal Loop Tolerance", col.InternalLoopTolerance,
                (tb, e) => { if (TryVal(tb.Text, out var v) && v > 0) col.InternalLoopTolerance = v; });
            panel.CreateAndAddTextBoxRow("G", "External Loop Tolerance", col.ExternalLoopTolerance,
                (tb, e) => { if (TryVal(tb.Text, out var v) && v > 0) col.ExternalLoopTolerance = v; });

            panel.CreateAndAddCheckBoxRow("Generate Convergence Report", col.CreateSolverConvergengeReport,
                (cb, e) => col.CreateSolverConvergengeReport = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddLabelRow("Operating Specifications");
            panel.CreateAndAddTextBoxRow(nf, "Condenser Pressure Drop (" + su.deltaP + ")",
                cv.ConvertFromSI(su.deltaP, col.CondenserDeltaP),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.CondenserDeltaP = cv.ConvertToSI(su.deltaP, v); });
            panel.CreateAndAddTextBoxRow(nf, "Column Pressure Drop (" + su.deltaP + ")",
                cv.ConvertFromSI(su.deltaP, col.ColumnPressureDrop),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.ColumnPressureDrop = cv.ConvertToSI(su.deltaP, v); });
            panel.CreateAndAddTextBoxRow(nf, "Reflux Ratio", col.RefluxRatio,
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.RefluxRatio = v; });
            panel.CreateAndAddTextBoxRow(nf, "Distillate Flow Rate (" + su.molarflow + ")",
                cv.ConvertFromSI(su.molarflow, col.DistillateFlowRate),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.DistillateFlowRate = cv.ConvertToSI(su.molarflow, v); });
            panel.CreateAndAddTextBoxRow(nf, "Vapor Flow Rate (" + su.molarflow + ")",
                cv.ConvertFromSI(su.molarflow, col.VaporFlowRate),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.VaporFlowRate = cv.ConvertToSI(su.molarflow, v); });
            panel.CreateAndAddTextBoxRow(nf, "Reboiler Duty (" + su.heatflow + ")",
                cv.ConvertFromSI(su.heatflow, col.ReboilerDuty),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.ReboilerDuty = cv.ConvertToSI(su.heatflow, v); });
            panel.CreateAndAddTextBoxRow(nf, "Condenser Duty (" + su.heatflow + ")",
                cv.ConvertFromSI(su.heatflow, col.CondenserDuty),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.CondenserDuty = cv.ConvertToSI(su.heatflow, v); });

            panel.CreateAndAddLabelRow("Geometry (Sizing)");
            panel.CreateAndAddTextBoxRow(nf, "Tray Spacing (" + su.distance + ")",
                cv.ConvertFromSI(su.distance, col.TraySpacing),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.TraySpacing = cv.ConvertToSI(su.distance, v); });
            panel.CreateAndAddTextBoxRow(nf, "Top Spacing (" + su.distance + ")",
                cv.ConvertFromSI(su.distance, col.TopSpacing),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.TopSpacing = cv.ConvertToSI(su.distance, v); });
            panel.CreateAndAddTextBoxRow(nf, "Bottom Spacing (" + su.distance + ")",
                cv.ConvertFromSI(su.distance, col.BottomSpacing),
                (tb, e) => { if (TryVal(tb.Text, out var v)) col.BottomSpacing = cv.ConvertToSI(su.distance, v); });
            panel.CreateAndAddTwoLabelsRow("Estimated Diameter (" + su.distance + ")",
                cv.ConvertFromSI(su.distance, col.EstimatedDiameter).ToString(nf, IC));
            panel.CreateAndAddTwoLabelsRow("Estimated Height (" + su.distance + ")",
                cv.ConvertFromSI(su.distance, col.EstimatedHeight).ToString(nf, IC));

            PopulateColumnStreams(col, panel, su, nf);
            PopulateColumnSpecs(col, panel, su, nf);

            // Per-stage editor: name, pressure, temperature, Murphree efficiency.
            panel.CreateAndAddLabelRow("Stage Properties");
            if (col.Stages == null || col.Stages.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("Stages will be created on next solve.");
                return;
            }

            panel.CreateAndAddDescriptionRow($"{col.Stages.Count} stage(s). Edit pressure, temperature and Murphree efficiency below.");

            int stageIdx = 0;
            foreach (var stage in col.Stages)
            {
                int idx = stageIdx++;
                var st = stage;
                panel.CreateAndAddLabelRow2($"Stage {idx + 1}: {st.Name}");
                panel.CreateAndAddTextBoxRow(nf, "  Pressure (" + su.pressure + ")",
                    cv.ConvertFromSI(su.pressure, st.P),
                    (tb, e) => { if (TryVal(tb.Text, out var v)) st.P = cv.ConvertToSI(su.pressure, v); });
                panel.CreateAndAddTextBoxRow(nf, "  Temperature (" + su.temperature + ")",
                    cv.ConvertFromSI(su.temperature, st.T),
                    (tb, e) => { if (TryVal(tb.Text, out var v)) st.T = cv.ConvertToSI(su.temperature, v); });
                panel.CreateAndAddTextBoxRow(nf, "  Murphree Efficiency", st.Efficiency,
                    (tb, e) => { if (TryVal(tb.Text, out var v)) st.Efficiency = v; });
            }

            PopulateColumnInitialEstimates(col, panel, su, nf);
        }

        /// <summary>
        /// Stage assignment for every stream attached to the column, plus the phase and molar
        /// flow of each side draw. Mirrors the "Streams" / "Side Draw Specs" sections of the
        /// Eto DistillationColumn editor.
        /// </summary>
        internal static void PopulateColumnStreams(DWSIM.UnitOperations.UnitOperations.Column col,
            AvaloniaEditorPanel panel, IUnitsOfMeasure su, string nf)
        {
            var fs = col.GetFlowsheet();
            if (col.Stages == null || col.Stages.Count == 0) return;

            var stageNames = col.Stages.Select(x => x.Name).ToList();
            var stageIDs = col.Stages.Select(x => x.ID).ToList();
            stageNames.Insert(0, "");
            stageIDs.Insert(0, "");

            string TagOf(string streamID)
            {
                if (streamID == null || !fs.SimulationObjects.ContainsKey(streamID)) return null;
                var go = fs.SimulationObjects[streamID].GraphicObject;
                return go == null ? null : go.Tag;
            }

            // AssociatedStage is written as a stage ID by the engine's Set*Stage helpers but as a
            // stage Name by the constructors, and Column.StageIndex accepts either. Match both,
            // otherwise every stream in a saved flowsheet shows up as unassigned.
            int StageIndexOf(string associated)
            {
                if (string.IsNullOrEmpty(associated)) return 0;
                for (int i = 1; i < stageIDs.Count; i++)
                {
                    if (stageIDs[i] == associated || stageNames[i] == associated) return i;
                }
                return 0;
            }

            void StageRow(string prefix, StreamInformation si)
            {
                var tag = TagOf(si.StreamID);
                if (tag == null) return; // stale entry: the stream is no longer on the flowsheet
                var info = si;
                panel.CreateAndAddDropDownRow(prefix + " " + tag, stageNames,
                    StageIndexOf(info.AssociatedStage),
                    (dd, e) => { if (dd.SelectedIndex >= 0) info.AssociatedStage = stageIDs[dd.SelectedIndex]; });
            }

            panel.CreateAndAddLabelRow("Streams");
            panel.CreateAndAddDescriptionRow("Assigns each connected stream to a column stage.");

            foreach (var si in col.MaterialStreams.Values)
            {
                switch (si.StreamBehavior)
                {
                    case StreamInformation.Behavior.Feed: StageRow("[FEED]", si); break;
                    case StreamInformation.Behavior.Sidedraw: StageRow("[SIDEDRAW]", si); break;
                    case StreamInformation.Behavior.OverheadVapor: StageRow("[OVH VAPOR]", si); break;
                    case StreamInformation.Behavior.Distillate: StageRow("[DISTILLATE]", si); break;
                    case StreamInformation.Behavior.BottomsLiquid: StageRow("[BOTTOMS]", si); break;
                }
            }
            foreach (var si in col.EnergyStreams.Values)
            {
                if (si.StreamBehavior == StreamInformation.Behavior.Distillate) StageRow("[COND. DUTY]", si);
                else if (si.StreamBehavior == StreamInformation.Behavior.BottomsLiquid) StageRow("[REB. DUTY]", si);
            }

            var sidedraws = col.MaterialStreams.Values
                .Where(x => x.StreamBehavior == StreamInformation.Behavior.Sidedraw && TagOf(x.StreamID) != null)
                .ToList();

            if (sidedraws.Count == 0) return;

            panel.CreateAndAddLabelRow("Side Draw Specs");
            var phases = new List<string> { "L", "V" };
            foreach (var si in sidedraws)
            {
                var info = si;
                var tag = TagOf(info.StreamID);
                // Phase.B / Phase.None have no editor entry; the engine treats them as liquid.
                var phaseIdx = info.StreamPhase == StreamInformation.Phase.V ? 1 : 0;
                panel.CreateAndAddDropDownRow(tag + " / Draw Phase", phases, phaseIdx, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    info.StreamPhase = dd.SelectedIndex == 1
                        ? StreamInformation.Phase.V
                        : StreamInformation.Phase.L;
                });
                panel.CreateAndAddTextBoxRow(nf, tag + " / Molar Flow (" + su.molarflow + ")",
                    cv.ConvertFromSI(su.molarflow, info.FlowRate.Value),
                    (tb, e) => { if (TryVal(tb.Text, out var v)) info.FlowRate.Value = cv.ConvertToSI(su.molarflow, v); });
            }
        }

        // Display order of StringArrays.columnspec() mapped onto ColumnSpec.SpecType, whose
        // members are NOT declared in that order.
        private static readonly ColumnSpec.SpecType[] ColumnSpecTypes =
        {
            ColumnSpec.SpecType.Product_Molar_Flow_Rate,
            ColumnSpec.SpecType.Product_Mass_Flow_Rate,
            ColumnSpec.SpecType.Stream_Ratio,
            ColumnSpec.SpecType.Heat_Duty,
            ColumnSpec.SpecType.Component_Mass_Flow_Rate,
            ColumnSpec.SpecType.Component_Molar_Flow_Rate,
            ColumnSpec.SpecType.Component_Recovery,
            ColumnSpec.SpecType.Component_Fraction,
            ColumnSpec.SpecType.Temperature,
            ColumnSpec.SpecType.Feed_Recovery
        };

        /// <summary>
        /// Editable condenser ("C") and reboiler ("R") specifications: type, compound, value
        /// and unit. Absorbers have neither, so each block is gated on the key being present.
        /// </summary>
        private static void PopulateColumnSpecs(DWSIM.UnitOperations.UnitOperations.Column col,
            AvaloniaEditorPanel panel, IUnitsOfMeasure su, string nf)
        {
            if (col.Specs == null) return;

            var fs = col.GetFlowsheet();
            var compounds = fs.SelectedCompounds.Keys.ToList();
            compounds.Insert(0, "");

            var specNames = StringResources.columnspec().ToList();

            var units = su.GetUnitSet(UnitOfMeasure.molarflow).ToList();
            units.AddRange(su.GetUnitSet(UnitOfMeasure.massflow));
            units.AddRange(su.GetUnitSet(UnitOfMeasure.heatflow));
            units.AddRange(su.GetUnitSet(UnitOfMeasure.temperature));
            units.AddRange(new[] { "% M/M", "% W/W", "M", "We", "%" });
            units.Insert(0, "");

            void SpecBlock(string key, string title)
            {
                if (!col.Specs.ContainsKey(key)) return;
                var spec = col.Specs[key];

                panel.CreateAndAddLabelRow(title);
                panel.CreateAndAddDropDownRow("Specification", specNames,
                    Math.Max(0, Array.IndexOf(ColumnSpecTypes, spec.SType)),
                    (dd, e) =>
                    {
                        if (dd.SelectedIndex >= 0 && dd.SelectedIndex < ColumnSpecTypes.Length)
                            spec.SType = ColumnSpecTypes[dd.SelectedIndex];
                    });
                panel.CreateAndAddDropDownRow("Compound", compounds,
                    Math.Max(0, compounds.IndexOf(spec.ComponentID ?? "")),
                    (dd, e) => { if (dd.SelectedIndex >= 0) spec.ComponentID = compounds[dd.SelectedIndex]; });
                panel.CreateAndAddTextBoxRow(nf, "Value", spec.SpecValue,
                    (tb, e) => { if (TryVal(tb.Text, out var v)) spec.SpecValue = v; });
                panel.CreateAndAddDropDownRow("Units", units,
                    Math.Max(0, units.IndexOf(spec.SpecUnit ?? "")),
                    (dd, e) => { if (dd.SelectedIndex >= 0) spec.SpecUnit = units[dd.SelectedIndex]; });
            }

            SpecBlock("C", "Condenser Specification");
            SpecBlock("R", "Reboiler Specification");
        }

        internal static void PopulateColumnInitialEstimates(
            DWSIM.UnitOperations.UnitOperations.Column col,
            AvaloniaEditorPanel panel, IUnitsOfMeasure su, string nf)
        {
            panel.CreateAndAddLabelRow("Initial Estimates");
            panel.CreateAndAddCheckBoxRow("Auto-update from previous solve", col.AutoUpdateInitialEstimates,
                (cb, e) => col.AutoUpdateInitialEstimates = cb.IsChecked.GetValueOrDefault());
            panel.CreateAndAddCheckBoxRow("Use Temperature Estimates", col.UseTemperatureEstimates,
                (cb, e) => col.UseTemperatureEstimates = cb.IsChecked.GetValueOrDefault());
            panel.CreateAndAddCheckBoxRow("Use Vapor Flow Estimates", col.UseVaporFlowEstimates,
                (cb, e) => col.UseVaporFlowEstimates = cb.IsChecked.GetValueOrDefault());
            panel.CreateAndAddCheckBoxRow("Use Liquid Flow Estimates", col.UseLiquidFlowEstimates,
                (cb, e) => col.UseLiquidFlowEstimates = cb.IsChecked.GetValueOrDefault());

            var ie = col.InitialEstimates;
            if (ie == null)
            {
                panel.CreateAndAddDescriptionRow("No initial-estimates container present. Solve once to populate.");
                return;
            }

            panel.CreateAndAddTextBoxRow(nf, "Distillate Flow Rate (" + su.molarflow + ")",
                ie.DistillateFlowRate.HasValue ? cv.ConvertFromSI(su.molarflow, ie.DistillateFlowRate.Value) : double.NaN,
                (tb, e) => { if (TryVal(tb.Text, out var v)) ie.DistillateFlowRate = cv.ConvertToSI(su.molarflow, v); });
            panel.CreateAndAddTextBoxRow(nf, "Vapor Product Flow Rate (" + su.molarflow + ")",
                ie.VaporProductFlowRate.HasValue ? cv.ConvertFromSI(su.molarflow, ie.VaporProductFlowRate.Value) : double.NaN,
                (tb, e) => { if (TryVal(tb.Text, out var v)) ie.VaporProductFlowRate = cv.ConvertToSI(su.molarflow, v); });
            panel.CreateAndAddTextBoxRow(nf, "Bottoms Flow Rate (" + su.molarflow + ")",
                ie.BottomsFlowRate.HasValue ? cv.ConvertFromSI(su.molarflow, ie.BottomsFlowRate.Value) : double.NaN,
                (tb, e) => { if (TryVal(tb.Text, out var v)) ie.BottomsFlowRate = cv.ConvertToSI(su.molarflow, v); });
            panel.CreateAndAddTextBoxRow(nf, "Initial Reflux Ratio",
                ie.RefluxRatio.GetValueOrDefault(),
                (tb, e) => { if (TryVal(tb.Text, out var v)) ie.RefluxRatio = v; });

            if (ie.StageTemps.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("Per-stage initial T / molar flows are not seeded yet. They populate after the first solve and can be tweaked here for the next run.");
                return;
            }

            panel.CreateAndAddLabelRow2("Per-Stage Estimates");
            for (int i = 0; i < ie.StageTemps.Count; i++)
            {
                int idx = i;
                var tParam = ie.StageTemps[idx];
                panel.CreateAndAddTextBoxRow(nf, $"  Stage {idx + 1} T (" + su.temperature + ")",
                    cv.ConvertFromSI(su.temperature, tParam.Value),
                    (tb, e) => { if (TryVal(tb.Text, out var v)) tParam.Value = cv.ConvertToSI(su.temperature, v); });

                if (idx < ie.VapMolarFlows.Count)
                {
                    var vParam = ie.VapMolarFlows[idx];
                    panel.CreateAndAddTextBoxRow(nf, $"  Stage {idx + 1} V (" + su.molarflow + ")",
                        cv.ConvertFromSI(su.molarflow, vParam.Value),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) vParam.Value = cv.ConvertToSI(su.molarflow, v); });
                }
                if (idx < ie.LiqMolarFlows.Count)
                {
                    var lParam = ie.LiqMolarFlows[idx];
                    panel.CreateAndAddTextBoxRow(nf, $"  Stage {idx + 1} L (" + su.molarflow + ")",
                        cv.ConvertFromSI(su.molarflow, lParam.Value),
                        (tb, e) => { if (TryVal(tb.Text, out var v)) lParam.Value = cv.ConvertToSI(su.molarflow, v); });
                }
            }
        }
    }
}
