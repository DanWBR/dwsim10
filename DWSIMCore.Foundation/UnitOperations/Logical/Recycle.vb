Imports DWSIMCore.Foundation.Enums
Imports DWSIMCore.Foundation.SpecialOps.Helpers.Recycle
Imports DWSIMCore.Foundation.Streams

Namespace SpecialOps

    <System.Serializable()> Public Class Recycle

        Inherits UnitOperations.SpecialOpBaseClass

        Implements IRecycle

        Protected m_ConvPar As Helpers.Recycle.ConvergenceParameters
        Protected m_ConvHist As IRecycleConvergenceHistory
        Protected m_AccelMethod As AccelMethod = AccelMethod.None
        Protected m_WegPars As Helpers.Recycle.WegsteinParameters

        Protected m_MaxIterations As Integer = 50
        Protected m_IterationCount As Integer = 0
        Protected m_InternalCounterT As Integer = 0
        Protected m_InternalCounterP As Integer = 0
        Protected m_InternalCounterW As Integer = 0
        Protected m_IterationsTaken As Integer = 0

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        Public Property Converged As Boolean = False Implements IRecycle.Converged

        Public Property CopyOnStreamDataError As Boolean = False

        Protected m_Errors As New Dictionary(Of String, Double)
        Protected m_Values As New Dictionary(Of String, Double)

        Public ReadOnly Property Errors As Dictionary(Of String, Double) Implements IRecycle.Errors
            Get
                Return m_Errors
            End Get
        End Property

        Public ReadOnly Property Values As Dictionary(Of String, Double) Implements IRecycle.Values
            Get
                Return m_Values
            End Get
        End Property

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Recycle()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Recycle)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            MyBase.LoadData(data)

            Dim xel As XElement

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "WegPars").SingleOrDefault

            If Not xel Is Nothing Then
                m_WegPars.AccelDelay = Double.Parse(xel.@AccelDelay, ci)
                m_WegPars.AccelFreq = Double.Parse(xel.@AccelFreq, ci)
                m_WegPars.Qmax = Double.Parse(xel.@Qmax, ci)
                m_WegPars.Qmin = Double.Parse(xel.@Qmin, ci)
            End If
            Return True
        End Function

        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                .Add(New XElement("WegPars", New XAttribute("AccelDelay", m_WegPars.AccelDelay),
                                  New XAttribute("AccelFreq", m_WegPars.AccelFreq),
                                  New XAttribute("Qmax", m_WegPars.Qmax),
                                  New XAttribute("Qmin", m_WegPars.Qmin)))
            End With

            Return elements

        End Function

        Public Property IterationsTaken() As Integer
            Get
                Return m_IterationsTaken
            End Get
            Set(ByVal value As Integer)
                m_IterationsTaken = value
            End Set
        End Property

        Public Property IterationCount() As Integer
            Get
                Return m_IterationCount
            End Get
            Set(ByVal value As Integer)
                m_IterationCount = value
            End Set
        End Property

        Public Property WegsteinParameters() As Helpers.Recycle.WegsteinParameters
            Get
                Return m_WegPars
            End Get
            Set(ByVal value As Helpers.Recycle.WegsteinParameters)
                m_WegPars = value
            End Set
        End Property

        Public Property AccelerationMethod() As AccelMethod Implements IRecycle.AccelerationMethod
            Get
                Return m_AccelMethod
            End Get
            Set(ByVal value As AccelMethod)
                m_AccelMethod = value
            End Set
        End Property

        Public Property ConvergenceParameters() As Helpers.Recycle.ConvergenceParameters
            Get
                Return m_ConvPar
            End Get
            Set(ByVal value As Helpers.Recycle.ConvergenceParameters)
                m_ConvPar = value
            End Set
        End Property

        Public Property ConvergenceHistory() As IRecycleConvergenceHistory Implements IRecycle.ConvergenceHistory
            Get
                Return m_ConvHist
            End Get
            Set(ByVal value As IRecycleConvergenceHistory)
                m_ConvHist = value
            End Set
        End Property

        Public Property MaximumIterations() As Integer
            Get
                Return Me.m_MaxIterations
            End Get
            Set(ByVal value As Integer)
                Me.m_MaxIterations = value
            End Set
        End Property

        Public Sub New()

            MyBase.CreateNew()

            m_ConvPar = New ConvergenceParameters
            m_ConvHist = New ConvergenceHistory
            m_WegPars = New WegsteinParameters

        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()

            m_ConvPar = New ConvergenceParameters
            m_ConvHist = New ConvergenceHistory
            m_WegPars = New WegsteinParameters

            Me.ComponentName = name
            Me.ComponentDescription = description



        End Sub

        Public Sub SetOutletStreamProperties() Implements IRecycle.SetOutletStreamProperties

            Dim msfrom, msto As MaterialStream

            If Me.GraphicObject.InputConnectors(0).IsAttached Then
                msfrom = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)
            Else
                msfrom = Nothing
            End If

            If Me.GraphicObject.OutputConnectors(0).IsAttached Then
                msto = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
                With msto

                    .PropertyPackage.CurrentMaterialStream = msto
                    .Phases(0).Properties.temperature = Values("Temperature")
                    .Phases(0).Properties.pressure = Values("Pressure")
                    .Phases(0).Properties.massflow = Values("MassFlow")
                    .Phases(0).Properties.enthalpy = Values("Enthalpy")

                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = msfrom.Phases(0).Compounds(comp.Name).MoleFraction.GetValueOrDefault
                    Next

                    .CalcOverallCompMassFractions()

                    .AtEquilibrium = False

                End With
            End If


        End Sub

        Public Overrides Sub RunDynamicModel()

            Dim msfrom, msto As MaterialStream
            msfrom = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)

            If Not msfrom.Calculated And Not msfrom.AtEquilibrium Then
                Throw New Exception(FlowSheet.GetTranslatedString("RecycleStreamNotCalculated"))
            End If

            msto = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
            Dim prevspec = msto.SpecType
            msto.Assign(msfrom)
            msto.AssignProps(msfrom)
            msto.SpecType = prevspec
            msto.AtEquilibrium = False

        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As InspectorItem = Host.GetNewInspectorItem()

            Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("The Recycle operation is composed by a block 
                                in the flowsheet which does convergence verifications in systems 
                                were downstream material connects somewhere upstream in the 
                                diagram. With this tool it is possible to build complex 
                                flowsheets, with many recycles, and solve them in an efficient 
                                way by using the acceleration methods presents in this logical 
                                operation.")

            IObj?.Paragraphs.Add("There are two acceleration methods available: Wegstein and 
                                Dominant Eigenvalue. The Wegstein method must be used when there 
                                isn't a significant interaction between convergent variables, in 
                                the contrary the other method can be used. The successive 
                                substitution method is slow, but convergence is guaranteed.")

            IObj?.Paragraphs.Add("The Wegstein method requires some parameters which can be edited 
                                by the user. The dominant eigenvalue does not require any 
                                additional parameter. The user can define convergence parameters 
                                for temperature, pressure and mass flow in the recycle, that is, 
                                the minimum acceptable values for the difference in these values 
                                between the inlet and outlet streams, which, rigorously, must be 
                                identical. The smaller these values are, the more time is used by 
                                the calculator in order to converge the recycle.")

            IObj?.Paragraphs.Add("As a result, the actual error values are shown, together with the 
                                necessary convergence iteration steps.")

            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Nohcorrentedematriac7"))
            ElseIf Not Me.GraphicObject.InputConnectors(0).IsAttached Then
                Throw New Exception(FlowSheet.GetTranslatedString("Verifiqueasconexesdo"))
            End If

            Dim Tnew, Pnew, Wnew, Hnew, Snew As Double

            Dim ems As MaterialStream = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)
            Dim oms As MaterialStream = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)

            Dim v1, v2 As Double()
            v1 = ems.Phases(0).Compounds.Values.Select(Function(x) x.MassFlow.GetValueOrDefault).ToArray
            v2 = oms.Phases(0).Compounds.Values.Select(Function(x) x.MassFlow.GetValueOrDefault).ToArray

            Dim Wsum As Double = v1.Sum
            Dim Wsum2 As Double = v2.Sum
            Dim Werr As Double = 0.0#
            Dim i As Integer
            For i = 0 To v1.Length - 1
                If v1(i) <> 0.0# Then
                    Werr += (v1(i) - v2(i)) / v1(i) * (v2(i) / Wsum2)
                Else
                    Werr += (v1(i) - v2(i)) * (v2(i) / Wsum2)
                End If
            Next

            With ems.Phases(0).Properties

                Me.ConvergenceHistory.TemperaturaE0 = Me.ConvergenceHistory.TemperaturaE
                Me.ConvergenceHistory.PressaoE0 = Me.ConvergenceHistory.PressaoE
                Me.ConvergenceHistory.VazaoMassicaE0 = Me.ConvergenceHistory.VazaoMassicaE

                Me.ConvergenceHistory.TemperaturaE = .temperature.GetValueOrDefault - oms.Phases(0).Properties.temperature.GetValueOrDefault
                Me.ConvergenceHistory.PressaoE = .pressure.GetValueOrDefault - oms.Phases(0).Properties.pressure.GetValueOrDefault
                Me.ConvergenceHistory.VazaoMassicaE = Werr

                Me.ConvergenceHistory.Temperatura0 = Me.ConvergenceHistory.Temperatura
                Me.ConvergenceHistory.Pressao0 = Me.ConvergenceHistory.Pressao
                Me.ConvergenceHistory.VazaoMassica0 = Me.ConvergenceHistory.VazaoMassica

                Me.ConvergenceHistory.Temperatura = .temperature.GetValueOrDefault
                Me.ConvergenceHistory.Pressao = .pressure.GetValueOrDefault
                Me.ConvergenceHistory.VazaoMassica = Wsum

                Hnew = .enthalpy.GetValueOrDefault
                Snew = .entropy.GetValueOrDefault

                If Me.Errors.Count = 0 Then
                    Me.Errors.Add("Temperature", .temperature.GetValueOrDefault)
                    Me.Errors.Add("Pressure", .pressure.GetValueOrDefault)
                    Me.Errors.Add("MassFlow", Wsum)
                    Me.Errors.Add("Enthalpy", .enthalpy.GetValueOrDefault)
                Else
                    Me.Errors("Temperature") = Me.Values("Temperature") - .temperature.GetValueOrDefault
                    Me.Errors("Pressure") = Me.Values("Pressure") - .pressure.GetValueOrDefault
                    Me.Errors("MassFlow") = Werr
                    Me.Errors("Enthalpy") = Me.Values("Enthalpy") - .enthalpy.GetValueOrDefault
                End If

            End With

            With oms.Phases(0).Properties

                If Me.Values.Count = 0 Then
                    Me.Values.Add("Temperature", .temperature.GetValueOrDefault)
                    Me.Values.Add("Pressure", .pressure.GetValueOrDefault)
                    Me.Values.Add("MassFlow", Wsum2)
                    Me.Values.Add("Enthalpy", .enthalpy.GetValueOrDefault)
                Else
                    Me.Values("Temperature") = .temperature.GetValueOrDefault
                    Me.Values("Pressure") = .pressure.GetValueOrDefault
                    Me.Values("MassFlow") = Wsum2
                    Me.Values("Enthalpy") = .enthalpy.GetValueOrDefault
                End If

            End With

            If Me.IterationCount <= 3 Then

                Tnew = Me.ConvergenceHistory.Temperatura
                Pnew = Me.ConvergenceHistory.Pressao
                Wnew = Me.ConvergenceHistory.VazaoMassica

            Else

                Select Case Me.AccelerationMethod

                    Case AccelMethod.None

                        Tnew = Me.ConvergenceHistory.Temperatura
                        Pnew = Me.ConvergenceHistory.Pressao
                        Wnew = Me.ConvergenceHistory.VazaoMassica

                    Case AccelMethod.Wegstein

                        If Me.WegsteinParameters.AccelDelay <= Me.IterationCount + 3 Then

                            Dim sT, sP, sW As Double
                            Dim qT, qP, qW As Double
                            sT = (Me.ConvergenceHistory.TemperaturaE - Me.ConvergenceHistory.TemperaturaE0) / (Me.ConvergenceHistory.Temperatura - Me.ConvergenceHistory.Temperatura0)
                            sP = (Me.ConvergenceHistory.PressaoE - Me.ConvergenceHistory.PressaoE0) / (Me.ConvergenceHistory.Pressao - Me.ConvergenceHistory.Pressao0)
                            sW = (Me.ConvergenceHistory.VazaoMassicaE - Me.ConvergenceHistory.VazaoMassicaE0) / (Me.ConvergenceHistory.VazaoMassica - Me.ConvergenceHistory.VazaoMassica0)
                            qT = sT / (sT - 1)
                            qP = sP / (sP - 1)
                            qW = sW / (sW - 1)
                            If Me.WegsteinParameters.AccelFreq <= Me.m_InternalCounterT And Double.IsNaN(sT) = False And qT > Me.WegsteinParameters.Qmin And qT < Me.WegsteinParameters.Qmax Then
                                Tnew = Me.ConvergenceHistory.TemperaturaE * (1 - qT) + Me.ConvergenceHistory.Temperatura * qT
                                Me.m_InternalCounterT = 0
                            Else
                                Tnew = Me.ConvergenceHistory.Temperatura
                                Me.m_InternalCounterT += 1
                            End If
                            If Me.WegsteinParameters.AccelFreq <= Me.m_InternalCounterP And Double.IsNaN(sP) = False And qP > Me.WegsteinParameters.Qmin And qP < Me.WegsteinParameters.Qmax Then
                                Pnew = Me.ConvergenceHistory.PressaoE * (1 - qP) + Me.ConvergenceHistory.Pressao * qP
                                Me.m_InternalCounterP = 0
                            Else
                                Pnew = Me.ConvergenceHistory.Pressao
                                Me.m_InternalCounterP += 1
                            End If
                            If Me.WegsteinParameters.AccelFreq <= Me.m_InternalCounterW And Double.IsNaN(sW) = False And qW > Me.WegsteinParameters.Qmin And qW < Me.WegsteinParameters.Qmax Then
                                Wnew = Me.ConvergenceHistory.VazaoMassicaE * (1 - qW) + Me.ConvergenceHistory.VazaoMassica * qW
                                Me.m_InternalCounterW = 0
                            Else
                                Wnew = Me.ConvergenceHistory.VazaoMassica
                                Me.m_InternalCounterW += 1
                            End If

                        Else

                            Tnew = Me.ConvergenceHistory.Temperatura
                            Pnew = Me.ConvergenceHistory.Pressao
                            Wnew = Me.ConvergenceHistory.VazaoMassica

                        End If

                    Case AccelMethod.Dominant_Eigenvalue

                        Dim eT(1), eP(1), eW(1), M As Double

                        eT(0) = Me.ConvergenceHistory.TemperaturaE0
                        eT(1) = Me.ConvergenceHistory.TemperaturaE
                        eP(0) = Me.ConvergenceHistory.PressaoE0
                        eP(1) = Me.ConvergenceHistory.PressaoE
                        eW(0) = Me.ConvergenceHistory.VazaoMassicaE0
                        eW(1) = Me.ConvergenceHistory.VazaoMassicaE

                        Dim ve0 As Double() = New Double() {Math.Abs(eT(0)), Math.Abs(eP(0)), Math.Abs(eW(0))}
                        Dim ve1 As Double() = New Double() {Math.Abs(eT(1)), Math.Abs(eP(1)), Math.Abs(eW(1))}

                        M = MAX(ve1) / MAX(ve0)

                        With Me.ConvergenceHistory
                            If Double.IsNaN(M) = False Then
                                Tnew = .Temperatura0 + (.Temperatura - .Temperatura0) / (1 - M)
                                Pnew = .Pressao0 + (.Pressao - .Pressao0) / (1 - M)
                                Wnew = .VazaoMassica0 + (.VazaoMassica - .VazaoMassica0) / (1 - M)
                            Else
                                Tnew = .Temperatura
                                Pnew = .Pressao
                                Wnew = .VazaoMassica
                            End If
                        End With

                End Select

            End If

            Dim copydata As Boolean = True

            ems.PropertyPackage.CurrentMaterialStream = ems

            If Me.CopyOnStreamDataError Then
                copydata = True
            Else
                If Not Tnew.IsValid Or Not Pnew.IsValid Or Not Wnew.IsValid Or Not ems.PropertyPackage.RET_VMOL(PropertyPackages.Phase.Mixture).Sum.IsValid Then copydata = False
            End If

            If Not Me.AccelerationMethod = AccelMethod.GlobalBroyden And copydata Then

                Dim msfrom, msto As MaterialStream
                msfrom = FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name)

                If Not msfrom.Calculated And Not msfrom.AtEquilibrium Then
                    Throw New Exception(FlowSheet.GetTranslatedString("RecycleStreamNotCalculated"))
                End If

                msto = FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
                Dim prevspec = msto.SpecType
                msto.Assign(msfrom)
                msto.AssignProps(msfrom)
                msto.SpecType = prevspec
                msto.AtEquilibrium = False

            End If

            If Me.IterationCount >= Me.MaximumIterations Then
                Me.IterationCount = 0
                Throw New TimeoutException(FlowSheet.GetTranslatedString("RecycleMaxItsReached"))
            End If

            If Math.Abs(Me.ConvergenceHistory.TemperaturaE) > Me.ConvergenceParameters.Temperatura Or
                Math.Abs(Me.ConvergenceHistory.PressaoE) > Me.ConvergenceParameters.Pressao Or
                Math.Abs(Me.ConvergenceHistory.VazaoMassicaE) > Me.ConvergenceParameters.VazaoMassica Then

                Me.Converged = False

            Else

                If Me.IterationCount <> 0 Then Me.IterationsTaken = Me.IterationCount
                Me.IterationCount = 0

                Me.Converged = True

            End If

            Me.IterationCount += 1

            IObj?.Close()

        End Sub

        Public Overloads Sub DeCalculate()

            Me.IterationCount = 0

        End Sub

        Function MAX(ByVal Vv As Object)

            Dim n = UBound(Vv)
            Dim mx As Double

            If n >= 1 Then
                Dim i As Integer = 1
                mx = Vv(i - 1)
                i = 0
                Do
                    If Vv(i) > mx Then
                        mx = Vv(i)
                    End If
                    i += 1
                Loop Until i = n + 1
                Return mx
            Else
                Return Vv(0)
            End If

        End Function

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As IUnitsOfMeasure = Nothing) As Object
            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As Double = 0
                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Select Case propidx

                    Case 0
                        'PROP_RY_0	Maximum Iterations
                        value = Me.MaximumIterations
                    Case 1
                        'PROP_RY_1	Mass Flow Tolerance
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.massflow, Me.ConvergenceParameters.VazaoMassica)
                    Case 2
                        'PROP_RY_2	Temperature Tolerance
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.ConvergenceParameters.Temperatura)
                    Case 3
                        'PROP_RY_3	Pressure Tolerance
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.ConvergenceParameters.Pressao)
                    Case 4
                        'PROP_RY_4	Mass Flow Error
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.massflow, Me.ConvergenceHistory.VazaoMassicaE)
                    Case 5
                        'PROP_RY_5	Temperature Error
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaT, Me.ConvergenceHistory.TemperaturaE)
                    Case 6
                        'PROP_RY_6	Pressure Error
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.ConvergenceHistory.PressaoE)
                End Select

                Return value
            End If


        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RO
                    For i = 4 To 6
                        proplist.Add("PROP_RY_" + CStr(i))
                    Next
                Case PropertyType.RW
                    For i = 0 To 3
                        proplist.Add("PROP_RY_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 3
                        proplist.Add("PROP_RY_" + CStr(i))
                    Next
                Case PropertyType.ALL
                    For i = 0 To 6
                        proplist.Add("PROP_RY_" + CStr(i))
                    Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

            Select Case propidx

                Case 0
                    'PROP_RY_0	Maximum Iterations
                    Me.MaximumIterations = propval
                Case 1
                    'PROP_RY_1	Mass Flow Tolerance
                    Me.ConvergenceParameters.VazaoMassica = SystemsOfUnits.Converter.ConvertToSI(su.massflow, propval)
                Case 2
                    'PROP_RY_2	Temperature Tolerance
                    Me.ConvergenceParameters.Temperatura = SystemsOfUnits.Converter.ConvertToSI(su.deltaT, propval)
                Case 3
                    'PROP_RY_3	Pressure Tolerance
                    Me.ConvergenceParameters.Pressao = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)

            End Select
            Return 1
        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As String = ""
                Dim propidx As Integer = Convert.ToInt32(prop.Split("_")(2))

                Select Case propidx

                    Case 0
                        'PROP_RY_0	Maximum Iterations
                        value = ""
                    Case 1
                        'PROP_RY_1	Mass Flow Tolerance
                        value = su.massflow
                    Case 2
                        'PROP_RY_2	Temperature Tolerance
                        value = su.deltaT
                    Case 3
                        'PROP_RY_3	Pressure Tolerance
                        value = su.deltaP
                    Case 4
                        'PROP_RY_4	Mass Flow Error
                        value = su.massflow
                    Case 5
                        'PROP_RY_5	Temperature Error
                        value = su.deltaT
                    Case 6
                        'PROP_RY_6	Pressure Error
                        value = su.deltaP
                End Select

                Return value
            End If
        End Function

        Public Overrides Function GetIconBitmap() As Object
            Using imgstream As IO.Stream = System.Reflection.Assembly.GetAssembly(Me.GetType).GetManifestResourceStream("DWSIMCore.Foundation.lo_recy_32.png")
                Using bitmap = SkiaSharp.SKBitmap.Decode(imgstream)
                    Return SkiaSharp.SKImage.FromBitmap(bitmap)
                End Using
            End Using
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Recycle"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Recycle"
        End Function


        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Namespace

