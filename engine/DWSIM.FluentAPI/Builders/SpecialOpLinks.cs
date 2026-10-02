using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps.Helpers;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Wiring shared by the logical block and controller builders: describing the variable a block
    /// reads or writes, and marking the object as attached to the block the way the editors do.
    /// </summary>
    internal static class SpecialOpLinks
    {
        /// <summary>
        /// Describes property <paramref name="propertyId"/> of the object tagged <paramref name="objectTag"/>.
        /// When <paramref name="units"/> is null the units are the property's own in the flowsheet's
        /// unit system (or the dynamic property's unit).
        /// </summary>
        internal static SpecialOpObjectInfo Describe(Flowsheet flowsheet, string objectTag, string propertyId,
            string units, out ISimulationObject obj)
        {
            obj = flowsheet.ResolveByTag(objectTag);
            var su = flowsheet.Inner.FlowsheetOptions.SelectedUnitSystem;

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
                UnitsType = isDynamic ? obj.GetDynamicPropertyUnitType(propertyId) : UnitOfMeasure.none
            };
        }

        /// <summary>Marks <paramref name="target"/> as linked to adjust <paramref name="block"/> in the given role.</summary>
        internal static void AttachAdjust(ISimulationObject block, ISimulationObject target, AdjustVarType role)
        {
            target.IsAdjustAttached = true;
            target.AttachedAdjustId = block.Name;
            target.AdjustVarType = role;
        }

        /// <summary>Marks <paramref name="target"/> as linked to spec <paramref name="block"/> in the given role.</summary>
        internal static void AttachSpec(ISimulationObject block, ISimulationObject target, SpecVarType role)
        {
            target.IsSpecAttached = true;
            target.AttachedSpecId = block.Name;
            target.SpecVarType = role;
        }

        /// <summary>Marks <paramref name="target"/> as linked to information carrier <paramref name="block"/> in the given role.</summary>
        internal static void AttachInformationCarrier(ISimulationObject block, ISimulationObject target, SpecVarType role)
        {
            target.IsInfoCarrierAttached = true;
            target.AttachedInfoCarrierId = block.Name;
            target.InfoCarrierVarType = role;
        }

        /// <summary>The drawing of an object, as the shape type the block graphics link to.</summary>
        internal static DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject Shape(ISimulationObject obj)
            => obj.GraphicObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject;

        /// <summary>The object as the base class the block's typed links hold.</summary>
        internal static DWSIM.SharedClasses.UnitOperations.BaseClass AsBase(ISimulationObject obj)
            => obj as DWSIM.SharedClasses.UnitOperations.BaseClass;
    }
}
