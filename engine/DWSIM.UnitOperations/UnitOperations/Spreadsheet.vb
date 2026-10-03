'    Excel/Spreadsheet Unit Calculation Routines 
'    Copyright 2014 Gregor Reichert, 2015 Daniel Wagner
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


#If Not NETCOREAPP Then
Imports Excel = NetOffice.ExcelApi
Imports NetOffice.ExcelApi.Enums
Imports GS = GemBox.Spreadsheet
#End If
Imports XL = OfficeOpenXml
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Interfaces.Enums
Imports System.IO

Namespace UnitOperations.Auxiliary

    ''' <summary>
    ''' Holds the definition of a single input or output parameter exchanged between
    ''' the flowsheet and the external spreadsheet in the <see cref="UnitOperations.ExcelUO"/> unit operation.
    ''' </summary>
    <System.Serializable()> Public Class ExcelParameter
        ''' <summary>The display name of the parameter.</summary>
        Public Name As String = ""
        ''' <summary>The numeric value of the parameter.</summary>
        Public Value As Double = 0.0#
        ''' <summary>The engineering unit string associated with this parameter.</summary>
        Public Unit As String = ""
        ''' <summary>An optional annotation or description of the parameter.</summary>
        Public Annotation As String = ""
    End Class

    ''' <summary>
    ''' Selects what recalculates the workbook of the <see cref="UnitOperations.ExcelUO"/>.
    ''' </summary>
    Public Enum SpreadsheetCalculationEngine
        ''' <summary>Microsoft Excel when it is installed, the internal engine otherwise.</summary>
        Automatic = 0
        ''' <summary>Always Microsoft Excel, through COM automation (Windows only).</summary>
        Excel = 1
        ''' <summary>Always the internal engine (EPPlus), which reads .xlsx and .xlsm workbooks.</summary>
        Internal = 2
    End Enum

End Namespace

Namespace UnitOperations

    ''' <summary>
    ''' Represents an Excel/spreadsheet-based unit operation that reads input parameters from
    ''' the flowsheet, writes them into a spreadsheet file (via Excel COM or GemBox), triggers
    ''' a recalculation, and reads back the computed output parameters into the flowsheet.
    ''' </summary>
    <System.Serializable()> Public Partial Class ExcelUO

        Inherits UnitOpBaseClass

        ''' <summary>Gets or sets the simulation object class category (UserModels).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.UserModels

        ''' <summary>Gets a value indicating this unit operation has no dedicated dynamic-mode properties.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>Gets a value indicating whether this unit operation supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

#If Not NETCOREAPP Then

        Private Declare Function GetWindowThreadProcessId Lib "user32.dll" (ByVal hWnd As Integer, ByRef lpdwProcessId As Integer) As Integer

        Private Function GetExcelProcess(ByVal excelApp As Object) As Process
            Dim id As Integer
            GetWindowThreadProcessId(excelApp.HinstancePtr, id)
            Return Process.GetProcessById(id)
        End Function

        Private Sub TerminateExcelProcess(ByVal excelApp As Object)
            Try
                Dim process = GetExcelProcess(excelApp)
                If (Not (process) Is Nothing) Then
                    process.Kill()
                End If
            Catch ex As System.Runtime.InteropServices.InvalidComObjectException
            End Try
        End Sub

#End If



        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Protected m_DQ As Nullable(Of Double)
        Protected m_FileName As String = ""
        Protected m_InputParams As New Dictionary(Of String, ExcelParameter)
        Protected m_OutputParams As New Dictionary(Of String, ExcelParameter)
        ''' <summary>Indicates whether the spreadsheet parameters have been successfully loaded from the file.</summary>
        Public ParamsLoaded As Boolean = False

        ''' <summary>Gets or sets whether the spreadsheet file is embedded within the simulation file.</summary>
        Public Property FileIsEmbedded As Boolean = False

        ''' <summary>Gets or sets the file name of the embedded spreadsheet.</summary>
        Public Property EmbeddedFileName As String = ""

        ''' <summary>Gets or sets the dictionary of input parameters sent from the flowsheet to the spreadsheet.</summary>
        Public Property InputParams() As Dictionary(Of String, ExcelParameter)
            Get
                Return m_InputParams
            End Get
            Set(value As Dictionary(Of String, ExcelParameter))
                m_InputParams = value
            End Set
        End Property

        ''' <summary>Gets or sets the dictionary of output parameters read back from the spreadsheet to the flowsheet.</summary>
        Public Property OutputParams() As Dictionary(Of String, ExcelParameter)
            Get
                Return m_OutputParams
            End Get
            Set(value As Dictionary(Of String, ExcelParameter))
                m_OutputParams = value
            End Set
        End Property

        ''' <summary>Gets or sets the full file path to the external spreadsheet workbook.</summary>
        Public Property Filename() As String
            Get
                Return m_FileName
            End Get
            Set(ByVal value As String)
                m_FileName = value
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets what recalculates the workbook. Automatic (the default) uses Excel when it
        ''' is installed and the internal engine otherwise.
        ''' </summary>
        Public Property CalculationEngine As SpreadsheetCalculationEngine = SpreadsheetCalculationEngine.Automatic

        ''' <summary>
        ''' Initializes a new instance of the <see cref="ExcelUO"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the spreadsheet unit operation.</param>
        ''' <param name="description">A brief description of the spreadsheet unit operation.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()

            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>Creates a deep copy of this spreadsheet UO via XML serialization.</summary>
        ''' <returns>A new <see cref="ExcelUO"/> instance with the same state.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New ExcelUO()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this spreadsheet UO via JSON serialization.</summary>
        ''' <returns>A new <see cref="ExcelUO"/> instance with the same state.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of ExcelUO)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Gets or sets the calculated energy imbalance / heat duty (kW).</summary>
        Public Property DeltaQ() As Nullable(Of Double)
            Get
                Return m_DQ
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_DQ = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="ExcelUO"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        Private Function ExctractFilepath(ByVal S As String) As String
            Dim P1, P2 As Integer
            P1 = InStr(1, S, "(") + 1
            P2 = InStrRev(S, "\") + 1

            Return Mid(S, P1, P2 - P1)
        End Function

        ''' <summary>Executes a single dynamic model step by delegating to the steady-state <see cref="Calculate"/> routine.</summary>
        Public Overrides Sub RunDynamicModel()

            Calculate()

        End Sub

        ''' <summary>
        ''' Performs the spreadsheet-based calculation: writes input parameters to the workbook,
        ''' triggers recalculation, and reads back output parameters into the flowsheet.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            Dim su = FlowSheet.FlowsheetOptions.SelectedUnitSystem

            If Not FileIsEmbedded Then

                If Not File.Exists(Filename) Then
                    'try to find the file in the current directory.
                    Dim fname = Path.GetFileName(Filename)
                    Dim newpath = Path.Combine(Path.GetDirectoryName(FlowSheet.FilePath), fname)
                    If File.Exists(newpath) Then
                        Filename = Path.GetFullPath(newpath)
                    End If
                End If

                If Not File.Exists(Filename) Then
                    Throw New Exception("Definition file '" & Filename & "' :" & FlowSheet.GetTranslatedString("Oarquivonoexisteoufo"))
                End If

            Else

                If Not FlowSheet.FileDatabaseProvider.CheckIfExists(EmbeddedFileName) Then
                    Throw New Exception("Definition file '" & EmbeddedFileName & "' :" & FlowSheet.GetTranslatedString("Oarquivonoexisteoufo"))
                End If

            End If

            Dim reason As String = ""
            Dim engine = SelectEngine(reason)

            If engine = EngineChoice.Unavailable Then Throw New Exception(reason)

            If engine = EngineChoice.Internal Then
                CalculateWithInternalEngine()
                IObj?.Close()
                Return
            End If

#If Not NETCOREAPP Then

            Dim k, ci, co As Integer

            Dim excelType As Type = Nothing

            If Not Calculator.IsRunningOnMono Then excelType = Type.GetTypeFromProgID("Excel.Application")

            If engine = EngineChoice.ExcelCom And Not excelType Is Nothing Then

                Dim excelProxy As Object = Activator.CreateInstance(excelType)

                Using xcl As New Excel.Application(Nothing, excelProxy)

                    'pid = excelProxy.

                    For Each CurrAddin As Excel.AddIn In xcl.AddIns
                        If CurrAddin.Installed Then
                            CurrAddin.Installed = False
                            CurrAddin.Installed = True
                        End If
                    Next

                    Dim mybook As Excel.Workbook
                    Dim AppPath = Application.StartupPath

                    Dim tmpfile As String = ""

                    If FileIsEmbedded Then
                        tmpfile = Path.ChangeExtension(SharedClasses.Utility.GetTempFileName(), Path.GetExtension(EmbeddedFileName))
                        FlowSheet.FileDatabaseProvider.ExportFile(EmbeddedFileName, tmpfile)
                        'Load Excel definition file
                        mybook = xcl.Workbooks.Open(tmpfile)
                    Else
                        'Load Excel definition file
                        mybook = xcl.Workbooks.Open(Filename)
                    End If

                    'xcl.Visible = True 'uncomment for debugging
                    xcl.Calculation = XlCalculation.xlCalculationManual

                    Dim mysheetIn As Excel.Worksheet = mybook.Sheets("Input")
                    Dim mysheetOut As Excel.Worksheet = mybook.Sheets("Output")
                    '=====================================================================================================

                    'check if at least one input and output connection is available
                    For k = 0 To 3
                        If GraphicObject.InputConnectors(k).IsAttached Then ci += 1
                        If GraphicObject.OutputConnectors(k).IsAttached Then co += 1
                    Next
                    If ci = 0 Or co = 0 Then
                        mybook.Close(saveChanges:=False)
                        xcl.Quit()
                        'xcl.Dispose()
                        'CalculateFlowsheet(FlowSheet, objargs, Nothing)
                        Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
                    End If

                    Dim Ti, Pi, Hi, Wi, T2, P2, H2, Hin, Hout, Win, Wout, MassBal As Double

                    Dim es As Streams.EnergyStream = Nothing

                    If GetInletEnergyStream(4) IsNot Nothing Then
                        es = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(4).AttachedConnector.AttachedFrom.Name)
                    End If

                    Dim ParName As String
                    Dim i As Integer

                    '======= write data to Excel ==============================================================
                    mysheetIn.Range("B5:E8").Value = "" 'delete Name, T, P, H of streams
                    mysheetIn.Range("A12:E150").Value = "" 'delete molar flows of streams

                    '======= write stream names to Excel =========
                    For k = 0 To 3
                        If GraphicObject.InputConnectors(k).IsAttached Then
                            mysheetIn.Cells(5, 2 + k).Value = Me.GraphicObject.InputConnectors(k).AttachedConnector.AttachedFrom.Tag
                        Else
                            mysheetIn.Cells(5, 2 + k).Value = ""
                        End If
                        If GraphicObject.OutputConnectors(k).IsAttached Then
                            mysheetOut.Cells(5, 2 + k).Value = Me.GraphicObject.OutputConnectors(k).AttachedConnector.AttachedTo.Tag
                        Else
                            mysheetOut.Cells(5, 2 + k).Value = ""
                        End If
                    Next

                    '======== write input parameters ============
                    k = 0
                    For Each EP As ExcelParameter In InputParams.Values
                        mysheetIn.Cells(5 + k, 8).Formula = EP.Value
                        k += 1
                    Next

                    '======== Input streams to unit =======
                    Dim S As MaterialStream
                    For k = 0 To 3
                        If Me.GraphicObject.InputConnectors(k).IsAttached Then
                            S = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(k).AttachedConnector.AttachedFrom.Name)
                            Me.PropertyPackage.CurrentMaterialStream = S
                            Ti = S.Phases(0).Properties.temperature.GetValueOrDefault
                            Pi = S.Phases(0).Properties.pressure.GetValueOrDefault
                            Hi = S.Phases(0).Properties.enthalpy.GetValueOrDefault
                            Wi = S.Phases(0).Properties.massflow.GetValueOrDefault
                            Hin += Hi * Wi
                            Win += Wi

                            '======= transfer data to Excel ===========================================================
                            mysheetIn.Cells(6, 2 + k).Value = Ti
                            mysheetIn.Cells(7, 2 + k).Value = Pi
                            mysheetIn.Cells(8, 2 + k).Value = Hi

                            Dim dy As Integer = 0
                            For Each comp As BaseClasses.Compound In S.Phases(0).Compounds.Values
                                mysheetIn.Cells(12 + dy, 1).Value = comp.ConstantProperties.Name
                                mysheetOut.Cells(12 + dy, 1).Value = comp.ConstantProperties.Name
                                mysheetIn.Cells(12 + dy, 2 + k).Value = comp.MolarFlow
                                dy += 1
                            Next
                        Else
                            mysheetIn.Cells(6, 2 + k).Value = ""
                            mysheetIn.Cells(7, 2 + k).Value = ""
                            mysheetIn.Cells(8, 2 + k).Value = ""
                            Dim dy As Integer = 0
                            For Each comp As BaseClasses.Compound In Me.PropertyPackage.CurrentMaterialStream.Phases(0).Compounds.Values
                                mysheetIn.Cells(12 + dy, 1).Value = comp.ConstantProperties.Name
                                mysheetOut.Cells(12 + dy, 1).Value = comp.ConstantProperties.Name
                                mysheetIn.Cells(12 + dy, 2 + k).Value = ""
                                mysheetIn.Cells(12 + dy, 3 + k).Value = ""
                                dy += 1
                            Next
                        End If
                    Next

                    xcl.Calculate()

                    '======= read results from Excel =============================================================
                    Dim Vmol As New Dictionary(Of String, Double)
                    Dim v As Double
                    Dim SMass, SMole As Double

                    For k = 0 To 3 'run through all streams to execute TP-flash
                        If Me.GraphicObject.OutputConnectors(k).IsAttached Then
                            Me.PropertyPackage.CurrentMaterialStream = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(k).AttachedConnector.AttachedTo.Name)

                            T2 = mysheetOut.Cells(6, 2 + k).Value
                            P2 = mysheetOut.Cells(7, 2 + k).Value

                            'Atribuir valores a corrente de materia conectada a jusante
                            With Me.PropertyPackage.CurrentMaterialStream
                                .Phases(0).Properties.temperature = T2
                                .Phases(0).Properties.pressure = P2

                                Dim comp As BaseClasses.Compound
                                i = 0
                                SMole = 0
                                SMass = 0
                                Vmol.Clear()
                                For Each comp In .Phases(0).Compounds.Values
                                    v = mysheetOut.Cells(12 + i, 2 + k).Value
                                    Vmol.Add(comp.Name, v)
                                    SMole += Vmol(comp.Name)
                                    SMass += Vmol(comp.Name) * comp.ConstantProperties.Molar_Weight / 1000
                                    i += 1
                                Next
                                For Each comp In .Phases(0).Compounds.Values
                                    comp.MoleFraction = Vmol(comp.Name) / SMole
                                    comp.MassFraction = Vmol(comp.Name) * comp.ConstantProperties.Molar_Weight / SMass / 1000
                                Next
                                .Phases(0).Properties.massflow = SMass

                                Try
                                    Dim tmp = Me.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, P2, T2, 0)
                                    H2 = tmp.CalculatedEnthalpy
                                    .Phases(0).Properties.enthalpy = H2
                                Catch ex As Exception
                                    mybook.Close(saveChanges:=True)
                                    xcl.Quit()
                                    xcl.Dispose()
                                    Throw New Exception("Flash calculation error")
                                End Try

                                Hout += H2 * SMass
                                Wout += SMass
                            End With

                        End If
                    Next

                    '======= caclculate output stream data ====================================================

                    Dim hfin, hfout As Double

                    k = 0
                    For Each ic In GraphicObject.InputConnectors
                        If ic.IsAttached And ic.Type = GraphicObjects.ConType.ConIn Then
                            hfin += GetInletMaterialStream(k).GetOverallHeatOfFormation()
                        End If
                        k += 1
                    Next

                    k = 0
                    For Each oc In GraphicObject.OutputConnectors
                        If oc.IsAttached Then
                            hfout += GetOutletMaterialStream(k).GetOverallHeatOfFormation()
                        End If
                        k += 1
                    Next

                    Me.DeltaQ = Hout - Hin + hfout - hfin

                    'energy stream - update energy flow value (kW)
                    If es IsNot Nothing Then
                        With es
                            .EnergyFlow = Me.DeltaQ.GetValueOrDefault
                            .GraphicObject.Calculated = True
                        End With
                    End If

                    '======== read input/output parameters from Excel table =========================================
                    k = 0
                    InputParams.Clear()
                    Do
                        Dim ExlPar As New ExcelParameter

                        ParName = mysheetIn.Cells(5 + k, 7).Value
                        If ParName <> "" Then
                            ExlPar.Name = ParName
                            Try
                                ExlPar.Value = mysheetIn.Cells(5 + k, 8).Value
                            Catch ex As Exception
                                ExlPar.Value = Nothing
                            End Try

                            ExlPar.Unit = mysheetIn.Cells(5 + k, 9).Value
                            ExlPar.Annotation = mysheetIn.Cells(5 + k, 10).Value
                            InputParams.Add(ExlPar.Name, ExlPar)

                            k += 1
                        End If
                    Loop While ParName <> ""

                    k = 0
                    OutputParams.Clear()
                    Do
                        Dim ExlPar As New ExcelParameter

                        ParName = mysheetOut.Cells(5 + k, 7).Value
                        If ParName <> "" Then
                            ExlPar.Name = ParName
                            Try
                                ExlPar.Value = mysheetOut.Cells(5 + k, 8).Value
                            Catch ex As Exception
                                ExlPar.Value = Nothing
                            End Try
                            ExlPar.Unit = mysheetOut.Cells(5 + k, 9).Value
                            ExlPar.Annotation = mysheetOut.Cells(5 + k, 10).Value
                            OutputParams.Add(ExlPar.Name, ExlPar)

                            k += 1
                        End If
                    Loop While ParName <> ""
                    ParamsLoaded = True

                    mybook.Close(saveChanges:=True)
                    xcl.Quit()

                    MassBal = 100 * (Wout - Win) / (Win)
                    If Math.Abs(MassBal) > 0.001 Then
                        FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": " & "Mass balance error: " & MassBal & "%", IFlowsheet.MessageType.GeneralError)
                    End If

                    If File.Exists(tmpfile) Then
                        Try
                            File.Delete(tmpfile)
                        Catch ex As Exception
                        End Try
                    End If

                End Using

                TerminateExcelProcess(excelProxy)

            Else

                'use GemBox to read and write data

                GS.SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")

                Dim xcl As GS.ExcelFile = Nothing

                Dim AppPath = Application.StartupPath

                Dim tmpfile As String = ""

                If FileIsEmbedded Then
                    tmpfile = Path.ChangeExtension(SharedClasses.Utility.GetTempFileName(), Path.GetExtension(EmbeddedFileName))
                    FlowSheet.FileDatabaseProvider.ExportFile(EmbeddedFileName, tmpfile)
                    'Load Excel definition file
                    xcl = GS.ExcelFile.Load(tmpfile)
                Else
                    'Load Excel definition file
                    If My.Computer.FileSystem.FileExists(Filename) Then
                        xcl = GS.ExcelFile.Load(Filename)
                    Else
                        Throw New Exception("Definition file '" & Filename & "' :" & FlowSheet.GetTranslatedString("Oarquivonoexisteoufo"))
                    End If
                End If

                Dim mysheetIn As GS.ExcelWorksheet = xcl.Worksheets("Input")
                Dim mysheetOut As GS.ExcelWorksheet = xcl.Worksheets("Output")
                '=====================================================================================================

                'check if at least one input and output connection is available
                For k = 0 To 3
                    If GraphicObject.InputConnectors(k).IsAttached Then ci += 1
                    If GraphicObject.OutputConnectors(k).IsAttached Then co += 1
                Next
                If ci = 0 Or co = 0 Then
                    Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
                End If

                Dim Ti, Pi, Hi, Wi, T2, P2, H2, Hin, Hout, Win, Wout, MassBal As Double
                Dim es As Streams.EnergyStream = Nothing
                If GetInletEnergyStream(4) IsNot Nothing Then
                    es = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(4).AttachedConnector.AttachedFrom.Name)
                End If
                Dim ParName As String
                Dim i As Integer

                '======= write data to Excel ==============================================================
                mysheetIn.Cells.GetSubrange("B5", "E8").Value = "" 'delete Name, T, P, H of streams
                mysheetIn.Cells.GetSubrange("A12", "E150").Value = "" 'delete molar flows of streams

                '======= write stream names to Excel =========
                For k = 0 To 3
                    If GraphicObject.InputConnectors(k).IsAttached Then
                        mysheetIn.Cells(4, 1 + k).Value = Me.GraphicObject.InputConnectors(k).AttachedConnector.AttachedFrom.Tag
                    Else
                        mysheetIn.Cells(4, 1 + k).Value = ""
                    End If
                    If GraphicObject.OutputConnectors(k).IsAttached Then
                        mysheetOut.Cells(4, 1 + k).Value = Me.GraphicObject.OutputConnectors(k).AttachedConnector.AttachedTo.Tag
                    Else
                        mysheetOut.Cells(4, 1 + k).Value = ""
                    End If
                Next

                '======== write input parameters ============
                k = 0
                For Each EP As ExcelParameter In InputParams.Values
                    mysheetIn.Cells(4 + k, 7).Formula = EP.Value
                    k += 1
                Next

                '======== Input streams to unit =======
                Dim S As MaterialStream
                For k = 0 To 3
                    If Me.GraphicObject.InputConnectors(k).IsAttached Then
                        S = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(k).AttachedConnector.AttachedFrom.Name)
                        Me.PropertyPackage.CurrentMaterialStream = S
                        Ti = S.Phases(0).Properties.temperature.GetValueOrDefault
                        Pi = S.Phases(0).Properties.pressure.GetValueOrDefault
                        Hi = S.Phases(0).Properties.enthalpy.GetValueOrDefault
                        Wi = S.Phases(0).Properties.massflow.GetValueOrDefault
                        Hin += Hi * Wi
                        Win += Wi

                        '======= transfer data to Excel ===========================================================
                        mysheetIn.Cells(5, 1 + k).Value = Ti
                        mysheetIn.Cells(6, 1 + k).Value = Pi
                        mysheetIn.Cells(7, 1 + k).Value = Hi

                        Dim dy As Integer = 0
                        For Each comp As BaseClasses.Compound In S.Phases(0).Compounds.Values
                            mysheetIn.Cells(11 + dy, 0).Value = comp.ConstantProperties.Name
                            mysheetOut.Cells(11 + dy, 0).Value = comp.ConstantProperties.Name
                            mysheetIn.Cells(11 + dy, 1 + k).Value = comp.MolarFlow.GetValueOrDefault
                            dy += 1
                        Next
                    Else
                        mysheetIn.Cells(5, 1 + k).Value = ""
                        mysheetIn.Cells(6, 1 + k).Value = ""
                        mysheetIn.Cells(7, 1 + k).Value = ""
                        Dim dy As Integer = 0
                        For Each comp As BaseClasses.Compound In Me.PropertyPackage.CurrentMaterialStream.Phases(0).Compounds.Values
                            mysheetIn.Cells(11 + dy, 0).Value = comp.ConstantProperties.Name
                            mysheetOut.Cells(11 + dy, 0).Value = comp.ConstantProperties.Name
                            mysheetIn.Cells(11 + dy, 1 + k).Value = ""
                            mysheetIn.Cells(11 + dy, 2 + k).Value = ""
                            dy += 1
                        Next
                    End If
                Next

                'open spreadsheet to be calculated manually by the user.

                If FileIsEmbedded Then
                    xcl.Save(tmpfile)
                Else
                    xcl.Save(Filename)
                End If

                If Calculator.IsRunningOnMono Then
                    If GlobalSettings.Settings.RunningPlatform = Settings.Platform.Linux Then
                        Dim p As New Process()
                        With p
                            .StartInfo.FileName = "xdg-open"
                            .StartInfo.Arguments = Filename
                            .StartInfo.UseShellExecute = False
                            .Start()
                            MessageBox.Show("Click 'OK' once the spreadsheet formula updating process is finished.")
                        End With
                    Else 'macOS
                        Dim p As New Process()
                        With p
                            .StartInfo.FileName = "open"
                            .StartInfo.Arguments = Filename
                            .StartInfo.UseShellExecute = False
                            .Start()
                            MessageBox.Show("Click 'OK' once the spreadsheet formula updating process is finished.")
                        End With
                    End If
                Else
                    Process.Start(Filename)
                    MessageBox.Show("Click 'OK' once the spreadsheet formula updating process is finished.")
                End If

                If FileIsEmbedded Then
                    'Load Excel definition file
                    xcl = GS.ExcelFile.Load(tmpfile)
                Else
                    'Load Excel definition file
                    xcl = GS.ExcelFile.Load(Filename)
                End If

                mysheetIn = xcl.Worksheets("Input")
                mysheetOut = xcl.Worksheets("Output")

                '======= read results from sheet =============================================================
                Dim Vmol As New Dictionary(Of String, Double)
                Dim v As Double
                Dim SMass, SMole As Double

                For k = 0 To 3 'run through all streams to execute TP-flash
                    If Me.GraphicObject.OutputConnectors(k).IsAttached Then
                        Me.PropertyPackage.CurrentMaterialStream = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(k).AttachedConnector.AttachedTo.Name)

                        T2 = mysheetOut.Cells(5, 1 + k).Value
                        P2 = mysheetOut.Cells(6, 1 + k).Value

                        'Atribuir valores a corrente de materia conectada a jusante
                        With Me.PropertyPackage.CurrentMaterialStream
                            .Phases(0).Properties.temperature = T2
                            .Phases(0).Properties.pressure = P2

                            Dim comp As BaseClasses.Compound
                            i = 0
                            SMole = 0
                            SMass = 0
                            Vmol.Clear()
                            For Each comp In .Phases(0).Compounds.Values
                                v = mysheetOut.Cells(11 + i, 1 + k).Value
                                Vmol.Add(comp.Name, v)
                                SMole += Vmol(comp.Name)
                                SMass += Vmol(comp.Name) * comp.ConstantProperties.Molar_Weight / 1000
                                i += 1
                            Next
                            For Each comp In .Phases(0).Compounds.Values
                                comp.MoleFraction = Vmol(comp.Name) / SMole
                                comp.MassFraction = Vmol(comp.Name) * comp.ConstantProperties.Molar_Weight / SMass / 1000
                            Next
                            .Phases(0).Properties.massflow = SMass
                            .DefinedFlow = FlowSpec.Mass

                            Try
                                IObj?.SetCurrent()
                                Dim tmp = Me.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, P2, T2, 0)
                                H2 = tmp.CalculatedEnthalpy
                                .Phases(0).Properties.enthalpy = H2
                            Catch ex As Exception
                                Throw New Exception("Flash calculation error")
                            End Try

                            Hout += H2 * SMass
                            Wout += SMass
                        End With

                    End If
                Next

                '======= caclculate output stream data ====================================================

                Dim hfin, hfout As Double

                k = 0
                For Each ic In GraphicObject.InputConnectors
                    If ic.IsAttached And ic.Type = GraphicObjects.ConType.ConIn Then
                        hfin += GetInletMaterialStream(k).GetOverallHeatOfFormation()
                    End If
                    k += 1
                Next

                k = 0
                For Each oc In GraphicObject.OutputConnectors
                    If oc.IsAttached Then
                        hfout += GetOutletMaterialStream(k).GetOverallHeatOfFormation()
                    End If
                    k += 1
                Next

                Me.DeltaQ = Hout - Hin + hfout - hfin

                'energy stream - update energy flow value (kW)
                If es IsNot Nothing Then
                    With es
                        .EnergyFlow = Me.DeltaQ.GetValueOrDefault
                        .GraphicObject.Calculated = True
                    End With
                End If

                '======== read output parameters from Excel table =========================================
                k = 0
                OutputParams.Clear()
                Do
                    Dim ExlPar As New ExcelParameter

                    ParName = mysheetOut.Cells(4 + k, 6).Value
                    If ParName <> "" Then
                        ExlPar.Name = ParName
                        ExlPar.Value = mysheetOut.Cells(4 + k, 7).Value
                        ExlPar.Unit = mysheetOut.Cells(4 + k, 8).Value
                        ExlPar.Annotation = mysheetOut.Cells(4 + k, 9).Value
                        OutputParams.Add(ExlPar.Name, ExlPar)

                        k += 1
                    End If
                Loop While ParName <> ""

                MassBal = 100 * (Wout - Win) / (Win)
                If Math.Abs(MassBal) > 0.001 Then
                    FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": " & "Mass balance error: " & MassBal & "%", IFlowsheet.MessageType.GeneralError)
                End If

                If File.Exists(tmpfile) Then
                    Try
                        File.Delete(tmpfile)
                    Catch ex As Exception
                    End Try
                End If

            End If

#End If

            IObj?.Close()

        End Sub

        ''' <summary>Clears all calculated results.</summary>
        Public Overrides Sub DeCalculate()

            Dim k As Integer

            For k = 0 To 3
                If Me.GraphicObject.OutputConnectors(k).IsAttached Then

                    'Zerar valores da corrente de materia conectada a jusante
                    With Me.GetOutletMaterialStream(k)
                        .Phases(0).Properties.temperature = Nothing
                        .Phases(0).Properties.pressure = Nothing
                        .Phases(0).Properties.enthalpy = Nothing
                        .Phases(0).Properties.molarfraction = 1
                        .Phases(0).Properties.massfraction = 1
                        Dim comp As BaseClasses.Compound
                        Dim i As Integer = 0
                        For Each comp In .Phases(0).Compounds.Values
                            comp.MoleFraction = 0
                            comp.MassFraction = 0
                            i += 1
                        Next
                        .Phases(0).Properties.massflow = Nothing
                        .Phases(0).Properties.molarflow = Nothing
                        .GraphicObject.Calculated = False
                    End With

                End If
            Next

            'energy stream - update energy flow value (kW)
            If Me.GraphicObject.EnergyConnector.IsAttached Then
                With Me.GetEnergyStream
                    .EnergyFlow = Nothing
                    .GraphicObject.Calculated = False
                End With
            End If

        End Sub

        ''' <summary>Reads input and output parameter definitions from the embedded Excel worksheet.</summary>
        Public Sub ReadExcelParams()

            'read input and output parameters from associated Excel table 

            If Not ParamsLoaded Then

                If FileIsEmbedded Then

                    If Not FlowSheet.FileDatabaseProvider.CheckIfExists(EmbeddedFileName) Then Exit Sub

                Else

                    If Not File.Exists(Filename) Then Exit Sub

                End If

                Dim reason As String = ""
                Dim engine = SelectEngine(reason)

                If engine = EngineChoice.Unavailable Then Exit Sub

                If engine = EngineChoice.Internal Then
                    Using pk = OpenInternalWorkbook()
                        'a workbook written by a library carries no cached results, so recalculate before reading
                        Try
                            ExpandExponentLiterals(pk)
                            XL.CalculationExtension.Calculate(pk.Workbook)
                        Catch ex As Exception
                        End Try
                        ReadInternalParameters(pk)
                    End Using
                    ParamsLoaded = True
                    Exit Sub
                End If

#If Not NETCOREAPP Then

                Dim excelType As Type = Nothing

                If Not Calculator.IsRunningOnMono Then excelType = Type.GetTypeFromProgID("Excel.Application")

                If engine = EngineChoice.ExcelCom And Not excelType Is Nothing Then

                    Dim excelProxy As Object = Activator.CreateInstance(excelType)

                    Using xcl As New Excel.Application(Nothing, excelProxy)

                        For Each CurrAddin As Excel.AddIn In xcl.AddIns
                            If CurrAddin.Installed Then
                                CurrAddin.Installed = False
                                CurrAddin.Installed = True
                            End If
                        Next

                        Dim mybook As Excel.Workbook
                        Dim AppPath = Application.StartupPath
                        Dim ParName As String
                        Dim i As Integer

                        Dim tmpfile As String = ""

                        If FileIsEmbedded Then

                            tmpfile = Path.ChangeExtension(SharedClasses.Utility.GetTempFileName(), Path.GetExtension(EmbeddedFileName))
                            FlowSheet.FileDatabaseProvider.ExportFile(EmbeddedFileName, tmpfile)
                            'Load Excel definition file
                            mybook = xcl.Workbooks.Open(tmpfile, True, True)

                        Else

                            'Load Excel definition file
                            mybook = xcl.Workbooks.Open(Filename, True, True)

                        End If

                        Dim mysheetIn As Excel.Worksheet = mybook.Sheets("Input")
                        Dim mysheetOut As Excel.Worksheet = mybook.Sheets("Output")

                        'xcl.Visible = True 'uncomment for debugging

                        InputParams.Clear()
                        i = 0
                        Do
                            Dim ExlPar As New ExcelParameter

                            ParName = mysheetIn.Cells(5 + i, 7).Value
                            If ParName <> "" Then
                                ExlPar.Name = ParName
                                Try
                                    ExlPar.Value = mysheetIn.Cells(5 + i, 8).Value
                                Catch ex As Exception
                                    ExlPar.Value = Nothing
                                End Try

                                ExlPar.Unit = mysheetIn.Cells(5 + i, 9).Value
                                ExlPar.Annotation = mysheetIn.Cells(5 + i, 10).Value
                                InputParams.Add(ExlPar.Name, ExlPar)

                                i += 1
                            End If
                        Loop While ParName <> ""

                        OutputParams.Clear()
                        i = 0
                        Do
                            Dim ExlPar As New ExcelParameter

                            ParName = mysheetOut.Cells(5 + i, 7).Value
                            If ParName <> "" Then
                                ExlPar.Name = ParName
                                Try
                                    ExlPar.Value = mysheetOut.Cells(5 + i, 8).Value
                                Catch ex As Exception
                                    ExlPar.Value = Nothing
                                End Try

                                ExlPar.Unit = mysheetOut.Cells(5 + i, 9).Value
                                ExlPar.Annotation = mysheetOut.Cells(5 + i, 10).Value
                                OutputParams.Add(ExlPar.Name, ExlPar)

                                i += 1
                            End If
                        Loop While ParName <> ""

                        mybook.Close(saveChanges:=False)

                        xcl.Quit()

                        ParamsLoaded = True

                        If File.Exists(tmpfile) Then
                            Try
                                File.Delete(tmpfile)
                            Catch ex As Exception
                            End Try
                        End If

                    End Using

                    TerminateExcelProcess(excelProxy)

                Else

                    'use GemBox to read and write data

                    GS.SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")

                    Dim xcl As GS.ExcelFile = Nothing

                    Dim AppPath = Application.StartupPath
                    Dim ParName As String
                    Dim i As Integer

                    Dim tmpfile As String = ""

                    If FileIsEmbedded Then
                        tmpfile = Path.ChangeExtension(SharedClasses.Utility.GetTempFileName(), Path.GetExtension(EmbeddedFileName))
                        FlowSheet.FileDatabaseProvider.ExportFile(EmbeddedFileName, tmpfile)
                        'Load Excel definition file
                        xcl = GS.ExcelFile.Load(tmpfile)
                    Else
                        'Load Excel definition file
                        xcl = GS.ExcelFile.Load(Filename)
                    End If

                    Dim mysheetIn As GS.ExcelWorksheet = xcl.Worksheets("Input")
                    Dim mysheetOut As GS.ExcelWorksheet = xcl.Worksheets("Output")

                    InputParams.Clear()
                    i = 0
                    Do
                        Dim ExlPar As New ExcelParameter

                        ParName = mysheetIn.Cells(4 + i, 6).Value
                        If ParName <> "" Then
                            ExlPar.Name = ParName
                            ExlPar.Value = mysheetIn.Cells(4 + i, 7).Value
                            ExlPar.Unit = mysheetIn.Cells(4 + i, 8).Value
                            ExlPar.Annotation = mysheetIn.Cells(4 + i, 9).Value
                            InputParams.Add(ExlPar.Name, ExlPar)

                            i += 1
                        End If
                    Loop While ParName <> ""

                    OutputParams.Clear()
                    i = 0
                    Do
                        Dim ExlPar As New ExcelParameter

                        ParName = mysheetOut.Cells(4 + i, 6).Value
                        If ParName <> "" Then
                            ExlPar.Name = ParName
                            Try
                                ExlPar.Value = mysheetOut.Cells(4 + i, 7).Value
                            Catch ex As Exception
                                ExlPar.Value = Nothing
                            End Try
                            ExlPar.Unit = mysheetOut.Cells(4 + i, 8).Value
                            ExlPar.Annotation = mysheetOut.Cells(4 + i, 9).Value
                            OutputParams.Add(ExlPar.Name, ExlPar)

                            i += 1
                        End If
                    Loop While ParName <> ""

                    ParamsLoaded = True

                    If File.Exists(tmpfile) Then
                        Try
                            File.Delete(tmpfile)
                        Catch ex As Exception
                        End Try
                    End If

                End If

