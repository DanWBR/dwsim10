using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Python (IronPython) controller, a controller whose control law is a
    /// script. Call <see cref="Flowsheet.AddPythonController"/> to obtain one. It runs once per
    /// integration step in a dynamic run.
    /// </summary>
    /// <remarks>
    /// The script receives <c>PV</c> (the process variable, in the controlled variable's units) and
    /// <c>Me</c> (the controller), and must assign <c>MV</c> (the manipulated variable, in its units)
    /// and <c>SP</c> (the setpoint).
    /// </remarks>
    /// <example>
    /// <code>
    /// fs.AddPythonController("LC-PY")
    ///   .Controls("T-01", "Liquid Level", "m")
    ///   .Manipulates("V-02", "PROP_VA_5", "")
    ///   .WithInitialOutput(29.0)
    ///   .WithScript("SP = 1.0\nMV = Me.Output + 40.0 * (PV - SP)");
    /// </code>
    /// </example>
    public sealed class PythonControllerBuilder : UnitOpBuilder<PythonController, PythonControllerBuilder>
    {
        internal PythonControllerBuilder(Flowsheet f, PythonController o) : base(f, o) { }

        /// <summary>
        /// Sets the process variable the script receives as <c>PV</c>. With <paramref name="units"/>
        /// null the property's own units in the flowsheet's unit system are used.
        /// </summary>
        public PythonControllerBuilder Controls(string objectTag, string propertyId, string units = null)
        {
            Object.ControlledObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, units, out var obj);
            Object.ControlledObject = SpecialOpLinks.AsBase(obj);
            return this;
        }

        /// <summary>
        /// Sets the manipulated variable the script writes as <c>MV</c>. With <paramref name="units"/>
        /// null the property's own units in the flowsheet's unit system are used.
        /// </summary>
        public PythonControllerBuilder Manipulates(string objectTag, string propertyId, string units = null)
        {
            Object.ManipulatedObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, units, out var obj);
            Object.ManipulatedObject = SpecialOpLinks.AsBase(obj);
            return this;
        }

        /// <summary>Sets the IronPython script run at each control step.</summary>
        public PythonControllerBuilder WithScript(string script) { Object.PythonScript = script ?? ""; return this; }

        /// <summary>Sets the setpoint the controller starts with; the script may change it through <c>SP</c>.</summary>
        public PythonControllerBuilder WithSetPoint(double setpoint) { Object.SetPoint = setpoint; return this; }

        /// <summary>Sets the output the controller starts from, read by a script through <c>Me.Output</c>.</summary>
        public PythonControllerBuilder WithInitialOutput(double output) { Object.Output = output; return this; }

        /// <summary>Takes the controller in or out of service.</summary>
        public PythonControllerBuilder Active(bool active = true) { Object.Active = active; return this; }

        /// <summary>The process variable at the last control step, in its units.</summary>
        public double ProcessVariable => Object.PVValue;
        /// <summary>The setpoint at the last control step.</summary>
        public double SetPoint => Object.SetPoint;
        /// <summary>The output (<c>MV</c>) of the last control step, in the manipulated variable's units.</summary>
        public double Output => Object.Output;
    }
}
