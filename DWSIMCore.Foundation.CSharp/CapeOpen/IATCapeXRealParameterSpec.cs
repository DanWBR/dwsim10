// Decompiled with JetBrains decompiler
// Type: CapeOpen.IATCapeXRealParameterSpec
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  ///  Aspen interface for providing dimension for a real-valued parameter.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Aspen Plus does not use the <see cref="P:CapeOpen.ICapeParameterSpec.Dimensionality">ICapeParameterSpec.Dimensionality</see> method. Instead a parameter
  /// can implement the IATCapeXRealParameterSpec interface which can be used to define the
  /// display unit for a parameter value.
  /// </para>
  /// </remarks>
  [Guid("B777A1BD-0C88-11D3-822E-00C04F4F66C9")]
  [Description("IATCapeXRealParameterSpec Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface IATCapeXRealParameterSpec
  {
    /// <summary>Gets the default value of the parameter.</summary>
    /// <remarks>
    /// <para>DisplayUnits defines the unit of measurement symbol for a parameter.</para>
    /// <para>Note: The symbol must be one of the uppercase strings recognized by Aspen
    /// Plus to ensure that it can perform unit of measurement conversions on the
    /// parameter value. The system converts the parameter's value from SI units for
    /// display in the data browser and converts updated values back into SI.
    /// </para>
    /// </remarks>
    /// <value>Defines the display unit for the parameter.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(1610874883)]
    [Description(" Provide the Aspen Plus display units for for this parameter.")]
    string DisplayUnits { get; }
  }
}
