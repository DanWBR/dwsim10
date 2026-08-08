'    The Reaktoro functions DWSIM calls, over the library's flat C API.
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports System.Runtime.InteropServices
Imports System.Text

''' <summary>
''' Reaktoro's chemical equilibrium solver, bound to the flat C API of DanWBR/reaktoro
''' (<c>ReaktoroC/reaktoro_c.h</c>).
''' </summary>
''' <remarks>
''' DWSIM used to reach Reaktoro 1 through its Python package, over the CPython bridge. That pinned
''' the whole application to a Python between 3.7 and 3.9, because those were the versions the
''' Reaktoro 1 wheels were built for, and it put an interpreter and a global interpreter lock in the
''' middle of a flowsheet solver that is otherwise free of both.
'''
''' Reaktoro 2 removed the classes that path was written against - <c>ChemicalEditor</c> and
''' <c>EquilibriumProblem</c> are gone - so staying on version 1 was the price of staying on Python.
''' This is the other way out: the equilibrium solver of version 2, called directly.
'''
''' Reaktoro's own interface is C++ and does not cross a P/Invoke boundary; what this binds to is a
''' small C shim built alongside the library, exposing the calls DWSIM makes and nothing else.
''' </remarks>
Public NotInheritable Class Reaktoro

    Private Sub New()
    End Sub

    ''' <summary>
    ''' The shared library's base name: <c>ReaktoroC.dll</c>, <c>libReaktoroC.so</c> or
    ''' <c>libReaktoroC.dylib</c>, resolved by the runtime for the platform it is on.
    ''' </summary>
    Private Const Library As String = "ReaktoroC"

    ''' <summary>Large enough for the species list of any system DWSIM builds.</summary>
    Private Const BufferSize As Integer = 65536

    <DllImport(Library, EntryPoint:="reaktoro_version", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
    Private Shared Function NativeVersion(buffer As StringBuilder, size As Integer) As Integer
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_last_error", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
    Private Shared Function NativeLastError(buffer As StringBuilder, size As Integer) As Integer
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_create", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
    Private Shared Function NativeCreate(database As String, aqueous As String,
                                         gaseous As String, gaseousModel As String) As IntPtr
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_create_speciated", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
    Private Shared Function NativeCreateSpeciated(databaseKind As String, database As String, elements As String,
                                                  aqueous As Integer, gaseous As Integer,
                                                  liquid As Integer, mineral As Integer,
                                                  gaseousModel As String) As IntPtr
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_database_species", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
    Private Shared Function NativeDatabaseSpecies(databaseKind As String, database As String,
                                                  buffer As StringBuilder, size As Integer) As Integer
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_destroy", CallingConvention:=CallingConvention.Cdecl)>
    Private Shared Sub NativeDestroy(system As IntPtr)
    End Sub

    <DllImport(Library, EntryPoint:="reaktoro_species_count", CallingConvention:=CallingConvention.Cdecl)>
    Private Shared Function NativeSpeciesCount(system As IntPtr) As Integer
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_species_names", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
    Private Shared Function NativeSpeciesNames(system As IntPtr, buffer As StringBuilder, size As Integer) As Integer
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_equilibrate", CallingConvention:=CallingConvention.Cdecl, CharSet:=CharSet.Ansi)>
    Private Shared Function NativeEquilibrate(system As IntPtr, temperature As Double, pressure As Double,
                                              substances As String, amounts As Double(), amountsSize As Integer,
                                              speciesAmounts As Double(), lnActivityCoefficients As Double(),
                                              ByRef aqueousAmount As Double, ByRef gaseousAmount As Double) As Integer
    End Function

    <DllImport(Library, EntryPoint:="reaktoro_properties", CallingConvention:=CallingConvention.Cdecl)>
    Private Shared Function NativeProperties(system As IntPtr, temperature As Double, pressure As Double,
                                             speciesAmounts As Double(), speciesAmountsSize As Integer,
                                             lnActivityCoefficients As Double()) As Integer
    End Function

    ''' <summary>The version of Reaktoro behind the library.</summary>
    Public Shared Function Version() As String

        Return ReadString(Function(buffer, size) NativeVersion(buffer, size))

    End Function

    ''' <summary>
    ''' A chemical system, with the species of each phase named as the database names them. The
    ''' gaseous list may be empty. Dispose it when done: it owns unmanaged memory.
    ''' </summary>
    ''' <exception cref="ReaktoroException">The system could not be built.</exception>
    Public Shared Function CreateSystem(aqueousSpecies As String,
                                        gaseousSpecies As String,
                                        Optional database As String = "supcrt07-organics",
                                        Optional gaseousModel As String = "IdealGas") As ChemicalSystem

        Dim handle = NativeCreate(database, aqueousSpecies, If(gaseousSpecies, ""), gaseousModel)

        If handle = IntPtr.Zero Then Throw New ReaktoroException(LastError())

        Return New ChemicalSystem(handle)

    End Function

    ''' <summary>
    ''' A chemical system built from a list of chemical elements: every species the database carries
    ''' that can be made from them goes into the phases asked for. This is how the Gibbs reactor
    ''' poses its problem, where the property package names its species one by one.
    ''' </summary>
    ''' <param name="databaseKind">"supcrt", "phreeqc", "nasa", "thermofun" or "file".</param>
    ''' <param name="database">A database name, or a path when the kind is "file".</param>
    ''' <exception cref="ReaktoroException">The system could not be built.</exception>
    Public Shared Function CreateSpeciatedSystem(databaseKind As String, database As String,
                                                 elements As String,
                                                 aqueous As Boolean, gaseous As Boolean,
                                                 liquid As Boolean, mineral As Boolean,
                                                 Optional gaseousModel As String = "IdealGas") As ChemicalSystem

        Dim handle = NativeCreateSpeciated(databaseKind, database, elements,
                                           If(aqueous, 1, 0), If(gaseous, 1, 0),
                                           If(liquid, 1, 0), If(mineral, 1, 0), gaseousModel)

        If handle = IntPtr.Zero Then Throw New ReaktoroException(LastError())

        Return New ChemicalSystem(handle)

    End Function

    ''' <summary>Everything a database carries: the name, the formula and the aggregate state of each species.</summary>
    ''' <exception cref="ReaktoroException">The database could not be opened.</exception>
    Public Shared Function ListSpecies(databaseKind As String, database As String) As List(Of DatabaseSpecies)

        Dim text = ReadString(Function(buffer, size) NativeDatabaseSpecies(databaseKind, database, buffer, size))

        If text = "" Then Throw New ReaktoroException(LastError())

        Dim species As New List(Of DatabaseSpecies)

        For Each line In text.Split({vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
            Dim parts = line.Split("|"c)
            If parts.Length = 3 Then
                species.Add(New DatabaseSpecies With {.Name = parts(0), .Formula = parts(1), .State = parts(2)})
            End If
        Next

        Return species

    End Function

    ''' <summary>One species of a database, as the database describes it.</summary>
    Public Structure DatabaseSpecies

        Public Name As String

        Public Formula As String

        ''' <summary>"aqueous", "gas", "liquid", "solid", or whatever else Reaktoro reports.</summary>
        Public State As String

    End Structure

    ''' <summary>
    ''' A chemical system built by <see cref="CreateSystem"/>, and the two calculations that can be
    ''' made on it.
    ''' </summary>
    Public NotInheritable Class ChemicalSystem
        Implements IDisposable

        Private handle As IntPtr

        Friend Sub New(handle As IntPtr)

            Me.handle = handle

            SpeciesCount = NativeSpeciesCount(handle)

            Dim joined = ReadString(Function(buffer, size) NativeSpeciesNames(Me.handle, buffer, size))

            SpeciesNames = If(joined = "", New String() {}, joined.Split(";"c))

        End Sub

        ''' <summary>
        ''' How many species the system holds. Every array in and out of the calculations below has
        ''' this length, in this order.
        ''' </summary>
        Public ReadOnly Property SpeciesCount As Integer

        ''' <summary>The species names, in that order.</summary>
        Public ReadOnly Property SpeciesNames As String()

        ''' <summary>
        ''' The equilibrium state at T kelvin and P pascal, from substances added by name or by
        ''' chemical formula. Returns the species amounts in moles, their natural-log activity
        ''' coefficients, and the amount in the aqueous and the gaseous phase.
        ''' </summary>
        ''' <exception cref="ReaktoroException">The equilibrium could not be computed.</exception>
        Public Function Equilibrate(T As Double, P As Double,
                                    substances As String(), amounts As Double()) As EquilibriumResult

            Dim result As New EquilibriumResult With {
                .SpeciesAmounts = New Double(SpeciesCount - 1) {},
                .LnActivityCoefficients = New Double(SpeciesCount - 1) {}
            }

            Dim status = NativeEquilibrate(handle, T, P, String.Join(";", substances), amounts, amounts.Length,
                                           result.SpeciesAmounts, result.LnActivityCoefficients,
                                           result.AqueousAmount, result.GaseousAmount)

            If status <> 0 Then Throw New ReaktoroException(LastError())

            Return result

        End Function

        ''' <summary>
        ''' The natural-log activity coefficients at a composition that is given rather than solved
        ''' for: one amount per species, in species order.
        ''' </summary>
        ''' <exception cref="ReaktoroException">The properties could not be evaluated.</exception>
        Public Function LnActivityCoefficients(T As Double, P As Double, speciesAmounts As Double()) As Double()

            Dim values = New Double(SpeciesCount - 1) {}

            Dim status = NativeProperties(handle, T, P, speciesAmounts, speciesAmounts.Length, values)

            If status <> 0 Then Throw New ReaktoroException(LastError())

            Return values

        End Function

        Public Sub Dispose() Implements IDisposable.Dispose

            If handle <> IntPtr.Zero Then
                NativeDestroy(handle)
                handle = IntPtr.Zero
            End If

            GC.SuppressFinalize(Me)

        End Sub

        Protected Overrides Sub Finalize()

            Dispose()

        End Sub

    End Class

    ''' <summary>What an equilibrium calculation gives back.</summary>
    Public Structure EquilibriumResult

        ''' <summary>The amount of each species, in moles, in species order.</summary>
        Public SpeciesAmounts As Double()

        ''' <summary>The natural log of each species' activity coefficient, in the same order.</summary>
        Public LnActivityCoefficients As Double()

        ''' <summary>The total amount in the aqueous phase, in moles.</summary>
        Public AqueousAmount As Double

        ''' <summary>The total amount in the gaseous phase, in moles. Zero where there is none.</summary>
        Public GaseousAmount As Double

    End Structure

    ''' <summary>
    ''' Why the last call failed. The C API reports a failure in its return value and leaves the
    ''' reason here, which is what turns into the message of the exception.
    ''' </summary>
    Private Shared Function LastError() As String

        Dim reason = ReadString(Function(buffer, size) NativeLastError(buffer, size))

        Return If(reason = "", "Reaktoro failed without saying why.", reason)

    End Function

    ''' <summary>
    ''' Calls one of the library's string functions, which write into a buffer and report the length
    ''' they need. One call is enough for anything DWSIM asks for; the second is there for the day
    ''' something does not fit.
    ''' </summary>
    Private Shared Function ReadString(reader As Func(Of StringBuilder, Integer, Integer)) As String

        Dim buffer As New StringBuilder(BufferSize)

        Dim needed = reader(buffer, BufferSize)

        If needed < 0 Then Return ""

        If needed >= BufferSize Then
            buffer = New StringBuilder(needed + 1)
            reader(buffer, needed + 1)
        End If

        Return buffer.ToString()

    End Function

End Class

''' <summary>Raised when Reaktoro cannot compute what it was asked for.</summary>
<Serializable>
Public Class ReaktoroException
    Inherits Exception

    Public Sub New()
    End Sub

    Public Sub New(message As String)
        MyBase.New(message)
    End Sub

    Public Sub New(message As String, inner As Exception)
        MyBase.New(message, inner)
    End Sub

End Class
