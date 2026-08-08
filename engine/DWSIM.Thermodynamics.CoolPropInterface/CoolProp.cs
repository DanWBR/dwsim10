//    The CoolProp functions the engine calls, over the library's flat C API.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// The CoolProp functions DWSIM calls, bound to the library's flat C API
/// (<c>include/CoolProp/CoolPropLib.h</c>).
/// </summary>
/// <remarks>
/// This replaces the SWIG-generated C# wrapper, which needed a CoolProp built with
/// <c>-DCOOLPROP_CSHARP_MODULE=ON</c> and therefore existed only for one architecture. The flat C
/// API is what the library exports on every platform it builds for, so the same managed code now
/// works on all six runtimes DWSIM ships.
///
/// The class name and the method signatures are the ones the SWIG wrapper presented, so no call
/// site changed. What is gone is the rest of that wrapper's surface, which nothing in DWSIM used:
/// the <c>AbstractState</c> object model and the enum and vector types around it. An add-on or a
/// user script that reached for those needs the equations of state through <see cref="PropsSI"/>
/// instead.
///
/// The flat API reports a failure by returning infinity and leaving the reason in a global error
/// string, where the SWIG wrapper threw. Every caller in the engine is written around the
/// exception - sixty-two try blocks in the CoolProp property package alone, most of them falling
/// back to an estimate - so the failure is turned back into one here. A silent infinity would be
/// read as a property value.
/// </remarks>
public static class CoolProp
{
    /// <summary>
    /// The shared library's base name: <c>CoolProp.dll</c>, <c>libCoolProp.so</c> or
    /// <c>libCoolProp.dylib</c>, resolved by the runtime for the platform it is on.
    /// </summary>
    private const string Library = "CoolProp";

    /// <summary>Large enough for the longest parameter string CoolProp returns, the fluids list.</summary>
    private const int BufferSize = 65536;

    [DllImport(Library, EntryPoint = "PropsSI", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern double NativePropsSI(string Output, string Name1, double Prop1,
                                               string Name2, double Prop2, string FluidName);

    [DllImport(Library, EntryPoint = "Props1SI", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern double NativeProps1SI(string FluidName, string Output);

    [DllImport(Library, EntryPoint = "set_debug_level", CallingConvention = CallingConvention.Cdecl)]
    private static extern void NativeSetDebugLevel(int level);

    [DllImport(Library, EntryPoint = "get_global_param_string", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int NativeGetGlobalParamString(string param, StringBuilder Output, int n);

    [DllImport(Library, EntryPoint = "get_fluid_param_string", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int NativeGetFluidParamString(string fluid, string param, StringBuilder Output, int n);

    /// <summary>
    /// A thermodynamic property in SI units, from two others and the fluid name. The workhorse:
    /// every CoolProp property package in DWSIM is built on it.
    /// </summary>
    /// <exception cref="CoolPropException">The state could not be evaluated.</exception>
    public static double PropsSI(string Output, string Name1, double Prop1,
                                 string Name2, double Prop2, string FluidName)
    {
        return Checked(NativePropsSI(Output, Name1, Prop1, Name2, Prop2, FluidName),
                       "PropsSI(" + Output + ", " + Name1 + ", " + Name2 + ", " + FluidName + ")");
    }

    /// <summary>A property that needs no state, such as a critical constant or the molar mass.</summary>
    /// <exception cref="CoolPropException">The property could not be evaluated.</exception>
    public static double Props1SI(string FluidName, string Output)
    {
        return Checked(NativeProps1SI(FluidName, Output),
                       "Props1SI(" + FluidName + ", " + Output + ")");
    }

    /// <summary>Sets how much the library writes to its own log. Zero is silent.</summary>
    public static void set_debug_level(int level)
    {
        NativeSetDebugLevel(level);
    }

    /// <summary>A global parameter of the library, such as its version or the list of fluids.</summary>
    public static string get_global_param_string(string ParamName)
    {
        var buffer = new StringBuilder(BufferSize);

        if (NativeGetGlobalParamString(ParamName, buffer, BufferSize) != 1)
        {
            throw new CoolPropException("CoolProp has no global parameter named '" + ParamName + "'.");
        }

        return buffer.ToString();
    }

    /// <summary>A parameter of one fluid, such as its CAS number or its aliases.</summary>
    public static string get_fluid_param_string(string FluidName, string ParamName)
    {
        var buffer = new StringBuilder(BufferSize);

        if (NativeGetFluidParamString(FluidName, ParamName, buffer, BufferSize) != 1)
        {
            throw new CoolPropException(
                "CoolProp has no parameter named '" + ParamName + "' for fluid '" + FluidName + "'.");
        }

        return buffer.ToString();
    }

    /// <summary>
    /// Turns the flat API's out-of-band failure back into the exception the callers expect. The
    /// library returns HUGE_VAL and records why; reading the reason also clears it for the next
    /// call.
    /// </summary>
    private static double Checked(double value, string what)
    {
        if (!double.IsNaN(value) && !double.IsInfinity(value)) return value;

        var reason = LastError();

        throw new CoolPropException(reason.Length > 0 ? reason : what + " could not be evaluated.");
    }

    private static string LastError()
    {
        try
        {
            var buffer = new StringBuilder(BufferSize);

            if (NativeGetGlobalParamString("errstring", buffer, BufferSize) == 1) return buffer.ToString();
        }
        catch (Exception)
        {
        }

        return "";
    }
}

/// <summary>Raised when CoolProp cannot evaluate what it was asked for.</summary>
[Serializable]
public class CoolPropException : Exception
{
    public CoolPropException() { }

    public CoolPropException(string message) : base(message) { }

    public CoolPropException(string message, Exception inner) : base(message, inner) { }
}
