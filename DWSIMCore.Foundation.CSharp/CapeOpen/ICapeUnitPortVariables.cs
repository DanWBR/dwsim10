// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeUnitPortVariables
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Port variables for equation-oriented simulators.</summary>
  /// <remarks>
  /// This interface is optional and would be implemented by a port object. It is intended
  /// to allow a port to describe which Equation-oriented variables are associated with it and
  /// should only be implemented for the ports contained in a unit operation which supports the
  /// ICapeNumericESO interface described in “CAPE-OPEN Interface Specification – Numerical
  /// Solvers”.
  /// </remarks>
  [Guid("678c09b1-7d66-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [Description("ICapeUnitPortVariables Interface")]
  [ComImport]
  public interface ICapeUnitPortVariables
  {
    /// <summary>The position of a port variable in the EO model.</summary>
    /// <remarks>
    /// Gets the position of a port variable in the EO model - used to
    /// correctly build the equations representing a connection to this port.
    ///  Variable type can be - flowrate, temperature, pressure,
    /// specificEnthalpy, VaporFraction and for Vapour fraction component
    /// name must also be specified.
    /// </remarks>
    /// <param name="Variable_type">The Type of the variable.</param>
    /// <param name="Component">The compnent of the variable.</param>
    /// <value>The position of the variable.</value>
    [Description("Return index of port variable in EO Model given its type")]
    [DispId(1)]
    int Variable(string Variable_type, string Component);

    /// <summary>
    /// Sets the position of port variables: this should ultimately
    /// be a private member function.
    /// </summary>
    /// <remarks>
    /// Sets the position of port variables: this should ultimately
    /// be a private member function.
    /// </remarks>
    /// <param name="Variable_type">The Type of the variable.</param>
    /// <param name="Component">The compnent of the variable.</param>
    /// <param name="index">The index of the variable.</param>
    [DispId(2)]
    [Description("Set index of port variable in EO model given its type")]
    void SetIndex(string Variable_type, string Component, int index);
  }
}
