using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.DynamicsManager;
using DWSIM.Interfaces;
using DynEnums = DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Configures a dynamic integrator: time step, duration, numerical method, calculation rates
    /// and the variables whose time series the run records.
    /// Obtain one from <see cref="DynamicsConfigBuilder.DefineIntegrator"/>.
    /// </summary>
    public sealed class IntegratorBuilder
    {
        private readonly Flowsheet _flowsheet;

        internal IntegratorBuilder(Flowsheet flowsheet, IDynamicsIntegrator integrator)
        {
            _flowsheet = flowsheet;
            Object = integrator;
        }

        /// <summary>The underlying DWSIM integrator.</summary>
        public IDynamicsIntegrator Object { get; }

        /// <summary>The integrator's description, which is how schedules and runs refer to it.</summary>
        public string Name => Object.Description;

        /// <summary>The integrator's internal ID.</summary>
        public string Id => Object.ID;

        // ------------------------------------------------------------------ Timing

        /// <summary>Sets the integration time step.</summary>
        public IntegratorBuilder WithIntegrationStep(Quantity step)
        {
            return WithIntegrationStep(TimeSpan.FromSeconds(step.SI));
        }

        /// <summary>Sets the integration time step.</summary>
        public IntegratorBuilder WithIntegrationStep(TimeSpan step)
        {
            if (step.TotalSeconds <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(step), "The integration step must be greater than zero.");
            Object.IntegrationStep = step;
            return this;
        }

        /// <summary>Sets how much simulated time a run covers.</summary>
        public IntegratorBuilder WithDuration(Quantity duration)
        {
            return WithDuration(TimeSpan.FromSeconds(duration.SI));
        }

        /// <summary>Sets how much simulated time a run covers.</summary>
        public IntegratorBuilder WithDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(duration), "The duration must be greater than zero.");
            Object.Duration = duration;
            return this;
        }

        /// <summary>Sets the wall-clock pace of a real-time run, in milliseconds per step.</summary>
        public IntegratorBuilder WithRealTimeStep(int milliseconds)
        {
            Object.RealTimeStepMs = milliseconds;
            return this;
        }

        // ------------------------------------------------------------------ Method

        /// <summary>Selects the numerical integration method.</summary>
        public IntegratorBuilder WithMethod(DynEnums.IntegrationMethod method)
        {
            Object.IntegrationMethod = method;
            return this;
        }

        /// <summary>
        /// Enables adaptive step sizing, with optional bounds. Only
        /// <see cref="DynEnums.IntegrationMethod.AdaptiveRK45"/> varies the step;
        /// the bounds are what it varies between.
        /// </summary>
        public IntegratorBuilder WithAdaptiveStep(bool enabled = true, Quantity? minimum = null, Quantity? maximum = null)
        {
            Object.AdaptiveStepEnabled = enabled;
            if (minimum.HasValue) Object.MinimumStep = TimeSpan.FromSeconds(minimum.Value.SI);
            if (maximum.HasValue) Object.MaximumStep = TimeSpan.FromSeconds(maximum.Value.SI);
            return this;
        }

        /// <summary>Sets the relative error tolerance used by the adaptive and step-doubling methods.</summary>
        public IntegratorBuilder WithErrorTolerance(double tolerance)
        {
            Object.ErrorTolerance = tolerance;
            return this;
        }

        /// <summary>Sets the inner-loop convergence criteria used by the implicit method.</summary>
        public IntegratorBuilder WithConvergence(double tolerance, int maxIterations = 20)
        {
            Object.ConvergenceTolerance = tolerance;
            Object.MaxIterations = maxIterations;
            return this;
        }

        /// <summary>
        /// Sets how often each subsystem is recalculated, in integration steps. Raising the
        /// equilibrium rate is the usual way to speed up a run whose flashes dominate the cost.
        /// </summary>
        public IntegratorBuilder WithCalculationRates(int equilibrium = 1, int pressureFlow = 1, int control = 1)
        {
            Object.CalculationRateEquilibrium = equilibrium;
            Object.CalculationRatePressureFlow = pressureFlow;
            Object.CalculationRateControl = control;
            return this;
        }

        // ------------------------------------------------------ Monitored variables

        /// <summary>
        /// Records a property's time series over the run. Only monitored variables end up in
        /// <see cref="DynamicsResult"/>, in the chart and in the spreadsheet.
        /// </summary>
        /// <param name="objectTag">Tag of the object holding the property.</param>
        /// <param name="propertyId">
        /// Property identifier, e.g. <c>"PROP_MS_2"</c> or a dynamic property name like <c>"Liquid Level"</c>.
        /// Use <see cref="Flowsheet.Properties"/> to discover them.
        /// </param>
        /// <param name="units">Display units; taken from the object's current unit system when null.</param>
        /// <param name="description">Series name; defaults to <c>"tag - property description"</c>.</param>
        /// <param name="axisMin">Lower chart axis bound; leave both bounds at 0 to autoscale.</param>
        /// <param name="axisMax">Upper chart axis bound.</param>
        public IntegratorBuilder Monitor(string objectTag, string propertyId, string units = null,
            string description = null, double axisMin = 0, double axisMax = 0)
        {
            var obj = _flowsheet.ResolveByTag(objectTag);
            var su = _flowsheet.Inner.FlowsheetOptions.SelectedUnitSystem;

            PropertyCatalog.EnsureDynamicProperties(obj);

            var known = obj.GetProperties(Interfaces.Enums.PropertyType.ALL) ?? new string[0];
            if (!known.Contains(propertyId) && !obj.IsDynamicProperty(propertyId))
            {
                var candidates = PropertyCatalog.Monitorable(obj, su)
                    .Select(p => p.Id + " (" + p.Description + ")")
                    .ToList();
                throw new KeyNotFoundException(
                    "'" + objectTag + "' has no property '" + propertyId + "'. Monitorable properties: " +
                    (candidates.Count == 0 ? "none" : string.Join(", ", candidates)) + ".");
            }

            if (units == null)
            {
                units = obj.IsDynamicProperty(propertyId)
                    ? su.GetCurrentUnits(obj.GetDynamicPropertyUnitType(propertyId))
                    : obj.GetPropertyUnit(propertyId, su);
            }

            var label = description;
            if (string.IsNullOrEmpty(label)) label = objectTag + " " + FriendlyName(obj, propertyId);

            var variable = new MonitoredVariable
            {
                ID = Guid.NewGuid().ToString(),
                Description = label,
                ObjectID = obj.Name,
                PropertyID = propertyId,
                PropertyUnits = units ?? "",
                MinimumChartAxisValue = axisMin,
                MaximumChartAxisValue = axisMax
            };

            Object.MonitoredVariables.Add(variable);
            return this;
        }

        /// <summary>Records several properties of the same object, using default units and names.</summary>
        public IntegratorBuilder MonitorAll(string objectTag, params string[] propertyIds)
        {
            foreach (var id in propertyIds) Monitor(objectTag, id);
            return this;
        }

        /// <summary>Drops every monitored variable, so a reconfiguration starts from a clean list.</summary>
        public IntegratorBuilder ClearMonitoredVariables()
        {
            Object.MonitoredVariables.Clear();
            return this;
        }

        /// <summary>The monitored variables currently configured, as "description (units)".</summary>
        public IReadOnlyList<string> MonitoredVariableNames =>
            Object.MonitoredVariables
                .Select(v => v.Description + (string.IsNullOrEmpty(v.PropertyUnits) ? "" : " (" + v.PropertyUnits + ")"))
                .ToList();

        /// <summary>
        /// The most readable name a property has. Dynamic properties are already named in words;
        /// regular ones carry a description, except when the object never wrote one, in which case
        /// the identifier beats the placeholder text.
        /// </summary>
        private static string FriendlyName(ISimulationObject obj, string propertyId)
        {
            return obj.IsDynamicProperty(propertyId)
                ? propertyId
                : PropertyCatalog.Describe(obj, propertyId);
        }

        /// <summary>Escape hatch: applies an arbitrary mutation to the underlying integrator.</summary>
        public IntegratorBuilder Configure(Action<IDynamicsIntegrator> action)
        {
            if (action != null) action(Object);
            return this;
        }
    }
}