Namespace SpecialOps.Helpers.Recycle

    Public Enum FlashType
        None
        FlashTP
        FlashPH
        FlashPS
    End Enum

    <System.Serializable()> Public Class ConvergenceParameters

        Implements ICustomXMLSerialization

        Public Temperatura As Double = 0.1
        Public Pressao As Double = 0.1
        Public VazaoMassica As Double = 0.01
        Public FracaoVapor As Double = 0.01
        Public Entalpia As Double = 1
        Public Entropia As Double = 0.01
        Public Composicao As Double = 0.001

        Sub New()

        End Sub

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            XMLSerializer.Deserialize(Me, data, True)
            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Return XMLSerializer.Serialize(Me, True)

        End Function

    End Class

    <System.Serializable()> Public Class ConvergenceHistory

        Implements ICustomXMLSerialization, IRecycleConvergenceHistory

        Sub New()

        End Sub

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements ICustomXMLSerialization.LoadData

            XMLSerializer.Deserialize(Me, data, True)
            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements ICustomXMLSerialization.SaveData

            Return XMLSerializer.Serialize(Me, True)

        End Function

        Public Property Entalpia As Double Implements IRecycleConvergenceHistory.Entalpia

        Public Property Entalpia0 As Double Implements IRecycleConvergenceHistory.Entalpia0

        Public Property EntalpiaE As Double Implements IRecycleConvergenceHistory.EntalpiaE

        Public Property EntalpiaE0 As Double Implements IRecycleConvergenceHistory.EntalpiaE0

        Public Property Entropia As Double Implements IRecycleConvergenceHistory.Entropia

        Public Property Entropia0 As Double Implements IRecycleConvergenceHistory.Entropia0

        Public Property EntropiaE As Double Implements IRecycleConvergenceHistory.EntropiaE

        Public Property EntropiaE0 As Double Implements IRecycleConvergenceHistory.EntropiaE0

        Public Property Pressao As Double Implements IRecycleConvergenceHistory.Pressao

        Public Property Pressao0 As Double Implements IRecycleConvergenceHistory.Pressao0

        Public Property PressaoE As Double Implements IRecycleConvergenceHistory.PressaoE

        Public Property PressaoE0 As Double Implements IRecycleConvergenceHistory.PressaoE0

        Public Property Temperatura As Double Implements IRecycleConvergenceHistory.Temperatura

        Public Property Temperatura0 As Double Implements IRecycleConvergenceHistory.Temperatura0

        Public Property TemperaturaE As Double Implements IRecycleConvergenceHistory.TemperaturaE

        Public Property TemperaturaE0 As Double Implements IRecycleConvergenceHistory.TemperaturaE0

        Public Property VazaoMassica As Double Implements IRecycleConvergenceHistory.VazaoMassica

        Public Property VazaoMassica0 As Double Implements IRecycleConvergenceHistory.VazaoMassica0

        Public Property VazaoMassicaE As Double Implements IRecycleConvergenceHistory.VazaoMassicaE

        Public Property VazaoMassicaE0 As Double Implements IRecycleConvergenceHistory.VazaoMassicaE0

    End Class

    <System.Serializable()> Public Class WegsteinParameters

        Public AccelFreq As Integer = 4
        Public Qmax As Double = 0
        Public Qmin As Double = -20
        Public AccelDelay = 2

    End Class

End Namespace

