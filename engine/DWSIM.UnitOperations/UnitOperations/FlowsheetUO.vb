'    Flowsheet Unit Operation
'    Copyright 2015 Daniel Wagner O. de Medeiros
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


Imports System.Globalization
Imports System.Linq
Imports System.Xml.Linq
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.DrawingTools
Imports DWSIM.Thermodynamics.BaseClasses
Imports System.IO
Imports System.Threading.Tasks
Imports DWSIM.SharedClasses.Flowsheet
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.SharedClasses.UnitOperations
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts
Imports System.Dynamic
Imports System.Reflection
Imports System.Threading

Namespace UnitOperations.Auxiliary

    ''' <summary>Represents a single input or output parameter mapping for the Flowsheet unit operation.</summary>
    <System.Serializable()> Public Class FlowsheetUOParameter
        Implements Interfaces.ICustomXMLSerialization
        ''' <summary>Gets or sets the parameter unique identifier.</summary>
        Public Property ID As String = ""
        ''' <summary>Gets or sets the mapped simulation object ID in the sub-flowsheet.</summary>
        Public Property ObjectID As String = ""
        ''' <summary>Gets or sets the mapped property name on the simulation object.</summary>
        Public Property ObjectProperty As String = ""
        ''' <summary>Restores the parameter from XML.</summary>
        Public Function LoadData(data As List(Of XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData
            XMLSerializer.XMLSerializer.Deserialize(Me, data)
            Return True
        End Function
        ''' <summary>Serializes the parameter to XML.</summary>
        Public Function SaveData() As List(Of XElement) Implements Interfaces.ICustomXMLSerialization.SaveData
            Return XMLSerializer.XMLSerializer.Serialize(Me)
        End Function
    End Class

    ''' <summary>Defines how mass is transferred between the parent and sub-flowsheet streams.</summary>
    Public Enum FlowsheetUOMassTransferMode
        ''' <summary>Transfer using individual compound mass flows.</summary>
        CompoundMassFlows = 0
        ''' <summary>Transfer using individual compound molar flows.</summary>
        CompoundMoleFlows = 1
        ''' <summary>Transfer using compound mass fractions with total flow.</summary>
        CompoundMassFractions = 2
        ''' <summary>Transfer using compound mole fractions with total flow.</summary>
        CompoundMoleFractions = 3
    End Enum

End Namespace

Namespace UnitOperations
    ''' <summary>
    ''' Represents a Flowsheet Unit Operation that embeds a complete sub-flowsheet as a single
    ''' block in the parent flowsheet. Input and output streams are mapped to corresponding
    ''' streams in the sub-flowsheet, enabling modular process simulation.
    ''' </summary>
    <System.Serializable()> Public Partial Class Flowsheet

        Inherits UnitOperations.UnitOpBaseClass

        ''' <summary>Gets or sets the simulation object class category (UserModels).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.UserModels

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets or sets the file path to the sub-flowsheet simulation file.</summary>
        Public Property SimulationFile As String = ""
        <System.Xml.Serialization.XmlIgnore> Public Property Initialized As Boolean = False
        ''' <summary>Gets or sets whether the sub-flowsheet is initialized when the parent flowsheet loads.</summary>
        Public Property InitializeOnLoad As Boolean = False
        ''' <summary>Gets or sets whether the sub-flowsheet file is updated when the parent flowsheet is saved.</summary>
        Public Property UpdateOnSave As Boolean = False
        ''' <summary>Gets or sets how compound flows are transferred between parent and sub-flowsheet streams.</summary>
        Public Property MassTransferMode As FlowsheetUOMassTransferMode = FlowsheetUOMassTransferMode.CompoundMassFlows
        ''' <summary>Gets or sets the dictionary of input parameter mappings, keyed by parameter ID.</summary>
        Public Property InputParams As Dictionary(Of String, FlowsheetUOParameter)
        ''' <summary>Gets or sets the dictionary of output parameter mappings, keyed by parameter ID.</summary>
        Public Property OutputParams As Dictionary(Of String, FlowsheetUOParameter)
        <System.Xml.Serialization.XmlIgnore> <System.NonSerialized> Public Fsheet As Interfaces.IFlowsheet

        ''' <summary>Why the sub-flowsheet did not load, kept so the failure can say so.</summary>
        <System.Xml.Serialization.XmlIgnore> <System.NonSerialized> Private InitializationError As Exception
        ''' <summary>Gets or sets the list of input stream connection IDs in the sub-flowsheet.</summary>
        Public Property InputConnections As List(Of String)
        ''' <summary>Gets or sets the list of output stream connection IDs in the sub-flowsheet.</summary>
        Public Property OutputConnections As List(Of String)
        ''' <summary>Gets or sets the mass balance error across the sub-flowsheet (fractional).</summary>
        Public Property MassBalanceError As Double = 0.0#
        ''' <summary>Gets or sets whether output from the sub-flowsheet solver is redirected to the parent flowsheet log.</summary>
        Public Property RedirectOutput As Boolean = False
        ''' <summary>Gets or sets the dictionary mapping parent-flowsheet compound names to sub-flowsheet compound names.</summary>
        Public Property CompoundMappings As Dictionary(Of String, String)

        ''' <summary>Gets or sets whether the sub-flowsheet file is embedded in the parent simulation file.</summary>
        Public Property FileIsEmbedded As Boolean = False
        ''' <summary>Gets or sets the embedded file name when the sub-flowsheet is embedded.</summary>
        Public Property EmbeddedFileName As String = ""

        ''' <summary>Initializes a new instance with a name and description.</summary>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

            CompoundMappings = New Dictionary(Of String, String)

            InputParams = New Dictionary(Of String, FlowsheetUOParameter)
            OutputParams = New Dictionary(Of String, FlowsheetUOParameter)

            InputConnections = New List(Of String) From {"", "", "", "", "", "", "", "", "", ""}
            OutputConnections = New List(Of String) From {"", "", "", "", "", "", "", "", "", ""}

        End Sub

        ''' <summary>Initializes a new default instance.</summary>
        Public Sub New()

            MyBase.CreateNew()

            CompoundMappings = New Dictionary(Of String, String)

            InputParams = New Dictionary(Of String, FlowsheetUOParameter)
            OutputParams = New Dictionary(Of String, FlowsheetUOParameter)

            InputConnections = New List(Of String) From {"", "", "", "", "", "", "", "", "", ""}
            OutputConnections = New List(Of String) From {"", "", "", "", "", "", "", "", "", ""}

        End Sub

        ''' <summary>Creates a deep copy via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Flowsheet()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Flowsheet)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Initialises compound mappings between parent and sub-flowsheet using name matching.</summary>
        Public Sub InitializeMappings()

            If CompoundMappings Is Nothing Then CompoundMappings = New Dictionary(Of String, String)

            'create mappings
            If CompoundMappings.Count = 0 Then
                For Each c In Me.FlowSheet.SelectedCompounds.Values
                    If Me.Fsheet.SelectedCompounds.ContainsKey(c.Name) Then CompoundMappings.Add(c.Name, c.Name) Else CompoundMappings.Add(c.Name, "")
                Next
            End If

            If CompoundMappings.Count <> Me.FlowSheet.SelectedCompounds.Count Then
                'update mappings
                For Each c In Me.FlowSheet.SelectedCompounds.Values
                    If Not CompoundMappings.ContainsKey(c.Name) Then
                        If Me.Fsheet.SelectedCompounds.ContainsKey(c.Name) Then CompoundMappings.Add(c.Name, c.Name) Else CompoundMappings.Add(c.Name, "")
                    End If
                Next
            End If

        End Sub

        ''' <summary>Resolves the sub-flowsheet file path relative to the parent flowsheet file.</summary>
        Public Sub ParseFilePath()

            If Not IO.File.Exists(SimulationFile) Then
                'look at the current simulation location to see if the file is there.
                Dim fname As String = IO.Path.GetFileName(SimulationFile)
                Dim currpath As String = IO.Path.GetDirectoryName(FlowSheet.FlowsheetOptions.FilePath)
                Dim newpath As String = IO.Path.Combine(currpath, fname)
                If IO.File.Exists(newpath) Then SimulationFile = newpath
            End If

        End Sub

        Shared Function ExtractXML(ByVal zippath As String) As String

            Dim pathtosave As String = IO.Path.GetTempPath().TrimEnd(IO.Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar
            Dim fullname As String = ""

            Using stream As ICSharpCode.SharpZipLib.Zip.ZipInputStream = New ICSharpCode.SharpZipLib.Zip.ZipInputStream(File.OpenRead(zippath))
                stream.Password = ""
                Dim entry As ICSharpCode.SharpZipLib.Zip.ZipEntry
Label_00CC:
                entry = stream.GetNextEntry()
                Do While (Not entry Is Nothing)
                    Dim fileName As String = Path.GetFileName(entry.Name)
                    If (fileName <> String.Empty) Then
                        Using stream2 As FileStream = File.Create(pathtosave + Path.GetFileName(entry.Name))
                            Dim count As Integer = 2048
                            Dim buffer As Byte() = New Byte(2048) {}
                            Do While True
                                count = stream.Read(buffer, 0, buffer.Length)
                                If (count <= 0) Then
                                    Dim extension = Path.GetExtension(entry.Name).ToLower()
                                    If extension = ".xml" Then
                                        fullname = pathtosave + Path.GetFileName(entry.Name)
                                    End If
                                    GoTo Label_00CC
                                End If
                                stream2.Write(buffer, 0, count)
                            Loop
                        End Using
                    End If
                    entry = stream.GetNextEntry
                Loop
            End Using
            Return fullname

        End Function

        ''' <summary>Creates and initializes a sub-flowsheet from the given file path.</summary>
        Public Shared Function InitializeFlowsheet(fpath As String, form As IFlowsheet, mainfs As IFlowsheet) As IFlowsheet
            If Path.GetExtension(fpath).ToLower.Equals(".dwxml") Then
                If GlobalSettings.Settings.OldUI Then
                    Return InitializeFlowsheetInternal(XDocument.Load(fpath), form, mainfs)
                Else
                    form.LoadFromXML(XDocument.Load(fpath))
                    Return form
                End If
            Else 'dwxmz
                If GlobalSettings.Settings.OldUI Then
                    Return InitializeFlowsheetInternal(XDocument.Load(ExtractXML(fpath)), form, mainfs)
                Else
                    form.LoadFromXML(XDocument.Load(ExtractXML(fpath)))
                    Return form
                End If
            End If
        End Function

        ''' <summary>Creates and initializes a sub-flowsheet from a compressed memory stream.</summary>
        Public Shared Function InitializeFlowsheet(compressedstream As MemoryStream, form As IFlowsheet, mainfs As IFlowsheet) As IFlowsheet
            Using decompressedstream As New IO.MemoryStream
                compressedstream.Position = 0
                Using gzs As New IO.BufferedStream(New Compression.GZipStream(compressedstream, Compression.CompressionMode.Decompress, True), 64 * 1024)
                    gzs.CopyTo(decompressedstream)
                    gzs.Close()
                    decompressedstream.Position = 0
                    Return InitializeFlowsheetInternal(XDocument.Load(decompressedstream), form, mainfs)
                End Using
            End Using
        End Function

        Private Shared Function InitializeFlowsheetInternal(xdoc As XDocument, fs As IFlowsheet, mainfs As IFlowsheet)

            Dim sver = New Version("1.0.0.0")

            Try
                sver = New Version(xdoc.Element("DWSIM_Simulation_Data").Element("GeneralInfo").Element("BuildVersion").Value)
            Catch ex As Exception
            End Try

            If sver < New Version("5.0.0.0") Then
                For Each xel1 In xdoc.Descendants
                    SharedClasses.Utility.UpdateElement(xel1)
                Next
            End If

            For Each xel1 In xdoc.Descendants
                SharedClasses.Utility.UpdateElementForNewUI(xel1)
            Next

            Dim ci As CultureInfo = CultureInfo.InvariantCulture

            Dim excs As New Concurrent.ConcurrentBag(Of Exception)

            Dim pp As New Thermodynamics.PropertyPackages.RaoultPropertyPackage()

            Dim data As List(Of XElement) = xdoc.Element("DWSIM_Simulation_Data").Element("GraphicObjects").Elements.ToList

            AddGraphicObjects(fs, mainfs, data, excs)

            data = xdoc.Element("DWSIM_Simulation_Data").Element("Compounds").Elements.ToList

            For Each xel As XElement In data
                Try
                    Dim obj As New ConstantProperties
                    obj.LoadData(xel.Elements.ToList)
                    fs.Options.SelectedComponents.Add(obj.Name, obj)
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Compound Information", ex))
                End Try
            Next

            data = xdoc.Element("DWSIM_Simulation_Data").Element("PropertyPackages").Elements.ToList

            For Each xel As XElement In data
                xel.Element("Type").Value = xel.Element("Type").Value.Replace("DWSIM.DWSIM.SimulationObjects", "DWSIM.Thermodynamics")
                Dim obj As PropertyPackage = Nothing
                Try
                    Dim ppkey As String = xel.Element("ComponentName").Value
                    If ppkey = "" Then
                        obj = CType(New RaoultPropertyPackage().ReturnInstance(xel.Element("Type").Value), PropertyPackage)
                    Else
                        Dim ptype = xel.Element("Type").Value
                        If ppkey.Contains("1978") And ptype.Contains("PengRobinsonPropertyPackage") Then
                            ptype = ptype.Replace("PengRobinson", "PengRobinson1978")
                        End If
                        If mainfs.AvailablePropertyPackages.ContainsKey(ppkey) Then
                            obj = mainfs.AvailablePropertyPackages(ppkey).ReturnInstance(ptype)
                        Else
                            Throw New Exception("The " & ppkey & " Property Package library was not found. Please download and install it in order to run this simulation.")
                        End If
                    End If
                    obj.Flowsheet = fs
                    obj.LoadData(xel.Elements.ToList)
                    Dim newID As String = Guid.NewGuid.ToString
                    If fs.PropertyPackages.ContainsKey(obj.UniqueID) Then obj.UniqueID = newID
                    fs.AddPropertyPackage(obj)
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Property Package Information", ex))
                End Try
            Next

            data = xdoc.Element("DWSIM_Simulation_Data").Element("SimulationObjects").Elements.ToList

            Dim objlist As New Concurrent.ConcurrentBag(Of SharedClasses.UnitOperations.BaseClass)

            For Each xel In data
                Try
                    Dim id As String = xel.<Name>.Value
                    Dim obj As SharedClasses.UnitOperations.BaseClass = Nothing
                    If xel.Element("Type").Value.Contains("Streams.MaterialStream") Then
                        obj = New Thermodynamics.Streams.MaterialStream()
                    Else
                        Dim uokey As String = xel.Element("ComponentDescription").Value
                        If mainfs.AvailableExternalUnitOperations.ContainsKey(uokey) Then
                            obj = mainfs.AvailableExternalUnitOperations(uokey).ReturnInstance(xel.Element("Type").Value)
                        Else
                            obj = Resolver.ReturnInstance(xel.Element("Type").Value)
                        End If
                    End If
                    Dim gobj As IGraphicObject = fs.GraphicObjects(id)
                    obj.GraphicObject = gobj
                    obj.GraphicObject.Owner = obj
                    obj.SetFlowsheet(fs)
                    If Not gobj Is Nothing Then
                        obj.LoadData(xel.Elements.ToList)
                        If TypeOf obj Is MaterialStream Then
                            For Each phase As BaseClasses.Phase In DirectCast(obj, MaterialStream).Phases.Values
                                For Each c As ICompoundConstantProperties In fs.SelectedCompounds.Values
                                    phase.Compounds(c.Name).ConstantProperties = c
                                Next
                            Next
                        End If
                    End If
                    objlist.Add(obj)
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Unit Operation Information", ex))
                End Try
            Next

            pp = Nothing

            For Each obj In objlist
                fs.AddSimulationObject(obj)
            Next

            For Each so In fs.SimulationObjects.Values
                Try
                    If TryCast(so, SpecialOps.Adjust) IsNot Nothing Then
                        Dim so2 As SpecialOps.Adjust = so
                        If fs.SimulationObjects.ContainsKey(so2.ManipulatedObjectData.ID) Then
                            so2.ManipulatedObject = fs.SimulationObjects(so2.ManipulatedObjectData.ID)
                            DirectCast(so2.GraphicObject, AdjustGraphic).ConnectedToMv = so2.ManipulatedObject.GraphicObject
                        End If
                        If fs.SimulationObjects.ContainsKey(so2.ControlledObjectData.ID) Then
                            so2.ControlledObject = fs.SimulationObjects(so2.ControlledObjectData.ID)
                            DirectCast(so2.GraphicObject, AdjustGraphic).ConnectedToCv = so2.ControlledObject.GraphicObject
                        End If
                        If fs.SimulationObjects.ContainsKey(so2.ReferencedObjectData.ID) Then
                            so2.ReferenceObject = fs.SimulationObjects(so2.ReferencedObjectData.ID)
                            DirectCast(so2.GraphicObject, AdjustGraphic).ConnectedToRv = so2.ReferenceObject.GraphicObject
                        End If
                    End If
                    If TryCast(so, SpecialOps.Spec) IsNot Nothing Then
                        Dim so2 As SpecialOps.Spec = so
                        If fs.SimulationObjects.ContainsKey(so2.TargetObjectData.ID) Then
                            so2.TargetObject = fs.SimulationObjects(so2.TargetObjectData.ID)
                            DirectCast(so2.GraphicObject, SpecGraphic).ConnectedToTv = so2.TargetObject.GraphicObject
                        End If
                        If fs.SimulationObjects.ContainsKey(so2.SourceObjectData.ID) Then
                            so2.SourceObject = fs.SimulationObjects(so2.SourceObjectData.ID)
                            DirectCast(so2.GraphicObject, SpecGraphic).ConnectedToSv = so2.SourceObject.GraphicObject
                        End If
                    End If
                    If TryCast(so, CapeOpenUO) IsNot Nothing Then
                        DirectCast(so, CapeOpenUO).UpdateConnectors2()
                        DirectCast(so, CapeOpenUO).UpdatePortsFromConnectors()
                    End If
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Unit Operation Connection Information", ex))
                End Try
            Next

            data = xdoc.Element("DWSIM_Simulation_Data").Element("ReactionSets").Elements.ToList

            fs.ReactionSets.Clear()
            For Each xel As XElement In data
                Try
                    Dim obj As New ReactionSet()
                    obj.LoadData(xel.Elements.ToList)
                    If Not fs.ReactionSets.ContainsKey(obj.ID) Then fs.ReactionSets.Add(obj.ID, obj)
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Reaction Set Information", ex))
                End Try
            Next

            data = xdoc.Element("DWSIM_Simulation_Data").Element("Reactions").Elements.ToList

            fs.Reactions.Clear()
            For Each xel As XElement In data
                Try
                    Dim obj As New Reaction()
                    obj.LoadData(xel.Elements.ToList)
                    fs.Reactions.Add(obj.ID, obj)
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Reaction Information", ex))
                End Try
            Next

            If xdoc.Element("DWSIM_Simulation_Data").Element("DynamicProperties") IsNot Nothing Then

                data = xdoc.Element("DWSIM_Simulation_Data").Element("DynamicProperties").Elements.ToList

                Try

                    fs.ExtraProperties = New ExpandoObject

                    If Not data Is Nothing Then
                        For Each xel As XElement In data
                            Try
                                Dim propname = xel.Element("Name").Value
                                Dim proptype = xel.Element("PropertyType").Value
                                Dim assembly1 As Assembly = Nothing
                                For Each assembly In AppDomain.CurrentDomain.GetAssemblies()
                                    If proptype.Contains(assembly.GetName().Name) Then
                                        assembly1 = assembly
                                        Exit For
                                    End If
                                Next
                                If assembly1 IsNot Nothing Then
                                    Dim ptype As Type = assembly1.GetType(proptype)
                                    Dim propval = Newtonsoft.Json.JsonConvert.DeserializeObject(xel.Element("Data").Value, ptype)
                                    DirectCast(fs.ExtraProperties, IDictionary(Of String, Object))(propname) = propval
                                End If
                            Catch ex As Exception
                            End Try
                        Next
                    End If

                Catch ex As Exception

                    excs.Add(New Exception("Error Loading Dynamic Properties", ex))

                End Try

            End If

            If xdoc.Element("DWSIM_Simulation_Data").Element("PetroleumAssays") IsNot Nothing Then

                data = xdoc.Element("DWSIM_Simulation_Data").Element("PetroleumAssays").Elements.ToList

                For Each xel As XElement In data
                    Try
                        Dim obj As New Utilities.PetroleumCharacterization.Assay.Assay()
                        obj.LoadData(xel.Elements.ToList)
                        fs.Options.PetroleumAssays.Add(obj.Name, obj)
                    Catch ex As Exception
                        excs.Add(New Exception("Error Loading Petroleum Assay Information", ex))
                    End Try
                Next

            End If

            fs.ScriptCollection = New Dictionary(Of String, Interfaces.IScript)

            If xdoc.Element("DWSIM_Simulation_Data").Element("ScriptItems") IsNot Nothing Then

                data = xdoc.Element("DWSIM_Simulation_Data").Element("ScriptItems").Elements.ToList

                Dim i As Integer = 0
                For Each xel As XElement In data
                    Try
                        Dim obj As New FlowsheetSolver.Script()
                        obj.LoadData(xel.Elements.ToList)
                        fs.ScriptCollection.Add(obj.ID, obj)
                    Catch ex As Exception
                        excs.Add(New Exception("Error Loading Script Item Information", ex))
                    End Try
                    i += 1
                Next

            End If

            fs.ChartCollection = New Dictionary(Of String, Interfaces.IChart)

            If xdoc.Element("DWSIM_Simulation_Data").Element("ChartItems") IsNot Nothing Then

                data = xdoc.Element("DWSIM_Simulation_Data").Element("ChartItems").Elements.ToList

                Dim i As Integer = 0
                For Each xel As XElement In data
                    Try
                        Dim obj As New SharedClasses.Charts.Chart()
                        obj.LoadData(xel.Elements.ToList)
                        fs.ChartCollection.Add(obj.ID, obj)
                    Catch ex As Exception
                        excs.Add(New Exception("Error Loading Chart Item Information", ex))
                    End Try
                    i += 1
                Next

            End If

            fs.ParticleSizeDistributions = New List(Of ISolidParticleSizeDistribution)

            If xdoc.Element("DWSIM_Simulation_Data").Element("ParticleSizeDistributions") IsNot Nothing Then

                data = xdoc.Element("DWSIM_Simulation_Data").Element("ParticleSizeDistributions").Elements.ToList

                Dim i As Integer = 0
                For Each xel As XElement In data
                    Try
                        Dim obj As New SharedClassesCSharp.Solids.SolidParticleSizeDistribution()
                        obj.LoadData(xel.Elements.ToList)
                        fs.ParticleSizeDistributions.Add(obj)
                    Catch ex As Exception
                        excs.Add(New Exception("Error Loading PSD Item Information", ex))
                    End Try
                    i += 1
                Next

            End If

            If xdoc.Element("DWSIM_Simulation_Data").Element("MessagesLog") IsNot Nothing Then
                Try
                    data = xdoc.Element("DWSIM_Simulation_Data").Element("MessagesLog").Elements.ToList
                    For Each xel As XElement In data
                        fs.MessagesLog.Add(xel.Value)
                    Next
                Catch ex As Exception
                End Try
            End If

            fs.Results = New SharedClasses.DWSIM.Flowsheet.FlowsheetResults

            If xdoc.Element("DWSIM_Simulation_Data").Element("Results") IsNot Nothing Then

                data = xdoc.Element("DWSIM_Simulation_Data").Element("Results").Elements.ToList

                DirectCast(fs.Results, ICustomXMLSerialization).LoadData(data)

            End If

            If excs.Count > 0 Then Throw New AggregateException(excs).Flatten Else Return fs

        End Function

        Shared Sub AddGraphicObjects(fs As IFlowsheet, mainfs As IFlowsheet, data As List(Of XElement), excs As Concurrent.ConcurrentBag(Of Exception),
                       Optional ByVal pkey As String = "", Optional ByVal shift As Integer = 0, Optional ByVal reconnectinlets As Boolean = False)

            Dim objcount As Integer, searchtext As String

            For Each xel As XElement In data
                Try
                    xel.Element("Type").Value = xel.Element("Type").Value.Replace("Microsoft.MSDN.Samples.GraphicObjects", "DWSIM.DrawingTools.GraphicObjects")
                    xel.Element("ObjectType").Value = xel.Element("ObjectType").Value.Replace("OT_Ajuste", "OT_Adjust")
                    xel.Element("ObjectType").Value = xel.Element("ObjectType").Value.Replace("OT_Especificacao", "OT_Spec")
                    xel.Element("ObjectType").Value = xel.Element("ObjectType").Value.Replace("OT_Reciclo", "OT_Recycle")
                    xel.Element("ObjectType").Value = xel.Element("ObjectType").Value.Replace("GO_Texto", "GO_Text")
                    xel.Element("ObjectType").Value = xel.Element("ObjectType").Value.Replace("GO_Figura", "GO_Image")
                    Dim obj As GraphicObject = Nothing
                    Dim t As Type = Type.GetType(xel.Element("Type").Value, False)
                    If Not t Is Nothing Then obj = Activator.CreateInstance(t)
                    If obj Is Nothing Then
                        If xel.Element("Type").Value.Contains("OxyPlotGraphic") Then
                            obj = CType(Drawing.SkiaSharp.Extended.Shared.ReturnInstance(xel.Element("Type").Value.Replace("Shapes", "Charts")), GraphicObject)
                        Else
                            obj = CType(DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject.ReturnInstance(xel.Element("Type").Value), GraphicObject)
                        End If
                    End If
                    If Not obj Is Nothing Then
                        obj.LoadData(xel.Elements.ToList)
                        obj.Name = pkey & obj.Name
                        obj.X += shift
                        obj.Y += shift
                        If pkey <> "" Then
                            searchtext = obj.Tag.Split("(")(0).Trim()
                            objcount = (From go As IGraphicObject In fs.GraphicObjects.Values Select go Where go.Tag.Equals(obj.Tag)).Count
                            If objcount > 0 Then obj.Tag = searchtext & " (" & (objcount + 1).ToString & ")"
                        End If
                        If TypeOf obj Is TableGraphic Then
                            DirectCast(obj, TableGraphic).Flowsheet = fs
                        ElseIf TypeOf obj Is MasterTableGraphic Then
                            DirectCast(obj, MasterTableGraphic).Flowsheet = fs
                        ElseIf TypeOf obj Is SpreadsheetTableGraphic Then
                            DirectCast(obj, SpreadsheetTableGraphic).Flowsheet = fs
                        ElseIf TypeOf obj Is OxyPlotGraphic Then
                            DirectCast(obj, OxyPlotGraphic).Flowsheet = fs
                        ElseIf TypeOf obj Is RigorousColumnGraphic Or TypeOf obj Is AbsorptionColumnGraphic Or TypeOf obj Is CAPEOPENGraphic Then
                            obj.CreateConnectors(xel.Element("InputConnectors").Elements.Count, xel.Element("OutputConnectors").Elements.Count)
                            obj.PositionConnectors()
                        ElseIf TypeOf obj Is ExternalUnitOperationGraphic Then
                            Dim euo = mainfs.AvailableExternalUnitOperations.Values.Where(Function(x) x.Description = obj.Description).FirstOrDefault
                            If euo IsNot Nothing Then
                                obj.Owner = euo
                                DirectCast(euo, Interfaces.ISimulationObject).GraphicObject = obj
                                obj.CreateConnectors(0, 0)
                                obj.Owner = Nothing
                                DirectCast(euo, Interfaces.ISimulationObject).GraphicObject = Nothing
                            End If
                        Else
                            If obj.Name = "" Then obj.Name = obj.Tag
                            obj.CreateConnectors(0, 0)
                        End If
                        fs.AddGraphicObject(obj)
                    End If
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Flowsheet Graphic Objects", ex))
                End Try
            Next

            For Each xel As XElement In data
                Try
                    Dim id As String = pkey & xel.Element("Name").Value
                    If id <> "" Then
                        Dim obj As GraphicObject = (From go As GraphicObject In fs.GraphicObjects.Values Where go.Name = id).SingleOrDefault
                        If obj Is Nothing Then obj = (From go As GraphicObject In fs.GraphicObjects.Values Where go.Name = xel.Element("Name").Value).SingleOrDefault
                        If Not obj Is Nothing Then
                            Dim i As Integer = 0
                            For Each xel2 As XElement In xel.Element("InputConnectors").Elements
                                If xel2.@IsAttached = True Then
                                    obj.InputConnectors(i).ConnectorName = pkey & xel2.@AttachedFromObjID & "|" & xel2.@AttachedFromConnIndex
                                    obj.InputConnectors(i).Type = [Enum].Parse(obj.InputConnectors(i).Type.GetType, xel2.@ConnType)
                                    If reconnectinlets Then
                                        Dim objFrom As GraphicObject = (From go As GraphicObject In fs.GraphicObjects.Values Where go.Name = xel2.@AttachedFromObjID).SingleOrDefault
                                        If Not objFrom Is Nothing Then
                                            If Not objFrom.OutputConnectors(xel2.@AttachedFromConnIndex).IsAttached Then
                                                fs.ConnectObjects(objFrom, obj, xel2.@AttachedFromConnIndex, xel2.@AttachedToConnIndex)
                                            End If
                                        End If
                                    End If
                                End If
                                i += 1
                            Next
                        End If
                    End If
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Flowsheet Object Connection Information", ex))
                End Try
            Next

            For Each xel As XElement In data
                Try
                    Dim id As String = pkey & xel.Element("Name").Value
                    If id <> "" Then
                        Dim obj As GraphicObject = (From go As GraphicObject In
                                                                fs.GraphicObjects.Values Where go.Name = id).SingleOrDefault
                        If Not obj Is Nothing Then
                            For Each xel2 As XElement In xel.Element("OutputConnectors").Elements
                                If xel2.@IsAttached = True Then
                                    Dim objToID = pkey & xel2.@AttachedToObjID
                                    If objToID <> "" Then
                                        Dim objTo As GraphicObject = (From go As GraphicObject In
                                                                                        fs.GraphicObjects.Values Where go.Name = objToID).SingleOrDefault
                                        If objTo Is Nothing Then objTo = (From go As GraphicObject In
                                                                                        fs.GraphicObjects.Values Where go.Name = xel2.@AttachedToObjID).SingleOrDefault
                                        Dim fromidx As Integer = -1
                                        Dim cp As IConnectionPoint = (From cp2 As IConnectionPoint In objTo.InputConnectors Select cp2 Where cp2.ConnectorName.Split("|")(0) = obj.Name).SingleOrDefault
                                        If cp Is Nothing Then cp = (From cp2 As IConnectionPoint In objTo.InputConnectors Select cp2 Where cp2.ConnectorName.Split("|")(0) = xel2.@AttachedToObjID).SingleOrDefault
                                        If Not cp Is Nothing Then
                                            fromidx = cp.ConnectorName.Split("|")(1)
                                        End If
                                        If Not obj Is Nothing And Not objTo Is Nothing Then fs.ConnectObjects(obj, objTo, fromidx, xel2.@AttachedToConnIndex)
                                    End If
                                End If
                            Next
                            For Each xel2 As XElement In xel.Element("EnergyConnector").Elements
                                If xel2.@IsAttached = True Then
                                    Dim objToID = pkey & xel2.@AttachedToObjID
                                    If objToID <> "" Then
                                        Dim objTo As GraphicObject = (From go As GraphicObject In
                                                                                        fs.GraphicObjects.Values Where go.Name = objToID).SingleOrDefault
                                        If objTo Is Nothing Then obj = (From go As GraphicObject In
                                                                                        fs.GraphicObjects.Values Where go.Name = xel2.@AttachedToObjID).SingleOrDefault
                                        If Not obj Is Nothing And Not objTo Is Nothing Then fs.ConnectObjects(obj, objTo, -1, xel2.@AttachedToConnIndex)
                                    End If
                                End If
                            Next
                        End If
                    End If
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Flowsheet Object Connection Information", ex))
                End Try
            Next

        End Sub

        ''' <summary>Updates process-level data in the XML document from the given flowsheet bag.</summary>
        Public Shared Function UpdateProcessData(form As IFlowsheetBag, xdoc As XDocument)

            Dim ci As CultureInfo = CultureInfo.InvariantCulture
            Dim excs As New List(Of Exception)

            Dim data As List(Of XElement)

            data = xdoc.Element("DWSIM_Simulation_Data").Element("SimulationObjects").Elements.ToList

            For Each xel As XElement In data
                Try
                    Dim id As String = xel.<Name>.Value
                    Dim obj = form.SimulationObjects(id)
                    DirectCast(obj, Interfaces.ICustomXMLSerialization).LoadData(xel.Elements.ToList)
                    If TypeOf obj Is MaterialStream Then
                        For Each phase As BaseClasses.Phase In DirectCast(obj, MaterialStream).Phases.Values
                            For Each c As ConstantProperties In form.Compounds.Values
                                phase.Compounds(c.Name).ConstantProperties = c
                            Next
                        Next
                    End If
                Catch ex As Exception
                    excs.Add(New Exception("Error Loading Unit Operation Information", ex))
                End Try
            Next

            If excs.Count > 0 Then Throw New AggregateException(excs).Flatten Else Return form

        End Function

        ''' <summary>Serializes the current sub-flowsheet data to a compressed memory stream.</summary>
        Public Shared Function ReturnProcessData(Form As IFlowsheetBag) As IO.MemoryStream

            Dim xdoc As New XDocument()
            Dim xel As XElement

            Dim ci As CultureInfo = CultureInfo.InvariantCulture

            xdoc.Add(New XElement("DWSIM_Simulation_Data"))

            xdoc.Element("DWSIM_Simulation_Data").Add(New XElement("SimulationObjects"))
            xel = xdoc.Element("DWSIM_Simulation_Data").Element("SimulationObjects")

            For Each so As BaseClass In Form.SimulationObjects.Values
                xel.Add(New XElement("SimulationObject", {so.SaveData().ToArray()}))
            Next

            'xdoc.Element("DWSIM_Simulation_Data").Add(New XElement("GraphicObjects"))
            'xel = xdoc.Element("DWSIM_Simulation_Data").Element("GraphicObjects")

            'For Each go As DWSIM.DrawingTools.GraphicObjects.GraphicObject In Form.FormSurface.FlowsheetDesignSurface.drawingObjects
            '    If Not go.IsConnector Then xel.Add(New XElement("GraphicObject", go.SaveData().ToArray()))
            'Next

            Dim bytestream As New IO.MemoryStream()
            xdoc.Save(bytestream)

            Return bytestream

        End Function

        ''' <summary>Saves the current sub-flowsheet state back to the file at the given path.</summary>
        Public Sub UpdateProcessData(path As String)

            If Me.Initialized Then

                Try

                    Dim xdoc As XDocument = XDocument.Load(If(path.ToLower.Contains("dwxml"), path, ExtractXML(path)))

                    Dim xel As XElement

                    xel = xdoc.Element("DWSIM_Simulation_Data").Element("GeneralInfo")

                    xel.RemoveAll()
                    xel.Add(New XElement("BuildVersion", Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()))
                    Dim buildversion = Reflection.Assembly.GetExecutingAssembly().GetName().Version
                    xel.Add(New XElement("BuildDate", CType("01/01/2000", DateTime).AddDays(buildversion.Build).AddSeconds(buildversion.Revision * 2)))
                    xel.Add(New XElement("OSInfo", Runtime.InteropServices.RuntimeInformation.OSDescription & ", " & Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()))
                    xel.Add(New XElement("SavedOn", Date.Now))

                    xel = xdoc.Element("DWSIM_Simulation_Data").Element("SimulationObjects")
                    xel.RemoveAll()

                    For Each so As BaseClass In Me.Fsheet.SimulationObjects.Values
                        xel.Add(New XElement("SimulationObject", {so.SaveData().ToArray()}))
                    Next

                    If path.ToLower.Contains("dwxml") Then

                        xdoc.Save(path)

                    Else

                        Dim xmlfile As String = IO.Path.ChangeExtension(SharedClasses.Utility.GetTempFileName(), "xml")

                        xdoc.Save(xmlfile)

                        Dim i_Files As ArrayList = New ArrayList()
                        If File.Exists(xmlfile) Then i_Files.Add(xmlfile)

                        Dim astrFileNames() As String = i_Files.ToArray(GetType(String))
                        Dim strmZipOutputStream As ICSharpCode.SharpZipLib.Zip.ZipOutputStream

                        strmZipOutputStream = New ICSharpCode.SharpZipLib.Zip.ZipOutputStream(File.Create(path))

                        ' Compression Level: 0-9
                        ' 0: no(Compression)
                        ' 9: maximum compression
                        strmZipOutputStream.SetLevel(9)

                        'save with password, if set
                        If Fsheet.Options.UsePassword Then strmZipOutputStream.Password = Fsheet.Options.Password

                        Dim strFile As String

                        For Each strFile In astrFileNames

                            Dim strmFile As FileStream = File.OpenRead(strFile)
                            Dim abyBuffer(strmFile.Length - 1) As Byte

                            strmFile.Read(abyBuffer, 0, abyBuffer.Length)
                            Dim objZipEntry As New ICSharpCode.SharpZipLib.Zip.ZipEntry(IO.Path.GetFileName(strFile))

                            objZipEntry.DateTime = DateTime.Now
                            objZipEntry.Size = strmFile.Length
                            strmFile.Close()
                            strmZipOutputStream.PutNextEntry(objZipEntry)
                            strmZipOutputStream.Write(abyBuffer, 0, abyBuffer.Length)

                        Next

                        strmZipOutputStream.Finish()
                        strmZipOutputStream.Close()

                        File.Delete(xmlfile)

                    End If

                    FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": " & FlowSheet.GetTranslatedString("SubFSUpdateSuccess"), IFlowsheet.MessageType.Information)

                Catch ex As Exception

                    FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": " & FlowSheet.GetTranslatedString("SubFSUpdateFailed") & " " & ex.ToString, IFlowsheet.MessageType.Information)

                End Try

            End If

        End Sub

        ''' <summary>Transfers input data to the sub-flowsheet, solves it, and maps results back to the parent.</summary>
        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            InitializeInternalFlowsheet()

            If Initialized Then InitializeMappings()

            If Fsheet Is Nothing Then

                ' The sub-flowsheet is the whole of this unit operation, so there is nothing to
                ' calculate without it. Saying which file is missing beats the null reference that
                ' the next line used to raise: the path is stored absolute, from whichever machine
                ' last saved the parent, so a file that moved with its owner is the common case.
                Dim reason As String = If(FileIsEmbedded,
                                          "the embedded file '" & EmbeddedFileName & "'",
                                          "'" & SimulationFile & "'")

                Throw New Exception("The sub-flowsheet could not be loaded from " & reason & ". " &
                                    "Open this unit operation and point it at the file.",
                                    InitializationError)

            End If

            Me.Fsheet.MasterUnitOp = Me

            Calculated = False

            MassBalanceError = 0.0#

            Dim msfrom, msto As MaterialStream

            Dim win, wout As Double

            win = 0.0#
            For Each c In Me.GraphicObject.InputConnectors

                If c.IsAttached Then

                    msfrom = FlowSheet.SimulationObjects(c.AttachedConnector.AttachedFrom.Name)
                    msto = Fsheet.SimulationObjects(InputConnections(Me.GraphicObject.InputConnectors.IndexOf(c)))

                    win += msfrom.Phases(0).Properties.massflow.GetValueOrDefault

                    msto.Clear()

                    msto.Phases(0).Properties.temperature = msfrom.Phases(0).Properties.temperature.GetValueOrDefault
                    msto.Phases(0).Properties.pressure = msfrom.Phases(0).Properties.pressure.GetValueOrDefault
                    msto.Phases(0).Properties.enthalpy = msfrom.Phases(0).Properties.enthalpy.GetValueOrDefault
                    msto.SpecType = Interfaces.Enums.StreamSpec.Temperature_and_Pressure

                    Dim wt, mt As Double

                    Select Case MassTransferMode

                        Case FlowsheetUOMassTransferMode.CompoundMassFlows

                            wt = 0.0#
                            For Each s In CompoundMappings
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Key) And msto.Phases(0).Compounds.ContainsKey(s.Value) Then
                                    wt += msfrom.Phases(0).Compounds(s.Key).MassFlow.GetValueOrDefault
                                End If
                            Next

                            msto.Phases(0).Properties.massflow = wt

                            For Each s In CompoundMappings
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Key) And msto.Phases(0).Compounds.ContainsKey(s.Value) Then
                                    If Not msto.Phases(0).Compounds(s.Value).MassFraction.HasValue Then msto.Phases(0).Compounds(s.Value).MassFraction = 0.0#
                                    msto.Phases(0).Compounds(s.Value).MassFraction += msfrom.Phases(0).Compounds(s.Key).MassFlow.GetValueOrDefault / wt
                                End If
                            Next

                            msto.CalcOverallCompMoleFractions()

                        Case FlowsheetUOMassTransferMode.CompoundMassFractions

                            msto.Phases(0).Properties.massflow = msfrom.Phases(0).Properties.massflow.GetValueOrDefault

                            For Each s In CompoundMappings
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Key) And msto.Phases(0).Compounds.ContainsKey(s.Value) Then
                                    If Not msto.Phases(0).Compounds(s.Value).MassFraction.HasValue Then msto.Phases(0).Compounds(s.Value).MassFraction = 0.0#
                                    msto.Phases(0).Compounds(s.Value).MassFraction += msfrom.Phases(0).Compounds(s.Key).MassFraction.GetValueOrDefault
                                End If
                            Next

                            msto.NormalizeOverallMassComposition()

                            msto.CalcOverallCompMoleFractions()

                        Case FlowsheetUOMassTransferMode.CompoundMoleFlows

                            mt = 0.0#
                            For Each s In CompoundMappings
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Key) And msto.Phases(0).Compounds.ContainsKey(s.Value) Then
                                    mt += msfrom.Phases(0).Compounds(s.Key).MolarFlow.GetValueOrDefault
                                End If
                            Next

                            msto.Phases(0).Properties.molarflow = mt

                            For Each s In CompoundMappings
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Key) And msto.Phases(0).Compounds.ContainsKey(s.Value) Then
                                    If Not msto.Phases(0).Compounds(s.Value).MoleFraction.HasValue Then msto.Phases(0).Compounds(s.Value).MoleFraction = 0.0#
                                    msto.Phases(0).Compounds(s.Value).MoleFraction += msfrom.Phases(0).Compounds(s.Key).MolarFlow.GetValueOrDefault / mt
                                End If
                            Next

                            msto.CalcOverallCompMassFractions()

                        Case FlowsheetUOMassTransferMode.CompoundMoleFractions

                            msto.Phases(0).Properties.molarflow = msfrom.Phases(0).Properties.molarflow.GetValueOrDefault

                            For Each s In CompoundMappings
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Key) And msto.Phases(0).Compounds.ContainsKey(s.Value) Then
                                    If Not msto.Phases(0).Compounds(s.Value).MoleFraction.HasValue Then msto.Phases(0).Compounds(s.Value).MoleFraction = 0.0#
                                    msto.Phases(0).Compounds(s.Value).MoleFraction += msfrom.Phases(0).Compounds(s.Key).MoleFraction.GetValueOrDefault
                                End If
                            Next

                            msto.NormalizeOverallMoleComposition()

                            msto.CalcOverallCompMassFractions()

                    End Select

                End If

            Next

            Fsheet.MasterFlowsheet = Me.FlowSheet
            Fsheet.RedirectMessages = Me.RedirectOutput

            Dim exclist As List(Of Exception)

            'GlobalSettings.Settings.CalculatorBusy = False

            exclist = Fsheet.RequestCalculationAndWait()

            If exclist.Count > 0 Then
                For Each ex In exclist
                    FlowSheet.ShowMessage(String.Format("{0}: {1}", GraphicObject?.Tag, ex.Message), IFlowsheet.MessageType.GeneralError)
                Next
                Throw New Exception("error calculating internal flowsheet. Please check the previous error messages for more details.")
            End If

            wout = 0.0#
            For Each c In Me.GraphicObject.OutputConnectors

                If c.IsAttached Then

                    msto = FlowSheet.SimulationObjects(c.AttachedConnector.AttachedTo.Name)
                    msfrom = Fsheet.SimulationObjects(OutputConnections(Me.GraphicObject.OutputConnectors.IndexOf(c)))

                    wout += msfrom.Phases(0).Properties.massflow.GetValueOrDefault

                    msto.Clear()

                    msto.Phases(0).Properties.temperature = msfrom.Phases(0).Properties.temperature.GetValueOrDefault
                    msto.Phases(0).Properties.pressure = msfrom.Phases(0).Properties.pressure.GetValueOrDefault
                    msto.Phases(0).Properties.enthalpy = msfrom.Phases(0).Properties.enthalpy.GetValueOrDefault
                    msto.SpecType = Interfaces.Enums.StreamSpec.Temperature_and_Pressure

                    Dim wt, mt As Double

                    Select Case MassTransferMode

                        Case FlowsheetUOMassTransferMode.CompoundMassFlows

                            wt = 0.0#
                            For Each s In msto.Phases(0).Compounds.Values
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Name) And msto.Phases(0).Compounds.ContainsKey(s.Name) Then
                                    wt += msfrom.Phases(0).Compounds(s.Name).MassFlow.GetValueOrDefault
                                End If
                            Next

                            msto.Phases(0).Properties.massflow = wt
                            msto.DefinedFlow = FlowSpec.Mass

                            For Each s In msto.Phases(0).Compounds.Values
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Name) And msto.Phases(0).Compounds.ContainsKey(s.Name) Then
                                    msto.Phases(0).Compounds(s.Name).MassFraction = msfrom.Phases(0).Compounds(s.Name).MassFlow.GetValueOrDefault / wt
                                End If
                            Next

                            msto.CalcOverallCompMoleFractions()

                        Case FlowsheetUOMassTransferMode.CompoundMassFractions

                            msto.Phases(0).Properties.massflow = msfrom.Phases(0).Properties.massflow.GetValueOrDefault

                            For Each s In msto.Phases(0).Compounds.Values
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Name) And msto.Phases(0).Compounds.ContainsKey(s.Name) Then
                                    msto.Phases(0).Compounds(s.Name).MassFraction = msfrom.Phases(0).Compounds(s.Name).MassFraction.GetValueOrDefault
                                End If
                            Next

                            msto.NormalizeOverallMassComposition()

                            msto.CalcOverallCompMoleFractions()

                        Case FlowsheetUOMassTransferMode.CompoundMoleFlows

                            mt = 0.0#
                            For Each s In msto.Phases(0).Compounds.Values
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Name) And msto.Phases(0).Compounds.ContainsKey(s.Name) Then
                                    mt += msfrom.Phases(0).Compounds(s.Name).MolarFlow.GetValueOrDefault
                                End If
                            Next

                            msto.Phases(0).Properties.molarflow = mt
                            msto.DefinedFlow = FlowSpec.Mole

                            For Each s In msto.Phases(0).Compounds.Values
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Name) And msto.Phases(0).Compounds.ContainsKey(s.Name) Then
                                    msto.Phases(0).Compounds(s.Name).MoleFraction = msfrom.Phases(0).Compounds(s.Name).MolarFlow.GetValueOrDefault / mt
                                End If
                            Next

                            msto.CalcOverallCompMassFractions()

                        Case FlowsheetUOMassTransferMode.CompoundMoleFractions

                            msto.Phases(0).Properties.molarflow = msfrom.Phases(0).Properties.molarflow.GetValueOrDefault
                            msto.DefinedFlow = FlowSpec.Mole

                            For Each s In msto.Phases(0).Compounds.Values
                                If msfrom.Phases(0).Compounds.ContainsKey(s.Name) And msto.Phases(0).Compounds.ContainsKey(s.Name) Then
                                    msto.Phases(0).Compounds(s.Name).MoleFraction = msfrom.Phases(0).Compounds(s.Name).MoleFraction.GetValueOrDefault
                                End If
                            Next

                            msto.NormalizeOverallMoleComposition()

                            msto.CalcOverallCompMassFractions()

                    End Select

                End If

            Next

            MassBalanceError = (wout - win) / win * 100

            Calculated = True

            IObj?.Close()

        End Sub

        ''' <summary>Returns the value of the specified property, converted to the given unit system.</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim pkey As String = prop.Split("][")(1).TrimStart("[").TrimEnd("]")

                Try
                    If prop.Contains("[I]") Then
                        Return Fsheet.SimulationObjects(InputParams(pkey).ObjectID).GetPropertyValue(InputParams(pkey).ObjectProperty, su)
                    Else
                        Return Fsheet.SimulationObjects(OutputParams(pkey).ObjectID).GetPropertyValue(OutputParams(pkey).ObjectProperty, su)
                    End If
                Catch ex As Exception
                    Return ex.ToString
                End Try
            End If

        End Function

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            If Initialized Then
                Select Case proptype
                    Case Enums.PropertyType.ALL
                        For Each p In InputParams.Values
                            proplist.Add(Fsheet.SimulationObjects(p.ObjectID).GraphicObject.Tag & " / " & FlowSheet.GetTranslatedString(p.ObjectProperty) & " / [I][" & p.ID & "]")
                        Next
                        For Each p In OutputParams.Values
                            proplist.Add(Fsheet.SimulationObjects(p.ObjectID).GraphicObject.Tag & " / " & FlowSheet.GetTranslatedString(p.ObjectProperty) & " / [O][" & p.ID & "]")
                        Next
                    Case Enums.PropertyType.WR
                        For Each p In InputParams.Values
                            proplist.Add(Fsheet.SimulationObjects(p.ObjectID).GraphicObject.Tag & " / " & FlowSheet.GetTranslatedString(p.ObjectProperty) & " / [I][" & p.ID & "]")
                        Next
                    Case Enums.PropertyType.RO
                        For Each p In OutputParams.Values
                            proplist.Add(Fsheet.SimulationObjects(p.ObjectID).GraphicObject.Tag & " / " & FlowSheet.GetTranslatedString(p.ObjectProperty) & " / [O][" & p.ID & "]")
                        Next
                End Select
            End If
            Return proplist.ToArray(GetType(System.String))
        End Function

        ''' <summary>Sets the value of the specified property from the given value and unit system.</summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim pkey As String = prop.Split("][")(1).TrimStart("[").TrimEnd("]")

            Fsheet.SimulationObjects(InputParams(pkey).ObjectID).SetPropertyValue(InputParams(pkey).ObjectProperty, propval, su)

            Return 1

        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else

                Dim pkey As String = prop.Split("][")(1).TrimStart("[").TrimEnd("]")

                Try
                    If prop.Contains("[I]") Then
                        Return Fsheet.SimulationObjects(InputParams(pkey).ObjectID).GetPropertyUnit(InputParams(pkey).ObjectProperty, su)
                    Else
                        Return Fsheet.SimulationObjects(OutputParams(pkey).ObjectID).GetPropertyUnit(OutputParams(pkey).ObjectProperty, su)
                    End If
                Catch ex As Exception
                    Return ex.ToString
                End Try

            End If

        End Function

        ''' <summary>Initializes the internal sub-flowsheet object from the simulation file or embedded data.</summary>
        Public Sub InitializeInternalFlowsheet()

            If Not Initialized Then

                Dim finished As Boolean = False

                FlowSheet.RunCodeOnUIThread(Sub()
                                                Try
                                                    If FileIsEmbedded Then
                                                        Dim tmpfile As String = ""
                                                        tmpfile = Path.ChangeExtension(SharedClasses.Utility.GetTempFileName(), Path.GetExtension(EmbeddedFileName))
                                                        FlowSheet.FileDatabaseProvider.ExportFile(EmbeddedFileName, tmpfile)
                                                        Fsheet = UnitOperations.Flowsheet.InitializeFlowsheet(tmpfile, FlowSheet.GetNewInstance(), FlowSheet)
                                                        File.Delete(tmpfile)
                                                    Else
                                                        Fsheet = UnitOperations.Flowsheet.InitializeFlowsheet(SimulationFile, FlowSheet.GetNewInstance(), FlowSheet)
                                                    End If
                                                    Initialized = True
                                                Catch ex As Exception
                                                    ' Kept, not swallowed: without it the only
                                                    ' evidence that the sub-flowsheet never loaded
                                                    ' is a null reference two lines into Calculate.
                                                    InitializationError = ex
                                                Finally
                                                    finished = True
                                                End Try
                                            End Sub)

                While Not finished
                    Thread.Sleep(500)
                End While

            End If

        End Sub


        ''' <summary>Restores the flowsheet UO state, including parameter mappings and connections, from XML.</summary>
        Public Overrides Function LoadData(data As List(Of XElement)) As Boolean

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            Me.Annotation = (From xel As XElement In data Select xel Where xel.Name = "Annotation").SingleOrDefault.Value

            ParseFilePath()

            If InitializeOnLoad Then
                If Not FileIsEmbedded Then
                    If IO.File.Exists(SimulationFile) Then
                        Me.Fsheet = Me.FlowSheet.GetNewInstance
                        InitializeFlowsheet(SimulationFile, Me.Fsheet, FlowSheet)
                        Me.Initialized = True
                    End If
                Else
                    Dim tmpfile As String = ""
                    Try
                        tmpfile = Path.ChangeExtension(SharedClasses.Utility.GetTempFileName(), Path.GetExtension(EmbeddedFileName))
                        FlowSheet.FileDatabaseProvider.ExportFile(EmbeddedFileName, tmpfile)
                        Fsheet = InitializeFlowsheet(tmpfile, FlowSheet.GetNewInstance, FlowSheet)
                        Me.Initialized = True
                    Catch ex As Exception
                    Finally
                        File.Delete(tmpfile)
                    End Try
                End If
            End If

            Dim i As Integer

            i = 0
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "InputConnections").Elements.ToList
                Me.InputConnections(i) = xel.Value
                i += 1
            Next

            i = 0
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "OutputConnections").Elements.ToList
                Me.OutputConnections(i) = xel.Value
                i += 1
            Next

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "InputParameters").Elements.ToList
                Dim fp As New FlowsheetUOParameter()
                fp.LoadData(xel.Elements.ToList)
                Me.InputParams.Add(fp.ID, fp)
            Next

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "OutputParameters").Elements.ToList
                Dim fp As New FlowsheetUOParameter()
                fp.LoadData(xel.Elements.ToList)
                Me.OutputParams.Add(fp.ID, fp)
            Next

            CompoundMappings = New Dictionary(Of String, String)
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "CompoundMappings").Elements.ToList
                If Not CompoundMappings.ContainsKey(xel.Attribute("From").Value) Then Me.CompoundMappings.Add(xel.Attribute("From").Value, xel.Attribute("To").Value)
            Next

            Return True

        End Function

        ''' <summary>Serializes the flowsheet UO state, including parameter mappings and connections, to XML.</summary>
        Public Overrides Function SaveData() As List(Of XElement)

            Dim elements As List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                '.Add(New XElement("InputConnections"))
                'For Each s In InputConnections
                '    .Item(.Count - 1).Add(New XElement("InputConnection", s))
                'Next
                '.Add(New XElement("OutputConnections"))
                'For Each s In OutputConnections
                '    .Item(.Count - 1).Add(New XElement("OutputConnection", s))
                'Next
                .Add(New XElement("InputParameters"))
                For Each p In InputParams.Values
                    .Item(.Count - 1).Add(New XElement("FlowsheetUOParameter", p.SaveData.ToArray))
                Next
                .Add(New XElement("OutputParameters"))
                For Each p In OutputParams.Values
                    .Item(.Count - 1).Add(New XElement("FlowsheetUOParameter", p.SaveData.ToArray))
                Next
                .Remove(.Where(Function(e) e.Name = "CompoundMappings").FirstOrDefault)
                .Add(New XElement("CompoundMappings"))
                For Each p In CompoundMappings
                    Dim xel As New XElement("CompoundMapping")
                    xel.SetAttributeValue("From", p.Key)
                    xel.SetAttributeValue("To", p.Value)
                    .Item(.Count - 1).Add(xel)
                Next
            End With

            If UpdateOnSave Then
                If Not FileIsEmbedded Then
                    ParseFilePath()
                    UpdateProcessData(SimulationFile)
                Else
                    Dim tpath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
                    Dim tfile = Path.Combine(tpath.FullName, EmbeddedFileName)
                    FlowSheet.FileDatabaseProvider.ExportFile(EmbeddedFileName, tfile)
                    UpdateProcessData(tfile)
                    FlowSheet.FileDatabaseProvider.DeleteFile(EmbeddedFileName)
                    FlowSheet.FileDatabaseProvider.PutFile(tfile, EmbeddedFileName)
                    Directory.Delete(tpath.FullName, True)
                End If
            End If

            Return elements

        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.flowsheet_block.png")

        End Function

        ''' <summary>Returns the localised display description.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("FLOWS_Desc")
        End Function

        ''' <summary>Returns the localised display name.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("FLOWS_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Generates a plain-text report of the flowsheet UO results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            If Initialized Then

                Dim str As New Text.StringBuilder

                Dim istr, ostr As MaterialStream
                istr = Me.GetInletMaterialStream(0)
                ostr = Me.GetOutletMaterialStream(0)

                istr.PropertyPackage.CurrentMaterialStream = istr

                str.AppendLine("Flowsheet Block: " & Me.GraphicObject.Tag)
                str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
                str.AppendLine()
                str.AppendLine("Calculation parameters")
                str.AppendLine()
                str.AppendLine("    Flowsheet Path: " & SimulationFile)
                str.AppendLine("    Mass Transfer Option: " & MassTransferMode.ToString)
                str.AppendLine()
                str.AppendLine("Linked Input Variables")
                str.AppendLine()
                For Each par In InputParams.Values
                    str.AppendLine("    " + Fsheet.SimulationObjects(par.ObjectID).GraphicObject.Tag + ", " +
                FlowSheet.GetTranslatedString(par.ObjectProperty) + ": " +
                Fsheet.SimulationObjects(par.ObjectID).GetPropertyValue(par.ObjectProperty, FlowSheet.FlowsheetOptions.SelectedUnitSystem).ToString() + " " +
                Fsheet.SimulationObjects(par.ObjectID).GetPropertyUnit(par.ObjectProperty, FlowSheet.FlowsheetOptions.SelectedUnitSystem))
                Next
                str.AppendLine()
                str.AppendLine("Linked Output Variables")
                str.AppendLine()
                For Each par In OutputParams.Values
                    str.AppendLine("    " + Fsheet.SimulationObjects(par.ObjectID).GraphicObject.Tag + ", " +
                FlowSheet.GetTranslatedString(par.ObjectProperty) + ": " +
                Fsheet.SimulationObjects(par.ObjectID).GetPropertyValue(par.ObjectProperty, FlowSheet.FlowsheetOptions.SelectedUnitSystem).ToString() + " " +
                Fsheet.SimulationObjects(par.ObjectID).GetPropertyUnit(par.ObjectProperty, FlowSheet.FlowsheetOptions.SelectedUnitSystem))
                Next

                Return str.ToString

            Else

                Return "Flowsheet not Initialized."

            End If

        End Function

    End Class

End Namespace


