using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using PythonScriptUO = DWSIM.UnitOperations.UnitOperations.CustomUO;
using FS = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.FluentAPI.Tests
{
    // The engines DWSIM hosts are embedded, so they start with sys.executable unset. site.py takes
    // the absolute path of it while it loads, and on Linux and macOS that raises "'NoneType' object
    // has no attribute 'startswith'" for every script that imports site, directly or through a
    // module that does. https://github.com/DanWBR/dwsim10/issues/85
    //
    // The standard library also has to be on the search path for any of this to be reachable at all
    // (issue #46), so the same script imports a stdlib module.
    internal static class PythonScriptUOTest
    {
        public static void Run()
        {
            var fs = FS.Create("PythonScriptUO")
                       .WithCompound("Water")
                       .WithPropertyPackage(PropertyPackages.PengRobinson);

            var uo = fs.AddUnitOperation(ObjectType.CustomUO, "PY-1");

            var script = (PythonScriptUO)uo.Object;
            script.ExecutionEngine = PythonScriptUO.PythonExecutionEngine.IronPython;
            script.ScriptText = string.Join("\n",
                "import site",
                "import pathlib",
                "import sys",
                "stdlib_ok = pathlib.Path(sys.executable).name != ''");

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("the Python Script unit operation did not run: " +
                                    string.Join("; ", errors.Select(e => e.Message)));

            if (!string.IsNullOrEmpty(script.ErrorMessage))
                throw new Exception("the script reported: " + script.ErrorMessage);

            Console.WriteLine("  Python Script unit operation: import site and import pathlib both ran");
        }
    }
}
