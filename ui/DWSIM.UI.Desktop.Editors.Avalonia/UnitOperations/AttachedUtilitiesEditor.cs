using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// The utilities attached to an object: phase envelopes, hydrates, sizing, cold flow
    /// properties and the critical point. Each one contributes its results to the object as
    /// properties, so they can be read from the spreadsheet and from a sensitivity study.
    /// </summary>
    public static class AttachedUtilitiesEditor
    {

        /// <summary>The types that can be attached, in the order the Windows dialog offers them.</summary>
        private static readonly (FlowsheetUtility Type, string Label)[] Offered =
        {
            (FlowsheetUtility.PhaseEnvelope,          "Phase Envelope"),
            (FlowsheetUtility.PhaseEnvelopeBinary,    "Binary Phase Envelope"),
            (FlowsheetUtility.PhaseEnvelopeTernary,   "Ternary Envelope (LLE)"),
            (FlowsheetUtility.NaturalGasHydrates,     "Natural Gas Hydrates"),
            (FlowsheetUtility.TrueCriticalPoint,      "True Critical Point"),
            (FlowsheetUtility.PSVSizing,              "Pressure Safety Valve Sizing"),
            (FlowsheetUtility.SeparatorSizing,        "Gas-Liquid Separator Sizing"),
            (FlowsheetUtility.PetroleumProperties,    "Petroleum Cold Flow Properties"),
            (FlowsheetUtility.PureCompoundProperties, "Pure Compound Properties"),
        };

        /// <summary>The utilities a rigorous column takes.</summary>
        private static readonly (FlowsheetUtility Type, string Label)[] OfferedForColumns =
        {
            (FlowsheetUtility.ColumnInternals, "Column Internals"),
        };

        /// <summary>Set by the application: opens the full tool of a utility that has one (the column internals rating).</summary>
        public static Action<IAttachedUtility>? OpenUtility;

        private static bool IsColumn(ISimulationObject o)
        {
            var t = o.GraphicObject?.ObjectType;
            return t == DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.DistillationColumn || t == DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.AbsorptionColumn
                || t == DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.ReboiledAbsorber || t == DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.RefluxedAbsorber;
        }

        private static (FlowsheetUtility Type, string Label)[] OfferedFor(ISimulationObject o) => IsColumn(o) ? OfferedForColumns : Offered;

        private static string LabelOf(FlowsheetUtility kind)
        {
            foreach (var o in Offered) if (o.Type == kind) return o.Label;
            foreach (var o in OfferedForColumns) if (o.Type == kind) return o.Label;
            return kind.ToString();
        }

        public static Control Build(ISimulationObject simobj)
        {
            var host = new AvaloniaEditorPanel();
            Populate(host, simobj);
            return new ScrollViewer { Content = host };
        }

        private static void Populate(AvaloniaEditorPanel panel, ISimulationObject simobj)
        {
            panel.Children.Clear();

            panel.CreateAndAddLabelRow("Attached Utilities");
            panel.CreateAndAddDescriptionRow(
                "A utility attached here follows the object: it is stored with the simulation, and " +
                "what it calculates shows up among the object's properties.");

            var offered = OfferedFor(simobj);
            var picker = panel.CreateAndAddDropDownRow("Utility",
                offered.Select(o => o.Label).ToList(), 0, null);

            var add = panel.CreateAndAddButtonRow("Add", null, null);
            add.Click += (_, _) =>
            {
                var index = picker.SelectedIndex;
                if (index < 0 || index >= offered.Length) return;

                var flowsheet = simobj.GetFlowsheet();
                var utility = flowsheet?.GetUtility(offered[index].Type);
                if (utility == null)
                {
                    flowsheet?.ShowMessage("This utility is not available in this build.",
                                           IFlowsheet.MessageType.Warning);
                    return;
                }

                var kind = offered[index].Type;
                utility.ID = new Random().Next(1, int.MaxValue);
                utility.Name = kind + (simobj.AttachedUtilities.Count(x => x.GetUtilityType() == kind) + 1).ToString();
                utility.AttachedTo = simobj;
                utility.Initialize();
                simobj.AttachedUtilities.Add(utility);

                TryUpdate(utility, flowsheet);
                Populate(panel, simobj);
            };

            panel.CreateAndAddEmptySpace();

            if (simobj.AttachedUtilities.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("Nothing attached to this object yet.");
                return;
            }

            foreach (var utility in simobj.AttachedUtilities.ToList())
                AddUtilityBlock(panel, simobj, utility);
        }

        private static void AddUtilityBlock(AvaloniaEditorPanel panel, ISimulationObject simobj,
            IAttachedUtility utility)
        {
            var label = LabelOf(utility.GetUtilityType());

            panel.CreateAndAddLabelRow(label);
            var hasTool = utility.GetUtilityType() == FlowsheetUtility.ColumnInternals;
            if (hasTool)
            {
                panel.CreateAndAddDescriptionRow("The case (sections, geometry, models) is edited in the tool and travels with the simulation; the figures below are the last rating.");
                if (OpenUtility != null) panel.CreateAndAddButtonRow("Open the column internals tool...", null, (_, _) => OpenUtility(utility));
            }

            panel.CreateAndAddStringEditorRow("Name", utility.Name,
                (s, e) => utility.Name = s.Text ?? "");

            panel.CreateAndAddCheckBoxRow("Update with the flowsheet", utility.AutoUpdate,
                (s, e) => utility.AutoUpdate = s.IsChecked.GetValueOrDefault());

            var properties = utility.GetPropertyList()
                                    .Where(p => p != "Name" && p != "AutoUpdate" && !(hasTool && p == "Case"))
                                    .ToList();

            foreach (var p in properties)
            {
                var name = p;
                var value = utility.GetPropertyValue(name);
                var unit = utility.GetPropertyUnits(name);
                var header = unit.Length > 0 ? name + " (" + unit + ")" : name;

                if (value is bool b)
                {
                    panel.CreateAndAddCheckBoxRow(header, b,
                        (s, e) => utility.SetPropertyValue(name, s.IsChecked.GetValueOrDefault()));
                }
                else
                {
                    panel.CreateAndAddStringEditorRow(header, Format(value),
                        (s, e) => Assign(utility, name, s.Text));
                }
            }

            var (update, remove) = panel.CreateAndAddTwoButtonsRow("Update", null, "Remove", null, null, null);

            update.Click += (_, _) =>
            {
                TryUpdate(utility, simobj.GetFlowsheet());
                Populate(panel, simobj);
            };

            remove.Click += (_, _) =>
            {
                simobj.AttachedUtilities.Remove(utility);
                Populate(panel, simobj);
            };

            panel.CreateAndAddEmptySpace();
        }

        private static void TryUpdate(IAttachedUtility utility, IFlowsheet? flowsheet)
        {
            try
            {
                utility.Update();
            }
            catch (Exception ex)
            {
                flowsheet?.ShowMessage("Error updating " + utility.Name + ": " + ex.Message,
                                       IFlowsheet.MessageType.GeneralError);
            }
        }

        /// <summary>Writes a typed value back, keeping a number a number.</summary>
        private static void Assign(IAttachedUtility utility, string name, string? text)
        {
            var current = utility.GetPropertyValue(name);
            if (current is double || current is int || current is float)
            {
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var d) ||
                    double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                {
                    utility.SetPropertyValue(name, d);
                }
                return;
            }

            utility.SetPropertyValue(name, text ?? "");
        }

        private static string Format(object? value)
        {
            if (value == null) return "";
            if (value is double d) return d.ToString("G6", CultureInfo.CurrentCulture);
            return value.ToString() ?? "";
        }
    }
}
