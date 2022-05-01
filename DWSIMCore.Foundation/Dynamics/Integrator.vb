Public Class Integrator

    Implements IDynamicsIntegrator, ICustomXMLSerialization

    Public Property ID As String = "" Implements IDynamicsIntegrator.ID

    Public Property Description As String = "" Implements IDynamicsIntegrator.Description

    Public Property ShouldCalculateEquilibrium As Boolean Implements IDynamicsIntegrator.ShouldCalculateEquilibrium

    Public Property ShouldCalculatePressureFlow As Boolean Implements IDynamicsIntegrator.ShouldCalculatePressureFlow

    Public Property ShouldCalculateControl As Boolean Implements IDynamicsIntegrator.ShouldCalculateControl

    Public Property IntegrationStep As TimeSpan = New TimeSpan(0, 0, 5) Implements IDynamicsIntegrator.IntegrationStep

    Public Property Duration As TimeSpan = New TimeSpan(0, 10, 0) Implements IDynamicsIntegrator.Duration

    Public Property CurrentTime As Date = New Date() Implements IDynamicsIntegrator.CurrentTime

    Public Property CalculationRateEquilibrium As Integer = 1 Implements IDynamicsIntegrator.CalculationRateEquilibrium

    Public Property CalculationRatePressureFlow As Integer = 1 Implements IDynamicsIntegrator.CalculationRatePressureFlow

    Public Property CalculationRateControl As Integer = 1 Implements IDynamicsIntegrator.CalculationRateControl

    Public Property RealTime As Boolean = False Implements IDynamicsIntegrator.RealTime

    Public Property MonitoredVariableValues As Dictionary(Of Long, List(Of IDynamicsMonitoredVariable)) = New Dictionary(Of Long, List(Of IDynamicsMonitoredVariable)) Implements IDynamicsIntegrator.MonitoredVariableValues

    Public Property MonitoredVariables As List(Of IDynamicsMonitoredVariable) = New List(Of IDynamicsMonitoredVariable) Implements IDynamicsIntegrator.MonitoredVariables

    Public Property RealTimeStepMs As Integer = 1000 Implements IDynamicsIntegrator.RealTimeStepMs

    Public Function SaveData() As List(Of XElement) Implements ICustomXMLSerialization.SaveData
        Dim data = XMLSerializer.Serialize(Me)
        Dim e3 = New XElement("MonitoredVariables")
        For Each item As ICustomXMLSerialization In MonitoredVariables
            Dim e4 = New XElement("MonitoredVariable")
            e4.Add(item.SaveData)
            e3.Add(e4)
        Next
        data.Add(e3)
        Return data
    End Function

    Public Function LoadData(data As List(Of XElement)) As Boolean Implements ICustomXMLSerialization.LoadData
        XMLSerializer.Deserialize(Me, data)
        Dim elm2 As XElement = (From xel2 As XElement In data Select xel2 Where xel2.Name = "MonitoredVariables").LastOrDefault
        If Not elm2 Is Nothing Then
            MonitoredVariables = New List(Of IDynamicsMonitoredVariable)
            For Each el In elm2.Elements
                Dim item As New MonitoredVariable
                item.LoadData(el.Elements.ToList)
                MonitoredVariables.Add(item)
            Next
        End If
        Return True
    End Function

End Class
