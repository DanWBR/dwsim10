using System;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the model predictive controller (dynamic matrix control). Call
    /// <see cref="Flowsheet.AddMPCController"/> to obtain one. It runs once per sample time in a dynamic
    /// run. Controlled and manipulated variables are numbered in the order they are added, from 0;
    /// a step response model links one of each.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddMPCController("LC-MPC")
    ///   .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
    ///   .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
    ///   .WithIntegratingModel(0, 0, -0.0004)
    ///   .WithSampleTime(5.0.Seconds())
    ///   .WithHorizons(30, 5);
    /// </code>
    /// </example>
    public sealed class MPCControllerBuilder : UnitOpBuilder<MPCController, MPCControllerBuilder>
    {
        internal MPCControllerBuilder(Flowsheet f, MPCController o) : base(f, o) { }

        /// <summary>
        /// Adds a controlled variable kept between <paramref name="minimum"/> and <paramref name="maximum"/>,
        /// in <paramref name="units"/>; the controller aims at the middle of the range. With
        /// <paramref name="units"/> null the property's own units in the flowsheet's unit system are used.
        /// </summary>
        public MPCControllerBuilder Controls(string objectTag, string propertyId, string units,
            double minimum, double maximum, double weight = 1.0)
        {
            Object.ControlledVariables.Add(Variable(objectTag, propertyId, units, minimum, maximum, weight));
            return this;
        }

        /// <summary>Adds a manipulated variable moved between <paramref name="minimum"/> and <paramref name="maximum"/>, in <paramref name="units"/>.</summary>
        public MPCControllerBuilder Manipulates(string objectTag, string propertyId, string units,
            double minimum, double maximum, double weight = 1.0)
        {
            Object.ManipulatedVariables.Add(Variable(objectTag, propertyId, units, minimum, maximum, weight));
            return this;
        }

        /// <summary>Adds a measured disturbance variable.</summary>
        public MPCControllerBuilder Measures(string objectTag, string propertyId, string units = null)
        {
            Object.DisturbanceVariables.Add(Variable(objectTag, propertyId, units, double.MinValue, double.MaxValue, 1.0));
            return this;
        }

        /// <summary>
        /// Adds a first order plus dead time response of controlled variable <paramref name="cvIndex"/>
        /// to manipulated variable <paramref name="mvIndex"/>: the steady-state gain, in CV units per MV
        /// unit, the time constant and the dead time.
        /// </summary>
        public MPCControllerBuilder WithFirstOrderModel(int cvIndex, int mvIndex, double gain,
            Quantity timeConstant, Quantity deadTime = default)
        {
            Object.StepResponseModels.Add(new StepResponseModel
            {
                CVIndex = cvIndex,
                MVIndex = mvIndex,
                Gain = gain,
                TimeConstant = timeConstant.SI,
                DeadTime = deadTime.SI,
                Integrating = false
            });
            return this;
        }

        /// <summary>
        /// Adds an integrating response, such as a level moved by an outlet valve: <paramref name="slope"/>
        /// is the CV rate of change per MV unit, in CV units per second, with an optional lag before the
        /// ramp and a dead time.
        /// </summary>
        public MPCControllerBuilder WithIntegratingModel(int cvIndex, int mvIndex, double slope,
            Quantity lag = default, Quantity deadTime = default)
        {
            Object.StepResponseModels.Add(new StepResponseModel
            {
                CVIndex = cvIndex,
                MVIndex = mvIndex,
                Gain = slope,
                TimeConstant = lag.SI,
                DeadTime = deadTime.SI,
                Integrating = true
            });
            return this;
        }

        /// <summary>Sets the time between two control moves; usually the integration step.</summary>
        public MPCControllerBuilder WithSampleTime(Quantity time)
        {
            if (!(time.SI > 0)) throw new ArgumentOutOfRangeException(nameof(time), time.SI, "The sample time must be positive.");
            Object.SampleTime = time.SI;
            return this;
        }

        /// <summary>Sets the prediction and control horizons, in samples.</summary>
        public MPCControllerBuilder WithHorizons(int prediction, int control)
        {
            if (control < 1 || prediction < control)
                throw new ArgumentException("The control horizon must be at least 1 and not longer than the prediction horizon.");
            Object.PredictionHorizon = prediction;
            Object.ControlHorizon = control;
            return this;
        }

        /// <summary>Sets the move suppression weight, which penalises large moves of the manipulated variables.</summary>
        public MPCControllerBuilder WithMoveSuppression(double weight) { Object.MoveSuppressionWeight = weight; return this; }

        /// <summary>Sets the order in which this controller runs relative to the others, low first.</summary>
        public MPCControllerBuilder WithExecutionOrder(int order) { Object.ExecutionOrder = order; return this; }

        /// <summary>Takes the controller in or out of service.</summary>
        public MPCControllerBuilder Active(bool active = true) { Object.Active = active; return this; }

        /// <summary>The current value of controlled variable <paramref name="index"/>, in its units.</summary>
        public double ControlledValue(int index) => Object.ControlledVariables[index].GetCurrentValue(Flowsheet.Inner);

        /// <summary>The current value of manipulated variable <paramref name="index"/>, in its units.</summary>
        public double ManipulatedValue(int index) => Object.ManipulatedVariables[index].GetCurrentValue(Flowsheet.Inner);

        private MPCVariable Variable(string objectTag, string propertyId, string units,
            double minimum, double maximum, double weight)
        {
            if (minimum > maximum) throw new ArgumentException("The minimum must not exceed the maximum.", nameof(minimum));
            var info = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, units, out var obj);
            return new MPCVariable
            {
                ObjectID = obj.Name,
                Name = objectTag + " " + propertyId,
                PropertyName = propertyId,
                Units = info.Units,
                UnitsType = info.UnitsType,
                MinValue = minimum,
                MaxValue = maximum,
                Weight = weight
            };
        }
    }
}
