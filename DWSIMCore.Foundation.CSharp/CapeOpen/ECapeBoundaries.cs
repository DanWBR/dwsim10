// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeBoundaries
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface provides information
  /// about error that result from values that are outside of their bounds. It can be raised
  /// to indicate that the value of either a method argument or the value of a object
  /// parameter is out of range.
  /// </summary>
  /// <remarks>
  /// <para>ECapeBoundaries is a "utility" interface which factorises a state which describes the value, its type and its boundaries.</para>
  /// </remarks>
  [Guid("678c0b29-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeBoundaries Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapeBoundaries
  {
    /// <summary>The value of the lower bound.</summary>
    /// <remarks>
    /// <para>This provides the user with the acceptable lower bounds of the argument.</para>
    /// </remarks>
    /// <value>The lower bound for the argument.</value>
    [Description("The value of the lower bound.")]
    [DispId(1)]
    double lowerBound { get; }

    /// <summary>The value of the upper bound.</summary>
    /// <remarks>
    /// <para>This provides the user with the acceptable upper bounds of the argument.</para>
    /// </remarks>
    /// <value>The upper bound for the argument.</value>
    [DispId(2)]
    [Description("The value of the upper bound.")]
    double upperBound { get; }

    /// <summary>The current value which has led to an error.</summary>
    /// <remarks>
    /// <para>This provides the user with the value that caused the error condition.</para>
    /// </remarks>
    /// <value>The value that resulted in the error condition.</value>
    [Description("The current value which has led to an error..")]
    [DispId(3)]
    double value { get; }

    /// <summary>The type/nature of the value.</summary>
    /// <remarks>
    /// The value could represent a thermodynamic property, a number of tables in a database, a quantity of memory, ..."
    /// </remarks>
    /// <value>A string that indicates the anture or type of the value required.</value>
    [Description("The type/nature of the value. The value could represent a thermodynamic property, a number of tables in a database, a quantity of memory, ...")]
    [DispId(4)]
    string type { get; }
  }
}
