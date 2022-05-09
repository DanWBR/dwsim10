// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoPropertyPackageManager
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The ICapeThermoPropertyPackageManager interface should only be implemented
  /// by a Property Package Manager component. This interface is used to access the
  /// Property Packages managed by such a component.</summary>
  [Description("ICapeThermoPropertyPackageManager Interface")]
  [ComVisible(false)]
  [Guid("678C0AA2-7D66-11D2-A67D-00105A42887F")]
  [ComImport]
  public interface ICapeThermoPropertyPackageManager
  {
    /// <summary>Retrieves the names of the Property Packages being managed by a
    /// Property Package Manager component.</summary>
    /// <returns>The names of the managed Property Packages</returns>
    /// <remarks>If no packages are managed by the Property Package Manager UNDEFINED
    /// should be returned.</remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetPropertyPackageList is “not”
    /// implemented even if this method can be called for reasons of compatibility with
    /// the CAPE-OPEN standards. That is to say that the operation exists, but it is
    /// not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the GetPropertyPackageList operation, are not suitable.</exception>
    [DispId(1)]
    [Description("method GetPropertyPackageList")]
    object GetPropertyPackageList();

    /// <summary>Creates a new instance of a Property Package with the configuration
    /// specified by the PackageName argument.</summary>
    /// <param name="PackageName">The name of one of the Property Packages managed
    /// by this Property Package Manager component.</param>
    /// <returns>The ICapeThermoPropertyRoutine interface of the named Property
    /// Package.</returns>
    /// <remarks><para>The Property Package Manager is only an indirect mechanism to create
    /// Property Packages.</para>
    /// <para>After the Property Package has been created, the Property Package Manager
    /// instance can be destroyed, and this will not affect the normal behaviour of
    /// the Property Packages.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetPropertyPackage is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists, but it
    /// is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">This error should be returned if
    /// the Property Package cannot be created for any reason.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">This error will be returned if the
    /// name of the Property Package asked for does not belong to the list of
    /// recognised names. Comparison of names is not case sensitive.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the GetPropertyPackage operation, are not suitable.</exception>
    [Description("method GetPropertyPackage")]
    [DispId(2)]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object GetPropertyPackage([In] string PackageName);
  }
}
