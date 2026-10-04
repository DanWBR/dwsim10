'    MPC (Model Predictive Control) Controller
'    Copyright 2024-2026 Daniel Wagner O. de Medeiros
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

Imports DWSIM.Interfaces.Enums
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.SpecialOps.Helpers
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series

Namespace SpecialOps

    <System.Serializable()> Public Class MPCVariable

        Public Property ObjectID As String = ""
        Public Property PropertyName As String = ""
        Public Property Units As String = ""
        Public Property UnitsType As UnitOfMeasure = UnitOfMeasure.none
        Public Property Name As String = ""
        Public Property MinValue As Double = Double.MinValue
        Public Property MaxValue As Double = Double.MaxValue
        Public Property Weight As Double = 1.0
        Public Property LastValue As Double = 0.0

        ''' <summary>
        ''' Reads the variable's current value from the flowsheet, in its units.
        ''' </summary>
        ''' <param name="fs">The flowsheet that holds the linked object.</param>
        ''' <returns>The value, or <c>Double.NaN</c> when the linked object is not on the flowsheet.</returns>
        Public Function GetCurrentValue(fs As IFlowsheet) As Double
            Dim obj = fs.SimulationObjects.Values.Where(Function(x) x.Name = ObjectID).SingleOrDefault()
            If obj IsNot Nothing Then
                Return SystemsOfUnits.Converter.ConvertFromSI(Units, obj.GetPropertyValue(PropertyName))
            End If
            Return Double.NaN
        End Function

        Public Sub SetCurrentValue(fs As IFlowsheet, value As Double)
            Dim obj = fs.SimulationObjects.Values.Where(Function(x) x.Name = ObjectID).SingleOrDefault()
            If obj IsNot Nothing Then
                obj.SetPropertyValue(PropertyName, SystemsOfUnits.Converter.ConvertToSI(Units, value))
            End If
        End Sub

    End Class

    <System.Serializable()> Public Class StepResponseModel

        Public Property CVIndex As Integer = 0
        Public Property MVIndex As Integer = 0

        ''' <summary>
        ''' Index of the measured disturbance variable in a model of the controller's DisturbanceModels list,
        ''' which relates that disturbance to controlled variable CVIndex. Models in StepResponseModels use
        ''' MVIndex instead and ignore this one. Default 0.
        ''' </summary>
        Public Property DVIndex As Integer = 0

        Public Property StepCoefficients As New List(Of Double)
        Public Property Gain As Double = 1.0
        Public Property TimeConstant As Double = 60.0
        Public Property DeadTime As Double = 0.0

        ''' <summary>
        ''' True for a CV that integrates the MV, such as a level moved by an outlet valve. Gain is then the
        ''' slope (CV units per MV unit per second) and Time Constant an optional lag before the ramp.
        ''' </summary>
        Public Property Integrating As Boolean = False

        Public Sub GenerateFromFOPDT(sampleTime As Double, horizonLength As Integer)
            StepCoefficients.Clear()
            Dim tau = Math.Max(TimeConstant, 0.001)
            For i = 0 To horizonLength - 1
                Dim t = (i + 1) * sampleTime
                Dim tEff = t - DeadTime
                If tEff <= 0 Then
                    StepCoefficients.Add(0.0)
                ElseIf Integrating Then
                    If TimeConstant > 0.0 Then
                        StepCoefficients.Add(Gain * (tEff - tau * (1.0 - Math.Exp(-tEff / tau))))
                    Else
                        StepCoefficients.Add(Gain * tEff)
                    End If
                Else
                    StepCoefficients.Add(Gain * (1.0 - Math.Exp(-tEff / tau)))
                End If
            Next
        End Sub

        ''' <summary>
        ''' Step response coefficient s(n), n >= 1. Past the stored coefficients the response is held,
        ''' or continued as a ramp for an integrating model.
        ''' </summary>
        Public Function Coefficient(n As Integer) As Double
            Dim count = StepCoefficients.Count
            If count = 0 OrElse n < 1 Then Return 0.0
            If n <= count Then Return StepCoefficients(n - 1)
            If Integrating AndAlso count >= 2 Then
                Return StepCoefficients(count - 1) + (n - count) * (StepCoefficients(count - 1) - StepCoefficients(count - 2))
            End If
            Return StepCoefficients(count - 1)
        End Function

        ''' <summary>
        ''' Samples the response needs to settle: dead time plus five time constants.
        ''' </summary>
        Public Function SettlingSamples(sampleTime As Double) As Double
            Return Math.Ceiling((Math.Max(DeadTime, 0.0) + 5.0 * Math.Max(TimeConstant, 0.0)) / sampleTime) + 2
        End Function

    End Class

    ''' <summary>
    ''' Model predictive controller for dynamic simulations, based on Dynamic Matrix Control (DMC). It moves
    ''' several manipulated variables at once to keep several controlled variables inside their ranges,
    ''' using first-order plus dead time step response models between each pair.
    ''' </summary>
    <System.Serializable()> Public Partial Class MPCController

        Inherits UnitOperations.SpecialOpBaseClass

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Controllers

        ''' <summary>
        ''' Gets or sets whether the controller acts during a dynamic run. The integrator skips an inactive
        ''' controller. Default <c>True</c>.
        ''' </summary>
        Public Property Active As Boolean = True

        ''' <summary>
        ''' Prediction horizon P, in samples: the number of future samples over which the controlled variable
        ''' error is minimized. Default 30.
        ''' </summary>
        Public Property PredictionHorizon As Integer = 30
        ''' <summary>
        ''' Control horizon M, in samples: the number of future moves computed for each manipulated variable
        ''' (only the first is applied). Limited to the prediction horizon. Default 5.
        ''' </summary>
        Public Property ControlHorizon As Integer = 5
        ''' <summary>
        ''' Control interval, in s. In dynamic mode the controller acts every n-th call, with n the sample time
        ''' divided by the integrator call interval, rounded and at least 1. Default 1.
        ''' </summary>
        Public Property SampleTime As Double = 1.0

        ''' <summary>
        ''' Controlled variables. Each one is driven to the middle of its range (MinValue + MaxValue) / 2, with
        ''' its Weight in the objective.
        ''' </summary>
        Public Property ControlledVariables As New List(Of MPCVariable)
        ''' <summary>Manipulated variables moved by the controller, each clamped to its MinValue and MaxValue.</summary>
        Public Property ManipulatedVariables As New List(Of MPCVariable)
        ''' <summary>
        ''' Measured disturbance variables. The controller reads them at each control step; a disturbance
        ''' with a model in <see cref="DisturbanceModels"/> acts as feedforward, its change since the last
        ''' step entering the predicted trajectory of the controlled variable as a manipulated variable move
        ''' does. MinValue, MaxValue and Weight are not used.
        ''' </summary>
        Public Property DisturbanceVariables As New List(Of MPCVariable)

        ''' <summary>
        ''' Step response models, one per controlled/manipulated variable pair, indexed by CVIndex and MVIndex
        ''' in the variable lists.
        ''' </summary>
        Public Property StepResponseModels As New List(Of StepResponseModel)

        ''' <summary>
        ''' Disturbance models, one per controlled/disturbance variable pair, indexed by CVIndex and DVIndex
        ''' in the variable lists: the response of the controlled variable to a unit step in the measured
        ''' disturbance, first order plus dead time or integrating. The disturbance is taken as constant over
        ''' the prediction horizon. An empty list leaves the controller as it is without disturbances.
        ''' </summary>
        Public Property DisturbanceModels As New List(Of StepResponseModel)

        ''' <summary>
        ''' Gets or sets whether the disturbance models act as feedforward. With <c>False</c> the controller
        ''' still reads and records the disturbances but predicts as if it had no disturbance models.
        ''' Default <c>True</c>.
        ''' </summary>
        Public Property UseMeasuredDisturbances As Boolean = True

        ''' <summary>
        ''' Move suppression weight (lambda), dimensionless. It is scaled by the mean diagonal of each
        ''' manipulated variable's block of the dynamic matrix; larger values give smaller, smoother moves.
        ''' Default 0.1.
        ''' </summary>
        Public Property MoveSuppressionWeight As Double = 0.1

        ''' <summary>
        ''' Position of this controller in the order the dynamic integrator runs the MPC controllers, in
        ''' ascending order. Default 0.
        ''' </summary>
        Public Property ExecutionOrder As Integer = 0

        Private Const MaxModelLength As Integer = 20000

        'controller state between calls; rebuilt when the models or the tuning change
        <System.NonSerialized> Private Prediction As Double(,)
        <System.NonSerialized> Private LastMV As Double()
        <System.NonSerialized> Private LastDV As Double()
        <System.NonSerialized> Private GainRows As Double(,)
        <System.NonSerialized> Private IntegratingCV As Boolean()
        <System.NonSerialized> Private ModelLength As Integer = 0
        <System.NonSerialized> Private ModelKey As String = ""
        <System.NonSerialized> Private TuningKey As String = ""
        <System.NonSerialized> Private CallCount As Integer = 0
        <System.NonSerialized> Private LastCallTime As DateTime? = Nothing

        ''' <summary>
        ''' Controlled variable values recorded at each control step, one array per step in the variables'
        ''' units, for the trend charts.
        ''' </summary>
        Public Property CVHistory As New List(Of Double())
        ''' <summary>
        ''' Manipulated variable values recorded at each control step, one array per step in the variables'
        ''' units, for the trend charts.
        ''' </summary>
        Public Property MVHistory As New List(Of Double())
        ''' <summary>
        ''' Measured disturbance values recorded at each control step, one array per step in the variables'
        ''' units, for the trend charts. Recorded only when the controller has disturbance variables.
        ''' </summary>
        Public Property DVHistory As New List(Of Double())

        ''' <summary>Initializes a new default instance of the <see cref="MPCController"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="MPCController"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The name of this controller.</param>
        ''' <param name="description">A brief description of this controller.</param>
        Public Sub New(name As String, description As String)
            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        ''' <summary>Creates a deep copy of this object by round-tripping through XML serialization.</summary>
        ''' <returns>A new <see cref="MPCController"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New MPCController()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ' the generic serializer skips the variable and model lists, so they are written here
        Public Overrides Function SaveData() As List(Of System.Xml.Linq.XElement)

            Dim elements = MyBase.SaveData()
            Dim ci = Globalization.CultureInfo.InvariantCulture

            Dim saveVars = Function(tag As String, vars As List(Of MPCVariable))
                               Dim xel As New System.Xml.Linq.XElement(tag)
                               For Each v In vars
                                   xel.Add(New System.Xml.Linq.XElement("Variable",
                                           New System.Xml.Linq.XAttribute("ObjectID", If(v.ObjectID, "")),
                                           New System.Xml.Linq.XAttribute("PropertyName", If(v.PropertyName, "")),
                                           New System.Xml.Linq.XAttribute("Units", If(v.Units, "")),
                                           New System.Xml.Linq.XAttribute("UnitsType", v.UnitsType.ToString()),
                                           New System.Xml.Linq.XAttribute("Name", If(v.Name, "")),
                                           New System.Xml.Linq.XAttribute("MinValue", v.MinValue.ToString("R", ci)),
                                           New System.Xml.Linq.XAttribute("MaxValue", v.MaxValue.ToString("R", ci)),
                                           New System.Xml.Linq.XAttribute("Weight", v.Weight.ToString("R", ci))))
                               Next
                               Return xel
                           End Function

            elements.Add(saveVars("MPCControlledVariables", ControlledVariables))
            elements.Add(saveVars("MPCManipulatedVariables", ManipulatedVariables))
            elements.Add(saveVars("MPCDisturbanceVariables", DisturbanceVariables))

            Dim models As New System.Xml.Linq.XElement("MPCStepResponseModels")
            For Each m In StepResponseModels
                models.Add(New System.Xml.Linq.XElement("Model",
                           New System.Xml.Linq.XAttribute("CVIndex", m.CVIndex),
                           New System.Xml.Linq.XAttribute("MVIndex", m.MVIndex),
                           New System.Xml.Linq.XAttribute("Gain", m.Gain.ToString("R", ci)),
                           New System.Xml.Linq.XAttribute("TimeConstant", m.TimeConstant.ToString("R", ci)),
                           New System.Xml.Linq.XAttribute("DeadTime", m.DeadTime.ToString("R", ci)),
                           New System.Xml.Linq.XAttribute("Integrating", m.Integrating)))
            Next
            elements.Add(models)

            Dim dmodels As New System.Xml.Linq.XElement("MPCDisturbanceModels")
            For Each m In DisturbanceModels
                dmodels.Add(New System.Xml.Linq.XElement("Model",
                            New System.Xml.Linq.XAttribute("CVIndex", m.CVIndex),
                            New System.Xml.Linq.XAttribute("DVIndex", m.DVIndex),
                            New System.Xml.Linq.XAttribute("Gain", m.Gain.ToString("R", ci)),
                            New System.Xml.Linq.XAttribute("TimeConstant", m.TimeConstant.ToString("R", ci)),
                            New System.Xml.Linq.XAttribute("DeadTime", m.DeadTime.ToString("R", ci)),
                            New System.Xml.Linq.XAttribute("Integrating", m.Integrating)))
            Next
            elements.Add(dmodels)

            Return elements

        End Function

        Public Overrides Function LoadData(data As List(Of System.Xml.Linq.XElement)) As Boolean

            MyBase.LoadData(data)

            Dim ci = Globalization.CultureInfo.InvariantCulture

            Dim loadVars = Sub(tag As String, vars As List(Of MPCVariable))
                               Dim xel = data.Where(Function(x) x.Name = tag).FirstOrDefault()
                               If xel Is Nothing Then Return
                               vars.Clear()
                               For Each xv In xel.Elements("Variable")
                                   Dim v As New MPCVariable With {
                                       .ObjectID = xv.@ObjectID,
                                       .PropertyName = xv.@PropertyName,
                                       .Units = xv.@Units,
                                       .Name = xv.@Name,
                                       .MinValue = Double.Parse(xv.@MinValue, ci),
                                       .MaxValue = Double.Parse(xv.@MaxValue, ci),
                                       .Weight = Double.Parse(xv.@Weight, ci)}
                                   Dim ut As UnitOfMeasure
                                   If [Enum].TryParse(xv.@UnitsType, ut) Then v.UnitsType = ut
                                   vars.Add(v)
                               Next
                           End Sub

            loadVars("MPCControlledVariables", ControlledVariables)
            loadVars("MPCManipulatedVariables", ManipulatedVariables)
            loadVars("MPCDisturbanceVariables", DisturbanceVariables)

            'an attribute an older file lacks keeps the model's default
            Dim loadModels = Sub(tag As String, list As List(Of StepResponseModel), disturbance As Boolean)
                                 Dim xel = data.Where(Function(x) x.Name = tag).FirstOrDefault()
                                 If xel Is Nothing Then Return
                                 list.Clear()
                                 For Each xm In xel.Elements("Model")
                                     Dim m As New StepResponseModel()
                                     Dim iv As Integer, dv As Double, bv As Boolean
                                     If Integer.TryParse(xm.@CVIndex, Globalization.NumberStyles.Integer, ci, iv) Then m.CVIndex = iv
                                     If disturbance Then
                                         If Integer.TryParse(xm.@DVIndex, Globalization.NumberStyles.Integer, ci, iv) Then m.DVIndex = iv
                                     Else
                                         If Integer.TryParse(xm.@MVIndex, Globalization.NumberStyles.Integer, ci, iv) Then m.MVIndex = iv
                                     End If
                                     If Double.TryParse(xm.@Gain, Globalization.NumberStyles.Float, ci, dv) Then m.Gain = dv
                                     If Double.TryParse(xm.@TimeConstant, Globalization.NumberStyles.Float, ci, dv) Then m.TimeConstant = dv
                                     If Double.TryParse(xm.@DeadTime, Globalization.NumberStyles.Float, ci, dv) Then m.DeadTime = dv
                                     If Boolean.TryParse(xm.@Integrating, bv) Then m.Integrating = bv
                                     list.Add(m)
                                 Next
                             End Sub

            loadModels("MPCStepResponseModels", StepResponseModels, False)
            loadModels("MPCDisturbanceModels", DisturbanceModels, True)

            Return True

        End Function

        ''' <summary>
        ''' Gets a value indicating whether this controller is compatible with mobile interfaces. Always
        ''' <c>False</c>.
        ''' </summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Returns the display name for this controller type.</summary>
        ''' <returns>The name "MPC Controller".</returns>
        Public Overrides Function GetDisplayName() As String
            Return "MPC Controller"
        End Function

        ''' <summary>Returns the description string for this controller type.</summary>
        ''' <returns>The description "Model Predictive Controller (DMC)".</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return "Model Predictive Controller (DMC)"
        End Function

        ''' <summary>Returns the raw bytes of the icon image for this controller.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.control_panel.png")

        End Function

        <System.NonSerialized> Private f As Object

        ''' <summary>
        ''' Clears the controller state: the predicted trajectories, the last moves, the cached model and
        ''' tuning, the call counter, the histories and the last values of the variables.
        ''' </summary>
        Public Sub Reset()
            Prediction = Nothing
            LastMV = Nothing
            LastDV = Nothing
            ModelKey = ""
            TuningKey = ""
            CallCount = 0
            LastCallTime = Nothing
            CVHistory.Clear()
            MVHistory.Clear()
            DVHistory.Clear()
            For Each cv In ControlledVariables
                cv.LastValue = 0.0
            Next
            For Each mv In ManipulatedVariables
                mv.LastValue = 0.0
            Next
            For Each dv In DisturbanceVariables
                dv.LastValue = 0.0
            Next
        End Sub

        ''' <summary>
        ''' Regenerates the step response coefficients of every model, disturbance models included, from its
        ''' gain, time constant and dead time, at the current sample time and a length that covers the
        ''' prediction horizon and the settling of each model.
        ''' </summary>
        Public Sub InitializeModels()
            Dim ts = Math.Max(SampleTime, 0.001)
            Dim NS = GetModelLength(ts, Math.Max(1, PredictionHorizon))
            For Each model In StepResponseModels
                model.GenerateFromFOPDT(ts, NS)
            Next
            For Each model In DisturbanceModels
                model.GenerateFromFOPDT(ts, NS)
            Next
        End Sub

        ''' <summary>
        ''' The disturbance models the prediction uses: none when the disturbances are switched off, otherwise
        ''' those that point to an existing controlled and disturbance variable.
        ''' </summary>
        Private Function ActiveDisturbanceModels() As List(Of StepResponseModel)
            If Not UseMeasuredDisturbances Then Return New List(Of StepResponseModel)
            Dim nCV = ControlledVariables.Count
            Dim nDV = DisturbanceVariables.Count
            Return DisturbanceModels.Where(Function(m) m.CVIndex >= 0 AndAlso m.CVIndex < nCV AndAlso
                                                       m.DVIndex >= 0 AndAlso m.DVIndex < nDV).ToList()
        End Function

        ''' <summary>
        ''' Dynamic Matrix Control. At each control step the predicted CV trajectory is shifted one sample,
        ''' updated with the MV moves measured since the last step and moved onto the current measurement
        ''' (model error correction). The first of the M moves that minimize the weighted squared error over
        ''' P samples plus the move suppression term is applied, within the MV limits. For an integrating CV
        ''' the model error is also extrapolated as a ramp, which removes the offset a load change leaves.
        ''' The changes of the measured disturbances that have a disturbance model enter the prediction the
        ''' same way as the MV moves (feedforward).
        ''' </summary>
        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            If ControlledVariables.Count = 0 OrElse ManipulatedVariables.Count = 0 Then Return
            If StepResponseModels.Count = 0 Then Return

            Dim nCV = ControlledVariables.Count
            Dim nMV = ManipulatedVariables.Count
            Dim P = Math.Max(1, PredictionHorizon)
            Dim M = Math.Max(1, Math.Min(ControlHorizon, P))

            'in dynamic mode the controller is called once per control calculation of the integrator and
            'acts every n-th call, n = Sample Time / call interval rounded and at least 1
            Dim interval = 0.0
            Dim simTime As DateTime? = Nothing
            Dim integrator = GetIntegrator()
            If integrator IsNot Nothing Then
                interval = integrator.IntegrationStep.TotalSeconds
                If integrator.RealTime Then interval = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0
                interval *= Math.Max(1, integrator.CalculationRateControl)
                simTime = integrator.CurrentTime
            End If

            Dim every = 1
            Dim ts = Math.Max(SampleTime, 0.001)
            If interval > 0.0 Then
                every = Math.Max(1, CInt(Math.Round(SampleTime / interval)))
                ts = every * interval
            End If

            If simTime.HasValue AndAlso LastCallTime.HasValue Then
                Dim gap = (simTime.Value - LastCallTime.Value).TotalSeconds
                'already acted at this time
                If gap = 0.0 Then Return
                'calls missed (controller switched off) or the clock went back: start the prediction over
                If gap < 0.0 OrElse gap > 1.5 * interval Then Prediction = Nothing
            End If
            LastCallTime = simTime

            Dim nDV = DisturbanceVariables.Count
            Dim dvModels = ActiveDisturbanceModels()

            Dim mKey = GetModelKey(ts, P)
            If mKey <> ModelKey Then
                ModelLength = GetModelLength(ts, P)
                For Each model In StepResponseModels
                    model.GenerateFromFOPDT(ts, ModelLength)
                Next
                For Each model In DisturbanceModels
                    model.GenerateFromFOPDT(ts, ModelLength)
                Next
                IntegratingCV = New Boolean(nCV - 1) {}
                For Each model In StepResponseModels
                    If IsValid(model, nCV, nMV) AndAlso model.Integrating Then IntegratingCV(model.CVIndex) = True
                Next
                'a disturbance the CV integrates makes it an integrating CV as well
                For Each model In dvModels
                    If model.Integrating Then IntegratingCV(model.CVIndex) = True
                Next
                ModelKey = mKey
                TuningKey = ""
                Prediction = Nothing
            End If

            Dim tKey = GetTuningKey(M)
            If tKey <> TuningKey Then
                BuildGain(P, M)
                TuningKey = tKey
            End If

            If Prediction Is Nothing Then CallCount = 0
            CallCount += 1
            If (CallCount - 1) Mod every <> 0 Then Return

            Dim NS = ModelLength

            Dim cvValues(nCV - 1) As Double
            Dim mvValues(nMV - 1) As Double

            For i = 0 To nCV - 1
                cvValues(i) = ControlledVariables(i).GetCurrentValue(FlowSheet)
            Next
            For i = 0 To nMV - 1
                mvValues(i) = ManipulatedVariables(i).GetCurrentValue(FlowSheet)
            Next

            'a controlled or manipulated variable that cannot be read (its object is gone) stops the
            'controller for this step; the prediction starts over once it reads again
            If cvValues.Any(Function(v) Double.IsNaN(v)) OrElse mvValues.Any(Function(v) Double.IsNaN(v)) Then
                Prediction = Nothing
                Return
            End If

            Dim dvValues(nDV - 1) As Double
            For i = 0 To nDV - 1
                dvValues(i) = DisturbanceVariables(i).GetCurrentValue(FlowSheet)
            Next

            CVHistory.Add(DirectCast(cvValues.Clone(), Double()))
            MVHistory.Add(DirectCast(mvValues.Clone(), Double()))
            If nDV > 0 Then DVHistory.Add(DirectCast(dvValues.Clone(), Double()))

            'a disturbance that cannot be read keeps its last reading, so it brings no move
            If LastDV IsNot Nothing AndAlso LastDV.Length = nDV Then
                For i = 0 To nDV - 1
                    If Double.IsNaN(dvValues(i)) Then dvValues(i) = LastDV(i)
                Next
            End If

            Dim modelError(nCV - 1) As Double

            If Prediction Is Nothing Then
                'start from steady state at the measured values
                Prediction = New Double(nCV - 1, NS - 1) {}
                For i = 0 To nCV - 1
                    For k = 0 To NS - 1
                        Prediction(i, k) = cvValues(i)
                    Next
                Next
            Else
                'shift one sample; the last point is held, or carried along the ramp for an integrating CV
                For i = 0 To nCV - 1
                    Dim last = Prediction(i, NS - 1)
                    Dim slope = If(IntegratingCV(i) AndAlso NS > 1, last - Prediction(i, NS - 2), 0.0)
                    For k = 0 To NS - 2
                        Prediction(i, k) = Prediction(i, k + 1)
                    Next
                    Prediction(i, NS - 1) = last + slope
                Next
                'moves since the last step as measured, so clamping and manual changes are accounted for
                For Each model In StepResponseModels
                    If Not IsValid(model, nCV, nMV) Then Continue For
                    Dim du = mvValues(model.MVIndex) - LastMV(model.MVIndex)
                    If du = 0.0 Then Continue For
                    For k = 0 To NS - 1
                        Prediction(model.CVIndex, k) += model.Coefficient(k + 1) * du
                    Next
                Next
                'feedforward: the disturbance changes measured since the last step enter as the MV moves
                'do, as if made one sample ago; the disturbance is then held over the horizon
                If LastDV IsNot Nothing AndAlso LastDV.Length = nDV Then
                    For Each model In dvModels
                        Dim dd = dvValues(model.DVIndex) - LastDV(model.DVIndex)
                        If Double.IsNaN(dd) OrElse dd = 0.0 Then Continue For
                        For k = 0 To NS - 1
                            Prediction(model.CVIndex, k) += model.Coefficient(k + 1) * dd
                        Next
                    Next
                End If
                For i = 0 To nCV - 1
                    modelError(i) = cvValues(i) - Prediction(i, 0)
                    For k = 0 To NS - 1
                        Prediction(i, k) += modelError(i)
                    Next
                Next
            End If

            LastMV = DirectCast(mvValues.Clone(), Double())
            LastDV = DirectCast(dvValues.Clone(), Double())

            Dim setpoints(nCV - 1) As Double
            For i = 0 To nCV - 1
                setpoints(i) = (ControlledVariables(i).MinValue + ControlledVariables(i).MaxValue) / 2.0
            Next

            'du = (A'QA + R)^-1 A'Q (r - free response); only the first move of each MV is applied
            Dim moves(nMV - 1) As Double
            For i = 0 To nCV - 1
                For k = 1 To P
                    Dim free = Prediction(i, Math.Min(k, NS - 1))
                    If IntegratingCV(i) Then free += k * modelError(i)
                    Dim e = setpoints(i) - free
                    For j = 0 To nMV - 1
                        moves(j) += GainRows(j, i * P + k - 1) * e
                    Next
                Next
            Next

            For j = 0 To nMV - 1
                Dim newMV = mvValues(j) + moves(j)
                If newMV > ManipulatedVariables(j).MaxValue Then newMV = ManipulatedVariables(j).MaxValue
                If newMV < ManipulatedVariables(j).MinValue Then newMV = ManipulatedVariables(j).MinValue

                ManipulatedVariables(j).SetCurrentValue(FlowSheet, newMV)
                ManipulatedVariables(j).LastValue = newMV
            Next

            For i = 0 To nCV - 1
                ControlledVariables(i).LastValue = cvValues(i)
            Next

            For i = 0 To nDV - 1
                DisturbanceVariables(i).LastValue = dvValues(i)
            Next

        End Sub

        Private Sub BuildGain(P As Integer, M As Integer)

            Dim nCV = ControlledVariables.Count
            Dim nMV = ManipulatedVariables.Count
            Dim rows = nCV * P
            Dim cols = nMV * M

            'dynamic matrix: block (cv, mv) holds s(k - l + 1) at prediction k and move l, k >= l
            Dim Amat(rows - 1, cols - 1) As Double
            For Each model In StepResponseModels
                If Not IsValid(model, nCV, nMV) Then Continue For
                For k = 1 To P
                    For l = 1 To Math.Min(k, M)
                        Amat(model.CVIndex * P + k - 1, model.MVIndex * M + l - 1) += model.Coefficient(k - l + 1)
                    Next
                Next
            Next

            'Q: CV weights along the prediction horizon
            Dim AtQ(cols - 1, rows - 1) As Double
            For r = 0 To rows - 1
                Dim q = ControlledVariables(r \ P).Weight
                For c = 0 To cols - 1
                    AtQ(c, r) = Amat(r, c) * q
                Next
            Next

            Dim H(cols - 1, cols - 1) As Double
            For c1 = 0 To cols - 1
                For c2 = c1 To cols - 1
                    Dim sum = 0.0
                    For r = 0 To rows - 1
                        sum += AtQ(c1, r) * Amat(r, c2)
                    Next
                    H(c1, c2) = sum
                    H(c2, c1) = sum
                Next
            Next

            'R: lambda times the mean diagonal of the MV's block, so lambda carries no units
            Dim lambda = Math.Max(MoveSuppressionWeight, 0.000001)
            For j = 0 To nMV - 1
                Dim scale = 0.0
                For l = 0 To M - 1
                    scale += H(j * M + l, j * M + l)
                Next
                scale /= M
                If Not scale > 0.0 Then scale = 1.0
                For l = 0 To M - 1
                    H(j * M + l, j * M + l) += lambda * scale
                Next
            Next

            Dim gainMatrix = SolveLinear(H, AtQ)

            GainRows = New Double(nMV - 1, rows - 1) {}
            For j = 0 To nMV - 1
                For r = 0 To rows - 1
                    GainRows(j, r) = gainMatrix(j * M, r)
                Next
            Next

        End Sub

        Private Shared Function SolveLinear(H As Double(,), B As Double(,)) As Double(,)

            'Gaussian elimination with partial pivoting, several right-hand sides
            Dim size = H.GetLength(0)
            Dim nrhs = B.GetLength(1)
            Dim mat = DirectCast(H.Clone(), Double(,))
            Dim x = DirectCast(B.Clone(), Double(,))

            For k = 0 To size - 1
                Dim piv = k
                For r = k + 1 To size - 1
                    If Math.Abs(mat(r, k)) > Math.Abs(mat(piv, k)) Then piv = r
                Next
                If piv <> k Then
                    For c = 0 To size - 1
                        Dim tmp = mat(k, c) : mat(k, c) = mat(piv, c) : mat(piv, c) = tmp
                    Next
                    For c = 0 To nrhs - 1
                        Dim tmp = x(k, c) : x(k, c) = x(piv, c) : x(piv, c) = tmp
                    Next
                End If
                If mat(k, k) = 0.0 Then Continue For
                For r = k + 1 To size - 1
                    Dim f = mat(r, k) / mat(k, k)
                    If f = 0.0 Then Continue For
                    For c = k To size - 1
                        mat(r, c) -= f * mat(k, c)
                    Next
                    For c = 0 To nrhs - 1
                        x(r, c) -= f * x(k, c)
                    Next
                Next
            Next

            For k = size - 1 To 0 Step -1
                For c = 0 To nrhs - 1
                    Dim sum = x(k, c)
                    For j = k + 1 To size - 1
                        sum -= mat(k, j) * x(j, c)
                    Next
                    x(k, c) = If(mat(k, k) <> 0.0, sum / mat(k, k), 0.0)
                Next
            Next

            Return x

        End Function

        Private Function GetModelLength(ts As Double, P As Integer) As Integer
            'long enough for the prediction horizon and for every model to settle
            Dim length As Double = P + 1
            For Each model In StepResponseModels
                length = Math.Max(length, model.SettlingSamples(ts))
            Next
            For Each model In ActiveDisturbanceModels()
                length = Math.Max(length, model.SettlingSamples(ts))
            Next
            Return CInt(Math.Min(length, Math.Max(MaxModelLength, P + 1)))
        End Function

        Private Shared Function IsValid(model As StepResponseModel, nCV As Integer, nMV As Integer) As Boolean
            Return model.CVIndex >= 0 AndAlso model.CVIndex < nCV AndAlso model.MVIndex >= 0 AndAlso model.MVIndex < nMV
        End Function

        Private Function GetModelKey(ts As Double, P As Integer) As String
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim sb As New System.Text.StringBuilder()
            sb.Append(ts.ToString("R", ci)).Append("|"c).Append(P).Append("|"c)
            sb.Append(ControlledVariables.Count).Append("|"c).Append(ManipulatedVariables.Count)
            For Each model In StepResponseModels
                sb.Append("|"c).Append(model.CVIndex).Append(","c).Append(model.MVIndex).Append(","c)
                sb.Append(model.Gain.ToString("R", ci)).Append(","c).Append(model.TimeConstant.ToString("R", ci)).Append(","c)
                sb.Append(model.DeadTime.ToString("R", ci)).Append(","c).Append(model.Integrating)
            Next
            Dim dvModels = ActiveDisturbanceModels()
            If dvModels.Count > 0 Then
                sb.Append("|D").Append(DisturbanceVariables.Count)
                For Each model In dvModels
                    sb.Append("|"c).Append(model.CVIndex).Append(","c).Append(model.DVIndex).Append(","c)
                    sb.Append(model.Gain.ToString("R", ci)).Append(","c).Append(model.TimeConstant.ToString("R", ci)).Append(","c)
                    sb.Append(model.DeadTime.ToString("R", ci)).Append(","c).Append(model.Integrating)
                Next
            End If
            Return sb.ToString()
        End Function

        Private Function GetTuningKey(M As Integer) As String
            Dim ci = Globalization.CultureInfo.InvariantCulture
            Dim sb As New System.Text.StringBuilder()
            sb.Append(M).Append("|"c).Append(MoveSuppressionWeight.ToString("R", ci))
            For Each cv In ControlledVariables
                sb.Append("|"c).Append(cv.Weight.ToString("R", ci))
            Next
            Return sb.ToString()
        End Function

        Private Function GetIntegrator() As IDynamicsIntegrator
            Try
                If Not FlowSheet.DynamicMode Then Return Nothing
                Dim dm = FlowSheet.DynamicsManager
                If dm Is Nothing OrElse dm.CurrentSchedule Is Nothing Then Return Nothing
                If Not dm.ScheduleList.ContainsKey(dm.CurrentSchedule) Then Return Nothing
                Dim id = dm.ScheduleList(dm.CurrentSchedule).CurrentIntegrator
                If id Is Nothing OrElse Not dm.IntegratorList.ContainsKey(id) Then Return Nothing
                Return dm.IntegratorList(id)
            Catch ex As Exception
                Return Nothing
            End Try
        End Function

        ''' <summary>Builds the trend chart with the given name.</summary>
        ''' <param name="name">The chart name: "CV Trends" (also used for an empty name), "MV Trends" or
        ''' "DV Trends".</param>
        ''' <returns>An OxyPlot <c>PlotModel</c> with one series per controlled, manipulated or disturbance
        ''' variable.</returns>
        Public Overrides Function GetChartModel(name As String) As Object

            Dim model = New PlotModel() With {.Subtitle = name, .Title = If(GraphicObject IsNot Nothing, GraphicObject.Tag, "MPC")}

            model.TitleFontSize = 12
            model.SubtitleFontSize = 10

            model.Axes.Add(New LinearAxis() With {
                .MajorGridlineStyle = LineStyle.Dash,
                .MinorGridlineStyle = LineStyle.Dot,
                .Position = AxisPosition.Bottom,
                .FontSize = 10,
                .Title = "Step"
            })

            model.Axes.Add(New LinearAxis() With {
                .MajorGridlineStyle = LineStyle.Dash,
                .MinorGridlineStyle = LineStyle.Dot,
                .Position = AxisPosition.Left,
                .FontSize = 10,
                .Title = "Value"
            })

            If name = "CV Trends" OrElse name = "" Then
                For cv = 0 To ControlledVariables.Count - 1
                    Dim series = New LineSeries() With {
                        .Title = ControlledVariables(cv).Name,
                        .MarkerType = MarkerType.None
                    }
                    For i = 0 To CVHistory.Count - 1
                        If cv < CVHistory(i).Length Then
                            series.Points.Add(New DataPoint(i, CVHistory(i)(cv)))
                        End If
                    Next
                    model.Series.Add(series)
                Next
            End If

            If name = "MV Trends" Then
                For mv = 0 To ManipulatedVariables.Count - 1
                    Dim series = New LineSeries() With {
                        .Title = ManipulatedVariables(mv).Name,
                        .MarkerType = MarkerType.None
                    }
                    For i = 0 To MVHistory.Count - 1
                        If mv < MVHistory(i).Length Then
                            series.Points.Add(New DataPoint(i, MVHistory(i)(mv)))
                        End If
                    Next
                    model.Series.Add(series)
                Next
            End If

            If name = "DV Trends" Then
                For dv = 0 To DisturbanceVariables.Count - 1
                    Dim series = New LineSeries() With {
                        .Title = DisturbanceVariables(dv).Name,
                        .MarkerType = MarkerType.None
                    }
                    For i = 0 To DVHistory.Count - 1
                        If dv < DVHistory(i).Length Then
                            series.Points.Add(New DataPoint(i, DVHistory(i)(dv)))
                        End If
                    Next
                    model.Series.Add(series)
                Next
            End If

            model.LegendFontSize = 10
            model.LegendPlacement = LegendPlacement.Outside

            Return model

        End Function

        ''' <summary>Returns the names of the charts this controller can embed in the flowsheet.</summary>
        ''' <returns>A list with "CV Trends" and "MV Trends", and "DV Trends" when the controller has
        ''' disturbance variables.</returns>
        Public Overrides Function GetChartModelNames() As List(Of String)
            Dim names As New List(Of String) From {"CV Trends", "MV Trends"}
            If DisturbanceVariables.Count > 0 Then names.Add("DV Trends")
            Return names
        End Function

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim proplist As New List(Of String)
            Select Case proptype
                Case PropertyType.ALL, PropertyType.RO, PropertyType.RW, PropertyType.WR
                    proplist.Add("Prediction Horizon")
                    proplist.Add("Control Horizon")
                    proplist.Add("Sample Time")
                    proplist.Add("Move Suppression Weight")
                    proplist.Add("Active")
                    proplist.Add("Use Measured Disturbances")
            End Select
            Return proplist.ToArray()
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Prediction Horizon" : Return PredictionHorizon
                Case "Control Horizon" : Return ControlHorizon
                Case "Sample Time" : Return SampleTime
                Case "Move Suppression Weight" : Return MoveSuppressionWeight
                Case "Active" : Return Active
                Case "Use Measured Disturbances" : Return UseMeasuredDisturbances
            End Select
            Return Nothing
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Sample Time" : Return "s"
                Case Else : Return ""
            End Select
        End Function

        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean
            Select Case prop
                Case "Prediction Horizon" : PredictionHorizon = Convert.ToInt32(propval)
                Case "Control Horizon" : ControlHorizon = Convert.ToInt32(propval)
                Case "Sample Time" : SampleTime = Convert.ToDouble(propval)
                Case "Move Suppression Weight" : MoveSuppressionWeight = Convert.ToDouble(propval)
                Case "Active" : Active = Convert.ToBoolean(propval)
                Case "Use Measured Disturbances" : UseMeasuredDisturbances = Convert.ToBoolean(propval)
            End Select
            Return True
        End Function

    End Class

End Namespace
