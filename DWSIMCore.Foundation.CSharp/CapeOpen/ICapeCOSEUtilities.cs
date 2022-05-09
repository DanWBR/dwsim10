// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeCOSEUtilities
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides a mechanism for the PMC to obtain a free FORTRAN channel from the PME.
  /// </summary>
  /// <remarks>
  /// When a PMC is wrapping a FORTRAN dll, there may be a technical problem when the PMC
  /// is loaded in the same process as the PME such as Simulator Execution. In this case, there
  /// may be a clash between different FORTRAN modules if two of them select the same output
  /// channel for FORTRAN messaging. Hence the PME should centralise the generation of
  /// unique output channels for each PMC that may require them. This requirement only occurs
  /// when PME and PMC belong to the same computing process, obviously this FORTRAN
  /// channel functionality is only applicable when the architecture is not distributed. As we can
  /// have in the future this kind of information to exchange, a generic and extensible mechanism
  /// has to be set up. The calling pattern is a good candidate. Thus a specific string value for
  /// FORTRAN channel would be standardised.
  /// </remarks>
  [Description("ICapeCOSEUtilities Interface")]
  [ComVisible(false)]
  [Guid("678c0a9f-0100-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeCOSEUtilities
  {
    /// <summary>The list of named values supported by the PME.</summary>
    /// <remarks>The list of NamedValues provided by the PME.</remarks>
    /// <returns>
    /// Returns a String Array list of named values supported by the COSE. Included in this list
    /// should be the FreeFORTRANchannel named value which will provide the name of free FORTRAN
    /// channel.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(1)]
    [Description("property NamedValueList")]
    object NamedValueList { get; }

    /// <summary>
    /// Returns a value corresponding to the request name, including a free FORTRAN channel.
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <returns>
    /// Returns the value corresponding to the value named name. Be aware that two
    /// consecutive calls passing the same name may return different values. The COSE will
    /// return a different FORTRAN channel each time the FreeFORTRANchannel NamedValue is
    /// called for this property. The COSE may not use any of the returned FORTRAN channels
    /// for any internally used FORTRAN module.
    /// </returns>
    /// <param name="value">Name of the requested value (which must be included in the list
    /// returned by NamedValueList).</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(2)]
    [Description("property NamedValue")]
    object NamedValue(string value);
  }
}
