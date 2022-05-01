
Imports DWSIMCore.Foundation.Enums

Namespace UnitOperations

    <System.Serializable()> Public Class Switch

        Inherits UnitOperations.UnitOpBaseClass

        Implements ISwitch

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Switches

        Public Property SelectedObjectID As String = "" Implements ISwitch.SelectedObjectID

        Public Property SelectedProperty As String = "" Implements ISwitch.SelectedProperty

        Public Property SelectedPropertyType As UnitOfMeasure = UnitOfMeasure.none Implements ISwitch.SelectedPropertyType

        Public Property SelectedPropertyUnits As String = "" Implements ISwitch.SelectedPropertyUnits

        Public Property OffValue As Double = 0.0 Implements ISwitch.OffValue

        Public Property OnValue As Double = 0.0 Implements ISwitch.OnValue

        Public Property IsOn As Boolean = False Implements ISwitch.IsOn

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Switch()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Switch)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Sub New()
            MyBase.New()
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            Dim SimObject = GetFlowsheet.SimulationObjects.Values.Where(Function(x) x.Name = SelectedObjectID).SingleOrDefault

            If IsOn Then
                SimObject.SetPropertyValue(SelectedProperty, OnValue.ConvertToSI(SelectedPropertyUnits))
            Else
                SimObject.SetPropertyValue(SelectedProperty, OffValue.ConvertToSI(SelectedPropertyUnits))
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
            Using imgstream As IO.Stream = System.Reflection.Assembly.GetAssembly(Me.GetType).GetManifestResourceStream("DWSIMCore.Foundation.switch_on.png")
                Using bitmap = SkiaSharp.SKBitmap.Decode(imgstream)
                    Return SkiaSharp.SKImage.FromBitmap(bitmap)
                End Using
            End Using
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Switch"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Switch"
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace


