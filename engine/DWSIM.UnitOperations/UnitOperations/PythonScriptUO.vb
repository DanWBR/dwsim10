'    Custom (Scripting) Unit Operation Calculation Routines 
'    Copyright 2010-2011 Daniel Wagner O. de Medeiros
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


Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports System.IO
Imports System.Runtime.InteropServices
Imports CapeOpen
Imports System.Runtime.Serialization.Formatters
Imports System.Linq
Imports System.ComponentModel
Imports Microsoft.Scripting.Hosting
Imports Python.Runtime
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Drawing.SkiaSharp

Namespace UnitOperations

    ''' <summary>
    ''' Represents a Python Script Unit Operation that executes user-supplied Python code
    ''' (IronPython or Python.NET) as part of the flowsheet calculation sequence.
    ''' </summary>
    <System.Serializable()> Public Partial Class CustomUO

        Inherits UnitOperations.UnitOpBaseClass
        ''' <summary>Gets or sets the simulation object class category (UserModels).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.UserModels

        ''' <summary>Gets a value indicating this unit operation supports dynamic mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating this unit operation has no specific dynamic-mode properties.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>Specifies the Python execution engine to use.</summary>
        Public Enum PythonExecutionEngine
            ''' <summary>IronPython (.NET-hosted) engine.</summary>
            IronPython = 0
            ''' <summary>Python.NET (CPython interop) engine.</summary>
            PythonNET = 1
        End Enum

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public fs As Object
        <NonSerialized> <Xml.Serialization.XmlIgnore> Public fsmono As Object

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private engine As ScriptEngine

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private scope As ScriptScope

        Private _scripttext As String = ""
        Private _includes() As String
        Private _fontname As String = "Consolas"
        Private _fontsize As Integer = 10

        Private Shared Lock As New Object

        ''' <summary>Gets or sets whether spaces are highlighted in the script editor.</summary>
        Public Property HighlightSpaces As Boolean = False
        ''' <summary>Gets or sets whether tabs are highlighted in the script editor.</summary>
        Public Property HighlightTabs As Boolean = False

        ''' <summary>Gets or sets the dictionary of string-typed input variables exposed to the script.</summary>
        Public Property InputStringVariables As New Dictionary(Of String, String)
        ''' <summary>Gets or sets the dictionary of numeric input variables exposed to the script.</summary>
        Public Property InputVariables As New Dictionary(Of String, Double)
        ''' <summary>Gets or sets the dictionary of numeric output variables produced by the script.</summary>
        Public Property OutputVariables As New Dictionary(Of String, Double)

        ''' <summary>Gets or sets the Base-64 encoded embedded image data for a custom icon.</summary>
        Public Property EmbeddedImageData As String = ""

        ''' <summary>Gets or sets whether the embedded image is used as the graphic object icon.</summary>
        Public Property UseEmbeddedImage As Boolean = False

        ''' <summary>Gets or sets the Python execution engine (IronPython or Python.NET).</summary>
        Public Property ExecutionEngine As PythonExecutionEngine = PythonExecutionEngine.IronPython

        ''' <summary>Gets or sets the font name used in the script editor.</summary>
        Public Property FontName() As String
            Get
                Return _fontname
            End Get
            Set(ByVal value As String)
                _fontname = value
            End Set
        End Property

        ''' <summary>Gets or sets the font size used in the script editor.</summary>
        Public Property FontSize() As Integer
            Get
                Return _fontsize
            End Get
            Set(ByVal value As Integer)
                _fontsize = value
            End Set
        End Property

        ''' <summary>Gets or sets the list of additional Python script files to include.</summary>
        Public Property Includes() As String()
            Get
                Return _includes
            End Get
            Set(ByVal value As String())
                _includes = value
            End Set
        End Property

        ''' <summary>Gets or sets the Python script text to execute during calculation.</summary>
        Public Property ScriptText() As String
            Get
                Return _scripttext
            End Get
            Set(ByVal value As String)
                _scripttext = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance.</summary>
        Public Sub New()
            MyBase.New()
            ComponentName = "Python UO"
            ComponentDescription = "Python Scripting Unit Operation"
            InputVariables = New Dictionary(Of String, Double)
            InputStringVariables = New Dictionary(Of String, String)
            OutputVariables = New Dictionary(Of String, Double)
        End Sub

        ''' <summary>Initializes a new instance with a name and description.</summary>
        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.CreateNew()
            InputVariables = New Dictionary(Of String, Double)
            InputStringVariables = New Dictionary(Of String, String)
            OutputVariables = New Dictionary(Of String, Double)
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        ''' <summary>Creates a deep copy via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New CustomUO()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of CustomUO)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Executes the script in dynamic simulation mode.</summary>
        Public Overrides Sub RunDynamicModel()

            Calculate()

        End Sub

        ''' <summary>Executes the Python script, passing flowsheet objects and variables into the scope.</summary>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            Dim ims1, ims2, ims3, ims4, ims5, ims6, oms1, oms2, oms3, oms4, oms5, oms6 As MaterialStream
            If Me.GraphicObject.InputConnectors(0).IsAttached Then
                ims1 = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)
            Else
                ims1 = Nothing
            End If
            If Me.GraphicObject.InputConnectors(1).IsAttached Then
                ims2 = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(1).AttachedConnector.AttachedFrom.Name)
            Else
                ims2 = Nothing
            End If
            If Me.GraphicObject.InputConnectors(2).IsAttached Then
                ims3 = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(2).AttachedConnector.AttachedFrom.Name)
            Else
                ims3 = Nothing
            End If
            If Me.GraphicObject.InputConnectors(4).IsAttached Then
                ims4 = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(4).AttachedConnector.AttachedFrom.Name)
            Else
                ims4 = Nothing
            End If
            If Me.GraphicObject.InputConnectors(5).IsAttached Then
                ims5 = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(5).AttachedConnector.AttachedFrom.Name)
            Else
                ims5 = Nothing
            End If
            If Me.GraphicObject.InputConnectors(6).IsAttached Then
                ims6 = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(6).AttachedConnector.AttachedFrom.Name)
            Else
                ims6 = Nothing
            End If
            If Me.GraphicObject.OutputConnectors(0).IsAttached Then
                oms1 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
            Else
                oms1 = Nothing
            End If
            If Me.GraphicObject.OutputConnectors(1).IsAttached Then
                oms2 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(1).AttachedConnector.AttachedTo.Name)
            Else
                oms2 = Nothing
            End If
            If Me.GraphicObject.OutputConnectors(2).IsAttached Then
                oms3 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(2).AttachedConnector.AttachedTo.Name)
            Else
                oms3 = Nothing
            End If
            If Me.GraphicObject.OutputConnectors(4).IsAttached Then
                oms4 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(4).AttachedConnector.AttachedTo.Name)
            Else
                oms4 = Nothing
            End If
            If Me.GraphicObject.OutputConnectors(5).IsAttached Then
                oms5 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(5).AttachedConnector.AttachedTo.Name)
            Else
                oms5 = Nothing
            End If
            If Me.GraphicObject.OutputConnectors(6).IsAttached Then
                oms6 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(6).AttachedConnector.AttachedTo.Name)
            Else
                oms6 = Nothing
            End If

            Dim ies1, oes1 As Streams.EnergyStream
            If Me.GraphicObject.InputConnectors(3).IsAttached Then
                ies1 = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(3).AttachedConnector.AttachedFrom.Name)
            Else
                ies1 = Nothing
            End If
            If Me.GraphicObject.OutputConnectors(3).IsAttached Then
                oes1 = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(3).AttachedConnector.AttachedTo.Name)
            Else
                oes1 = Nothing
            End If

            Dim txtcode As String = ""
            If Not Includes Is Nothing Then
                For Each fname As String In Me.Includes
                    txtcode += File.ReadAllText(fname) + vbCrLf
                Next
            End If
            txtcode += Me.ScriptText

            FlowSheet.PythonPreprocessor?.Invoke(txtcode)

            If ExecutionEngine = PythonExecutionEngine.IronPython Then

                engine = IronPython.Hosting.Python.CreateEngine()

                ' Point the engine at the IronPython standard library that ships alongside the app, the
                ' same way the flowsheet-level Python script does, so a Python Script unit operation can
                ' import stdlib modules such as pathlib (issue #46). Both casings are added because the
                ' folder is "Lib" on Windows and "lib" on the cross-platform build, and Linux is
                ' case-sensitive; a path that does not exist is simply ignored by the import machinery.
                Try
                    Dim paths0 As New List(Of String)(engine.GetSearchPaths())
                    Dim apppath = IO.Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
                    paths0.Add(IO.Path.Combine(apppath, "Lib"))
                    paths0.Add(IO.Path.Combine(apppath, "lib"))
                    engine.SetSearchPaths(paths0)
                Catch ex As Exception
                End Try

                engine.Runtime.LoadAssembly(GetType(System.String).Assembly)
                engine.Runtime.LoadAssembly(GetType(BaseClasses.ConstantProperties).Assembly)
                engine.Runtime.LoadAssembly(GetType(GraphicObject).Assembly)
                engine.Runtime.LoadAssembly(GetType(GraphicsSurface).Assembly)
                scope = engine.CreateScope()
                scope.SetVariable("Flowsheet", FlowSheet)
                scope.SetVariable("Me", Me)
                scope.SetVariable("This", Me)
                scope.SetVariable("flowsheet", FlowSheet)
                scope.SetVariable("me", Me)
                scope.SetVariable("this", Me)

                For Each variable In InputStringVariables
                    scope.SetVariable(variable.Key, variable.Value)
                Next

                For Each variable In InputVariables
                    scope.SetVariable(variable.Key, variable.Value)
                Next

                If Not ims1 Is Nothing Then scope.SetVariable("ims1", ims1)
                If Not ims2 Is Nothing Then scope.SetVariable("ims2", ims2)
                If Not ims3 Is Nothing Then scope.SetVariable("ims3", ims3)
                If Not ims4 Is Nothing Then scope.SetVariable("ims4", ims4)
                If Not ims5 Is Nothing Then scope.SetVariable("ims5", ims5)
                If Not ims6 Is Nothing Then scope.SetVariable("ims6", ims6)
                If Not oms1 Is Nothing Then scope.SetVariable("oms1", oms1)
                If Not oms2 Is Nothing Then scope.SetVariable("oms2", oms2)
                If Not oms3 Is Nothing Then scope.SetVariable("oms3", oms3)
                If Not oms4 Is Nothing Then scope.SetVariable("oms4", oms4)
                If Not oms5 Is Nothing Then scope.SetVariable("oms5", oms5)
                If Not oms6 Is Nothing Then scope.SetVariable("oms6", oms6)
                If Not ies1 Is Nothing Then scope.SetVariable("ies1", ies1)
                If Not oes1 Is Nothing Then scope.SetVariable("oes1", oes1)

                Dim source As Microsoft.Scripting.Hosting.ScriptSource = Me.engine.CreateScriptSourceFromString(txtcode, Microsoft.Scripting.SourceCodeKind.Statements)
                Try
                    Me.ErrorMessage = ""
                    source.Execute(Me.scope)
                    OutputVariables.Clear()
                    For Each variable In scope.GetVariableNames
                        If TypeOf scope.GetVariable(variable) Is Double Or TypeOf scope.GetVariable(variable) Is Integer Then OutputVariables.Add(variable, scope.GetVariable(variable))
                    Next
                Catch ex As Exception
                    Dim ops As ExceptionOperations = engine.GetService(Of ExceptionOperations)()
                    Me.ErrorMessage = ops.FormatException(ex).ToString
                    Me.DeCalculate()
                    engine = Nothing
                    scope = Nothing
                    source = Nothing
                    Dim msg = String.Format("Python Script error: {1}", GraphicObject.Tag, ErrorMessage)
                    Throw New Exception(msg)
                Finally
                    engine = Nothing
                    scope = Nothing
                    source = Nothing
                End Try

            Else

                SyncLock Lock

                    Settings.InitializePythonEnvironment()

                    Using Py.GIL

                        Try

                            Dim sys As Object = Py.Import("sys")
                            Dim codeToRedirectOutput As String = "import sys" & vbCrLf + "from io import BytesIO as StringIO" & vbCrLf + "sys.stdout = mystdout = StringIO()" & vbCrLf + "sys.stdout.flush()" & vbCrLf + "sys.stderr = mystderr = StringIO()" & vbCrLf + "sys.stderr.flush()"
                            PythonEngine.RunSimpleString(codeToRedirectOutput)

                            Me.ErrorMessage = ""

                            Dim locals As New PyDict()

                            locals.SetItem("Flowsheet", FlowSheet.ToPython)
                            locals.SetItem("Me", Me.ToPython)
                            locals.SetItem("This", Me.ToPython)
                            locals.SetItem("flowsheet", FlowSheet.ToPython)
                            locals.SetItem("me", Me.ToPython)
                            locals.SetItem("this", Me.ToPython)

                            Dim arg1 As Python.Runtime.PyObject = Nothing

                            For Each variable In InputStringVariables
                                locals.SetItem(variable.Key, variable.Value.ToPython)
                            Next

                            For Each variable In InputVariables
                                locals.SetItem(variable.Key, variable.Value.ToPython)
                            Next

                            If Not ims1 Is Nothing Then locals.SetItem("ims1", ims1.ToPython)
                            If Not ims2 Is Nothing Then locals.SetItem("ims2", ims2.ToPython)
                            If Not ims3 Is Nothing Then locals.SetItem("ims3", ims3.ToPython)
                            If Not ims4 Is Nothing Then locals.SetItem("ims4", ims4.ToPython)
                            If Not ims5 Is Nothing Then locals.SetItem("ims5", ims5.ToPython)
                            If Not ims6 Is Nothing Then locals.SetItem("ims6", ims6.ToPython)
                            If Not oms1 Is Nothing Then locals.SetItem("oms1", oms1.ToPython)
                            If Not oms2 Is Nothing Then locals.SetItem("oms2", oms2.ToPython)
                            If Not oms3 Is Nothing Then locals.SetItem("oms3", oms3.ToPython)
                            If Not oms4 Is Nothing Then locals.SetItem("oms4", oms4.ToPython)
                            If Not oms5 Is Nothing Then locals.SetItem("oms5", oms5.ToPython)
                            If Not oms6 Is Nothing Then locals.SetItem("oms6", oms6.ToPython)
                            If Not ies1 Is Nothing Then locals.SetItem("ies1", ies1.ToPython)
                            If Not oes1 Is Nothing Then locals.SetItem("oes1", oes1.ToPython)

                            PythonEngine.Exec(txtcode, Nothing, locals)

                            If Not GlobalSettings.Settings.IsRunningOnMono() Then
                                FlowSheet.ShowMessage(sys.stdout.getvalue().ToString(), IFlowsheet.MessageType.Information)
                            End If

                            OutputVariables.Clear()
                            Dim i As Integer = 0
                            For Each variable As PyObject In locals.Items
                                Dim val = locals.Values(i).ToString
                                If Double.TryParse(val, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, New Double) Then
                                    OutputVariables.Add(locals.Keys(i).ToString, val.ToDoubleFromInvariant)
                                End If
                                i += 1
                            Next

                        Catch ex As Exception

                            Me.ErrorMessage = ex.Message

                            Me.DeCalculate()

                            Throw New Exception(ex.Message & vbCrLf & ex.StackTrace.Replace("\n", vbCrLf), ex)

                        Finally

                        End Try

                    End Using

                End SyncLock

            End If

            If Not oes1 Is Nothing Then
                oes1.GraphicObject.Calculated = True
            End If

            IObj?.Close()

        End Sub

        ''' <summary>Clears all calculated results.</summary>
        Public Overrides Sub DeCalculate()


        End Sub

        ''' <summary>Validates the unit operation configuration.</summary>
        Public Overrides Sub Validate()
            'MyBase.Validate()
        End Sub

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.ALL
                    proplist.AddRange(Me.OutputVariables.Keys.ToArray.Union(Me.InputVariables.Keys.ToArray).ToArray)
                Case PropertyType.RO
                    proplist.AddRange(Me.OutputVariables.Keys.ToArray)
                Case PropertyType.WR
                    proplist.AddRange(Me.InputVariables.Keys.ToArray)
                Case Else
            End Select
            Return proplist.ToArray(Type.GetType("System.String"))
        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                Return ""
            End If
        End Function

        ''' <summary>Returns the value of the specified property.</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                If Me.OutputVariables.ContainsKey(prop) Then Return Me.OutputVariables(prop)
                If Me.InputVariables.ContainsKey(prop) Then Return Me.InputVariables(prop)
                Return Nothing
            End If

        End Function

        ''' <summary>Sets the value of the specified property.</summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If Me.InputVariables.ContainsKey(prop) Then Me.InputVariables(prop) = propval
            Return Nothing
        End Function

        ''' <summary>Restores the unit operation state from XML, including variable dictionaries.</summary>
        Public Overrides Function LoadData(data As List(Of XElement)) As Boolean

            If InputVariables Is Nothing Then InputVariables = New Dictionary(Of String, Double)
            If OutputVariables Is Nothing Then OutputVariables = New Dictionary(Of String, Double)

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            Me.InputStringVariables.Clear()
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "InputStringVariables").Elements.ToList
                If Not InputStringVariables.ContainsKey(xel.@Key) Then Me.InputStringVariables.Add(xel.@Key, xel.@Value)
            Next

            Me.InputVariables.Clear()
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "InputVariables").Elements.ToList
                If Not InputVariables.ContainsKey(xel.@Key) Then Me.InputVariables.Add(xel.@Key, Double.Parse(xel.@Value, ci))
            Next

            Me.OutputVariables.Clear()
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "OutputVariables").Elements.ToList
                If Not OutputVariables.ContainsKey(xel.@Key) Then Me.OutputVariables.Add(xel.@Key, Double.Parse(xel.@Value, ci))
            Next
            Return True
        End Function

        ''' <summary>Serializes the unit operation state to XML, including variable dictionaries.</summary>
        Public Overrides Function SaveData() As List(Of XElement)

            Dim elements As List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            'With elements
            '    .Add(New XElement("InputStringVariables"))
            '    For Each p In InputStringVariables
            '        .Item(.Count - 1).Add(New XElement("Variable", New XAttribute("Key", p.Key), New XAttribute("Value", p.Value)))
            '    Next
            '    .Add(New XElement("InputVariables"))
            '    For Each p In InputVariables
            '        .Item(.Count - 1).Add(New XElement("Variable", New XAttribute("Key", p.Key), New XAttribute("Value", p.Value.ToString(ci))))
            '    Next
            '    .Add(New XElement("OutputVariables"))
            '    For Each p In OutputVariables
            '        .Item(.Count - 1).Add(New XElement("Variable", New XAttribute("Key", p.Key), New XAttribute("Value", p.Value.ToString(ci))))
            '    Next
            'End With

            Return elements

        End Function

        ''' <summary>
        ''' Opens the script editor window. Does nothing where the WinForms editors are not built.
        ''' </summary>
        Public Sub DisplayScriptEditorForm()

            ShowScriptEditor()

        End Sub

        Partial Private Sub ShowScriptEditor()
        End Sub
        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.python_script.png")

        End Function

        ''' <summary>Returns the localised display description.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("IPUO_Desc")
        End Function

        ''' <summary>Returns the localised display name.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("IPUO_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Generates a plain-text report of the unit operation results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String


            Dim str As New Text.StringBuilder

            Dim istr, ostr As MaterialStream
            istr = Me.GetInletMaterialStream(0)
            ostr = Me.GetOutletMaterialStream(0)

            istr.PropertyPackage.CurrentMaterialStream = istr

            str.AppendLine("Python Script Block: " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Calculation parameters")
            str.AppendLine()
            str.AppendLine("    Python Interpreter: " & ExecutionEngine.ToString())
            str.AppendLine()
            str.AppendLine("Input String Variables")
            str.AppendLine()
            For Each par In InputStringVariables
                str.AppendLine("    " + par.Key + ": " + par.Value)
            Next
            str.AppendLine()
            str.AppendLine("Input Numeric Variables")
            str.AppendLine()
            For Each par In InputVariables
                str.AppendLine("    " + par.Key + ": " + par.Value.ToString(numberformat))
            Next
            str.AppendLine()
            str.AppendLine("Output Variables")
            str.AppendLine()
            For Each par In OutputVariables
                str.AppendLine("    " + par.Key + ": " + par.Value.ToString(numberformat))
            Next


            Return str.ToString

        End Function

    End Class

End Namespace
