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

        Public Function GetCurrentValue(fs As IFlowsheet) As Double
            Dim obj = fs.SimulationObjects.Values.Where(Function(x) x.Name = ObjectID).SingleOrDefault()
            If obj IsNot Nothing Then
                Return SystemsOfUnits.Converter.ConvertFromSI(Units, obj.GetPropertyValue(PropertyName))
            End If
            Return 0.0
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
        Public Property StepCoefficients As New List(Of Double)
        Public Property Gain As Double = 1.0
        Public Property TimeConstant As Double = 60.0
        Public Property DeadTime As Double = 0.0

        Public Sub GenerateFromFOPDT(sampleTime As Double, horizonLength As Integer)
            StepCoefficients.Clear()
            For i = 0 To horizonLength - 1
                Dim t = (i + 1) * sampleTime
                Dim tEff = t - DeadTime
                If tEff <= 0 Then
                    StepCoefficients.Add(0.0)
                Else
                    StepCoefficients.Add(Gain * (1.0 - Math.Exp(-tEff / Math.Max(TimeConstant, 0.001))))
                End If
            Next
        End Sub

    End Class

    <System.Serializable()> Public Partial Class MPCController

        Inherits UnitOperations.SpecialOpBaseClass

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Controllers

        Public Property Active As Boolean = True

        Public Property PredictionHorizon As Integer = 30
        Public Property ControlHorizon As Integer = 5
        Public Property SampleTime As Double = 1.0

        Public Property ControlledVariables As New List(Of MPCVariable)
        Public Property ManipulatedVariables As New List(Of MPCVariable)
        Public Property DisturbanceVariables As New List(Of MPCVariable)

        Public Property StepResponseModels As New List(Of StepResponseModel)

        Public Property MoveSuppressionWeight As Double = 0.1

        Public Property ExecutionOrder As Integer = 0

        Private PastMoves As New List(Of Double())
        Private Predictions As New List(Of Double())

        Public Property CVHistory As New List(Of Double())
        Public Property MVHistory As New List(Of Double())

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(name As String, description As String)
            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New MPCController()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of MPCController)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function GetDisplayName() As String
            Return "MPC Controller"
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Model Predictive Controller (DMC)"
        End Function

        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.control_panel.png")

        End Function

        <System.NonSerialized> Private f As Object

        Public Sub Reset()
            PastMoves.Clear()
            Predictions.Clear()
            CVHistory.Clear()
            MVHistory.Clear()
            For Each cv In ControlledVariables
                cv.LastValue = 0.0
            Next
            For Each mv In ManipulatedVariables
                mv.LastValue = 0.0
            Next
        End Sub

        Public Sub InitializeModels()
            For Each model In StepResponseModels
                model.GenerateFromFOPDT(SampleTime, PredictionHorizon)
            Next
        End Sub

        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            If ControlledVariables.Count = 0 OrElse ManipulatedVariables.Count = 0 Then Return
            If StepResponseModels.Count = 0 Then Return

            Dim nCV = ControlledVariables.Count
            Dim nMV = ManipulatedVariables.Count
            Dim P = PredictionHorizon
            Dim M = Math.Min(ControlHorizon, P)

            For Each model In StepResponseModels
                If model.StepCoefficients.Count = 0 Then
                    model.GenerateFromFOPDT(SampleTime, P)
                End If
            Next

            Dim cvValues(nCV - 1) As Double
            Dim mvValues(nMV - 1) As Double

            For i = 0 To nCV - 1
                cvValues(i) = ControlledVariables(i).GetCurrentValue(FlowSheet)
            Next
            For i = 0 To nMV - 1
                mvValues(i) = ManipulatedVariables(i).GetCurrentValue(FlowSheet)
            Next

            CVHistory.Add(DirectCast(cvValues.Clone(), Double()))
            MVHistory.Add(DirectCast(mvValues.Clone(), Double()))

            Dim setpoints(nCV - 1) As Double
            For i = 0 To nCV - 1
                setpoints(i) = (ControlledVariables(i).MinValue + ControlledVariables(i).MaxValue) / 2.0
            Next

            Dim errors(nCV - 1) As Double
            For i = 0 To nCV - 1
                errors(i) = setpoints(i) - cvValues(i)
            Next

            Dim bestMoves(nMV - 1) As Double

            For mv = 0 To nMV - 1
                Dim totalMove As Double = 0.0

                For cv = 0 To nCV - 1
                    Dim model = StepResponseModels.Where(
                        Function(srm) srm.CVIndex = cv AndAlso srm.MVIndex = mv).FirstOrDefault()
                    If model Is Nothing Then Continue For
                    If model.StepCoefficients.Count = 0 Then Continue For

                    Dim s1 = model.StepCoefficients(0)
                    If Math.Abs(s1) < 1.0E-20 Then
                        If model.StepCoefficients.Count > 1 Then s1 = model.StepCoefficients(1)
                        If Math.Abs(s1) < 1.0E-20 Then Continue For
                    End If

                    Dim cvWeight = ControlledVariables(cv).Weight
                    totalMove += cvWeight * errors(cv) / s1
                Next

                Dim suppression = 1.0 / (1.0 + MoveSuppressionWeight)
                bestMoves(mv) = totalMove * suppression

                Dim newMV = mvValues(mv) + bestMoves(mv)
                If newMV > ManipulatedVariables(mv).MaxValue Then newMV = ManipulatedVariables(mv).MaxValue
                If newMV < ManipulatedVariables(mv).MinValue Then newMV = ManipulatedVariables(mv).MinValue

                ManipulatedVariables(mv).SetCurrentValue(FlowSheet, newMV)
                ManipulatedVariables(mv).LastValue = newMV
            Next

            PastMoves.Add(bestMoves)

            For i = 0 To nCV - 1
                ControlledVariables(i).LastValue = cvValues(i)
            Next

        End Sub

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

            model.LegendFontSize = 10
            model.LegendPlacement = LegendPlacement.Outside

            Return model

        End Function

        Public Overrides Function GetChartModelNames() As List(Of String)
            Return New List(Of String) From {"CV Trends", "MV Trends"}
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
            End Select
            Return True
        End Function

    End Class

End Namespace
