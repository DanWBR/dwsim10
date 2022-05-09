// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapeValidationStatus
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Enumeration flag to indicate parameter validation status.
  /// </summary>
  /// <remarks>
  /// <para>The enumeration has the following meanings:</para>
  /// <para>(i)   notValidated(CAPE_NOT_VALIDATED): The PMC's Validate()
  /// method has not been called after the last time that its value had been changed.</para>
  /// <para>(ii)  invalid(CAPE_INVALID): The last time that the PMC's Validate()
  /// method was called it returned false.</para>
  /// <para>(iii) valid(CAPE_VALID): the last time that the PMC's Validate() method
  /// was called it returned true.</para>
  /// </remarks>
  [Guid("678c0b04-7d66-11d2-a67d-00105a42887f")]
  [ComVisible(true)]
  [Serializable]
  public enum CapeValidationStatus
  {
    /// <summary>
    /// The PMC's Validate() method has not been called after the last time that its value had been changed.
    /// </summary>
    CAPE_NOT_VALIDATED,
    /// <summary>
    /// The last time that the PMC's Validate() method was called it returned false.
    /// </summary>
    CAPE_INVALID,
    /// <summary>
    /// The last time that the PMC's Validate() method was called it returned true.
    /// </summary>
    CAPE_VALID,
  }
}
