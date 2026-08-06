using DWSIM.GlobalSettings;
using DWSIM.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DWSIM.DynamicRunner
{
    /// <summary>
    /// Provides automation support for loading and running DWSIM flowsheets without a GUI.
    /// </summary>
    public class DynamicsAutomation
    {

        /// <summary>
        /// Initializes the automation environment, enabling automation mode and loading property packages.
        /// </summary>
        public DynamicsAutomation()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            Settings.AutomationMode = true;
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        /// <summary>
        /// Loads a DWSIM flowsheet from the specified file path.
        /// Supports both plain XML (.dwxml) and compressed/zipped (.dwxmz) flowsheet formats.
        /// </summary>
        /// <param name="filepath">The full path to the flowsheet file.</param>
        /// <returns>An <see cref="IFlowsheet"/> instance representing the loaded flowsheet.</returns>
        public IFlowsheet LoadFlowsheet(string filepath)
        {
            Settings.AutomationMode = true;
            Settings.CultureInfo = "en";
            var fsheet = new Flowsheet(null, null);
            fsheet.Init();
            if (System.IO.Path.GetExtension(filepath).ToLower().EndsWith("z"))
            {
                fsheet.LoadZippedXML(filepath);
            }
            else
            {
                fsheet.LoadFromXML(XDocument.Load(filepath));
            }
            return fsheet;
        }
       
    }

}