#End If

            End If

        End Sub

        Private Enum EngineChoice
            ExcelCom
            Internal
            GemBoxRoundTrip
            Unavailable
        End Enum

        ''' <summary>
        ''' Picks the engine for the current settings and workbook format. Automatic keeps the
        ''' Excel path whenever Excel is installed, so existing flowsheets behave as before.
        ''' </summary>
        Private Function SelectEngine(ByRef reason As String) As EngineChoice

            Dim workbook = If(FileIsEmbedded, EmbeddedFileName, Filename)
            Dim ext = Path.GetExtension(If(workbook, "")).ToLowerInvariant()
            Dim openXml = (ext = ".xlsx" Or ext = ".xlsm")
            Dim formatReason = "The internal spreadsheet engine reads only .xlsx and .xlsm workbooks (" & Path.GetFileName(workbook) & ")."

#If NETCOREAPP Then
            Dim excelInstalled = False
#Else
            Dim excelInstalled = Not Calculator.IsRunningOnMono AndAlso Type.GetTypeFromProgID("Excel.Application") IsNot Nothing
#End If

            Select Case CalculationEngine
                Case SpreadsheetCalculationEngine.Excel
                    If excelInstalled Then Return EngineChoice.ExcelCom
                    reason = "The calculation engine is set to Excel, and Excel is not available. Set it to Automatic or Internal."
                    Return EngineChoice.Unavailable
                Case SpreadsheetCalculationEngine.Internal
                    If openXml Then Return EngineChoice.Internal
                    reason = formatReason
                    Return EngineChoice.Unavailable
                Case Else
                    If excelInstalled Then Return EngineChoice.ExcelCom
                    If openXml Then Return EngineChoice.Internal
