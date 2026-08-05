'    CAPE-OPEN Unit Operation Base Class
'    Copyright 2016 Daniel Wagner O. de Medeiros
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
Imports CapeOpen
Imports DWSIM.Interfaces.Interfaces2
Imports DWSIM.Thermodynamics
Imports System.Runtime.Serialization.Formatters
Imports System.IO

''' <summary>
''' Provides CAPE-OPEN compliant wrappers that allow DWSIM unit operations to be used inside
''' third-party process simulators that support the CAPE-OPEN standard.
''' </summary>
Namespace UnitOperations.CAPEOPENWrappers

    ''' <summary>
    ''' Abstract base class for CAPE-OPEN unit operation wrappers in DWSIM.
    ''' Implements CAPE-OPEN utilities and stream persistence interfaces so that
    ''' derived classes can be hosted inside third-party CAPE-OPEN-compliant simulators.
    ''' </summary>
    <System.Serializable()> <ComVisible(True)> Partial Public MustInherit Class CapeOpenBase

        Inherits CapeOpen.CapeUnitBase

        Implements CapeOpen.ICapeUtilities, IPersistStreamInit, ICapeUnit

        Protected _sctxt As Object

        ''' <summary>
        ''' Sets the CAPE-OPEN simulation context object provided by the host simulator.
        ''' </summary>
        Public Shadows WriteOnly Property simulationContext As Object Implements ICapeUtilities.simulationContext
            Set(value As Object)
                _sctxt = value
            End Set
        End Property

        ''' <summary>
        ''' Initialises the unit operation within the host CAPE-OPEN simulator,
        ''' setting the UI culture and registering unhandled-exception handlers.
        ''' </summary>
        Public Overridable Shadows Sub Initialize() Implements ICapeUtilities.Initialize

            ' Cross-platform alternative to My.Application.ChangeUICulture (which is part of
            ' the Microsoft.VisualBasic.Forms runtime, Windows-only on .NET 5+).
            System.Threading.Thread.CurrentThread.CurrentUICulture = New System.Globalization.CultureInfo("en")

            'handler for unhandled exceptions

            InstallErrorHandler()

        End Sub

        ''' <summary>
        ''' Terminates the unit operation and releases the simulation context COM object.
        ''' </summary>
        Public Overridable Shadows Sub Terminate() Implements ICapeUtilities.Terminate

            If Not _sctxt Is Nothing Then
                If System.Runtime.InteropServices.Marshal.IsComObject(_sctxt) Then
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(_sctxt)
                End If
            End If

            Me.simulationContext = Nothing

        End Sub

        ''' <summary>
        ''' Called by the base CAPE-OPEN unit when a calculation is triggered. Override in derived classes to perform the calculation.
        ''' </summary>
        Public Overrides Sub OnCalculate()

        End Sub

        ''' <summary>
        ''' Performs the unit operation calculation. Must be implemented by derived classes.
        ''' </summary>
        Public MustOverride Shadows Sub Calculate() Implements CapeOpen.ICapeUnit.Calculate

        ''' <summary>
        ''' Creates and registers the CAPE-OPEN parameter collection for this unit operation. Must be implemented by derived classes.
        ''' </summary>
        Public MustOverride Sub CreateParameters()

        ''' <summary>
        ''' Installs the window that shows an exception nobody caught. Does nothing where the
        ''' WinForms editors are not built.
        ''' </summary>
        Partial Private Sub InstallErrorHandler()
        End Sub

 
