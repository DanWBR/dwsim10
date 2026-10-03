using System;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Information Carrier logical block: it copies one property of a source
    /// object to up to three targets. Call <see cref="Flowsheet.AddInformationCarrier"/> to obtain one.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddInformationCarrier("IC-1")
    ///   .WithSource("H-1", "PROP_HT_2")
    ///   .WithTarget("H-2", "PROP_HT_2")
    ///   .WithTarget("H-3", "PROP_HT_2");
    /// </code>
    /// </example>
    public sealed class InformationCarrierBuilder : UnitOpBuilder<InformationCarrier, InformationCarrierBuilder>
    {
        internal InformationCarrierBuilder(Flowsheet f, InformationCarrier o) : base(f, o) { }

        /// <summary>Sets the property whose value is copied.</summary>
        public InformationCarrierBuilder WithSource(string objectTag, string propertyId)
        {
            Object.SourceObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, null, out var obj);
            Object.SourceObject = SpecialOpLinks.AsBase(obj);
            SpecialOpLinks.AttachInformationCarrier(Object, obj, SpecVarType.Source);
            if (Object.GraphicObject is DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.InformationCarrierGraphic g)
                g.ConnectedToSv = SpecialOpLinks.Shape(obj);
            return this;
        }

        /// <summary>Adds a property the value is copied to, in the first free of the three target slots.</summary>
        /// <exception cref="InvalidOperationException">The three targets are already set.</exception>
        public InformationCarrierBuilder WithTarget(string objectTag, string propertyId)
        {
            var info = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, null, out var obj);
            var g = Object.GraphicObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.InformationCarrierGraphic;
            var shape = SpecialOpLinks.Shape(obj);

            if (IsFree(Object.TargetObjectData))
            {
                Object.TargetObjectData = info;
                Object.TargetObject = SpecialOpLinks.AsBase(obj);
                if (g != null) g.ConnectedToTv = shape;
            }
            else if (IsFree(Object.TargetObjectData2))
            {
                Object.TargetObjectData2 = info;
                Object.TargetObject2 = SpecialOpLinks.AsBase(obj);
                if (g != null) g.ConnectedToTv2 = shape;
            }
            else if (IsFree(Object.TargetObjectData3))
            {
                Object.TargetObjectData3 = info;
                Object.TargetObject3 = SpecialOpLinks.AsBase(obj);
                if (g != null) g.ConnectedToTv3 = shape;
            }
            else
            {
                throw new InvalidOperationException("'" + Object.GraphicObject?.Tag + "' already has three targets.");
            }

            SpecialOpLinks.AttachInformationCarrier(Object, obj, SpecVarType.Target);
            return this;
        }

        /// <summary>
        /// Sets when the solver runs the information carriers: after the source object (the default)
        /// or before the target object, for instance. This is a flowsheet setting and applies to every
        /// information carrier whose own mode is <see cref="SpecCalcMode2.GlobalSetting"/>
        /// (see <see cref="WithCalculationMode"/>).
        /// </summary>
        public InformationCarrierBuilder WithFlowsheetCalculationMode(SpecCalcMode mode)
        {
            Flowsheet.Inner.FlowsheetOptions.InformationCarrierCalculationMode = mode;
            return this;
        }

        /// <summary>
        /// Sets when the solver runs this information carrier, overriding the flowsheet setting for it
        /// alone: <see cref="SpecCalcMode2.AfterSourceObject"/>, <see cref="SpecCalcMode2.BeforeTargetObject"/>,
        /// <see cref="SpecCalcMode2.BeforeFlowsheet"/> or <see cref="SpecCalcMode2.AfterFlowsheet"/>.
        /// <see cref="SpecCalcMode2.GlobalSetting"/> (the default) follows the flowsheet setting; an
        /// information carrier has no reference object, so BeforeObject and AfterObject follow it too.
        /// </summary>
        public InformationCarrierBuilder WithCalculationMode(SpecCalcMode2 mode)
        {
            Object.CalculationMode = mode;
            return this;
        }

        private static bool IsFree(DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo info)
            => info == null || string.IsNullOrEmpty(info.ID);
    }
}
