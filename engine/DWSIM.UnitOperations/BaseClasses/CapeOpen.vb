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

#If MOBILE Then
        ' CAPE-OPEN is a Windows COM standard and is unused on mobile. Re-implementing ICapeUtilities /
        ' ICapeUnit here (they are already implemented by the CapeObjectBase / CapeUnitBase bases) trips
        ' an ILLink IL1012 crash under AOT, so the re-implementation is dropped for MOBILE builds.
        Implements IPersistStreamInit
#Else
        Implements CapeOpen.ICapeUtilities, IPersistStreamInit, ICapeUnit
#End If

        Protected _sctxt As Object

        ''' <summary>
        ''' Sets the CAPE-OPEN simulation context object provided by the host simulator.
        ''' </summary>
#If MOBILE Then
        Public Shadows WriteOnly Property simulationContext As Object
#Else
        Public Shadows WriteOnly Property simulationContext As Object Implements ICapeUtilities.simulationContext
#End If
            Set(value As Object)
                _sctxt = value
            End Set
        End Property

        ''' <summary>
        ''' Initialises the unit operation within the host CAPE-OPEN simulator,
        ''' setting the UI culture and registering unhandled-exception handlers.
        ''' </summary>
#If MOBILE Then
        Public Overridable Shadows Sub Initialize()
#Else
        Public Overridable Shadows Sub Initialize() Implements ICapeUtilities.Initialize
#End If

            ' Cross-platform alternative to My.Application.ChangeUICulture (which is part of
            ' the Microsoft.VisualBasic.Forms runtime, Windows-only on .NET 5+).
            System.Threading.Thread.CurrentThread.CurrentUICulture = New System.Globalization.CultureInfo("en")

            'handler for unhandled exceptions

            InstallErrorHandler()

        End Sub

        ''' <summary>
        ''' Terminates the unit operation and releases the simulation context COM object.
        ''' </summary>
#If MOBILE Then
        Public Overridable Shadows Sub Terminate()
#Else
        Public Overridable Shadows Sub Terminate() Implements ICapeUtilities.Terminate
#End If

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
#If MOBILE Then
        Public MustOverride Shadows Sub Calculate()
#Else
        Public MustOverride Shadows Sub Calculate() Implements CapeOpen.ICapeUnit.Calculate
