<System.Serializable()> Public Class CalculationArgs

    Implements ICalculationArgs

    Public Property Sender As String = "" Implements ICalculationArgs.Sender
    Public Property Calculated As Boolean = False Implements ICalculationArgs.Calculated
    Public Property Tag As String = "" Implements ICalculationArgs.Tag
    Public Property Name As String = "" Implements ICalculationArgs.Name
    Public Property ObjectType As Enums.GraphicObjects.ObjectType = Enums.GraphicObjects.ObjectType.Nenhum Implements ICalculationArgs.ObjectType

End Class

