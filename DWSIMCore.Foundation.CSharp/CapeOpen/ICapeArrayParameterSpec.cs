// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeArrayParameterSpec
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface is for a parameter specification
  /// when the parameter is an array of values (maybebe integers,reals,
  /// booleans or arrays again, which represents.
  /// </summary>
  [Guid("678c09a9-0093-11d2-a67d-00105a42887f")]
  [Description("ICapeArrayParameterSpec Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeArrayParameterSpec
  {
    /// <summary>Get the number of dimensions of the array.</summary>
    /// <remarks>The number of dimensions of the paramater array.</remarks>
    /// <value>The number of dimensions of the array.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(1)]
    [Description("Get the number of dimensions of the array")]
    int NumDimensions { get; }

    /// <summary>
    /// Gets the size of each one of the dimensions of the array.
    /// </summary>
    /// <remarks>
    /// An array containing the specfication of each member of the paramater array.
    /// </remarks>
    /// <returns>The size of each dimension of the array.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Get the size of each one of the dimensions of the array")]
    [DispId(2)]
    object Size { get; }

    /// <summary>
    /// Gets an array of the specifications of each of the items in the
    /// value of a parameter.
    /// </summary>
    /// <remarks>
    /// ﻿An array of interfaces to the correct specification type (<see cref="T:CapeOpen.ICapeRealParameterSpec" /> ,
    /// <see cref="T:CapeOpen.ICapeIntegerParameterSpec" /> , <see cref="T:CapeOpen.ICapeBooleanParameterSpec" /> ,
    /// <see cref="T:CapeOpen.ICapeOptionParameterSpec" /> ). Note that it is also possible, for
    /// example, to configure an array of arrays of integers, which would a similar
    /// but not identical concept to a two-dimensional matrix of integers.
    /// </remarks>
    /// <returns>
    /// An array of <see cref="T:CapeOpen.ICapeParameterSpec" /> objects.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(3)]
    [Description("Get the specification of each of the values in the array")]
    object ItemsSpecifications { get; }

    /// <summary>
    /// Validates the value against the specification of the parameter.
    /// The message is used to return the reason that the parameter is invalid.
    /// </summary>
    /// <remarks>
    /// This method checks the current value of the parameter to determine if it is an allowed value.
    /// </remarks>
    /// <returns>True if the parameter is valid, false if not valid.</returns>
    /// <param name="inputArray">The message is used to return the reason that the parameter is invalid.</param>
    /// <param name="value">A string array containing the message is used to return the reason that the parameter is invalid.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Check if value is OK for this spec ")]
    [DispId(4)]
    object Validate(object inputArray, ref object value);
  }
}