#End If

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
        ''' <summary>
        ''' Restores the parameter values from the block a host simulator saved.
        ''' </summary>
        ''' <remarks>
        ''' The payload is a length prefix and then UTF-8 XML, one element per parameter, in the
        ''' order the parameters were created. A block written by an older version is a
        ''' BinaryFormatter array instead, and is still read: unlike the property package next door,
        ''' this format did round-trip, so hosts have files with real state in them. That path only
        ''' works on the .NET Framework build, since BinaryFormatter is removed from .NET; on this
        ''' one it fails and says so rather than pretending the parameters were restored.
        ''' </remarks>
        Public Sub Load(ByVal pStm As System.Runtime.InteropServices.ComTypes.IStream) Implements IPersistStreamInit.Load

            CreateParameters()

            ' The stream belongs to the host that called this method: read from, never released here.
            Dim length As Integer = BitConverter.ToInt32(ReadExactly(pStm, 4), 0)

            If length < 0 Then Throw New IO.InvalidDataException(
                String.Format("The persisted block declares a length of {0} bytes.", length))

            Dim bytes As Byte() = ReadExactly(pStm, length)

            If LooksLikeXml(bytes) Then
                LoadFromXml(bytes)
            Else
                LoadFromLegacyBinary(bytes)
            End If

        End Sub

        ''' <summary>Whether a payload is the XML form rather than the old BinaryFormatter one.</summary>
        ''' <remarks>
        ''' A BinaryFormatter stream starts with its own header record and never with a bracket, so
        ''' the first character that is not whitespace or a byte order mark tells the two apart.
        ''' </remarks>
        Private Shared Function LooksLikeXml(bytes As Byte()) As Boolean

            For Each b In bytes
                Select Case b
                    Case Asc("<"c) : Return True
                    Case &H20, &H9, &HA, &HD, &HEF, &HBB, &HBF : Continue For
                    Case Else : Return False
                End Select
            Next

            Return False

        End Function

        Private Sub LoadFromXml(bytes As Byte())

            Dim root = XElement.Parse(Text.Encoding.UTF8.GetString(bytes).TrimStart(ChrW(&HFEFF)))

            For Each item In root.Elements("Parameter")

                Dim index As Integer

                If Not Integer.TryParse(item.Attribute("Index")?.Value, index) Then Continue For
                If index < 0 OrElse index >= Parameters.Count Then Continue For

                Dim value = item.Attribute("Value")?.Value

                If value Is Nothing Then Continue For

                Dim ci = Globalization.CultureInfo.InvariantCulture

                Try
                    If TypeOf Parameters(index) Is RealParameter Then
                        DirectCast(Parameters(index), RealParameter).SIValue = Double.Parse(value, ci)
                    ElseIf TypeOf Parameters(index) Is OptionParameter Then
                        DirectCast(Parameters(index), OptionParameter).Value = value
                    ElseIf TypeOf Parameters(index) Is BooleanParameter Then
                        DirectCast(Parameters(index), BooleanParameter).Value = Boolean.Parse(value)
                    ElseIf TypeOf Parameters(index) Is IntegerParameter Then
                        DirectCast(Parameters(index), IntegerParameter).Value = Integer.Parse(value, ci)
                    End If
                Catch ex As Exception
                    Console.WriteLine(String.Format("Parameter {0} could not be restored: {1}", index, ex.Message))
                End Try

            Next

        End Sub

        Private Sub LoadFromLegacyBinary(bytes As Byte())

            Dim domain As AppDomain = AppDomain.CurrentDomain

            Using memoryStream As New System.IO.MemoryStream(bytes)

                Try

                    AddHandler domain.AssemblyResolve, New ResolveEventHandler(AddressOf MyResolveEventHandler)

                    Dim mySerializer As Binary.BinaryFormatter = New Binary.BinaryFormatter(Nothing, New System.Runtime.Serialization.StreamingContext())

                    Dim myarr As ArrayList = mySerializer.Deserialize(memoryStream)

                    For i As Integer = 0 To Math.Min(myarr.Count, Parameters.Count) - 1
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

                Catch p_Ex As System.Exception

                    Console.WriteLine(p_Ex.ToString())

                Finally

                    ' In a Finally: the handler used to come off on the last line of the Try, so a
                    ' failure anywhere in the deserialization left it on the application domain.
                    RemoveHandler domain.AssemblyResolve, New ResolveEventHandler(AddressOf MyResolveEventHandler)

                End Try

            End Using

        End Sub

        ''' <summary>
        ''' Reads exactly <paramref name="count"/> bytes from a COM stream, however many calls it takes.
        ''' </summary>
        ''' <remarks>
        ''' IStream.Read may return fewer bytes than asked for and reports how many in its third
        ''' argument. This used to pass a null pointer there and assume the buffer had been filled, so
        ''' a stream that answered in pieces was deserialized with the tail still zeroed.
        ''' </remarks>
        Private Shared Function ReadExactly(stream As System.Runtime.InteropServices.ComTypes.IStream, count As Integer) As Byte()

            Dim buffer(Math.Max(count - 1, -1)) As Byte

            If count = 0 Then Return buffer

            Dim read As IntPtr = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(4)

            Try

                Dim total As Integer = 0

                While total < count

                    Dim chunk(count - total - 1) As Byte

                    stream.Read(chunk, chunk.Length, read)

                    Dim got As Integer = System.Runtime.InteropServices.Marshal.ReadInt32(read)

                    If got <= 0 Then
                        Throw New IO.EndOfStreamException(
                            String.Format("The stream ended after {0} of {1} bytes.", total, count))
                    End If

                    Array.Copy(chunk, 0, buffer, total, got)

                    total += got

                End While

            Finally

                System.Runtime.InteropServices.Marshal.FreeCoTaskMem(read)

            End Try

            Return buffer

        End Function

        ''' <summary>
        ''' Serialises the unit operation parameter values to a COM stream.
        ''' </summary>
        ''' <param name="pStm">The COM stream to write the state into.</param>
        ''' <param name="fClearDirty">When <c>True</c>, clears the dirty flag after a successful save.</param>
        ''' <summary>
        ''' Writes the parameter values into the stream the host supplied, as a length prefix and
        ''' UTF-8 XML, one element per parameter.
        ''' </summary>
        Public Sub Save(ByVal pStm As System.Runtime.InteropServices.ComTypes.IStream, ByVal fClearDirty As Boolean) Implements IPersistStreamInit.Save

            Dim ci = Globalization.CultureInfo.InvariantCulture

            Dim root As New XElement("UnitOperationPersistedState", New XAttribute("Version", "2"))

            For i As Integer = 0 To Parameters.Count - 1

                Dim kind As String = Nothing
                Dim value As String = Nothing

                If TypeOf Parameters(i) Is RealParameter Then
                    kind = "Real"
                    value = DirectCast(Parameters(i), RealParameter).SIValue.ToString(ci)
                ElseIf TypeOf Parameters(i) Is OptionParameter Then
                    kind = "Option"
                    value = DirectCast(Parameters(i), OptionParameter).Value
                ElseIf TypeOf Parameters(i) Is BooleanParameter Then
                    kind = "Boolean"
                    value = DirectCast(Parameters(i), BooleanParameter).Value.ToString()
                ElseIf TypeOf Parameters(i) Is IntegerParameter Then
                    kind = "Integer"
                    value = DirectCast(Parameters(i), IntegerParameter).Value.ToString(ci)
                End If

                If kind IsNot Nothing Then
                    root.Add(New XElement("Parameter", New XAttribute("Index", i),
                                          New XAttribute("Type", kind),
                                          New XAttribute("Value", If(value, ""))))
                End If

            Next

            Dim bytes = Text.Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting))

            Try

                ' The stream belongs to the host that called this method: written to, not released here.
                pStm.Write(BitConverter.GetBytes(bytes.Length), 4, IntPtr.Zero)
                pStm.Write(bytes, bytes.Length, IntPtr.Zero)

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