#If NETCOREAPP Then
                    reason = formatReason
                    Return EngineChoice.Unavailable
#Else
                    Return EngineChoice.GemBoxRoundTrip
#End If
            End Select

        End Function

        Private Function OpenInternalWorkbook() As XL.ExcelPackage

            If FileIsEmbedded Then
                Using ms = FlowSheet.FileDatabaseProvider.GetFileStream(EmbeddedFileName)
                    ms.Position = 0
                    Return New XL.ExcelPackage(ms)
                End Using
            Else
                Using fs As New FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    Return New XL.ExcelPackage(fs)
                End Using
            End If

        End Function

        Private Shared Function CellText(ws As XL.ExcelWorksheet, row As Integer, col As Integer) As String
            Return Convert.ToString(ws.Cells(row, col).Value, Globalization.CultureInfo.InvariantCulture)
        End Function

        ''' <summary>
        ''' Reads a cell as a number. Empty cells read as zero, as they do through Excel. Error values
        ''' and text that is not a number return False.
        ''' </summary>
        Private Shared Function TryCellNumber(ws As XL.ExcelWorksheet, row As Integer, col As Integer, ByRef value As Double) As Boolean
            Dim v = ws.Cells(row, col).Value
            value = 0.0
            If v Is Nothing Then Return True
            If TypeOf v Is XL.ExcelErrorValue Then
                value = Double.NaN
                Return False
            End If
            If TypeOf v Is String Then
                Dim s = DirectCast(v, String).Trim()
                If s = "" Then Return True
                If Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value) Then Return True
                value = Double.NaN
                Return False
            End If
            If TypeOf v Is Boolean Then
                value = If(DirectCast(v, Boolean), 1.0, 0.0)
                Return True
            End If
            value = Convert.ToDouble(v, Globalization.CultureInfo.InvariantCulture)
            Return True
        End Function

        Private Function CellNumberOrThrow(ws As XL.ExcelWorksheet, row As Integer, col As Integer) As Double
            Dim value As Double
            If Not TryCellNumber(ws, row, col, value) Then
                Dim c = ws.Cells(row, col)
                Throw New Exception(ws.Name & "!" & c.Address & If(c.Formula <> "", " (=" & c.Formula & ")", "") &
                                    " evaluated to '" & Convert.ToString(c.Value, Globalization.CultureInfo.InvariantCulture) &
                                    "' in the internal spreadsheet engine. The formula may use a function the internal engine does not implement; use Excel or rewrite the formula.")
            End If
            Return value
        End Function

        Private Function ReadInternalParameterList(ws As XL.ExcelWorksheet, failed As List(Of String)) As Dictionary(Of String, ExcelParameter)
            Dim list As New Dictionary(Of String, ExcelParameter)
            Dim k As Integer = 0
            Do
                Dim parName = CellText(ws, 5 + k, 7)
                If parName = "" Then Exit Do
                Dim par As New ExcelParameter With {.Name = parName}
                If Not TryCellNumber(ws, 5 + k, 8, par.Value) Then failed?.Add(ws.Name & "!H" & (5 + k).ToString() & " (" & parName & ")")
                par.Unit = CellText(ws, 5 + k, 9)
                par.Annotation = CellText(ws, 5 + k, 10)
                list.Add(par.Name, par)
                k += 1
            Loop
            Return list
        End Function

        Private Sub ReadInternalParameters(pk As XL.ExcelPackage, Optional failed As List(Of String) = Nothing)
            InputParams = ReadInternalParameterList(pk.Workbook.Worksheets("Input"), failed)
            OutputParams = ReadInternalParameterList(pk.Workbook.Worksheets("Output"), failed)
        End Sub

        ''' <summary>
        ''' Excel keeps a number typed in a formula with an exponent (1.380649E-23) in that form, and the
        ''' EPPlus parser does not read exponents. Rewrites those literals as plain decimals.
        ''' </summary>
        Private Shared Sub ExpandExponentLiterals(pk As XL.ExcelPackage)
            For Each ws In pk.Workbook.Worksheets
                If ws.Dimension Is Nothing Then Continue For
                Dim changes As New List(Of Tuple(Of String, String))
                For Each c In ws.Cells(ws.Dimension.Address)
                    Dim f = c.Formula
                    If f <> "" AndAlso (f.Contains("E") Or f.Contains("e")) Then
                        Dim nf = ExpandExponentLiterals(f)
                        If nf <> f Then changes.Add(Tuple.Create(c.Address, nf))
                    End If
                Next
                For Each change In changes
                    ws.Cells(change.Item1).Formula = change.Item2
                Next
            Next
        End Sub

        Private Shared ReadOnly ExponentLiteral As New Text.RegularExpressions.Regex("(?<![A-Za-z0-9_.$!:])(\d+\.?\d*|\.\d+)[eE]([+-]?\d+)(?![A-Za-z0-9_(.])")

        Friend Shared Function ExpandExponentLiterals(formula As String) As String
            'leave text inside double quotes (strings) and single quotes (sheet names) alone
            Dim sb As New Text.StringBuilder
            Dim seg As New Text.StringBuilder
            Dim quote As Char = Nothing
            For Each ch In formula
                If quote <> Nothing Then
                    sb.Append(ch)
                    If ch = quote Then quote = Nothing
                ElseIf ch = """"c Or ch = "'"c Then
                    sb.Append(ExponentLiteral.Replace(seg.ToString(), AddressOf ExpandExponentMatch))
                    seg.Clear()
                    sb.Append(ch)
                    quote = ch
                Else
                    seg.Append(ch)
                End If
            Next
            sb.Append(ExponentLiteral.Replace(seg.ToString(), AddressOf ExpandExponentMatch))
            Return sb.ToString()
        End Function

        Private Shared Function ExpandExponentMatch(m As Text.RegularExpressions.Match) As String
            Dim mantissa = m.Groups(1).Value
            Dim exponent = Integer.Parse(m.Groups(2).Value, Globalization.CultureInfo.InvariantCulture)
            Dim dot = mantissa.IndexOf("."c)
            Dim digits = mantissa.Replace(".", "")
            Dim point = If(dot < 0, mantissa.Length, dot) + exponent
            Dim result As String
            If point <= 0 Then
                result = "0." & New String("0"c, -point) & digits
            ElseIf point >= digits.Length Then
                result = digits & New String("0"c, point - digits.Length)
            Else
                result = digits.Substring(0, point) & "." & digits.Substring(point)
            End If
            If result.Contains(".") Then result = result.TrimEnd("0"c).TrimEnd("."c)
            result = result.TrimStart("0"c)
            If result = "" Then Return "0"
            If result.StartsWith(".") Then result = "0" & result
            Return result
        End Function

        ''' <summary>
        ''' Same exchange as the Excel path, with EPPlus loading the workbook and recalculating its
        ''' formulas: inlet streams go into the Input sheet, outlet streams come from the same cells of
        ''' the Output sheet. The workbook file is left untouched.
        ''' </summary>
        Private Sub CalculateWithInternalEngine()

            Dim k, ci, co As Integer

            For k = 0 To 3
                If GraphicObject.InputConnectors(k).IsAttached Then ci += 1
                If GraphicObject.OutputConnectors(k).IsAttached Then co += 1
            Next
            If ci = 0 Or co = 0 Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            Dim es As Streams.EnergyStream = Nothing
            If GetInletEnergyStream(4) IsNot Nothing Then
                es = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(4).AttachedConnector.AttachedFrom.Name)
            End If

            Using pk = OpenInternalWorkbook()

                Dim mysheetIn = pk.Workbook.Worksheets("Input")
                Dim mysheetOut = pk.Workbook.Worksheets("Output")

                If mysheetIn Is Nothing Or mysheetOut Is Nothing Then
                    Throw New Exception("The workbook needs an 'Input' and an 'Output' sheet.")
                End If

                Dim Ti, Pi, Hi, Wi, T2, P2, H2, Hin, Hout, Win, Wout, MassBal As Double

                '======= write data to the sheet ======================================================
                mysheetIn.Cells("B5:E8").Value = Nothing 'delete Name, T, P, H of streams
                mysheetIn.Cells("A12:E150").Value = Nothing 'delete molar flows of streams

                For k = 0 To 3
                    If GraphicObject.InputConnectors(k).IsAttached Then
                        mysheetIn.Cells(5, 2 + k).Value = Me.GraphicObject.InputConnectors(k).AttachedConnector.AttachedFrom.Tag
                    Else
                        mysheetIn.Cells(5, 2 + k).Value = Nothing
                    End If
                    If GraphicObject.OutputConnectors(k).IsAttached Then
                        mysheetOut.Cells(5, 2 + k).Value = Me.GraphicObject.OutputConnectors(k).AttachedConnector.AttachedTo.Tag
                    Else
                        mysheetOut.Cells(5, 2 + k).Value = Nothing
                    End If
                Next

                k = 0
                For Each EP As ExcelParameter In InputParams.Values
                    mysheetIn.Cells(5 + k, 8).Value = EP.Value
                    k += 1
                Next

                Dim S As MaterialStream
                For k = 0 To 3
                    If Me.GraphicObject.InputConnectors(k).IsAttached Then
                        S = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(k).AttachedConnector.AttachedFrom.Name)
                        Me.PropertyPackage.CurrentMaterialStream = S
                        Ti = S.Phases(0).Properties.temperature.GetValueOrDefault
                        Pi = S.Phases(0).Properties.pressure.GetValueOrDefault
                        Hi = S.Phases(0).Properties.enthalpy.GetValueOrDefault
                        Wi = S.Phases(0).Properties.massflow.GetValueOrDefault
                        Hin += Hi * Wi
                        Win += Wi

                        mysheetIn.Cells(6, 2 + k).Value = Ti
                        mysheetIn.Cells(7, 2 + k).Value = Pi
                        mysheetIn.Cells(8, 2 + k).Value = Hi

                        Dim dy As Integer = 0
                        For Each comp As BaseClasses.Compound In S.Phases(0).Compounds.Values
                            mysheetIn.Cells(12 + dy, 1).Value = comp.ConstantProperties.Name
                            mysheetOut.Cells(12 + dy, 1).Value = comp.ConstantProperties.Name
                            mysheetIn.Cells(12 + dy, 2 + k).Value = If(comp.MolarFlow.HasValue, CObj(comp.MolarFlow.Value), Nothing)
                            dy += 1
                        Next
                    End If
                Next

                '======= recalculate ==================================================================
                ExpandExponentLiterals(pk)
                Try
                    XL.CalculationExtension.Calculate(pk.Workbook)
                Catch ex As XL.FormulaParsing.Exceptions.CircularReferenceException
                    Throw New Exception("The workbook has a circular reference, which the internal spreadsheet engine does not iterate. Use Excel or remove the circular reference.", ex)
                End Try

                '======= read results =================================================================
                Dim Vmol As New Dictionary(Of String, Double)
                Dim v As Double
                Dim SMass, SMole As Double
                Dim i As Integer

                For k = 0 To 3
                    If Me.GraphicObject.OutputConnectors(k).IsAttached Then
                        Me.PropertyPackage.CurrentMaterialStream = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(k).AttachedConnector.AttachedTo.Name)

                        T2 = CellNumberOrThrow(mysheetOut, 6, 2 + k)
                        P2 = CellNumberOrThrow(mysheetOut, 7, 2 + k)

                        With Me.PropertyPackage.CurrentMaterialStream
                            .Phases(0).Properties.temperature = T2
                            .Phases(0).Properties.pressure = P2

                            Dim comp As BaseClasses.Compound
                            i = 0
                            SMole = 0
                            SMass = 0
                            Vmol.Clear()
                            For Each comp In .Phases(0).Compounds.Values
                                v = CellNumberOrThrow(mysheetOut, 12 + i, 2 + k)
                                Vmol.Add(comp.Name, v)
                                SMole += Vmol(comp.Name)
                                SMass += Vmol(comp.Name) * comp.ConstantProperties.Molar_Weight / 1000
                                i += 1
                            Next
                            For Each comp In .Phases(0).Compounds.Values
                                comp.MoleFraction = Vmol(comp.Name) / SMole
                                comp.MassFraction = Vmol(comp.Name) * comp.ConstantProperties.Molar_Weight / SMass / 1000
                            Next
                            .Phases(0).Properties.massflow = SMass

                            Try
                                Dim tmp = Me.PropertyPackage.CalculateEquilibrium2(FlashCalculationType.PressureTemperature, P2, T2, 0)
                                H2 = tmp.CalculatedEnthalpy
                                .Phases(0).Properties.enthalpy = H2
                            Catch ex As Exception
                                Throw New Exception("Flash calculation error")
                            End Try

                            Hout += H2 * SMass
                            Wout += SMass
                        End With

                    End If
                Next

                Dim hfin, hfout As Double

                k = 0
                For Each ic In GraphicObject.InputConnectors
                    If ic.IsAttached And ic.Type = GraphicObjects.ConType.ConIn Then
                        hfin += GetInletMaterialStream(k).GetOverallHeatOfFormation()
                    End If
                    k += 1
                Next

                k = 0
                For Each oc In GraphicObject.OutputConnectors
                    If oc.IsAttached Then
                        hfout += GetOutletMaterialStream(k).GetOverallHeatOfFormation()
                    End If
                    k += 1
                Next

                Me.DeltaQ = Hout - Hin + hfout - hfin

                If es IsNot Nothing Then
                    With es
                        .EnergyFlow = Me.DeltaQ.GetValueOrDefault
                        .GraphicObject.Calculated = True
                    End With
                End If

                Dim failed As New List(Of String)
                ReadInternalParameters(pk, failed)
                ParamsLoaded = True

                If failed.Count > 0 Then
                    FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": " & "parameters that did not evaluate to a number in the internal spreadsheet engine: " & String.Join(", ", failed), IFlowsheet.MessageType.Warning)
                End If

                MassBal = 100 * (Wout - Win) / (Win)
                If Math.Abs(MassBal) > 0.001 Then
                    FlowSheet.ShowMessage(Me.GraphicObject.Tag & ": " & "Mass balance error: " & MassBal & "%", IFlowsheet.MessageType.GeneralError)
                End If

            End Using

        End Sub

        ''' <summary>Returns the value of the specified property.</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As Double = 0

                Dim propType As String = prop.Split("_")(0)
                Dim propID As String = prop.Split("_")(1)

                Select Case propType
                    Case "Calc"
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, DeltaQ.GetValueOrDefault)
                    Case "In"
                        If InputParams.ContainsKey(propID) Then value = InputParams(propID).Value
                    Case "Out"
                        If OutputParams.ContainsKey(propID) Then value = OutputParams(propID).Value
                End Select

                Return value
            End If

        End Function

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()

            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)

            Select Case proptype
                Case PropertyType.RO
                    proplist.Add("Calc_dQ")
                    For Each P As ExcelParameter In OutputParams.Values
                        proplist.Add("Out_" + P.Name)
                    Next
                Case PropertyType.RW
                    proplist.Add("Calc_dQ")
                    For Each P As ExcelParameter In OutputParams.Values
                        proplist.Add("Out_" + P.Name)
                    Next
                    For Each P As ExcelParameter In InputParams.Values
                        proplist.Add("In_" + P.Name)
                    Next
                Case PropertyType.WR
                    For Each P As ExcelParameter In InputParams.Values
                        proplist.Add("In_" + P.Name)
                    Next
                Case PropertyType.ALL
                    proplist.Add("Calc_dQ")
                    For Each P As ExcelParameter In InputParams.Values
                        proplist.Add("In_" + P.Name)
                    Next
                    For Each P As ExcelParameter In OutputParams.Values
                        proplist.Add("Out_" + P.Name)
                    Next
            End Select

            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>Sets the value of the specified property.</summary>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter

            Dim propType As String = prop.Split("_")(0)
            Dim propID As String = prop.Split("_")(1)

            Select Case propType
                Case "Calc"
                    DeltaQ = SystemsOfUnits.Converter.ConvertToSI(su.heatflow, propval)
                Case "In"
                    If InputParams.ContainsKey(propID) Then InputParams(propID).Value = propval
                Case "Out"
                    If OutputParams.ContainsKey(propID) Then OutputParams(propID).Value = propval
            End Select

            Return 1
        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As String = ""

                Dim propType As String = prop.Split("_")(0)
                Dim propID As String = prop.Split("_")(1)

                Select Case propType
                    Case "Calc"
                        value = su.heatflow
                    Case "In"
                        Return "" 'If InputParams.ContainsKey(propID) Then value = InputParams(propID).Unit
                    Case "Out"
                        Return "" 'If OutputParams.ContainsKey(propID) Then value = OutputParams(propID).Unit
                End Select

                Return value
            End If
        End Function

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.table.png")

        End Function

        ''' <summary>Returns the localised display description.</summary>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("EXLUO_Desc")
        End Function

        ''' <summary>Returns the localised display name.</summary>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("EXLUO_Name")
        End Function

        ''' <summary>Gets a value indicating whether this unit operation is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Generates a plain-text report of the spreadsheet results.</summary>
        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String


            Dim str As New Text.StringBuilder

            Dim istr, ostr As MaterialStream
            istr = Me.GetInletMaterialStream(0)
            ostr = Me.GetOutletMaterialStream(0)

            istr.PropertyPackage.CurrentMaterialStream = istr

            str.AppendLine("Spreadsheet Block: " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Calculation parameters")
            str.AppendLine()
            str.AppendLine("    Spreadsheet Path: " & Filename)
            str.AppendLine()
            str.AppendLine("Input Parameters")
            str.AppendLine()
            For Each par In InputParams.Values
                str.AppendLine("    " + par.Name + ": " + par.Value.ToString(numberformat) + " " + par.Unit)
            Next
            str.AppendLine()
            str.AppendLine("Output Parameters")
            str.AppendLine()
            For Each par In OutputParams.Values
                str.AppendLine("    " + par.Name + ": " + par.Value.ToString(numberformat) + " " + par.Unit)
            Next

            Return str.ToString

        End Function

    End Class

End Namespace
