Public Interface IDimension

    Property ID As String

    Property Name As Enums.DimensionName

    Property Value As Double

    Property IsUserDefined As Boolean

    Property UserDefinedValue As Double

    Function GetDisplayName() As String

    Function GetUnitsType() As Enums.UnitOfMeasure

End Interface
