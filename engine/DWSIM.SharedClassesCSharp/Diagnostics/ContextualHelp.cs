//    Contextual help: the tutorials page that explains the selected object or tool.
//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.Automation.FluentAPI.Diagnostics
{
    /// <summary>
    /// F1 help: which page of the tutorials site explains an object of the flowsheet or a tool.
    /// Every page is the English path under the site root; the Portuguese site's own page names
    /// come from the same table the diagnostic links use.
    /// </summary>
    public static class ContextualHelp
    {
        /// <summary>The page opened when nothing is selected: the track that explains the simulator.</summary>
        public const string DefaultPage = "fundamentals/index.html";

        private static readonly IReadOnlyDictionary<ObjectType, string> ObjectPages = new Dictionary<ObjectType, string>
        {
            { ObjectType.MaterialStream, "fundamentals/01-streams-state-and-balances.html" },
            { ObjectType.EnergyStream, "beginner/03-heater-cooler.html" },
            { ObjectType.NodeIn, "beginner/02-mixer-basics.html" },
            { ObjectType.Mixer, "beginner/02-mixer-basics.html" },
            { ObjectType.EnergyMixer, "beginner/03-heater-cooler.html" },
            { ObjectType.NodeOut, "intermediate/04-recycle-loops.html" },
            { ObjectType.Heater, "beginner/03-heater-cooler.html" },
            { ObjectType.Cooler, "beginner/03-heater-cooler.html" },
            { ObjectType.HeaterCooler, "beginner/03-heater-cooler.html" },
            { ObjectType.Vessel, "beginner/04-simple-flash-drum.html" },
            { ObjectType.TPVessel, "beginner/04-simple-flash-drum.html" },
            { ObjectType.Tank, "beginner/04-simple-flash-drum.html" },
            { ObjectType.HeatExchanger, "intermediate/02-heat-exchanger-design.html" },
            { ObjectType.Pump, "advanced/01-refrigeration-cycle.html" },
            { ObjectType.Compressor, "advanced/01-refrigeration-cycle.html" },
            { ObjectType.Expander, "advanced/01-refrigeration-cycle.html" },
            { ObjectType.CompressorExpander, "advanced/01-refrigeration-cycle.html" },
            { ObjectType.Valve, "advanced/01-refrigeration-cycle.html" },
            { ObjectType.Pipe, "advanced/04-natural-gas-processing.html" },
            { ObjectType.OrificePlate, "advanced/04-natural-gas-processing.html" },
            { ObjectType.RCT_Conversion, "intermediate/03-reaction-systems.html" },
            { ObjectType.RCT_Equilibrium, "intermediate/03-reaction-systems.html" },
            { ObjectType.RCT_Gibbs, "intermediate/03-reaction-systems.html" },
            { ObjectType.RCT_GibbsReaktoro, "intermediate/03-reaction-systems.html" },
            { ObjectType.RCT_CSTR, "intermediate/03-reaction-systems.html" },
            { ObjectType.RCT_PFR, "intermediate/03-reaction-systems.html" },
            { ObjectType.ShortcutColumn, "intermediate/01-distillation-column.html" },
            { ObjectType.DistillationColumn, "intermediate/01-distillation-column.html" },
            { ObjectType.AbsorptionColumn, "intermediate/01-distillation-column.html" },
            { ObjectType.RefluxedAbsorber, "intermediate/01-distillation-column.html" },
            { ObjectType.ReboiledAbsorber, "intermediate/01-distillation-column.html" },
            { ObjectType.ComponentSeparator, "beginner/04-simple-flash-drum.html" },
            { ObjectType.SolidSeparator, "beginner/04-simple-flash-drum.html" },
            { ObjectType.Filter, "beginner/04-simple-flash-drum.html" },
            { ObjectType.OT_Recycle, "fundamentals/03-recycles-and-convergence.html" },
            { ObjectType.OT_EnergyRecycle, "fundamentals/03-recycles-and-convergence.html" },
            { ObjectType.OT_Adjust, "fundamentals/02-degrees-of-freedom.html" },
            { ObjectType.OT_Spec, "fundamentals/02-degrees-of-freedom.html" },
            { ObjectType.Controller_PID, "advanced/01-refrigeration-cycle.html" },
            { ObjectType.CustomUO, "features/index.html" },
            { ObjectType.ExcelUO, "features/index.html" },
            { ObjectType.CapeOpenUO, "features/index.html" },
            { ObjectType.FlowsheetUO, "features/index.html" },
        };

        private static readonly IReadOnlyDictionary<string, string> ToolPages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "flowsheet-check", "fundamentals/06-common-mistakes.html" },
            { "explain-result", "fundamentals/07-the-learning-tools.html#explain-result" },
            { "mccabe-thiele", "fundamentals/07-the-learning-tools.html#mccabe-thiele-diagram" },
            { "eos-explorer", "fundamentals/07-the-learning-tools.html#equation-of-state-explorer" },
            { "package-comparison", "fundamentals/04-choosing-a-thermodynamic-model.html" },
            { "scenario-comparison", "fundamentals/07-the-learning-tools.html#scenario-comparison" },
            { "live-sliders", "fundamentals/07-the-learning-tools.html#live-sliders" },
            { "property-packages", "fundamentals/04-choosing-a-thermodynamic-model.html" },
            { "units", "fundamentals/05-units-and-numbers.html" },
            { "recycle", "fundamentals/03-recycles-and-convergence.html" },
            { "degrees-of-freedom", "fundamentals/02-degrees-of-freedom.html" },
        };

        /// <summary>The site path (English) that explains the object type; the Fundamentals index when there is none.</summary>
        public static string PageFor(ObjectType type)
        {
            string page;
            return ObjectPages.TryGetValue(type, out page) ? page : DefaultPage;
        }

        /// <summary>The absolute URL for an object type in the given site language (en or pt-BR).</summary>
        public static string UrlFor(ObjectType type, string language = "en")
        {
            return FindingExplanations.TutorialsRoot + FindingExplanations.Localise(PageFor(type), language);
        }

        /// <summary>The absolute URL for a tool by its key (flowsheet-check, explain-result, mccabe-thiele, ...).</summary>
        public static string UrlForTool(string tool, string language = "en")
        {
            string page;
            if (string.IsNullOrEmpty(tool) || !ToolPages.TryGetValue(tool, out page)) page = DefaultPage;
            return FindingExplanations.TutorialsRoot + FindingExplanations.Localise(page, language);
        }

        /// <summary>The URL of the track's index in the given language.</summary>
        public static string DefaultUrl(string language = "en")
        {
            return FindingExplanations.TutorialsRoot + FindingExplanations.Localise(DefaultPage, language);
        }
    }
}
