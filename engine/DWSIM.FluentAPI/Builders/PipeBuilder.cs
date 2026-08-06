using System.Collections.Generic;
using System.Linq;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Pipe unit operation. Call <see cref="Flowsheet.AddPipe"/> to obtain one.</summary>
    public sealed class PipeBuilder : UnitOpBuilder<Pipe, PipeBuilder>
    {
        internal PipeBuilder(Flowsheet f, Pipe o) : base(f, o) { }

        // ---- Property profile access (populated after Calculate) ----

        /// <summary>Hydraulic profile containing all pipe sections and their computed results.</summary>
        public PipeProfile HydraulicProfile => Object.Profile;

        /// <summary>Thermal boundary-condition definitions for the pipe.</summary>
        public ThermalEditorDefinitions ThermalProfile => Object.ThermalProfile;

        /// <summary>All computed results across all pipe sections, flattened into a single list.</summary>
        public List<PipeResults> AllSectionResults
        {
            get
            {
                if (Object.Profile?.Sections == null) return new List<PipeResults>();
                return Object.Profile.Sections.Values
                    .SelectMany(s => s.Results)
                    .ToList();
            }
        }

        /// <summary>Number of computed result points across all pipe sections.</summary>
        public int ProfilePointCount => AllSectionResults.Count;
    }
}
