using System;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.UnitOperations.SpecialOps.Helpers;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for a PID controller. A controller needs three things before it can run:
    /// the variable it reads (<see cref="Controls"/>), the one it writes
    /// (<see cref="Manipulates"/>), and a setpoint.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddPIDController("LIC-01")
    ///   .Controls("TK-01", "Liquid Level", "m")
    ///   .Manipulates("V-01", "Opening Setpoint", "%")
    ///   .WithSetPoint(1.0)
    ///   .WithTuning(kp: 5.0, ki: 0.5, kd: 0.0)
    ///   .WithOutputLimits(0.0, 100.0);
    /// </code>
    /// </example>
    public sealed class PIDControllerBuilder : UnitOpBuilder<PIDController, PIDControllerBuilder>
    {
        internal PIDControllerBuilder(Flowsheet flowsheet, PIDController obj) : base(flowsheet, obj) { }

        // ------------------------------------------------------------- Wiring

        /// <summary>Sets the process variable: what the controller reads and tries to hold at setpoint.</summary>
        public PIDControllerBuilder Controls(string objectTag, string propertyId, string units = null)
        {
            Object.ControlledObjectData = Describe(objectTag, propertyId, units);
            return this;
        }

        /// <summary>Sets the manipulated variable: what the controller writes to.</summary>
        public PIDControllerBuilder Manipulates(string objectTag, string propertyId, string units = null)
        {
            Object.ManipulatedObjectData = Describe(objectTag, propertyId, units);
            return this;
        }

        /// <summary>Sets the disturbance variable read by the feedforward term.</summary>
        public PIDControllerBuilder References(string objectTag, string propertyId, string units = null)
        {
            Object.DisturbanceObjectData = (SpecialOpObjectInfo)Describe(objectTag, propertyId, units);
            return this;
        }

        // ------------------------------------------------------------- Tuning

        /// <summary>Sets the setpoint, in the controlled property's display units.</summary>
        public PIDControllerBuilder WithSetPoint(double setpoint)
        {
            Object.SetPoint = setpoint;
            return this;
        }

        /// <summary>Sets the proportional, integral and derivative gains.</summary>
        public PIDControllerBuilder WithTuning(double kp, double ki, double kd)
        {
            Object.Kp = kp;
            Object.Ki = ki;
            Object.Kd = kd;
            return this;
        }

        /// <summary>
        /// Clamps the controller output. These are the manipulated variable's physical bounds —
        /// a valve opening cannot leave 0-100 % — and they also bound the anti-windup.
        /// </summary>
        public PIDControllerBuilder WithOutputLimits(double minimum, double maximum)
        {
            if (minimum >= maximum)
                throw new ArgumentException("The output minimum must be below the maximum.", nameof(minimum));
            Object.OutputMin = minimum;
            Object.OutputMax = maximum;
            return this;
        }

        /// <summary>Sets the integral anti-windup guard.</summary>
        public PIDControllerBuilder WithWindupGuard(double guard)
        {
            Object.WindupGuard = guard;
            return this;
        }

        /// <summary>
        /// Reverses the control action. Get this backwards and the controller drives the error up
        /// instead of down, which looks exactly like a diverging simulation.
        /// </summary>
        public PIDControllerBuilder ReverseActing(bool reverse = true)
        {
            Object.ReverseActing = reverse;
            return this;
        }

        /// <summary>Sets the bias added to the controller output.</summary>
        public PIDControllerBuilder WithOffset(double offset)
        {
            Object.Offset = offset;
            return this;
        }

        /// <summary>Sets the order in which this controller runs relative to the others, low first.</summary>
        public PIDControllerBuilder WithExecutionOrder(int order)
        {
            Object.ExecutionOrder = order;
            return this;
        }

        /// <summary>Filters the derivative term, and optionally takes it on the PV instead of the error.</summary>
        public PIDControllerBuilder WithDerivativeFilter(double coefficient, bool onProcessVariable = false)
        {
            Object.DerivativeFilterCoefficient = coefficient;
            Object.UseDerivativeOnPV = onProcessVariable;
            return this;
        }

        /// <summary>Sets the setpoint weights of the proportional and derivative terms.</summary>
        public PIDControllerBuilder WithSetpointWeights(double proportional, double derivative)
        {
            Object.SetpointWeightP = proportional;
            Object.SetpointWeightD = derivative;
            return this;
        }

        /// <summary>Takes the setpoint from a master controller's output, forming a cascade.</summary>
        public PIDControllerBuilder CascadeFrom(string masterControllerTag)
        {
            var master = Flowsheet.ResolveByTag(masterControllerTag);
            if (!(master is PIDController))
                throw new ArgumentException("'" + masterControllerTag + "' is not a PID controller.",
                    nameof(masterControllerTag));
            Object.CascadeMasterID = master.Name;
            return this;
        }

        /// <summary>Configures the feedforward term acting on the disturbance set by <see cref="References"/>.</summary>
        public PIDControllerBuilder WithFeedforward(double gain, Quantity leadTime, Quantity lagTime)
        {
            Object.FeedforwardGain = gain;
            Object.FeedforwardLeadTime = leadTime.SI;
            Object.FeedforwardLagTime = lagTime.SI;
            return this;
        }

        // ------------------------------------------------------------- State

        /// <summary>Takes the controller in or out of service.</summary>
        public PIDControllerBuilder Active(bool active = true)
        {
            Object.Active = active;
            return this;
        }

        /// <summary>Puts the controller in manual, holding its output at a fixed value.</summary>
        public PIDControllerBuilder ManualOverride(bool manual, double output = 0.0)
        {
            Object.ManualOverride = manual;
            if (manual) Object.Output = output;
            return this;
        }

        // ------------------------------------------------------------- Read-back

        /// <summary>The process variable at the last solved step, in display units.</summary>
        public double ProcessVariable => Object.PVValue;

        /// <summary>The setpoint, in display units.</summary>
        public double SetPoint => Object.SPValue;

        /// <summary>The manipulated variable at the last solved step, in display units.</summary>
        public double ManipulatedVariable => Object.MVValue;

        /// <summary>The controller output at the last solved step.</summary>
        public double Output => Object.Output;

        /// <summary>The accumulated error over the run; the objective the PID tuner minimises.</summary>
        public double CumulativeError => Object.CumulativeError;

        private ISpecialOpObjectInfo Describe(string objectTag, string propertyId, string units)
        {
            var obj = Flowsheet.ResolveByTag(objectTag);
            var su = Flowsheet.Inner.FlowsheetOptions.SelectedUnitSystem;

            PropertyCatalog.EnsureDynamicProperties(obj);

            var isDynamic = obj.IsDynamicProperty(propertyId);

            if (units == null)
            {
                units = isDynamic
                    ? su.GetCurrentUnits(obj.GetDynamicPropertyUnitType(propertyId))
                    : obj.GetPropertyUnit(propertyId, su);
            }

            return new SpecialOpObjectInfo
            {
                ID = obj.Name,
                Name = objectTag,
                PropertyName = propertyId,
                ObjectType = obj.GetDisplayName(),
                Units = units ?? "",
                UnitsType = isDynamic
                    ? obj.GetDynamicPropertyUnitType(propertyId)
                    : Interfaces.Enums.UnitOfMeasure.none
            };
        }
    }
}
