// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeUnitReport
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface provides access to the active unit report and the available list of options.
  /// </summary>
  /// <remarks>
  /// It also provides a trigger for the creation of a report.
  /// </remarks>
  [Description("ICapeUnitReport Interface")]
  [Guid("678c099b-0093-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeUnitReport
  {
    /// <summary>
    /// Gets the list of possible reports for the unit operation.
    /// </summary>
    /// <remarks>Return the list of available Flowsheet Unit reports.</remarks>
    /// <value>The list of possible reports for the unit operation.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">ECapeNoImpl</exception>
    [DispId(1)]
    [Description("Gets the list of unit reports")]
    object reports { get; }

    /// <summary>
    /// Gets and sets the current active report for the unit operation.
    /// </summary>
    /// <remarks>Return/set the active report in the Flowsheet Unit.</remarks>
    /// <value>The current active report for the unit operation.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">ECapeNoImpl</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Gets the active unit report")]
    [DispId(2)]
    string selectedReport { get; set; }

    /// <summary>Produces the active report for the unit operation.</summary>
    /// <remarks>
    /// Produce the designated report. If no value has been set, it produces the default
    /// report.
    /// </remarks>
    /// <param name="message">String containing the text for the currently selected report.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">ECapeNoImpl</exception>
    [Description("Creates the active report")]
    [DispId(3)]
    void ProduceReport(ref string message);
  }
}
