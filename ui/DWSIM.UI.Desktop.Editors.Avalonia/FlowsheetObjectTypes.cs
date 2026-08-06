using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The simulation object types the engine ships, with the properties each one can show.
    ///
    /// The floating property tables and the anchored property lists read
    /// <see cref="IFlowsheetOptions.VisibleProperties"/>, and draw nothing at all for a type
    /// that has no entry there. The WinForms UI fills the map when a flowsheet is created;
    /// <see cref="EnsureDefaults"/> is the same thing for any other host.
    /// </summary>
    public static class FlowsheetObjectTypes
    {

        public sealed class Entry
        {
            public string DisplayName { get; set; } = "";
            public string TypeName { get; set; } = "";
            public string[] Properties { get; set; } = new string[0];
            public string[] DefaultProperties { get; set; } = new string[0];
        }

        /// <summary>
        /// Instantiates every non-abstract simulation object of the engine assemblies to read its
        /// display name and property list. Costly enough to be worth calling once per editor.
        /// </summary>
        public static List<Entry> All(IFlowsheet flowsheet)
        {
            var entries = new List<Entry>();
            var seen = new HashSet<string>();

            foreach (var type in EngineTypes())
            {
                try
                {
                    var obj = (ISimulationObject)Activator.CreateInstance(type);
                    obj.SetFlowsheet(flowsheet);

                    var display = obj.GetDisplayName();
                    if (!string.IsNullOrEmpty(display) && seen.Add(display))
                    {
                        entries.Add(new Entry
                        {
                            DisplayName = display,
                            TypeName = type.Name,
                            Properties = obj.GetProperties(PropertyType.ALL) ?? new string[0],
                            DefaultProperties = obj.GetDefaultProperties() ?? new string[0]
                        });
                    }

                    obj.SetFlowsheet(null);
                }
                catch (Exception) { }
            }

            return entries.OrderBy(x => x.DisplayName).ToList();
        }

        /// <summary>
        /// Gives every object type that has no entry yet its default property list, so the
        /// floating tables of a flowsheet created outside the WinForms UI are not empty.
        /// </summary>
        /// <returns>How many types were filled in.</returns>
        public static int EnsureDefaults(IFlowsheet flowsheet)
        {
            if (flowsheet == null || flowsheet.FlowsheetOptions == null) return 0;

            var visible = flowsheet.FlowsheetOptions.VisibleProperties;
            var added = 0;

            foreach (var entry in All(flowsheet))
            {
                if (visible.ContainsKey(entry.TypeName)) continue;
                visible.Add(entry.TypeName, entry.DefaultProperties.ToList());
                added += 1;
            }

            return added;
        }

        private static IEnumerable<Type> EngineTypes()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name == "DWSIM.Thermodynamics" ||
                            a.GetName().Name == "DWSIM.UnitOperations")
                .ToList();

            var types = new List<Type>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    types.AddRange(assembly.GetTypes().Where(x =>
                        !x.IsAbstract && !x.IsInterface &&
                        typeof(ISimulationObject).IsAssignableFrom(x) &&
                        x.GetConstructor(Type.EmptyTypes) != null));
                }
                catch (Exception) { }
            }

            return types.OrderBy(x => x.Name);
        }

    }

}
