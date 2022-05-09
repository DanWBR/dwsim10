// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapePersistenceNotFound
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// An exception that indicates that the persistence was not found.
  /// </summary>
  /// <remarks>
  /// The requested object, table, or something else within the persistence system was not found.
  /// </remarks>
  [Description("ECapePersistenceNotFound Interface")]
  [Guid("678c0b26-7d66-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapePersistenceNotFound
  {
    /// <summary>The name of the item.</summary>
    /// <remarks>
    /// The name of the requested object, table, or something else within the persistence system
    /// that was not found.
    /// </remarks>
    /// <value>The name of the item not found.</value>
    [DispId(1)]
    [Description("The name of the item")]
    string itemName { get; }
  }
}
