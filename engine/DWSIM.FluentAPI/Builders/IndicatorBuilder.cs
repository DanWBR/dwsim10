using System;
using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Which indicator face to draw on the flowsheet.</summary>
    public enum IndicatorKind
    {
        /// <summary>A dial gauge.</summary>
        Analog,
        /// <summary>A numeric readout.</summary>
        Digital,
        /// <summary>A vertical level bar.</summary>
        Level
    }

    /// <summary>
    /// Fluent builder for an indicator: it reads one property and raises alarms on it. Alarms are
    /// what a cause-and-effect matrix reacts to, so an interlock needs an indicator first.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddIndicator("LI-01", IndicatorKind.Level)
    ///   .Reads("TK-01", "Liquid Level", "m")
    ///   .WithRange(0.0, 2.0)
    ///   .WithAlarms(veryLow: 0.1, low: 0.3, high: 1.7, veryHigh: 1.9);
    /// </code>
    /// </example>
    public sealed class IndicatorBuilder : UnitOpBuilder<ISimulationObject, IndicatorBuilder>
    {
        internal IndicatorBuilder(Flowsheet flowsheet, ISimulationObject obj) : base(flowsheet, obj)
        {
            Indicator = obj as IIndicator;
            if (Indicator == null)
                throw new ArgumentException("'" + obj.Name + "' is not an indicator.", nameof(obj));
        }

        /// <summary>The underlying indicator.</summary>
        public IIndicator Indicator { get; }

        /// <summary>Points the indicator at a property of an object.</summary>
        public IndicatorBuilder Reads(string objectTag, string propertyId, string units = null)
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

            Indicator.SelectedObjectID = obj.Name;
            Indicator.SelectedProperty = propertyId;
            Indicator.SelectedPropertyUnits = units ?? "";
            Indicator.SelectedPropertyType = isDynamic
                ? obj.GetDynamicPropertyUnitType(propertyId)
                : Interfaces.Enums.UnitOfMeasure.none;
            return this;
        }

        /// <summary>Sets the indicator's scale, in the read property's display units.</summary>
        public IndicatorBuilder WithRange(double minimum, double maximum)
        {
            Indicator.MinimumValue = minimum;
            Indicator.MaximumValue = maximum;
            return this;
        }

        /// <summary>
        /// Sets the alarm thresholds. Passing null leaves a level disabled; each supplied value
        /// enables its level.
        /// </summary>
        public IndicatorBuilder WithAlarms(double? veryLow = null, double? low = null,
            double? high = null, double? veryHigh = null)
        {
            if (veryLow.HasValue) { Indicator.VeryLowAlarmValue = veryLow.Value; Indicator.VeryLowAlarmEnabled = true; }
            if (low.HasValue) { Indicator.LowAlarmValue = low.Value; Indicator.LowAlarmEnabled = true; }
            if (high.HasValue) { Indicator.HighAlarmValue = high.Value; Indicator.HighAlarmEnabled = true; }
            if (veryHigh.HasValue) { Indicator.VeryHighAlarmValue = veryHigh.Value; Indicator.VeryHighAlarmEnabled = true; }
            Indicator.ShowAlarms = true;
            return this;
        }

        /// <summary>Sets how many digits the readout shows either side of the decimal point.</summary>
        public IndicatorBuilder WithDigits(int integral, int decimals)
        {
            Indicator.IntegralDigits = integral;
            Indicator.DecimalDigits = decimals;
            return this;
        }

        /// <summary>The value read at the last solved step, in display units.</summary>
        public double CurrentValue => Indicator.CurrentValue;
    }
}
