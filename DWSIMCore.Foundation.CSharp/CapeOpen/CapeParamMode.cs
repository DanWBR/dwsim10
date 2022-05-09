// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapeParamMode
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;

namespace CapeOpen
{
  /// <summary>Modes of parameters.</summary>
  /// <remarks>
  /// <para> It allows the following values:</para>
  /// <para>   (i)   Input (CAPE_INPUT): the Unit(or whichever owner component)</para>
  /// <para>         will use its value to calculate.</para>
  /// <para>   (ii)  Output (CAPE_OUTPUT): the Unit will place in the parameter</para>
  /// <para>         a result of its calculations.</para>
  /// <para>   (iii) Input-Output (CAPE_INPUT_OUTPUT): the user inputs an initial</para>
  /// <para>         estimation value and the user outputs a calculated value.</para>
  /// Reference document: Parameter Common Interface
  /// </remarks>
  [Serializable]
  public enum CapeParamMode
  {
    /// <summary>
    /// The Unit(or whichever owner component) will use the parameter's value as an
    /// input to its calculation.
    /// </summary>
    CAPE_INPUT,
    /// <summary>
    /// The Unit(or whichever owner component) will set the parameter's value as
    /// an output to its calculation.
    /// </summary>
    CAPE_OUTPUT,
    /// <summary>
    /// The Unit(or whichever owner component) will use the parameter's initial value as
    /// an estimate and will calculate the final value.
    /// </summary>
    CAPE_INPUT_OUTPUT,
  }
}
