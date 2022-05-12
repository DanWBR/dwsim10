'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


Imports DWSIMCore.Foundation.PropertyPackages
''' <summary>
''' This interface defines the basic properties of a flash algorithm, including an instance of the class which contains its current settings.
''' </summary>
Public Interface IFlashAlgorithm

    Property FlashSettings As Dictionary(Of Enums.FlashSetting, String)

    Function Clone() As IFlashAlgorithm

    ReadOnly Property AlgoType As Enums.FlashMethod

    ReadOnly Property Name As String

    ReadOnly Property Description As String

    Property Tag As String

    ReadOnly Property InternalUseOnly As Boolean

    ReadOnly Property MobileCompatible As Boolean

    Property Order As Integer
    Function Flash_PT(Vz() As Double, P As Double, T As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = Nothing, Optional PrevKi() As Double = Nothing) As Object
    Function Flash_PH(Vz() As Double, P As Double, H As Double, Tref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = Nothing, Optional PrevKi() As Double = Nothing) As Object
    Function Flash_PS(Vz() As Double, P As Double, S As Double, Tref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = Nothing, Optional PrevKi() As Double = Nothing) As Object
    Function Flash_PV(Vz() As Double, P As Double, V As Double, Tref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = Nothing, Optional PrevKi() As Double = Nothing) As Object
    Function Flash_TV(Vz() As Double, T As Double, V As Double, Pref As Double, PP As PropertyPackage, Optional ReuseKI As Boolean = Nothing, Optional PrevKi() As Double = Nothing) As Object
End Interface

''' <summary>
''' This interface defines the parameters of a flash calculation result.
''' </summary>
Public Interface IFlashCalculationResult

    Property BaseMoleAmount As Double
    Property Kvalues As List(Of Double)
    Property MixtureMoleAmounts As List(Of Double)
    Property VaporPhaseMoleAmounts As List(Of Double)
    Property LiquidPhase1MoleAmounts As List(Of Double)
    Property LiquidPhase2MoleAmounts As List(Of Double)
    Property SolidPhaseMoleAmounts As List(Of Double)
    Property CalculatedTemperature As Nullable(Of Double)
    Property CalculatedPressure As Nullable(Of Double)
    Property CalculatedEnthalpy As Nullable(Of Double)
    Property CalculatedEntropy As Nullable(Of Double)
    Property CompoundProperties As List(Of ICompoundConstantProperties)
    Property FlashAlgorithmType As String
    Property ResultException As Exception
    Property IterationsTaken As Integer
    Property TimeTaken As TimeSpan

    Function GetVaporPhaseMoleFractions() As Double()

    Function GetLiquidPhase1MoleFractions() As Double()

    Function GetLiquidPhase2MoleFractions() As Double()

    Function GetSolidPhaseMoleFractions() As Double()

    Function GetVaporPhaseMoleFraction() As Double

    Function GetLiquidPhase1MoleFraction() As Double

    Function GetLiquidPhase2MoleFraction() As Double

    Function GetSolidPhaseMoleFraction() As Double

    Function GetVaporPhaseMassFractions() As Double()

    Function GetLiquidPhase1MassFractions() As Double()

    Function GetLiquidPhase2MassFractions() As Double()

    Function GetSolidPhaseMassFractions() As Double()

    Function ConvertToMassFractions(ByVal Vz As Double()) As Double()

    Function CalcMolarWeight(ByVal Vz() As Double) As Double

    Function GetVaporPhaseMassFraction() As Double

    Function GetLiquidPhase1MassFraction() As Double

    Function GetLiquidPhase2MassFraction() As Double

    Function GetSolidPhaseMassFraction() As Double

End Interface
