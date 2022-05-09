// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoUniversalConstant
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Implemented by a component that can return the value of a Universal
  /// Constant.</summary>
  /// <remarks>Any component that can return the value of a Universal Constant can
  /// implement the ICapeThermoUniversalConstants interface in order that clients can
  /// access these values. This interface is optional for all components. It is
  /// recommended that it is implemented by Property Package components and Material
  /// Objects being used by CAPE-OPEN Unit Operations.</remarks>
  [Description("ICapeThermoUniversalConstant Interface")]
  [ComVisible(false)]
  [Guid("678C0AA1-7D66-11D2-A67D-00105A42887F")]
  [ComImport]
  public interface ICapeThermoUniversalConstant
  {
    /// <summary>Retrieves the value of a Universal Constant.</summary>
    /// <param name="constantId">Identifier of Universal Constant. The list of
    /// constants supported should be obtained by using the GetUniversalConstList
    /// method.</param>
    /// <returns>Value of Universal Constant. This could be a numeric or a string
    /// value. For numeric values the units of measurement are specified in section
    /// 7.5.1.</returns>
    /// <remarks>Universal Constants (often called fundamental constants) are
    /// quantities like the gas constant, or the Avogadro constant.</remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetUniversalConstant is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists, but
    /// it is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">For example, UNDEFINED for constantId
    /// argument is used, or value for constantId argument does not belong to the
    /// list of recognised values.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the GetUniversalConstant operation, are not suitable.</exception>
    [Description("method GetUniversalConstant")]
    [DispId(1)]
    object GetUniversalConstant([In] string constantId);

    /// <summary>Returns the identifiers of the supported Universal Constants.</summary>
    /// <returns>List of identifiers of Universal Constants. The list of standard
    /// identifiers is given in section 7.5.1.</returns>
    /// <remarks>A component may return Universal Constant identifiers that do not
    /// belong to the list defined in section 7.5.1. However, these proprietary
    /// identifiers may not be understood by most of the clients of this component.
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetUniversalConstantList is
    /// “not” implemented even if this method can be called for reasons of
    /// compatibility with the CAPE-OPEN standards. That is to say that the operation
    /// exists, but it is not supported by the current implementation. This may occur
    /// when the Property Package does not support any Universal Constants, or if it
    /// does not want to provide values for any Universal Constants which may be used
    /// within the Property Package.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the GetUniversalConstantList operation, are not suitable.
    /// </exception>
    [DispId(2)]
    [Description("method GetUniversalConstantList")]
    object GetUniversalConstantList();
  }
}
