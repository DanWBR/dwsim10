Imports DWSIMCore.Foundation.Enums

Namespace UnitOperations

    <System.Serializable()> Public Class AnalogGauge

        Inherits UnitOperations.UnitOpBaseClass

        Implements IIndicator

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Indicators

        Public Property DecimalDigits As Integer = 2 Implements IIndicator.DecimalDigits

        Public Property IntegralDigits As Integer = 4 Implements IIndicator.IntegralDigits

        Public Property MinimumValue As Double Implements IIndicator.MinimumValue

        Public Property MaximumValue As Double = 100 Implements IIndicator.MaximumValue

        Public Property CurrentValue As Double Implements IIndicator.CurrentValue

        Public Property SelectedObjectID As String = "" Implements IIndicator.SelectedObjectID

        Public Property SelectedProperty As String = "" Implements IIndicator.SelectedProperty

        Public Property SelectedPropertyType As UnitOfMeasure = UnitOfMeasure.none Implements IIndicator.SelectedPropertyType

        Public Property SelectedPropertyUnits As String = "" Implements IIndicator.SelectedPropertyUnits

        Public Property VeryLowAlarmEnabled As Boolean = False Implements IIndicator.VeryLowAlarmEnabled

        Public Property LowAlarmEnabled As Boolean = False Implements IIndicator.LowAlarmEnabled

        Public Property HighAlarmEnabled As Boolean = False Implements IIndicator.HighAlarmEnabled

        Public Property VeryHighAlarmEnabled As Boolean = False Implements IIndicator.VeryHighAlarmEnabled

        Public Property VeryLowAlarmValue As Double Implements IIndicator.VeryLowAlarmValue

        Public Property LowAlarmValue As Double Implements IIndicator.LowAlarmValue

        Public Property HighAlarmValue As Double Implements IIndicator.HighAlarmValue

        Public Property VeryHighAlarmValue As Double Implements IIndicator.VeryHighAlarmValue

        Public Property VeryLowAlarmActive As Boolean = False Implements IIndicator.VeryLowAlarmActive

        Public Property LowAlarmActive As Boolean = False Implements IIndicator.LowAlarmActive

        Public Property HighAlarmActive As Boolean = False Implements IIndicator.HighAlarmActive

        Public Property VeryHighAlarmActive As Boolean = False Implements IIndicator.VeryHighAlarmActive

        Public Property ShowAlarms As Boolean = False Implements IIndicator.ShowAlarms

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        Public Property DisplayInPercent As Boolean = False Implements IIndicator.DisplayInPercent

        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New AnalogGauge()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of AnalogGauge)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Sub New()
            MyBase.New()
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If GetFlowsheet.SimulationObjects.ContainsKey(SelectedObjectID) Then

                Try

                    Dim SelectedObject = GetFlowsheet.SimulationObjects.Values.Where(Function(x) x.Name = SelectedObjectID).FirstOrDefault

                    Dim currentvalue = SystemsOfUnits.Converter.ConvertFromSI(SelectedPropertyUnits, SelectedObject.GetPropertyValue(SelectedProperty))

                    VeryLowAlarmActive = currentvalue <= VeryLowAlarmValue And VeryLowAlarmEnabled

                    LowAlarmActive = currentvalue <= LowAlarmValue And LowAlarmEnabled

                    HighAlarmActive = currentvalue >= HighAlarmValue And HighAlarmEnabled

                    VeryHighAlarmActive = currentvalue >= VeryHighAlarmValue And VeryHighAlarmEnabled

                Catch ex As Exception

                End Try

            End If

        End Sub

        Public Overrides Sub DeCalculate()

        End Sub

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As IUnitsOfMeasure = Nothing) As Object

            Return ""

        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Enums.PropertyType) As String()

            Return New String() {}

        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As IUnitsOfMeasure = Nothing) As Boolean

            Return True

        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As IUnitsOfMeasure = Nothing) As String

            Return ""

        End Function

        Public Overrides Function GetIconBitmap() As Object
            Using imgstream As IO.Stream = System.Reflection.Assembly.GetAssembly(Me.GetType).GetManifestResourceStream("DWSIMCore.Foundation.analog_gauge1.png")
                Using bitmap = SkiaSharp.SKBitmap.Decode(imgstream)
                    Return SkiaSharp.SKImage.FromBitmap(bitmap)
                End Using
            End Using
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Analog Gauge"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Analog Gauge"
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace


