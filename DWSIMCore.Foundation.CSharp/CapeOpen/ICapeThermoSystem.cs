// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoSystem
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Interface that provides access to property packages supported by a Thermodynamics Package.
  /// </summary>
  /// <remarks>
  /// <para>This interface is used to access the various substiuent Property Packages provided by a thermodynamic system.</para>
  /// <para>In the class library, the <see cref="T:CapeOpen.CapeThermoSystem">CapeThermoSystem</see> class provides a list of all
  /// classes Property Packages registered with COM and all .Net-based property packages that are contained in the Global Assembly Cache.</para>
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c0995-7d66-11d2-a67d-00105a42887f")]
  [Description("ICapeThermoSystem Interface")]
  [ComImport]
  public interface ICapeThermoSystem
  {
    /// <summary>Get the list of available property packages</summary>
    /// <remarks>
    /// Returns StringArray of property pacakge names supported by the thermo system.
    /// </remarks>
    /// <returns>
    /// The returned set of supported property packages.
    /// A System.Object containing a String array marshalled from a COM Object.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">ECapeNoImpl</exception>
    [DispId(1)]
    [Description("method GetPropertyPackages")]
    object GetPropertyPackages();

    /// <summary>Resolve a particular property package</summary>
    /// <remarks>
    /// Resolves referenced property package to a property package interface.
    /// </remarks>
    /// <returns>The Property Package Interface.</returns>
    /// <param name="propertyPackage">
    /// The property package to be resolved.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    [Description("method ResolvePropertyPackage")]
    [DispId(2)]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object ResolvePropertyPackage(string propertyPackage);
  }
}
