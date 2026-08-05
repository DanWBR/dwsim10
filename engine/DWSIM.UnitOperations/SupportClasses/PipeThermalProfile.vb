''' <summary>
''' Contains auxiliary data classes for pipe segment thermal and hydraulic profile editing,
''' including thermal definitions and hydraulic profile data.
''' </summary>
Namespace UnitOperations.Auxiliary.Pipe

    <System.Serializable()> Public Class ThermalEditorDefinitions

        Implements Interfaces.ICustomXMLSerialization

        Public Property UseUserDefinedU As Boolean = False
        Public Property UserDefinedU_Length As New List(Of Double)
        Public Property UserDefinedU_Temp As New List(Of Double)
        Public Property UserDefinedU_U As New List(Of Double)
        Public Property IncludeSolarRadiation As Boolean = False
        Public Property UseGlobalSolarRadiation As Boolean = False
        Public Property SolarRadiationValue_kWh_m2 As Double = 3.0
        Public Property SolarRadiationAbsorptionEfficiency As Double = 0.1

        'External surface emissivity for thermal radiation to ambient (dimensionless, 0-1).
        'Default 0.8 is typical for oxidized/painted carbon steel. Used only when the external
        'medium is air ("Meio = 0"). Set to 0 to disable the radiation contribution.
        Public Property SurfaceEmissivity As Double = 0.8

        Public Sub New()

        End Sub

        Public Property AmbientTemperatureGradient As Double = 0.0#

        Public Property AmbientTemperatureGradient_EstimateHTC As Double = 0.0#

        Public Property Incluir_isolamento As Boolean = False

        Public Property Incluir_cte As Boolean = False

        Public Property Incluir_cti As Boolean = False

        Public Property Incluir_paredes As Boolean = False

        Public Property Meio As Integer = 0

        Public Property Material As Integer = 0

        Public Property Velocidade As Double = 0.0

        Public Property Espessura As Double = 0.0

        Public Property Condtermica As Double = 0.0

        Public Property Temp_amb_estimar As Double = 298.15

        Public Property Calor_trocado As Double = 0.0

        Public Property Temp_amb_definir As Double = 298.15

        Public Property CGTC_Definido As Double = 0.0

        Public Property TipoPerfil As ThermalProfileType = ThermalProfileType.Definir_CGTC

        Public Enum ThermalProfileType
            Definir_CGTC = 0
            Definir_Q = 1
            Estimar_CGTC = 2
        End Enum

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data)
            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = XMLSerializer.XMLSerializer.Serialize(Me)

            Return elements

        End Function

    End Class

End Namespace

