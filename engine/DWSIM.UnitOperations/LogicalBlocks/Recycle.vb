'    Recycle Calculation Routines 
'    Copyright 2008 Daniel Wagner O. de Medeiros
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
Imports DWSIM.UnitOperations.SpecialOps.Helpers.Recycle

Namespace SpecialOps

    ''' <summary>
    ''' Represents a material recycle convergence block that iterates stream conditions until
    ''' inlet and outlet properties match within the specified tolerances.
    ''' Supports Wegstein, Dominant Eigenvalue, and successive substitution acceleration methods.
    ''' </summary>
    <System.Serializable()> Public Partial Class Recycle

        Inherits UnitOperations.SpecialOpBaseClass

        Implements Interfaces.IRecycle

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Protected m_ConvPar As Helpers.Recycle.ConvergenceParameters
        Protected m_ConvHist As IRecycleConvergenceHistory
        Protected m_AccelMethod As AccelMethod = AccelMethod.None
        Protected m_WegPars As Helpers.Recycle.WegsteinParameters

        ' The last pass, kept for Wegstein's secant and the dominant-eigenvalue error ratio: the compound
        ' mass flows the inlet brought (g) and the outlet carried (x), and the norm of the relative error.
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private m_PrevInletFlows As Double() = Nothing
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private m_PrevOutletFlows As Double() = Nothing
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private m_PrevErrorNorm As Double = 0.0
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private m_PrevPassAccelerated As Boolean = False

        Protected m_MaxIterations As Integer = 50
        Protected m_IterationCount As Integer = 0
        Protected m_InternalCounterT As Integer = 0
        Protected m_InternalCounterP As Integer = 0
        Protected m_InternalCounterW As Integer = 0
        Protected m_IterationsTaken As Integer = 0

        ''' <summary>Gets a value indicating whether this recycle block supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this recycle block exposes properties for dynamic mode.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>Gets or sets whether the recycle loop has converged to within the specified tolerances.</summary>
        Public Property Converged As Boolean = False Implements Interfaces.IRecycle.Converged

        ''' <summary>Gets or sets whether stream data should be copied even when an error occurs reading stream properties.</summary>
        Public Property CopyOnStreamDataError As Boolean = False

        Protected m_Errors As New Dictionary(Of String, Double)
        Protected m_Values As New Dictionary(Of String, Double)

        ''' <summary>Gets or sets the damping factor applied to variable updates during convergence iterations (1.0 = no damping).</summary>
        Public Property SmoothingFactor As Double = 1.0

        ''' <summary>Gets or sets whether to use the legacy convergence algorithm.</summary>
        Public Property LegacyMode As Boolean = True

        ''' <summary>Gets the dictionary of current convergence errors keyed by variable name (e.g., "Temperature", "Pressure").</summary>
        Public ReadOnly Property Errors As Dictionary(Of String, Double) Implements Interfaces.IRecycle.Errors
            Get
                Return m_Errors
            End Get
        End Property

        ''' <summary>Gets the dictionary of the last calculated stream values keyed by variable name (e.g., "Temperature", "MassFlow").</summary>
        Public ReadOnly Property Values As Dictionary(Of String, Double) Implements Interfaces.IRecycle.Values
            Get
                Return m_Values
            End Get
        End Property

        ''' <summary>Creates a deep copy of this recycle block via XML serialization.</summary>
        ''' <returns>A new <see cref="Recycle"/> instance with the same data.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Recycle()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this recycle block via JSON serialization.</summary>
        ''' <returns>A new <see cref="Recycle"/> instance with the same data.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Recycle)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>
        ''' Restores the recycle block state from a list of XML elements.
        ''' </summary>
        ''' <param name="data">The list of <see cref="XElement"/> objects containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
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

        ''' <summary>
        ''' Serializes the recycle block state to a list of XML elements for persistence.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
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

        ''' <summary>Gets or sets the total number of iterations taken during the last convergence run.</summary>
        Public Property IterationsTaken() As Integer
            Get
                Return m_IterationsTaken
            End Get
            Set(ByVal value As Integer)
                m_IterationsTaken = value
            End Set
        End Property

        ''' <summary>Gets or sets the current iteration counter during an active convergence run.</summary>
        Public Property IterationCount() As Integer
            Get
                Return m_IterationCount
            End Get
            Set(ByVal value As Integer)
                m_IterationCount = value
            End Set
        End Property

        ''' <summary>Gets or sets the Wegstein acceleration parameters used during convergence.</summary>
        Public Property WegsteinParameters() As Helpers.Recycle.WegsteinParameters
            Get
                Return m_WegPars
            End Get
            Set(ByVal value As Helpers.Recycle.WegsteinParameters)
                m_WegPars = value
            End Set
        End Property

        ''' <summary>Gets or sets the convergence acceleration method (None, Wegstein, Dominant_Eigenvalue or GlobalBroyden).</summary>
        Public Property AccelerationMethod() As AccelMethod Implements Interfaces.IRecycle.AccelerationMethod
            Get
                Return m_AccelMethod
            End Get
            Set(ByVal value As AccelMethod)
                m_AccelMethod = value
            End Set
        End Property

        ''' <summary>Gets or sets the convergence tolerance parameters (temperature, pressure, mass flow, etc.).</summary>
        Public Property ConvergenceParameters() As Helpers.Recycle.ConvergenceParameters
            Get
                Return m_ConvPar
            End Get
            Set(ByVal value As Helpers.Recycle.ConvergenceParameters)
                m_ConvPar = value
            End Set
        End Property

        ''' <summary>Gets or sets the convergence history (current and previous stream variable values and errors).</summary>
        Public Property ConvergenceHistory() As Interfaces.IRecycleConvergenceHistory Implements Interfaces.IRecycle.ConvergenceHistory
            Get
                Return m_ConvHist
            End Get
            Set(ByVal value As Interfaces.IRecycleConvergenceHistory)
                m_ConvHist = value
            End Set
        End Property

        ''' <summary>Gets or sets the maximum number of iterations allowed before convergence is considered failed.</summary>
        Public Property MaximumIterations() As Integer
            Get
                Return Me.m_MaxIterations
            End Get
            Set(ByVal value As Integer)
                Me.m_MaxIterations = value
            End Set
        End Property

        ''' <summary>Initializes a new default instance of the <see cref="Recycle"/> class.</summary>
        Public Sub New()

            MyBase.CreateNew()

            m_ConvPar = New ConvergenceParameters
            m_ConvHist = New ConvergenceHistory
            m_WegPars = New WegsteinParameters

        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Recycle"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the recycle block.</param>
        ''' <param name="description">A brief description of the recycle block.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()

            m_ConvPar = New ConvergenceParameters
            m_ConvHist = New ConvergenceHistory
            m_WegPars = New WegsteinParameters

            Me.ComponentName = name
            Me.ComponentDescription = description



        End Sub

        ''' <summary>
        ''' Copies the converged inlet stream properties (temperature, pressure, mass flow, enthalpy, composition)
        ''' to the connected outlet stream so that the recycle loop is closed with the last iterated values.
        ''' </summary>
        Public Sub SetOutletStreamProperties() Implements Interfaces.IRecycle.SetOutletStreamProperties

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

        ''' <summary>
        ''' Executes one dynamic-mode step by directly assigning the inlet stream properties to the outlet stream.
        ''' </summary>
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

        ''' <summary>
        ''' Performs one iteration of the recycle convergence check, optionally applying Wegstein or Broyden
        ''' acceleration, and updates the outlet stream with the new estimated values.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

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

            Dim Tnew, Pnew, Hnew, Snew As Double

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
                    Werr += Math.Abs(v1(i) - v2(i)) '/ v1(i) * (v2(i) / Wsum2)
                Else
                    Werr += Math.Abs(v1(i) - v2(i)) '* (v2(i) / Wsum2)
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

                Me.ConvergenceHistory.EntalpiaE0 = Me.ConvergenceHistory.EntalpiaE
                Me.ConvergenceHistory.EntalpiaE = Hnew - oms.Phases(0).Properties.enthalpy.GetValueOrDefault
                Me.ConvergenceHistory.Entalpia0 = Me.ConvergenceHistory.Entalpia
                Me.ConvergenceHistory.Entalpia = Hnew

                If Me.Errors.Count = 0 Then
                    Me.Errors.Add("Temperature", .temperature.GetValueOrDefault)
                    Me.Errors.Add("Pressure", .pressure.GetValueOrDefault)
                    Me.Errors.Add("MassFlow", Wsum)
                    Me.Errors.Add("Enthalpy", .enthalpy.GetValueOrDefault)
                Else
                    Me.Errors("Temperature") = Me.Values("Temperature") - .temperature.GetValueOrDefault
                    Me.Errors("Pressure") = Me.Values("Pressure") - .pressure.GetValueOrDefault
                    ' A signed residual for Broyden, matching the other three: a sum of absolute
                    ' compound-flow differences can never go negative, so Broyden could only ever push
                    ' the flow one way (issue #63). The convergence test keeps Werr via ConvergenceHistory.
                    Me.Errors("MassFlow") = Me.Values("MassFlow") - Wsum
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

            Dim copydata As Boolean = True

            ems.PropertyPackage.CurrentMaterialStream = ems

            ' The state the outlet gets for the next pass. Plain substitution hands it the inlet as it
            ' is. Damping (SmoothingFactor below 1) blends the inlet with the outlet as it was, and
            ' Wegstein or the dominant eigenvalue extrapolate from the last two passes. Every one of
            ' them needs a previous pass, so the first pass of a solve is always plain substitution.
            Dim Wnew As Double() = DirectCast(v1.Clone(), Double())
            Tnew = Me.ConvergenceHistory.Temperatura
            Pnew = Me.ConvergenceHistory.Pressao
            Dim accelerated As Boolean = False

            Dim errnorm As Double = RelativeErrorNorm(v1, v2, Wsum)

            Dim haveprevious As Boolean = Me.IterationCount > 0 AndAlso m_PrevInletFlows IsNot Nothing AndAlso
                m_PrevInletFlows.Length = v1.Length AndAlso m_PrevOutletFlows IsNot Nothing AndAlso m_PrevOutletFlows.Length = v2.Length

            If haveprevious AndAlso Me.AccelerationMethod <> AccelMethod.GlobalBroyden Then

                With Me.ConvergenceHistory

                    Dim sf As Double = SmoothingFactor
                    If sf > 0.0 AndAlso sf < 1.0 Then
                        Tnew = sf * .Temperatura + (1.0 - sf) * (.Temperatura - .TemperaturaE)
                        Pnew = sf * .Pressao + (1.0 - sf) * (.Pressao - .PressaoE)
                        Hnew = sf * .Entalpia + (1.0 - sf) * (.Entalpia - .EntalpiaE)
                        For i = 0 To v1.Length - 1
                            Wnew(i) = sf * v1(i) + (1.0 - sf) * v2(i)
                        Next
                        accelerated = True
                    End If

                    Dim delay As Integer = Math.Max(Convert.ToInt32(Me.WegsteinParameters.AccelDelay), 1)
                    Dim freq As Integer = Math.Max(Convert.ToInt32(Me.WegsteinParameters.AccelFreq), 1)
                    Dim due As Boolean = Me.IterationCount >= delay AndAlso (Me.IterationCount - delay) Mod freq = 0

                    Select Case Me.AccelerationMethod

                        Case AccelMethod.Wegstein

                            ' one secant per tear variable, bounded by the block's Qmin/Qmax; a variable that
                            ' did not move between the passes (a fixed pressure, say) is handed over as it is
                            If due Then
                                Tnew = WegsteinStep(.Temperatura, .Temperatura0, .Temperatura - .TemperaturaE, .Temperatura0 - .TemperaturaE0)
                                Pnew = WegsteinStep(.Pressao, .Pressao0, .Pressao - .PressaoE, .Pressao0 - .PressaoE0)
                                Hnew = WegsteinStep(.Entalpia, .Entalpia0, .Entalpia - .EntalpiaE, .Entalpia0 - .EntalpiaE0)
                                For i = 0 To v1.Length - 1
                                    Wnew(i) = WegsteinStep(v1(i), m_PrevInletFlows(i), v2(i), m_PrevOutletFlows(i))
                                Next
                                accelerated = True
                            End If

                        Case AccelMethod.Dominant_Eigenvalue

                            ' the ratio of the error norms of two consecutive plain passes estimates the
                            ' dominant eigenvalue of the loop; the whole error is then extrapolated by
                            ' 1/(1 - lambda), capped by the same bound as Wegstein's q
                            If due AndAlso Not m_PrevPassAccelerated AndAlso m_PrevErrorNorm > 0.0 Then
                                Dim lambda As Double = errnorm / m_PrevErrorNorm
                                Dim lambdamax As Double = 1.0 - 1.0 / (1.0 - Math.Min(Me.WegsteinParameters.Qmin, -1.0))
                                If lambda.IsValid AndAlso lambda > 0.0 Then
                                    lambda = Math.Min(lambda, lambdamax)
                                    Dim f As Double = lambda / (1.0 - lambda)
                                    Tnew = .Temperatura + f * .TemperaturaE
                                    Pnew = .Pressao + f * .PressaoE
                                    Hnew = .Entalpia + f * .EntalpiaE
                                    For i = 0 To v1.Length - 1
                                        Wnew(i) = v1(i) + f * (v1(i) - v2(i))
                                    Next
                                    accelerated = True
                                End If
                            End If

                    End Select

                    ' an extrapolation that leaves the physical range falls back to the inlet value
                    If Not Tnew.IsValid OrElse Tnew <= 0.0 Then Tnew = .Temperatura
                    If Not Pnew.IsValid OrElse Pnew <= 0.0 Then Pnew = .Pressao
                    If Not Hnew.IsValid Then Hnew = .Entalpia
                    For i = 0 To v1.Length - 1
                        If Not Wnew(i).IsValid OrElse Wnew(i) < 0.0 Then Wnew(i) = v1(i)
                    Next

                End With

            End If

            m_PrevInletFlows = v1
            m_PrevOutletFlows = v2
            m_PrevErrorNorm = errnorm
            m_PrevPassAccelerated = accelerated

            If LegacyMode Then

                ' the whole inlet is copied over, phases included; an accelerated pass then overwrites
                ' the tear variables on top of that copy
                If Me.CopyOnStreamDataError Then
                    copydata = True
                Else
                    If Not Tnew.IsValid Or Not Pnew.IsValid Or Not Wnew.Sum.IsValid Or Not ems.PropertyPackage.RET_VMOL(PropertyPackages.Phase.Mixture).Sum.IsValid Then copydata = False
                End If

                If Not Me.AccelerationMethod = AccelMethod.GlobalBroyden And copydata Then

                    If Not ems.Calculated And Not ems.AtEquilibrium Then
                        Throw New Exception(FlowSheet.GetTranslatedString("RecycleStreamNotCalculated"))
                    End If

                    Dim prevspec = oms.SpecType
                    oms.Assign(ems)
                    oms.AssignProps(ems)
                    oms.SpecType = prevspec
                    oms.AtEquilibrium = False

                    If accelerated Then WriteOutletState(oms, Tnew, Pnew, Hnew, Snew, Wnew)

                End If

            Else

                ' only the tear variables are written; the solver flashes the outlet afterwards. The
                ' enthalpy and entropy go along with the temperature because that flash may be run at
                ' pressure and enthalpy (a single-compound outlet always is), and an outlet flashed at
                ' the enthalpy it happened to carry never meets the inlet.
                If Not Me.AccelerationMethod = AccelMethod.GlobalBroyden Then

                    If Not oms.Calculated And Not oms.AtEquilibrium Then
                        Throw New Exception(FlowSheet.GetTranslatedString("RecycleStreamNotCalculated"))
                    End If

                    WriteOutletState(oms, Tnew, Pnew, Hnew, Snew, Wnew)

                End If

            End If

            If Me.IterationCount >= Me.MaximumIterations Then
                Me.IterationCount = 0
                Throw New TimeoutException(FlowSheet.GetTranslatedString("RecycleMaxItsReached"))
            End If

            Dim cvTErr = Math.Abs(Me.ConvergenceHistory.TemperaturaE)
            Dim cvPErr = Math.Abs(Me.ConvergenceHistory.PressaoE)
            Dim cvWErr = Math.Abs(Me.ConvergenceHistory.VazaoMassicaE)
            Dim cvTol = Me.ConvergenceParameters

            ' A torn stream carrying essentially no mass - a separator that makes no vapour on this
            ' iteration, say - has no meaningful temperature or pressure, so those errors never settle
            ' and the recycle spins to its iteration limit on a stream that recycles nothing. When the
            ' flow itself is within tolerance of zero, converge on the flow alone.
            Dim cvNegligibleFlow = Math.Abs(Me.ConvergenceHistory.VazaoMassica) <= Math.Max(cvTol.VazaoMassica, 0.000000000001)

            If cvWErr <= cvTol.VazaoMassica AndAlso (cvNegligibleFlow OrElse (cvTErr <= cvTol.Temperatura AndAlso cvPErr <= cvTol.Pressao)) Then

                If Me.IterationCount <> 0 Then Me.IterationsTaken = Me.IterationCount
                Me.IterationCount = 0

                Me.Converged = True

            Else

                Me.Converged = False

            End If

            Me.IterationCount += 1

            IObj?.Close()

        End Sub

        ''' <summary>Resets the iteration counter, effectively unsetting the convergence state.</summary>
        Public Overloads Sub DeCalculate()

            Me.IterationCount = 0

        End Sub

        ''' <summary>
        ''' One Wegstein step of a tear variable: <paramref name="g"/> and <paramref name="g0"/> are what the
        ''' inlet brought on this pass and the one before, <paramref name="x"/> and <paramref name="x0"/> what
        ''' the outlet carried on those passes. The secant slope of the loop gives q = s/(s-1), bounded by
        ''' the block's Qmin/Qmax, and the outlet gets q*x + (1-q)*g. Without a usable slope the inlet value
        ''' is handed over as it is.
        ''' </summary>
        Private Function WegsteinStep(g As Double, g0 As Double, x As Double, x0 As Double) As Double

            Dim dx As Double = x - x0
            If Not dx.IsValid OrElse Math.Abs(dx) <= 0.000000000001 * Math.Max(1.0, Math.Abs(x)) Then Return g

            Dim s As Double = (g - g0) / dx
            If Not s.IsValid Then Return g

            Dim q As Double = s / (s - 1.0)
            If Not q.IsValid Then q = Me.WegsteinParameters.Qmin
            q = Math.Min(Math.Max(q, Me.WegsteinParameters.Qmin), Me.WegsteinParameters.Qmax)

            Dim xnew As Double = q * x + (1.0 - q) * g
            If Not xnew.IsValid Then Return g
            Return xnew

        End Function

        ''' <summary>
        ''' The norm of the relative inlet-outlet error over every tear variable: temperature, pressure,
        ''' enthalpy and each compound mass flow (scaled by the total inlet flow).
        ''' </summary>
        Private Function RelativeErrorNorm(inletflows As Double(), outletflows As Double(), totalflow As Double) As Double

            Dim sum As Double = 0.0
            With Me.ConvergenceHistory
                sum += (.TemperaturaE / Math.Max(Math.Abs(.Temperatura), 0.000000000001)) ^ 2
                sum += (.PressaoE / Math.Max(Math.Abs(.Pressao), 0.000000000001)) ^ 2
                sum += (.EntalpiaE / Math.Max(Math.Abs(.Entalpia), 1.0)) ^ 2
            End With
            Dim scale As Double = Math.Max(Math.Abs(totalflow), 0.000000000001)
            For i As Integer = 0 To inletflows.Length - 1
                sum += ((inletflows(i) - outletflows(i)) / scale) ^ 2
            Next
            Return Math.Sqrt(sum)

        End Function

        ''' <summary>
        ''' Hands a tear state to the outlet stream: the compound mass flows (which set the total flow and the
        ''' composition), then temperature, pressure, enthalpy and entropy, so that the flash the solver runs
        ''' on the outlet finds the state whichever specification it uses.
        ''' </summary>
        Private Sub WriteOutletState(oms As MaterialStream, T As Double, P As Double, H As Double, S As Double, flows As Double())

            Dim total As Double = flows.Sum

            If total > 0.0 Then
                oms.SetOverallComposition(oms.MassFractionsToMoleFractions(flows.NormalizeY))
            End If
            oms.SetMassFlow(total)

            Dim molarflow As Double = oms.GetMolarFlow()
            Dim i As Integer = 0
            For Each c In oms.Phases(0).Compounds.Values
                c.MassFlow = flows(i)
                c.MassFraction = If(total > 0.0, flows(i) / total, 0.0)
                c.MolarFlow = c.MoleFraction.GetValueOrDefault * molarflow
                i += 1
            Next

            oms.SetTemperature(T)
            oms.SetPressure(P)
            oms.SetMassEnthalpy(H)
            oms.SetMassEntropy(S)
            oms.AtEquilibrium = False

        End Sub

        ''' <summary>
        ''' Returns the maximum value from an array-like object.
        ''' </summary>
        ''' <param name="Vv">An array whose maximum element is to be found.</param>
        ''' <returns>The maximum value found in <paramref name="Vv"/>.</returns>
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

        ''' <summary>
        ''' Returns the value of the specified property converted to the given unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string (e.g., "PROP_RY_0").</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
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

        ''' <summary>
        ''' Returns the list of property identifiers available for this recycle block filtered by property type.
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
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

        ''' <summary>
        ''' Sets the value of the specified property after converting from the given unit system to SI.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new value in the units of <paramref name="su"/>.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

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

        ''' <summary>
        ''' Returns the unit string for the specified property in the given unit system.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>A string representing the unit of the property.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
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

        ''' <summary>Returns the raw bytes of the recycle block icon image resource.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.recycle.png")

        End Function

        ''' <summary>Returns the localized display description for the recycle block type.</summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("MRECY_Desc")
        End Function

        ''' <summary>Returns the localized display name for the recycle block type.</summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("MRECY_Name")
        End Function

        ''' <summary>Gets a value indicating whether this recycle block is compatible with the DWSIM mobile interface.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Namespace

Namespace SpecialOps.Helpers.Recycle

    ''' <summary>Specifies the type of flash calculation used when updating the outlet stream of a recycle block.</summary>
    Public Enum FlashType
        ''' <summary>No flash calculation is performed.</summary>
        None
        ''' <summary>Temperature-pressure flash.</summary>
        FlashTP
        ''' <summary>Pressure-enthalpy flash.</summary>
        FlashPH
        ''' <summary>Pressure-entropy flash.</summary>
        FlashPS
    End Enum

    ''' <summary>Holds the convergence tolerance parameters for a material recycle loop.</summary>
    <System.Serializable()> Public Class ConvergenceParameters

        Implements Interfaces.ICustomXMLSerialization

        ''' <summary>Gets or sets the temperature tolerance in Kelvin.</summary>
        Public Temperatura As Double = 0.1
        ''' <summary>Gets or sets the pressure tolerance in Pa.</summary>
        Public Pressao As Double = 0.1
        ''' <summary>Gets or sets the mass flow tolerance in kg/s.</summary>
        Public VazaoMassica As Double = 0.01
        ''' <summary>Gets or sets the vapour fraction tolerance (dimensionless).</summary>
        Public FracaoVapor As Double = 0.01
        ''' <summary>Gets or sets the enthalpy tolerance in kJ/kg.</summary>
        Public Entalpia As Double = 1
        ''' <summary>Gets or sets the entropy tolerance in kJ/kg·K.</summary>
        Public Entropia As Double = 0.01
        ''' <summary>Gets or sets the composition tolerance (mole fraction).</summary>
        Public Composicao As Double = 0.001

        ''' <summary>Initializes a new default instance of <see cref="ConvergenceParameters"/>.</summary>
        Sub New()

        End Sub

        ''' <summary>Restores the convergence parameters from a list of XML elements.</summary>
        ''' <param name="data">The serialized XML data.</param>
        ''' <returns><c>True</c> if successful.</returns>
        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data, True)
            Return True

        End Function

        ''' <summary>Serializes the convergence parameters to a list of XML elements.</summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Return XMLSerializer.XMLSerializer.Serialize(Me, True)

        End Function

    End Class

    ''' <summary>Stores current and previous values of all convergence variables for a material recycle loop.</summary>
    <System.Serializable()> Public Class ConvergenceHistory

        Implements Interfaces.ICustomXMLSerialization, Interfaces.IRecycleConvergenceHistory

        ''' <summary>Initializes a new default instance of <see cref="ConvergenceHistory"/>.</summary>
        Sub New()

        End Sub

        ''' <summary>Restores the convergence history from a list of XML elements.</summary>
        ''' <param name="data">The serialized XML data.</param>
        ''' <returns><c>True</c> if successful.</returns>
        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data, True)
            Return True

        End Function

        ''' <summary>Serializes the convergence history to a list of XML elements.</summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Return XMLSerializer.XMLSerializer.Serialize(Me, True)

        End Function

        ''' <summary>Gets or sets the current enthalpy value in kJ/kg.</summary>
        Public Property Entalpia As Double Implements Interfaces.IRecycleConvergenceHistory.Entalpia
        ''' <summary>Gets or sets the previous enthalpy value in kJ/kg.</summary>
        Public Property Entalpia0 As Double Implements Interfaces.IRecycleConvergenceHistory.Entalpia0
        ''' <summary>Gets or sets the current enthalpy convergence error in kJ/kg.</summary>
        Public Property EntalpiaE As Double Implements Interfaces.IRecycleConvergenceHistory.EntalpiaE
        ''' <summary>Gets or sets the previous enthalpy convergence error in kJ/kg.</summary>
        Public Property EntalpiaE0 As Double Implements Interfaces.IRecycleConvergenceHistory.EntalpiaE0
        ''' <summary>Gets or sets the current entropy value in kJ/kg·K.</summary>
        Public Property Entropia As Double Implements Interfaces.IRecycleConvergenceHistory.Entropia
        ''' <summary>Gets or sets the previous entropy value in kJ/kg·K.</summary>
        Public Property Entropia0 As Double Implements Interfaces.IRecycleConvergenceHistory.Entropia0
        ''' <summary>Gets or sets the current entropy convergence error in kJ/kg·K.</summary>
        Public Property EntropiaE As Double Implements Interfaces.IRecycleConvergenceHistory.EntropiaE
        ''' <summary>Gets or sets the previous entropy convergence error in kJ/kg·K.</summary>
        Public Property EntropiaE0 As Double Implements Interfaces.IRecycleConvergenceHistory.EntropiaE0
        ''' <summary>Gets or sets the current pressure value in Pa.</summary>
        Public Property Pressao As Double Implements Interfaces.IRecycleConvergenceHistory.Pressao
        ''' <summary>Gets or sets the previous pressure value in Pa.</summary>
        Public Property Pressao0 As Double Implements Interfaces.IRecycleConvergenceHistory.Pressao0
        ''' <summary>Gets or sets the current pressure convergence error in Pa.</summary>
        Public Property PressaoE As Double Implements Interfaces.IRecycleConvergenceHistory.PressaoE
        ''' <summary>Gets or sets the previous pressure convergence error in Pa.</summary>
        Public Property PressaoE0 As Double Implements Interfaces.IRecycleConvergenceHistory.PressaoE0
        ''' <summary>Gets or sets the current temperature value in K.</summary>
        Public Property Temperatura As Double Implements Interfaces.IRecycleConvergenceHistory.Temperatura
        ''' <summary>Gets or sets the previous temperature value in K.</summary>
        Public Property Temperatura0 As Double Implements Interfaces.IRecycleConvergenceHistory.Temperatura0
        ''' <summary>Gets or sets the current temperature convergence error in K.</summary>
        Public Property TemperaturaE As Double Implements Interfaces.IRecycleConvergenceHistory.TemperaturaE
        ''' <summary>Gets or sets the previous temperature convergence error in K.</summary>
        Public Property TemperaturaE0 As Double Implements Interfaces.IRecycleConvergenceHistory.TemperaturaE0
        ''' <summary>Gets or sets the current mass flow value in kg/s.</summary>
        Public Property VazaoMassica As Double Implements Interfaces.IRecycleConvergenceHistory.VazaoMassica
        ''' <summary>Gets or sets the previous mass flow value in kg/s.</summary>
        Public Property VazaoMassica0 As Double Implements Interfaces.IRecycleConvergenceHistory.VazaoMassica0
        ''' <summary>Gets or sets the current mass flow convergence error in kg/s.</summary>
        Public Property VazaoMassicaE As Double Implements Interfaces.IRecycleConvergenceHistory.VazaoMassicaE
        ''' <summary>Gets or sets the previous mass flow convergence error in kg/s.</summary>
        Public Property VazaoMassicaE0 As Double Implements Interfaces.IRecycleConvergenceHistory.VazaoMassicaE0

    End Class

    ''' <summary>Holds the tuning parameters of the Wegstein and dominant-eigenvalue accelerations: their schedule and the bounds of Wegstein's q.</summary>
    <System.Serializable()> Public Class WegsteinParameters

        ''' <summary>Gets or sets how often the acceleration is applied once the delay has passed: on every Nth pass, so 1 accelerates every pass.</summary>
        Public AccelFreq As Integer = 4
        ''' <summary>Gets or sets the maximum value of the Wegstein q parameter.</summary>
        Public Qmax As Double = 0
        ''' <summary>Gets or sets the minimum value of the Wegstein q parameter.</summary>
        Public Qmin As Double = -20
        ''' <summary>Gets or sets the number of plain substitution passes before the first accelerated one.</summary>
        Public AccelDelay = 2

    End Class

End Namespace