#Region "   CAPE-OPEN Persistence Implementation"

        Protected m_dirty As Boolean = True

        ''' <summary>
        ''' Returns the CLSID of the persisted class to the caller.
        ''' </summary>
        ''' <param name="pClassID">Receives the class GUID of this unit operation.</param>
        Public Sub GetClassID(ByRef pClassID As System.Guid) Implements IPersistStreamInit.GetClassID
            pClassID = New Guid(CO_CustomUO.ClassId)
        End Sub

        ''' <summary>
        ''' Returns the maximum byte size of the serialised state stream.
        ''' </summary>
        ''' <param name="pcbSize">Receives the maximum stream size in bytes.</param>
        Public Sub GetSizeMax(ByRef pcbSize As Long) Implements IPersistStreamInit.GetSizeMax
            pcbSize = 1024 * 1024
        End Sub

        ''' <summary>
        ''' Initialises the unit operation to a default state when no previously saved data is available.
        ''' </summary>
        Public Overridable Sub InitNew() Implements IPersistStreamInit.InitNew

        End Sub

        ''' <summary>
        ''' Returns whether the unit operation state has changed since the last save.
        ''' </summary>
        ''' <returns>Non-zero if the state is dirty (unsaved changes exist); zero otherwise.</returns>
        Public Function IsDirty() As Integer Implements IPersistStreamInit.IsDirty
            Return m_dirty
        End Function

        ''' <summary>
        ''' Deserialises the unit operation parameter values from a COM stream.
        ''' </summary>
        ''' <param name="pStm">The COM stream from which to read the saved state.</param>
        Public Sub Load(ByVal pStm As System.Runtime.InteropServices.ComTypes.IStream) Implements IPersistStreamInit.Load

            CreateParameters()

            ' Read the length of the string  
            Dim arrLen As Byte() = New [Byte](3) {}
            pStm.Read(arrLen, arrLen.Length, IntPtr.Zero)

            ' Calculate the length  
            Dim cb As Integer = BitConverter.ToInt32(arrLen, 0)

            ' Read the stream to get the string    
            Dim bytes As Byte() = New Byte(cb - 1) {}
            Dim pcb As New IntPtr()
            pStm.Read(bytes, bytes.Length, pcb)
            If System.Runtime.InteropServices.Marshal.IsComObject(pStm) Then System.Runtime.InteropServices.Marshal.ReleaseComObject(pStm)

            ' Deserialize byte array    

            Dim memoryStream As New System.IO.MemoryStream(bytes)

            Try

                Dim domain As AppDomain = AppDomain.CurrentDomain
                AddHandler domain.AssemblyResolve, New ResolveEventHandler(AddressOf MyResolveEventHandler)

                Dim myarr As ArrayList

                Dim mySerializer As Binary.BinaryFormatter = New Binary.BinaryFormatter(Nothing, New System.Runtime.Serialization.StreamingContext())
                myarr = mySerializer.Deserialize(memoryStream)

                For i As Integer = 0 To myarr.Count - 1
                    If TypeOf Parameters(i) Is RealParameter Then
                        DirectCast(Parameters(i), RealParameter).SIValue = myarr(i)
                    ElseIf TypeOf Parameters(i) Is OptionParameter Then
                        DirectCast(Parameters(i), OptionParameter).Value = myarr(i)
                    ElseIf TypeOf Parameters(i) Is BooleanParameter Then
                        DirectCast(Parameters(i), BooleanParameter).Value = myarr(i)
                    ElseIf TypeOf Parameters(i) Is IntegerParameter Then
                        DirectCast(Parameters(i), IntegerParameter).Value = myarr(i)
                    End If
                Next

                myarr = Nothing
                mySerializer = Nothing

                RemoveHandler domain.AssemblyResolve, New ResolveEventHandler(AddressOf MyResolveEventHandler)

            Catch p_Ex As System.Exception

                Console.WriteLine(p_Ex.ToString())

            End Try

            memoryStream.Close()

        End Sub

        ''' <summary>
        ''' Serialises the unit operation parameter values to a COM stream.
        ''' </summary>
        ''' <param name="pStm">The COM stream to write the state into.</param>
        ''' <param name="fClearDirty">When <c>True</c>, clears the dirty flag after a successful save.</param>
        Public Sub Save(ByVal pStm As System.Runtime.InteropServices.ComTypes.IStream, ByVal fClearDirty As Boolean) Implements IPersistStreamInit.Save

            Dim props As New ArrayList

            With props

                For i As Integer = 0 To Parameters.Count - 1
                    If TypeOf Parameters(i) Is RealParameter Then
                        props.Add(DirectCast(Parameters(i), RealParameter).SIValue)
                    ElseIf TypeOf Parameters(i) Is OptionParameter Then
                        props.Add(DirectCast(Parameters(i), OptionParameter).Value)
                    ElseIf TypeOf Parameters(i) Is BooleanParameter Then
                        props.Add(DirectCast(Parameters(i), BooleanParameter).Value)
                    ElseIf TypeOf Parameters(i) Is IntegerParameter Then
                        props.Add(DirectCast(Parameters(i), IntegerParameter).Value)
                    End If
                Next

            End With

            Dim mySerializer As Binary.BinaryFormatter = New Binary.BinaryFormatter(Nothing, New System.Runtime.Serialization.StreamingContext())
            Dim mstr As New MemoryStream
            mySerializer.Serialize(mstr, props)
            Dim bytes As Byte() = mstr.ToArray()
            mstr.Close()

            ' construct length (separate into two separate bytes)    

            Dim arrLen As Byte() = BitConverter.GetBytes(bytes.Length)
            Try

                ' Save the array in the stream    
                pStm.Write(arrLen, arrLen.Length, IntPtr.Zero)
                pStm.Write(bytes, bytes.Length, IntPtr.Zero)
                If System.Runtime.InteropServices.Marshal.IsComObject(pStm) Then System.Runtime.InteropServices.Marshal.ReleaseComObject(pStm)

            Catch p_Ex As System.Exception

                Console.WriteLine(p_Ex.ToString())

            End Try

            If fClearDirty Then
                m_dirty = False
            End If

        End Sub

        Protected Function MyResolveEventHandler(ByVal sender As Object, ByVal args As ResolveEventArgs) As System.Reflection.Assembly
            Return Me.[GetType]().Assembly
        End Function

#End Region

    End Class

End Namespace
