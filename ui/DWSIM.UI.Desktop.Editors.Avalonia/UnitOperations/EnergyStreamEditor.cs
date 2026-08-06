using Avalonia.Controls;
using DWSIM.UI.Shared.Avalonia;
using DWSIM.Interfaces.Enums;
using EnergyStream = DWSIM.UnitOperations.Streams.EnergyStream;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Energy stream editor, as the Windows EditingForm_EnergyStream lays it out: general info,
    /// connections, the energy flow and the notes. The energy flow is read-only while something
    /// upstream is feeding the stream, which is what the Windows form does with its inlet.
    /// </summary>
    public static class EnergyStreamEditor
    {

        public static Control Build(EnergyStream stream)
        {
            return UnitOpEditor.Build(stream,
                input: panel =>
                {
                    var driven = stream.GraphicObject.InputConnectors.Count > 0 &&
                                 stream.GraphicObject.InputConnectors[0].IsAttached;

                    panel.CreateAndAddValueUnitRow(stream, "Energy Flow / Power",
                        UnitOfMeasure.heatflow,
                        stream.EnergyFlow.GetValueOrDefault(),
                        si => stream.EnergyFlow = si,
                        enabled: !driven);

                    if (driven)
                        panel.CreateAndAddDescriptionRow(
                            "The energy flow comes from the object connected upstream.");
                },
                propertyPackage: false);
        }

    }

}
