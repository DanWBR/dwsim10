'    Flash Algorithm Abstract Base Class
'    Copyright 2010-2022 Daniel Wagner O. de Medeiros
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

Imports System.Math
Imports System.Threading.Tasks
Imports DWSIM.Thermodynamics.PropertyPackages.ThermoPlugs
Imports DWSIM.Interfaces.Enums.FlashSetting
Imports System.Linq
Imports System.IO
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.Runtime.Serialization
Imports IronPython.Runtime.Operations
Imports DWSIM.SharedClasses
Imports DWSIM.Interfaces

''' <summary>
''' Implements phase equilibrium flash algorithms including nested loops, Gibbs minimization,
''' inside-out methods, and specialized algorithms for steam, seawater, electrolytes, and black oil.
''' </summary>
Namespace PropertyPackages.Auxiliary.FlashAlgorithms

    ''' <summary>
    ''' This is the base class for the flash algorithms.
    ''' </summary>
    ''' <remarks></remarks>
    <System.Serializable()> Public MustInherit Class FlashAlgorithm

        Implements Interfaces.IFlashAlgorithm, Interfaces.ICustomXMLSerialization

        Public Property Order As Integer = 1000 Implements Interfaces.IFlashAlgorithm.Order

        Public Property FlashSettings As New Dictionary(Of Interfaces.Enums.FlashSetting, String) Implements Interfaces.IFlashAlgorithm.FlashSettings

        Public Property StabSearchSeverity As Integer
            Get
                Return Integer.Parse(FlashSettings(ThreePhaseFlashStabTestSeverity))
            End Get
            Set(value As Integer)
                FlashSettings(ThreePhaseFlashStabTestSeverity) = value.ToString
            End Set
        End Property

        Public Property StabSearchCompIDs As String()
            Get
                Dim list1 As String() = FlashSettings(ThreePhaseFlashStabTestCompIds).ToArray(System.Globalization.CultureInfo.CurrentCulture, Type.GetType("System.String"))
                Dim list2 = list1.ToList()
                list2.Remove("")
                Return list2.ToArray
            End Get
            Set(value As String())
                Dim comps As String = ""
                For Each s As String In value
                    comps += s + ","
                Next
                FlashSettings(ThreePhaseFlashStabTestCompIds) = comps
            End Set
        End Property

        Private _P As Double, _Vz, _Vx1est, _Vx2est As Double(), _pp As PropertyPackage

        Sub New()

            FlashSettings = GetDefaultSettings()

        End Sub

        Public Overrides Function ToString() As String
            If Name <> "" Then
                Return Name
            Else
                Return MyBase.ToString()
            End If
        End Function

        Public Shared Function GetDefaultSettings() As Dictionary(Of Interfaces.Enums.FlashSetting, String)

            Dim settings As New Dictionary(Of Interfaces.Enums.FlashSetting, String)

            settings(Replace_PTFlash) = False
            settings(ValidateEquilibriumCalc) = False
            settings(UsePhaseIdentificationAlgorithm) = False
            settings(CalculateBubbleAndDewPoints) = False

            settings(ValidationGibbsTolerance) = "0.01"

            settings(PHFlash_Maximum_Number_Of_External_Iterations) = "100"
            settings(PHFlash_External_Loop_Tolerance) = "0.0001"
            settings(PHFlash_Maximum_Number_Of_Internal_Iterations) = "100"
            settings(PHFlash_Internal_Loop_Tolerance) = "0.0001"
            settings(PTFlash_Maximum_Number_Of_External_Iterations) = "100"
            settings(PTFlash_External_Loop_Tolerance) = "0.0001"
            settings(PTFlash_Maximum_Number_Of_Internal_Iterations) = "100"
            settings(PTFlash_Internal_Loop_Tolerance) = "0.0001"

            settings(NL_FastMode) = True

            settings(IO_FastMode) = True

            settings(GM_OptimizationMethod) = "IPOPT"

            settings(ThreePhaseFlashStabTestSeverity) = "0"

            settings(ThreePhaseFlashStabTestCompIds) = ""

            settings(PVFlash_FixedDampingFactor) = "1.0"
            settings(PVFlash_MaximumTemperatureChange) = "10.0"
            settings(PVFlash_TemperatureDerivativeEpsilon) = "0.1"

            settings(ST_Number_of_Random_Tries) = "20"

            settings(CheckIncipientLiquidForStability) = False

            settings(PHFlash_MaximumTemperatureChange) = "30.0"

            settings(PTFlash_DampingFactor) = "1.0"

            settings(ForceEquilibriumCalculationType) = "Default"

            settings(ImmiscibleWaterOption) = False

            settings(HandleSolidsInDefaultEqCalcMode) = False

            settings(UseIOFlash) = False

            settings(GibbsMinimizationExternalSolver) = ""

            settings(GibbsMinimizationExternalSolverConfigData) = ""

            settings(PHFlash_Use_Interpolated_Result_In_Oscillating_Temperature_Cases) = False

            settings(PVFlash_TryIdealCalcOnFailure) = True

            settings(FailSafeCalculationMode) = 1

            settings(PVFlash_FivePointStencilNumericalDerivative) = False

            settings(NestedLoops_v2) = False

            Return settings

        End Function

        Public Sub WriteDebugInfo(text As String)

            Calculator.WriteToConsole(text, 1)

        End Sub

        ''' <summary>
        ''' Calculates Phase Equilibria for a given mixture at specified conditions.
        ''' </summary>
        ''' <param name="spec1">Flash state specification 1</param>
        ''' <param name="spec2">Flash state specification 2</param>
        ''' <param name="val1">Value of the first flash state specification (P in Pa, T in K, H in kJ/kg, S in kJ/[kg.K], VAP/SF in mole fraction from 0 to 1)</param>
        ''' <param name="val2">Value of the second flash state specification (P in Pa, T in K, H in kJ/kg, S in kJ/[kg.K], VAP/SF in mole fraction from 0 to 1)</param>
        ''' <param name="pp">Property Package instance</param>
        ''' <param name="mixmolefrac">Vector of mixture mole fractions</param>
        ''' <param name="initialKval">Vector containing initial estimates for the K-values (set to 'Nothing' (VB) or 'null' (C#) if none).</param>
        ''' <param name="initialestimate">Initial estimate for Temperature (K) or Pressure (Pa), whichever will be calculated</param>
        ''' <returns>A FlashCalculationResult instance with the results of the calculations</returns>
        ''' <remarks>This function must be used instead of the older type-specific flash functions.
        ''' Check if the 'ResultException' property of the result object is nothing/null before proceeding.</remarks>

        Public Function CalculateEquilibrium(spec1 As FlashSpec, spec2 As FlashSpec,
                                            val1 As Double, val2 As Double,
                                            pp As PropertyPackage,
                                            mixmolefrac As Double(),
                                            initialKval As Double(),
                                            initialestimate As Double) As FlashCalculationResult

            Dim constprops As List(Of Interfaces.ICompoundConstantProperties) = pp.DW_GetConstantProperties()

            Dim d1, d2 As Date

            Dim result As Object = Nothing
            Dim calcresult As New FlashCalculationResult(constprops)

            With calcresult
                .MixtureMoleAmounts = New List(Of Double)(mixmolefrac)
                .FlashAlgorithmType = Me.GetType.ToString
                .FlashSpecification1 = spec1
                .FlashSpecification2 = spec2
            End With

            Dim useestimates = False

            If Not initialKval Is Nothing Then useestimates = True

            d1 = Date.Now

            Try
                If spec1 = FlashSpec.P And spec2 = FlashSpec.T Then
                    'PT = {L1, V, Vx1, Vy, ecount, L2, Vx2, S, Vs}
                    result = Flash_PT(mixmolefrac, val1, val2, pp, useestimates, initialKval)
                    With calcresult
                        .CalculatedPressure = val1
                        .CalculatedTemperature = val2
                        .Kvalues = New List(Of Double)(DirectCast(result(3), Double()).DivideY(DirectCast(result(2), Double())))
                        .VaporPhaseMoleAmounts = New List(Of Double)(DirectCast(result(3), Double()).MultiplyConstY(Convert.ToDouble(result(1))))
                        .LiquidPhase1MoleAmounts = New List(Of Double)(DirectCast(result(2), Double()).MultiplyConstY(Convert.ToDouble(result(0))))
                        .LiquidPhase2MoleAmounts = New List(Of Double)(DirectCast(result(6), Double()).MultiplyConstY(Convert.ToDouble(result(5))))
                        .SolidPhaseMoleAmounts = New List(Of Double)(DirectCast(result(8), Double()).MultiplyConstY(Convert.ToDouble(result(7))))
                        .IterationsTaken = Convert.ToInt32(result(4))
                        .CalculatedEnthalpy = CalculateMixtureEnthalpy(val2, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction, .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction,
                                                                       .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions, .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions,
                                                                       pp)
                        .CalculatedEntropy = CalculateMixtureEntropy(val2, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction, .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction,
                                                                       .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions, .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions,
                                                                       pp)
                    End With
                ElseIf spec1 = FlashSpec.T And spec2 = FlashSpec.P Then
                    'PT = {L1, V, Vx1, Vy, ecount, L2, Vx2, S, Vs}
                    result = Flash_PT(mixmolefrac, val2, val1, pp, useestimates, initialKval)
                    With calcresult
                        .CalculatedPressure = val2
                        .CalculatedTemperature = val1
                        .Kvalues = New List(Of Double)(DirectCast(result(3), Double()).DivideY(DirectCast(result(2), Double())))
                        .VaporPhaseMoleAmounts = New List(Of Double)(DirectCast(result(3), Double()).MultiplyConstY(Convert.ToDouble(result(1))))
                        .LiquidPhase1MoleAmounts = New List(Of Double)(DirectCast(result(2), Double()).MultiplyConstY(Convert.ToDouble(result(0))))
                        .LiquidPhase2MoleAmounts = New List(Of Double)(DirectCast(result(6), Double()).MultiplyConstY(Convert.ToDouble(result(5))))
                        .SolidPhaseMoleAmounts = New List(Of Double)(DirectCast(result(8), Double()).MultiplyConstY(Convert.ToDouble(result(7))))
                        .IterationsTaken = Convert.ToInt32(result(4))
                        .CalculatedEnthalpy = CalculateMixtureEnthalpy(val1, val2, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction, .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction,
                                                        .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions, .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions,
                                                        pp)
                        .CalculatedEntropy = CalculateMixtureEntropy(val1, val2, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction, .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction,
                                                                       .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions, .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions,
                                                                       pp)
                    End With
                ElseIf spec1 = FlashSpec.P And spec2 = FlashSpec.H Then
                    'PH, PS, PV {L1, V, Vx1, Vy, T, ecount, Ki1, L2, Vx2, S, Vs}
                    result = Flash_PH(mixmolefrac, val1, val2, initialestimate, pp, useestimates, initialKval)
                    With calcresult
                        .CalculatedPressure = val1
                        .Kvalues = New List(Of Double)(DirectCast(result(3), Double()).DivideY(DirectCast(result(2), Double())))
                        .VaporPhaseMoleAmounts = New List(Of Double)(DirectCast(result(3), Double()).MultiplyConstY(Convert.ToDouble(result(1))))
                        .LiquidPhase1MoleAmounts = New List(Of Double)(DirectCast(result(2), Double()).MultiplyConstY(Convert.ToDouble(result(0))))
                        .LiquidPhase2MoleAmounts = New List(Of Double)(DirectCast(result(8), Double()).MultiplyConstY(Convert.ToDouble(result(7))))
                        .SolidPhaseMoleAmounts = New List(Of Double)(DirectCast(result(10), Double()).MultiplyConstY(Convert.ToDouble(result(9))))
                        .CalculatedTemperature = Convert.ToDouble(result(4))
                        .IterationsTaken = Convert.ToInt32(result(5))
                        .CalculatedEnthalpy = CalculateMixtureEnthalpy(.CalculatedTemperature.GetValueOrDefault, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                       .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                       .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                        .CalculatedEntropy = CalculateMixtureEntropy(.CalculatedTemperature.GetValueOrDefault, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                     .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                     .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                    End With
                ElseIf spec1 = FlashSpec.P And spec2 = FlashSpec.S Then
                    'PH, PS, PV {L1, V, Vx1, Vy, T, ecount, Ki1, L2, Vx2, S, Vs}
                    result = Flash_PS(mixmolefrac, val1, val2, initialestimate, pp, useestimates, initialKval)
                    With calcresult
                        .CalculatedPressure = val1
                        .Kvalues = New List(Of Double)(DirectCast(result(3), Double()).DivideY(DirectCast(result(2), Double())))
                        .VaporPhaseMoleAmounts = New List(Of Double)(DirectCast(result(3), Double()).MultiplyConstY(Convert.ToDouble(result(1))))
                        .LiquidPhase1MoleAmounts = New List(Of Double)(DirectCast(result(2), Double()).MultiplyConstY(Convert.ToDouble(result(0))))
                        .LiquidPhase2MoleAmounts = New List(Of Double)(DirectCast(result(8), Double()).MultiplyConstY(Convert.ToDouble(result(7))))
                        .SolidPhaseMoleAmounts = New List(Of Double)(DirectCast(result(10), Double()).MultiplyConstY(Convert.ToDouble(result(9))))
                        .CalculatedTemperature = Convert.ToDouble(result(4))
                        .IterationsTaken = Convert.ToInt32(result(5))
                        .CalculatedEnthalpy = CalculateMixtureEnthalpy(.CalculatedTemperature.GetValueOrDefault, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                        .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                        .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                        .CalculatedEntropy = CalculateMixtureEntropy(.CalculatedTemperature.GetValueOrDefault, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                     .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                     .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                    End With
                ElseIf spec1 = FlashSpec.P And spec2 = FlashSpec.VAP Then
                    'PH, PS, PV {L1, V, Vx1, Vy, T, ecount, Ki1, L2, Vx2, S, Vs}
                    result = Flash_PV(mixmolefrac, val1, val2, initialestimate, pp, useestimates, initialKval)
                    With calcresult
                        .CalculatedPressure = val1
                        .Kvalues = New List(Of Double)(DirectCast(result(3), Double()).DivideY(DirectCast(result(2), Double())))
                        .VaporPhaseMoleAmounts = New List(Of Double)(DirectCast(result(3), Double()).MultiplyConstY(Convert.ToDouble(result(1))))
                        .LiquidPhase1MoleAmounts = New List(Of Double)(DirectCast(result(2), Double()).MultiplyConstY(Convert.ToDouble(result(0))))
                        .LiquidPhase2MoleAmounts = New List(Of Double)(DirectCast(result(8), Double()).MultiplyConstY(Convert.ToDouble(result(7))))
                        .SolidPhaseMoleAmounts = New List(Of Double)(DirectCast(result(10), Double()).MultiplyConstY(Convert.ToDouble(result(9))))
                        .CalculatedTemperature = Convert.ToDouble(result(4))
                        .IterationsTaken = Convert.ToInt32(result(5))
                        .CalculatedEnthalpy = CalculateMixtureEnthalpy(.CalculatedTemperature.GetValueOrDefault, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                             .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                             .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                        .CalculatedEntropy = CalculateMixtureEntropy(.CalculatedTemperature.GetValueOrDefault, val1, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                     .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                     .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                    End With
                ElseIf spec1 = FlashSpec.T And spec2 = FlashSpec.VAP Then
                    'TV {L1, V, Vx1, Vy, P, ecount, Ki1, L2, Vx2, S, Vs}
                    result = Flash_TV(mixmolefrac, val1, val2, initialestimate, pp, useestimates, initialKval)
                    With calcresult
                        .CalculatedTemperature = val1
                        .Kvalues = New List(Of Double)(DirectCast(result(3), Double()).DivideY(DirectCast(result(2), Double())))
                        .VaporPhaseMoleAmounts = New List(Of Double)(DirectCast(result(3), Double()).MultiplyConstY(Convert.ToDouble(result(1))))
                        .LiquidPhase1MoleAmounts = New List(Of Double)(DirectCast(result(2), Double()).MultiplyConstY(Convert.ToDouble(result(0))))
                        .LiquidPhase2MoleAmounts = New List(Of Double)(DirectCast(result(8), Double()).MultiplyConstY(Convert.ToDouble(result(7))))
                        .SolidPhaseMoleAmounts = New List(Of Double)(DirectCast(result(10), Double()).MultiplyConstY(Convert.ToDouble(result(9))))
                        .CalculatedPressure = Convert.ToDouble(result(4))
                        .IterationsTaken = Convert.ToInt32(result(5))
                        .CalculatedEnthalpy = CalculateMixtureEnthalpy(val1, .CalculatedPressure.GetValueOrDefault, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                            .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                            .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                        .CalculatedEntropy = CalculateMixtureEntropy(val1, .CalculatedPressure.GetValueOrDefault, .GetLiquidPhase1MoleFraction, .GetLiquidPhase2MoleFraction,
                                                                     .GetVaporPhaseMoleFraction, .GetSolidPhaseMoleFraction, .GetLiquidPhase1MoleFractions, .GetLiquidPhase2MoleFractions,
                                                                     .GetVaporPhaseMoleFractions, .GetSolidPhaseMoleFractions, pp)
                    End With
                ElseIf spec1 = FlashSpec.P And spec2 = FlashSpec.SF Then
                    'PH, PS, PV {L1, V, Vx1, Vy, T, ecount, Ki1, L2, Vx2, S, Vs}
                    result = Flash_PSF(mixmolefrac, val1, val2, initialestimate, pp, useestimates, initialKval)
                    With calcresult
                        .CalculatedPressure = val1
                        .Kvalues = New List(Of Double)(DirectCast(result(3), Double()).DivideY(DirectCast(result(2), Double())))
                        .VaporPhaseMoleAmounts = New List(Of Double)(DirectCast(result(3), Double()).MultiplyConstY(Convert.ToDouble(result(1))))
                        .LiquidPhase1MoleAmounts = New List(Of Double)(DirectCast(result(2), Double()).MultiplyConstY(Convert.ToDouble(result(0))))
                        .LiquidPhase2MoleAmounts = New List(Of Double)(DirectCast(result(8), Double()).MultiplyConstY(Convert.ToDouble(result(7))))
                        .SolidPhaseMoleAmounts = New List(Of Double)(DirectCast(result(10), Double()).MultiplyConstY(Convert.ToDouble(result(9))))
                        .CalculatedTemperature = Convert.ToDouble(result(4))
                        .IterationsTaken = Convert.ToInt32(result(5))
                    End With
                ElseIf spec1 = FlashSpec.T And spec2 = FlashSpec.SF Then
                    Throw New NotImplementedException("Flash specification set not supported.")
                ElseIf spec1 = FlashSpec.V And spec2 = FlashSpec.T Then
                    Return Flash_VT(mixmolefrac, val1, val2, initialestimate, pp, useestimates, initialKval)
                ElseIf spec1 = FlashSpec.V And spec2 = FlashSpec.P Then
                    Return Flash_VP(mixmolefrac, val1, val2, initialestimate, pp, useestimates, initialKval)
                ElseIf spec1 = FlashSpec.V And spec2 = FlashSpec.H Then
                    Return Flash_VH(mixmolefrac, val1, val2, 101325, initialestimate, pp, useestimates, initialKval)
                ElseIf spec1 = FlashSpec.V And spec2 = FlashSpec.S Then
                    Return Flash_VS(mixmolefrac, val1, val2, 101325, initialestimate, pp, useestimates, initialKval)
                Else
                    Throw New NotImplementedException("Flash specification set not supported.")
                End If

                d2 = Date.Now

                calcresult.TimeTaken = (d2 - d1)

            Catch ex As Exception

                calcresult.ResultException = ex

                'Throw ex

            End Try

            Return calcresult

        End Function

        Function CalculateMixtureEnthalpy(ByVal T As Double, ByVal P As Double, ByVal L As Double, ByVal L2 As Double, ByVal V As Double, ByVal S As Double,
                                          ByVal Vx As Double(), ByVal Vx2 As Double(), ByVal Vy As Double(), ByVal Vs As Double(), ByVal pp As PropertyPackage) As Double

            Dim _Hv, _Hl, _Hl2, _Hs As Double

            Dim n As Integer = Vx.Length - 1

            _Hv = 0
            _Hl = 0
            _Hl2 = 0
            _Hs = 0

            Dim mmg, mml, mml2, mms As Double

            If V > 0.0# And Vy.Sum > 0.0# Then _Hv = pp.DW_CalcEnthalpy(Vy, T, P, State.Vapor)
            If L > 0.0# And Vx.Sum > 0.0# Then _Hl = pp.DW_CalcEnthalpy(Vx, T, P, State.Liquid)
            If L2 > 0.0# And Vx2.Sum > 0.0# Then _Hl2 = pp.DW_CalcEnthalpy(Vx2, T, P, State.Liquid)
            If S > 0.0# And Vs.Sum > 0.0# Then _Hs = pp.DW_CalcEnthalpy(Vs, T, P, State.Solid)

            If V > 0.0# And Vy.Sum > 0.0# Then mmg = pp.AUX_MMM(Vy)
            If L > 0.0# And Vx.Sum > 0.0# Then mml = pp.AUX_MMM(Vx)
            If L2 > 0.0# And Vx2.Sum > 0.0# Then mml2 = pp.AUX_MMM(Vx2)
            If S > 0.0# And Vs.Sum > 0.0# Then mms = pp.AUX_MMM(Vs)

            Return (mmg * V / (mmg * V + mml * L + mml2 * L2 + mms * S)) * _Hv +
                (mml * L / (mmg * V + mml * L + mml2 * L2 + mms * S)) * _Hl +
                (mml2 * L2 / (mmg * V + mml * L + mml2 * L2 + mms * S)) * _Hl2 +
                (mms * S / (mmg * V + mml * L + mml2 * L2 + mms * S)) * _Hs

        End Function

        Function CalculateMixtureEntropy(ByVal T As Double, ByVal P As Double, ByVal L As Double, ByVal L2 As Double, ByVal V As Double, ByVal S As Double,
                                  ByVal Vx As Double(), ByVal Vx2 As Double(), ByVal Vy As Double(), ByVal Vs As Double(), ByVal pp As PropertyPackage) As Double

            Dim _Sv, _Sl, _Sl2 As Double

            Dim n As Integer = Vx.Length - 1

            _Sv = 0
            _Sl = 0
            _Sl2 = 0

            Dim mmg, mml, mml2, mms As Double

            If V > 0.0# And Vy.Sum > 0.0# Then _Sv = pp.DW_CalcEntropy(Vy, T, P, State.Vapor)
            If L > 0.0# And Vx.Sum > 0.0# Then _Sl = pp.DW_CalcEntropy(Vx, T, P, State.Liquid)
            If L2 > 0.0# And Vx2.Sum > 0.0# Then _Sl2 = pp.DW_CalcEntropy(Vs, T, P, State.Solid)

            If V > 0.0# And Vy.Sum > 0.0# Then mmg = pp.AUX_MMM(Vy)
            If L > 0.0# And Vx.Sum > 0.0# Then mml = pp.AUX_MMM(Vx)
            If L2 > 0.0# And Vx2.Sum > 0.0# Then mml2 = pp.AUX_MMM(Vx2)

            Return (mmg * V / (mmg * V + mml * L + mml2 * L2 + mms * S)) * _Sv +
                (mml * L / (mmg * V + mml * L + mml2 * L2 + mms * S)) * _Sl +
                (mml2 * L2 / (mmg * V + mml * L + mml2 * L2 + mms * S)) * _Sl2

        End Function

#Region "Generic Functions"

        Public Overridable Function Flash_PSF(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object
            Throw New Exception(Calculator.GetLocalString("PropPack_FlashPSFError"))
            Return Nothing
        End Function

        Public MustOverride Function Flash_PT(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

        Public MustOverride Function Flash_PH(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

        Public MustOverride Function Flash_PS(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

        Public MustOverride Function Flash_PV(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

        Public MustOverride Function Flash_TV(ByVal Vz As Double(), ByVal T As Double, ByVal V As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

        ''' <summary>
        ''' Mixture molar volume (m3/mol) of a PT flash result, from the liquid densities and the vapour
        ''' compressibility of the property package. Phases that are absent contribute nothing.
        ''' </summary>
        Private Function MixtureMolarVolume(flashresult As FlashCalculationResult, T As Double, P As Double, PP As PropertyPackages.PropertyPackage, Optional bubblePressure As Double = 0.0) As Double
            Dim VL1, VL2, VV As Double
            Dim L1 = flashresult.GetLiquidPhase1MoleFraction, L2 = flashresult.GetLiquidPhase2MoleFraction, V = flashresult.GetVaporPhaseMoleFraction
            If L1 > 0.0 Then
                Dim x = flashresult.GetLiquidPhase1MoleFractions
                Dim Pdens = P
                Dim ratio = 1.0
                If V <= 0.0 AndAlso L2 <= 0.0 AndAlso bubblePressure > 0.0 AndAlso P > bubblePressure Then
                    'A compressed liquid: the density correlations carry the saturated volume well but
                    'their pressure correction is far too stiff near the critical point, and it is not
                    'the volume behind the package's enthalpy. Take the saturated volume from the
                    'correlation at the bubble pressure and the compression from the equation of state,
                    'so pressure, volume and internal energy come from one consistent surface.
                    Dim zp = PP.AUX_Z(x, T, P, Interfaces.Enums.PhaseName.Liquid)
                    Dim zb = PP.AUX_Z(x, T, bubblePressure, Interfaces.Enums.PhaseName.Liquid)
                    If zp > 0.0 AndAlso zb > 0.0 AndAlso Not Double.IsNaN(zp + zb) Then
                        ratio = (zp / P) / (zb / bubblePressure)
                        If ratio > 0.5 AndAlso ratio <= 1.0 Then Pdens = bubblePressure Else ratio = 1.0
                    End If
                End If
                If V <= 0.0 AndAlso L2 <= 0.0 AndAlso (bubblePressure <= 0.0 OrElse bubblePressure >= P) Then
                    'No bubble point at this temperature (the mixture sits above its critical locus, as
                    'CO2 with a few percent of N2 does near 298 K): the correlations have no saturated
                    'volume to start from, so the dense phase takes its volume from the equation of state.
                    Dim tc = 0.0
                    Dim vtc = PP.RET_VTC()
                    For i = 0 To x.Length - 1
                        tc += x(i) * vtc(i)
                    Next
                    If tc > 0.0 AndAlso T > 0.9 * tc Then
                        Dim zl = PP.AUX_Z(x, T, P, Interfaces.Enums.PhaseName.Liquid)
                        If zl > 0.0 AndAlso Not Double.IsNaN(zl) Then
                            VL1 = L1 * zl * 8.314 * T / P
                            ratio = 0.0
                        End If
                    End If
                End If
                If ratio > 0.0 Then
                    Dim rho = PP.AUX_LIQDENS(T, x, Pdens) / PP.AUX_MMM(x) * 1000 'mol/m3
                    VL1 = L1 / rho * ratio
                End If
            End If
            If L2 > 0.0 Then
                Dim rho = PP.AUX_LIQDENS(T, flashresult.GetLiquidPhase2MoleFractions, P) / PP.AUX_MMM(flashresult.GetLiquidPhase2MoleFractions) * 1000 'mol/m3
                VL2 = L2 / rho
            End If
            If V > 0.0 Then
                VV = V * PP.AUX_Z(flashresult.GetVaporPhaseMoleFractions, T, P, Interfaces.Enums.PhaseName.Vapor) * 8.314 * T / P 'm3/mol
            End If
            If Double.IsInfinity(VL1) Or Double.IsNaN(VL1) Then VL1 = 0.0
            If Double.IsInfinity(VL2) Or Double.IsNaN(VL2) Then VL2 = 0.0
            If Double.IsInfinity(VV) Or Double.IsNaN(VV) Then VV = 0.0
            Return VL1 + VL2 + VV
        End Function

        ''' <summary>
        ''' Brent root finder on [a, b] with f(a) and f(b) of opposite sign. Returns the root; the caller
        ''' keeps whatever state the last evaluation left behind.
        ''' </summary>
        Private Function BrentRoot(f As Func(Of Double, Double), a As Double, b As Double, fa As Double, fb As Double, xtol As Double, ftol As Double, maxit As Integer) As Double
            If Math.Abs(fa) <= ftol Then Return a
            If Math.Abs(fb) <= ftol Then Return b
            If fa * fb > 0.0 Then Throw New Exception("Volume flash: the root is not bracketed.")
            If Math.Abs(fa) < Math.Abs(fb) Then
                Dim tmp = a : a = b : b = tmp
                tmp = fa : fa = fb : fb = tmp
            End If
            Dim c = a, fc = fa, d = b - a, e = d
            For it = 1 To maxit
                If fb * fc > 0.0 Then
                    c = a : fc = fa : d = b - a : e = d
                End If
                If Math.Abs(fc) < Math.Abs(fb) Then
                    a = b : b = c : c = a
                    fa = fb : fb = fc : fc = fa
                End If
                Dim tol1 = 2.0 * Double.Epsilon * Math.Abs(b) + 0.5 * xtol
                Dim xm = 0.5 * (c - b)
                If Math.Abs(xm) <= tol1 Or Math.Abs(fb) <= ftol Then Return b
                If Math.Abs(e) >= tol1 And Math.Abs(fa) > Math.Abs(fb) Then
                    Dim sx = fb / fa, pq As Double, qq As Double
                    If a = c Then
                        pq = 2.0 * xm * sx
                        qq = 1.0 - sx
                    Else
                        Dim q = fa / fc, r = fb / fc
                        pq = sx * (2.0 * xm * q * (q - r) - (b - a) * (r - 1.0))
                        qq = (q - 1.0) * (r - 1.0) * (sx - 1.0)
                    End If
                    If pq > 0.0 Then qq = -qq
                    pq = Math.Abs(pq)
                    If 2.0 * pq < Math.Min(3.0 * xm * qq - Math.Abs(tol1 * qq), Math.Abs(e * qq)) Then
                        e = d : d = pq / qq
                    Else
                        d = xm : e = d
                    End If
                Else
                    d = xm : e = d
                End If
                a = b : fa = fb
                If Math.Abs(d) > tol1 Then b += d Else b += If(xm > 0, tol1, -tol1)
                fb = f(b)
            Next
            Return b
        End Function

        ''' <summary>
        ''' Volume-Temperature Flash: the pressure at which the mixture occupies the specified molar
        ''' volume at T. Volume falls with pressure, so the residual is monotonic; the bracket starts
        ''' around the estimate and widens until the root is inside it, then Brent finishes.
        ''' </summary>
        ''' <param name="Vz">Mole fractions</param>
        ''' <param name="Vspec">Molar Volume (m3/mol)</param>
        ''' <param name="T">Temperature (K)</param>
        ''' <param name="Pref">Pressure estimate (Pa)</param>
        Public Function Flash_VT(ByVal Vz As Double(), ByVal Vspec As Double, ByVal T As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As FlashCalculationResult

            If Pref <= 0.0 Or Double.IsNaN(Pref) Then Pref = 101325.0
            Dim flashresult As FlashCalculationResult = Nothing
            'the bubble pressure at T, found once and only when a trial pressure leaves the mixture
            'all liquid (the compressed-liquid volume needs it, see MixtureMolarVolume)
            Dim pbub = 0.0, pbubKnown = False
            Dim f = Function(P As Double) As Double
                        flashresult = CalculateEquilibrium(FlashSpec.P, FlashSpec.T, P, T, PP, Vz, PrevKi, 0.0)
                        If Not pbubKnown AndAlso flashresult.GetVaporPhaseMoleFraction <= 0.0 AndAlso flashresult.GetLiquidPhase2MoleFraction <= 0.0 Then
                            pbubKnown = True
                            Try
                                pbub = Convert.ToDouble(CalculateEquilibrium(FlashSpec.T, FlashSpec.VAP, T, 0.0, PP, Vz, PrevKi, P).CalculatedPressure)
                                If Double.IsNaN(pbub) OrElse pbub <= 0.0 OrElse pbub > 1.0E+9 Then pbub = 0.0
                            Catch ex As Exception
                                pbub = 0.0
                            End Try
                        End If
                        Return (Vspec - MixtureMolarVolume(flashresult, T, P, PP, pbub)) / Vspec
                    End Function

            'f rises with P: negative when the mixture is too big for Vspec (P too low), positive when too small
            Dim a = Pref / 1.5, b = Pref * 1.5

            'A pure compound has no two-phase pressure window: at T it is liquid above Psat, vapour below
            'and any split exactly at Psat, so the PT residual jumps there instead of crossing zero. Take
            'the saturation pressure from the package's own bubble point, and settle the split with the
            'lever rule when the specified volume lies between the saturated liquid and vapour volumes.
            'A nearly pure mixture (a trace of a light gas left in CO2 or steam) has a two-phase window
            'narrower than the search can resolve, so it is handled the same way, with the bubble
            'pressure standing for the window.
            Dim nonZero = 0
            For i = 0 To Vz.Length - 1
                If Vz(i) > 1.0E-10 Then nonZero += 1
            Next
            If nonZero = 1 OrElse Vz.Max() > 1.0 - 1.0E-3 Then
                Dim sat = CalculateEquilibrium(FlashSpec.T, FlashSpec.VAP, T, 0.0, PP, Vz, PrevKi, Pref)
                Dim Psat = Convert.ToDouble(sat.CalculatedPressure)
                If Psat > 0.0 AndAlso Not Double.IsNaN(Psat) Then
                    pbub = Psat : pbubKnown = True
                    Dim VL = PP.AUX_MMM(Vz) / 1000.0 / PP.AUX_LIQDENS(T, Vz, Psat) 'm3/mol
                    Dim VV = PP.AUX_Z(Vz, T, Psat, Interfaces.Enums.PhaseName.Vapor) * 8.314 * T / Psat
                    If VL > 0.0 AndAlso VV > VL Then
                        If Vspec > VL * (1.0 + 1.0E-9) AndAlso Vspec < VV * (1.0 - 1.0E-9) Then
                            Dim vf = (Vspec - VL) / (VV - VL)
                            flashresult = CalculateEquilibrium(FlashSpec.T, FlashSpec.VAP, T, vf, PP, Vz, PrevKi, Psat)
                            flashresult.CalculatedPressure = Psat
                            Return flashresult
                        ElseIf Vspec <= VL * (1.0 + 1.0E-9) Then
                            a = Psat : b = Math.Max(Pref, Psat) * 1.5   'compressed liquid: search above Psat
                        Else
                            a = Math.Min(Pref, Psat) / 1.5 : b = Psat  'superheated vapour: search below Psat
                        End If
                    End If
                End If
            End If

            Dim fa = f(a), fb = f(b)
            Dim n = 0
            While fa > 0.0 And a > 1.0 And n < 40
                b = a : fb = fa
                a /= 2.0 : fa = f(a) : n += 1
            End While
            n = 0
            While fb < 0.0 And b < 1.0E+10 And n < 40
                a = b : fa = fb
                b *= 2.0 : fb = f(b) : n += 1
            End While

            Dim P0 = BrentRoot(f, a, b, fa, fb, 1.0E-6 * Pref, 1.0E-7, 100)
            If flashresult Is Nothing OrElse Math.Abs(Convert.ToDouble(flashresult.CalculatedPressure) - P0) > 1.0E-9 * Pref Then f(P0)
            Return flashresult

        End Function

        ''' <summary>
        ''' Molar volume (m3/mol) of the mixture at T and P, defined exactly as the volume flashes define
        ''' it, so a vessel filled from this value starts on the surface the flashes will walk.
        ''' </summary>
        Public Function MixtureMolarVolumeAtTP(ByVal Vz As Double(), ByVal T As Double, ByVal P As Double, ByVal PP As PropertyPackages.PropertyPackage) As Double
            Dim r = CalculateEquilibrium(FlashSpec.P, FlashSpec.T, P, T, PP, Vz, Nothing, 0.0)
            Dim pbub = 0.0
            If r.GetVaporPhaseMoleFraction <= 0.0 AndAlso r.GetLiquidPhase2MoleFraction <= 0.0 Then
                Try
                    pbub = Convert.ToDouble(CalculateEquilibrium(FlashSpec.T, FlashSpec.VAP, T, 0.0, PP, Vz, Nothing, P).CalculatedPressure)
                    If Double.IsNaN(pbub) OrElse pbub <= 0.0 OrElse pbub > 1.0E+9 Then pbub = 0.0
                Catch ex As Exception
                    pbub = 0.0
                End Try
            End If
            Return MixtureMolarVolume(r, T, P, PP, pbub)
        End Function

        ''' <summary>
        ''' Homogeneous-equilibrium mass flux (kg/(m2.s)) of a frictionless nozzle fed at h0 (kJ/kg) and
        ''' s0 (kJ/(kg.K)) discharging from P1 to P2 (Pa): the fluid expands along the isentrope, the
        ''' flux G = sqrt(2 (h0 - h(P))) / v(P) rises as the throat pressure falls until the mixture
        ''' reaches its own speed of sound, and the flow chokes there. For an ideal gas this reduces to
        ''' the textbook nozzle formula; for a dense supercritical fluid or a flashing liquid the choke
        ''' comes much closer to the upstream pressure than the ideal-gas ratio, which is the case a
        ''' blowdown valve or a leak on a high-pressure line has to be sized for. The throat pressure
        ''' is returned (P2 when the flow is not choked).
        ''' </summary>
        Public Function HEMMassFlux(ByVal Vz As Double(), ByVal h0 As Double, ByVal s0 As Double, ByVal P1 As Double, ByVal P2 As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, ByRef throatPressure As Double, Optional ByVal throatGuess As Double = 0.0) As Double

            Dim mw = PP.AUX_MMM(Vz) 'kg/kmol
            Dim lastT = Tref
            Dim flux = Function(P As Double) As Double
                           Dim r = CalculateEquilibrium(FlashSpec.P, FlashSpec.S, P, s0, PP, Vz, Nothing, lastT)
                           Dim T = Convert.ToDouble(r.CalculatedTemperature)
                           If T > 0.0 AndAlso Not Double.IsNaN(T) Then lastT = T
                           Dim dh = (h0 - Convert.ToDouble(r.CalculatedEnthalpy)) * 1000.0 'J/kg
                           If dh <= 0.0 OrElse Double.IsNaN(dh) Then Return 0.0
                           Dim v = MixtureMolarVolume(r, T, P, PP) * 1000.0 / mw 'm3/kg
                           If v <= 0.0 OrElse Double.IsNaN(v) Then Return 0.0
                           Return Math.Sqrt(2.0 * dh) / v
                       End Function

            'With a throat pressure from the previous step (a dynamic run moves it by a percent or so
            'a step) a short hill climb around it costs three to five flashes instead of the sixteen of
            'the full search below.
            If throatGuess > P2 * 1.02 AndAlso throatGuess < P1 * 0.98 Then
                Dim d = Math.Log(1.03)
                Dim x0 = Math.Log(throatGuess)
                Dim f0 = flux(Math.Exp(x0))
                Dim xp = Math.Min(x0 + d, Math.Log(P1)), xm = Math.Max(x0 - d, Math.Log(P2))
                Dim fp = flux(Math.Exp(xp)), fm = flux(Math.Exp(xm))
                Dim n = 0
                While fp > f0 AndAlso xp < Math.Log(P1) - 1.0E-9 AndAlso n < 12
                    xm = x0 : fm = f0 : x0 = xp : f0 = fp
                    xp = Math.Min(x0 + d, Math.Log(P1)) : fp = flux(Math.Exp(xp)) : n += 1
                End While
                While fm > f0 AndAlso xm > Math.Log(P2) + 1.0E-9 AndAlso n < 12
                    xp = x0 : fp = f0 : x0 = xm : f0 = fm
                    xm = Math.Max(x0 - d, Math.Log(P2)) : fm = flux(Math.Exp(xm)) : n += 1
                End While
                If f0 > 0.0 AndAlso f0 >= fp AndAlso f0 >= fm Then
                    'parabolic refinement through the three bracketing points
                    Dim denom = (xp - x0) * (fm - f0) - (xm - x0) * (fp - f0)
                    If Math.Abs(denom) > 0.0 Then
                        Dim xs = x0 + 0.5 * ((xp - x0) ^ 2 * (fm - f0) - (xm - x0) ^ 2 * (fp - f0)) / denom
                        If xs > xm AndAlso xs < xp Then
                            Dim fs = flux(Math.Exp(xs))
                            If fs > f0 Then x0 = xs : f0 = fs
                        End If
                    End If
                    If fm >= f0 * (1.0 - 1.0E-6) AndAlso xm <= Math.Log(P2) + 1.0E-9 Then
                        throatPressure = P2
                        Return fm
                    End If
                    throatPressure = Math.Exp(x0)
                    Return f0
                End If
            End If

            'golden-section search for the maximum of G over ln P in [ln P2, ln P1]
            Dim lo = Math.Log(P2), hi = Math.Log(P1)
            Dim gr = (Math.Sqrt(5.0) - 1.0) / 2.0
            Dim x1 = hi - gr * (hi - lo), x2 = lo + gr * (hi - lo)
            Dim f1 = flux(Math.Exp(x1)), f2 = flux(Math.Exp(x2))
            For i = 1 To 14
                If f1 > f2 Then
                    hi = x2 : x2 = x1 : f2 = f1
                    x1 = hi - gr * (hi - lo) : f1 = flux(Math.Exp(x1))
                Else
                    lo = x1 : x1 = x2 : f1 = f2
                    x2 = lo + gr * (hi - lo) : f2 = flux(Math.Exp(x2))
                End If
            Next
            Dim Pt = Math.Exp(If(f1 > f2, x1, x2))
            Dim Gt = Math.Max(f1, f2)
            'unchoked: the flux keeps rising down to the back pressure
            Dim G2 = flux(P2)
            If G2 >= Gt Then
                throatPressure = P2
                Return G2
            End If
            throatPressure = Pt
            Return Gt

        End Function

        ''' <summary>
        ''' Volume-Pressure Flash: the temperature at which the mixture occupies the specified molar
        ''' volume at P. Volume rises with temperature, so the residual is monotonic.
        ''' </summary>
        Public Function Flash_VP(ByVal Vz As Double(), ByVal Vspec As Double, ByVal P As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As FlashCalculationResult

            If Tref <= 0.0 Or Double.IsNaN(Tref) Then Tref = 298.15
            Dim flashresult As FlashCalculationResult = Nothing
            Dim f = Function(T As Double) As Double
                        flashresult = CalculateEquilibrium(FlashSpec.P, FlashSpec.T, P, T, PP, Vz, PrevKi, 0.0)
                        Return (Vspec - MixtureMolarVolume(flashresult, T, P, PP)) / Vspec
                    End Function

            'f falls with T: positive when the mixture is too small for Vspec (T too low)
            Dim a = Tref / 1.25, b = Tref * 1.25
            Dim fa = f(a), fb = f(b)
            Dim n = 0
            While fa < 0.0 And a > 20.0 And n < 30
                b = a : fb = fa
                a /= 1.25 : fa = f(a) : n += 1
            End While
            n = 0
            While fb > 0.0 And b < 5000.0 And n < 30
                a = b : fa = fb
                b *= 1.25 : fb = f(b) : n += 1
            End While

            Dim T0 = BrentRoot(f, a, b, fa, fb, 1.0E-6 * Tref, 1.0E-7, 100)
            If flashresult Is Nothing OrElse Math.Abs(Convert.ToDouble(flashresult.CalculatedTemperature) - T0) > 1.0E-9 * Tref Then f(T0)
            Return flashresult

        End Function

        ''' <summary>
        ''' Solves a volume flash with one more specification (enthalpy, entropy or internal energy, all
        ''' per kg of mixture): an outer temperature search where every trial temperature is closed with
        ''' a VT flash for the pressure, so the mixture always has the specified volume. At constant
        ''' volume the property rises with temperature, so the outer residual is monotonic.
        ''' </summary>
        Private Function Flash_VX(Vz As Double(), Vspec As Double, target As Double, Pref As Double, Tref As Double, PP As PropertyPackages.PropertyPackage, PrevKi As Double(), prop As Func(Of FlashCalculationResult, Double, Double, Double)) As FlashCalculationResult

            If Tref <= 0.0 Or Double.IsNaN(Tref) Then Tref = 298.15
            If Pref <= 0.0 Or Double.IsNaN(Pref) Then Pref = 101325.0
            Dim scale = Math.Max(Math.Abs(target), 1.0)
            Dim flashresult As FlashCalculationResult = Nothing
            Dim lastP = Pref
            Dim f = Function(T As Double) As Double
                        flashresult = Flash_VT(Vz, Vspec, T, lastP, PP, False, PrevKi)
                        lastP = Convert.ToDouble(flashresult.CalculatedPressure)
                        Return (target - prop(flashresult, T, lastP)) / scale
                    End Function

            'f falls with T: positive when the mixture is too cold. Walk from the estimate in the
            'direction the sign points, with a step that starts at 0.5 % and doubles, and bracket the
            'first sign change. That picks the root nearest the estimate, which is the state the
            'previous time step left: the volume basis of a liquid (density correlation) and of a
            'supercritical fluid (equation of state) differ, so a wide bracket that straddles the
            'critical temperature can hold a second, spurious root.
            Dim f0 = f(Tref)
            If Math.Abs(f0) <= 1.0E-6 Then Return flashresult
            Dim a = Tref, b = Tref, fa = f0, fb = f0
            Dim stepFraction = 0.005
            Dim found = False
            For n = 1 To 40
                If f0 > 0.0 Then
                    Dim tUp = Math.Min(b * (1.0 + stepFraction), 5000.0)
                    Dim fUp = f(tUp)
                    If fUp <= 0.0 Then a = b : fa = fb : b = tUp : fb = fUp : found = True : Exit For
                    b = tUp : fb = fUp
                    If tUp >= 5000.0 Then Exit For
                Else
                    Dim tDn = Math.Max(a / (1.0 + stepFraction), 20.0)
                    Dim fDn = f(tDn)
                    If fDn >= 0.0 Then b = a : fb = fa : a = tDn : fa = fDn : found = True : Exit For
                    a = tDn : fa = fDn
                    If tDn <= 20.0 Then Exit For
                End If
                stepFraction = Math.Min(stepFraction * 2.0, 0.5)
            Next
            If Not found Then Throw New Exception("Volume flash: the root is not bracketed.")

            Dim T0 = BrentRoot(f, a, b, fa, fb, 1.0E-6 * Tref, 1.0E-6, 100)
            If flashresult Is Nothing OrElse Math.Abs(Convert.ToDouble(flashresult.CalculatedTemperature) - T0) > 1.0E-9 * Tref Then f(T0)
            Return flashresult

        End Function

        ''' <summary>Volume-Enthalpy Flash (H in kJ/kg of mixture, the same basis as the PT flash result).</summary>
        Public Function Flash_VH(ByVal Vz As Double(), ByVal Vspec As Double, ByVal H As Double, ByVal Pref As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As FlashCalculationResult
            Return Flash_VX(Vz, Vspec, H, Pref, Tref, PP, PrevKi, Function(r, T, P) Convert.ToDouble(r.CalculatedEnthalpy))
        End Function

        ''' <summary>Volume-Entropy Flash (S in kJ/(kg.K) of mixture).</summary>
        Public Function Flash_VS(ByVal Vz As Double(), ByVal Vspec As Double, ByVal S As Double, ByVal Pref As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As FlashCalculationResult
            Return Flash_VX(Vz, Vspec, S, Pref, Tref, PP, PrevKi, Function(r, T, P) Convert.ToDouble(r.CalculatedEntropy))
        End Function

        ''' <summary>Volume-Internal Energy Flash (U in kJ/kg of mixture, U = H - P v).</summary>
        Public Function Flash_VU(ByVal Vz As Double(), ByVal Vspec As Double, ByVal U As Double, ByVal Pref As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As FlashCalculationResult
            Dim mw = PP.AUX_MMM(Vz) 'kg/kmol
            Return Flash_VX(Vz, Vspec, U, Pref, Tref, PP, PrevKi, Function(r, T, P) Convert.ToDouble(r.CalculatedEnthalpy) - P * Vspec / mw)
        End Function


#End Region

#Region "Auxiliary Functions"

        Public Function BubbleTemperature_LLE(ByVal Vz As Double(), ByVal Vx1est As Double(), ByVal Vx2est As Double(), ByVal P As Double, ByVal Tmin As Double, ByVal Tmax As Double, ByVal PP As PropertyPackages.PropertyPackage) As Double

            _P = P
            _pp = PP
            _Vz = Vz
            _Vx1est = Vx1est
            _Vx2est = Vx2est

            Dim T, err As Double

            Dim bm As New MathEx.BrentOpt.BrentMinimize
            bm.DefineFuncDelegate(AddressOf BubbleTemperature_LLEPerror)

            err = bm.brentoptimize(Tmin, Tmax, 0.0001, T)

            err = BubbleTemperature_LLEPerror(T)

            Return T

        End Function

        Private Function BubbleTemperature_LLEPerror(ByVal x As Double) As Double

            Dim n As Integer = UBound(_Vz)

            Dim Vp(n), fi1(n), fi2(n), act1(n), act2(n), Vx1(n), Vx2(n) As Double

            Dim result As Object = New SimpleLLE() With {.UseInitialEstimatesForPhase1 = True, .UseInitialEstimatesForPhase2 = True,
                                                          .InitialEstimatesForPhase1 = _Vx1est, .InitialEstimatesForPhase2 = _Vx2est}.Flash_PT(_Vz, _P, x, _pp)

            'Dim result As Object = New GibbsMinimization3P() With {.ForceTwoPhaseOnly = False, .StabSearchSeverity = 0, .StabSearchCompIDs = _pp.RET_VNAMES}.Flash_PT(_Vz, _P, x, _pp)

            Vx1 = result(2)
            Vx2 = result(6)
            fi1 = _pp.DW_CalcFugCoeff(Vx1, x, _P, State.Liquid)
            fi2 = _pp.DW_CalcFugCoeff(Vx2, x, _P, State.Liquid)

            Dim i As Integer

            For i = 0 To n
                Vp(i) = _pp.AUX_PVAPi(i, x)
                act1(i) = _P / Vp(i) * fi1(i)
                act2(i) = _P / Vp(i) * fi2(i)
            Next

            Dim err As Double = _P
            For i = 0 To n
                err -= Vx2(i) * act2(i) * Vp(i)
            Next

            Return Math.Abs(err)

        End Function

        Public Function BubblePressure_LLE(ByVal Vz As Double(), ByVal Vx1est As Double(), ByVal Vx2est As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage) As Double

            Dim n As Integer = UBound(_Vz)

            Dim Vp(n), fi1(n), fi2(n), act1(n), act2(n), Vx1(n), Vx2(n) As Double

            Dim result As Object = New GibbsMinimization3P() With {.ForceTwoPhaseOnly = False,
                                                                   .StabSearchCompIDs = _pp.RET_VNAMES,
                                                                   .StabSearchSeverity = 0}.Flash_PT(_Vz, P, T, PP)

            Vx1 = result(2)
            Vx2 = result(6)
            fi1 = _pp.DW_CalcFugCoeff(Vx1, T, P, State.Liquid)
            fi2 = _pp.DW_CalcFugCoeff(Vx2, T, P, State.Liquid)

            Dim i As Integer

            For i = 0 To n
                Vp(i) = _pp.AUX_PVAPi(i, T)
                act1(i) = P / Vp(i) * fi1(i)
                act2(i) = P / Vp(i) * fi2(i)
            Next

            _P = 0.0#
            For i = 0 To n
                _P += Vx2(i) * act2(i) * Vp(i)
            Next

            Return _P

        End Function

#End Region

#Region "Liquid Phase Stability Check"

        Public Function StabTest(ByVal T As Double, ByVal P As Double, ByVal Vz As Double(), ByVal VTc As Double(), ByVal pp As PropertyPackage)

            If pp.AUX_IS_SINGLECOMP(Vz) Then
                Return New Object() {True, Nothing}
            End If

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "StabTest", Name & " (Stability Test)", "Liquid Phase Stability Test Routine", True)

            IObj?.Paragraphs.Add("Stability analysis represents the most challenging problem associated with multiphase flash calculations. 
The phase split calculation, which given the number of phases leads to locating an unconstrained local minimum, is essentially a purely 
technical problem, where the choice of solution procedure affects speed rather than reliability. The stability analysis, in contrast, 
requires the determination of a global minimum, with no advance information on the location of this minimum. Any practical implementation 
of multiphase stability analysis has to balance speed of execution against reliability, the more extensive, and thus more costly, search 
being less likely to overlook indications of instability. Previous algorithms have mostly been based on partly empirical observations 
relating to the characteristics of multiphase equilibrium, selecting trial phase compositions in the manner most likely to yield conclusive 
information. Currently, 'safe' algorithms that are guaranteed to resolve the stability question are under active investigation by many groups. 
The techniques used comprise global optimization methods and interval analysis, and currently the time expenditure for the calculations appear 
prohibitive for more complex problems.")

            IObj?.Paragraphs.Add("The essential difference between stability analysis for two-phase (vapour/liquid) and multiphase problems 
is that selection of two trial phase compositions (with subsequent local minimization) is adequate and feasible � in the first case, whereas
even selection of as many trial phases as the number of components in the mixture may not be sufficient in the latter. For mixtures containing 
10 or more components, converging the tangent plane distance minimization for a large number of individual initial estimates represents a 
substantial effort, and we therefore suggest the use of a screening procedure rather than a full search. The outcome of the screening procedure 
then decides which of the trial phases to investigate further.")

            IObj?.Paragraphs.Add("<h3>Selection of initial estimates for stability analysis</h3>")

            IObj?.Paragraphs.Add("Assume that the current status of a multiphase equilibrium calculation is that a local minimum in the Gibbs 
energy corresponding to F phases has been determined, i.e.")

            IObj?.Paragraphs.Add("<m>\ln f_{iL}=\ln f_{i2} = ... = \ln f_{iF}\space\space(=\ln f^*_i)</m>")

            IObj?.Paragraphs.Add("where each of the F phases")

            IObj?.Paragraphs.Add("<m>\mathbf{n}^1 ,\mathbf{n}^2,...,\mathbf{n}^F</m>")

            IObj?.Paragraphs.Add("are intrinsically stable. ")

            IObj?.Paragraphs.Add("The tangent plane distance for a trial phase of composition w is given by")

            IObj?.Paragraphs.Add("<m>tpd(\mathbf{w})=\sum\limits_{i}{w_i}(\ln f_i(\mathbf{w})-\ln f^*_i)=\sum\limits_{i}{w_i(\ln w_i +\ln \varphi _i(\mathbf{w})-d_i)} </m>")

            IObj?.Paragraphs.Add("with <mi>d_i = \ln (f^*_i/P)</mi> and if <mi>tpd(\mathbf{w})</mi> is non-negative for all w the equilibrium 
phase distribution is stable and no further calculation is required. If, on the other hand, a composition w with a negative tangent plane 
distance can be located, the phase distribution is unstable and the composition w can be utilised for generating initial estimates for an 
F+l-phase calculation, as it is known that the mixture Gibbs energy can be reduced by introducing a small amount of a phase with this composition.")

            IObj?.Paragraphs.Add("In practice we shall test a sequence of composition estimates w for negative tangent plane distances. 
If none of the trial phases verify instability, the mixture is assumed to be stable. One of the objectives is to select and evaluate 
the set of trial phase compositions most likely to indicate instability and thus to form new phases. Another objective is to perform 
the  for a given test phase as economically as possible.")

            IObj?.Paragraphs.Add("When a trial phase has been chosen, refinement is performed by means of successive substitution, 
as for the two-phase flash. Continuation of successivo substitution until convergence will locate either a non-trivial minimum, i.e. 
a minimum with a composition different from that of any of the equilibrium phases, or a trivial solution. The aim of the screening 
procedure is to decide at an early stage whether convergence to a trivial solution is likely to occur, in which case further iteration 
on the chosen trial phase can be abandoned, Obviously, increasing the number of successive substitution steps increases the reliability 
of the screening procedure, as well as the associated cost.")

            IObj?.Paragraphs.Add("In the procedure of Michelsen, 2-4 steps of successive substitution  performed in parallel for each 
trial phase. If instability (negative till) is not encountered during these steps, only the trial phase composition with the smaller, 
decreasing U-value is converged. The screening procedure is evidently empirical and cannot be guaranteed to succeed, but practical 
experience indicates that with proper selection of the initial phase compositionsthe approach represents a reasonable compromise 
between reliability and cost.")

            IObj?.Paragraphs.Add("<h3>Selection of trial phase compositions</h3>")

            IObj?.Paragraphs.Add("In the absence of any advance knowledge about the nature of the mixture to be flashed, at least C + 1 
different initial trial phases are required. One of these is used to search for a vapour (fortunately, equilibrium comprises at most 
one vapour phase), and the remaining C trial phases cater for the possibility of formation of a liquid phase rich in the corresponding 
mixture component.")

            IObj?.Paragraphs.Add("The initial composition of the vapour phase is calculated as Wi = exp(di), based on the assumption 
that the trial phase is an ideal vapour (with a fugacity coefficient of 1), and in all subsequent iterations properties of this trial 
phases are calculated using the vapour density, if the equation of state has multiple roots.")

            IObj?.Paragraphs.Add("The additional C trial phases are initiated as the respective pure components, properties of the trial 
phase being calculated with the liquid density, as the purpose of this search is to reveal the potential formation of new liquid like 
phases. Starting from each 'corner' of the composition space aims at ensuring that the search will cover the entire region as well as possible. 
The use of pure trial phases also ensures rapid detection of instability with highly immiscible components.")

            IObj?.Paragraphs.Add("Unfortunately, even converging all C + 1 trial phase compositions does not guarantee that the global 
minimum of the tangent plane distance will be located, and many practically important exceptions are found. These are characterised by 
the existence of a liquid phase dominated by a light component, e.g. methane or carbon dioxide, under conditions where this component is 
unable to exist as a pure liquid. As a consequence the pure component initialization is incapable of creating the liquid phase rich in
this particular component, and there is no certainty that the alternative initializations, which start far from the desired minimum, 
will converge to this solution.")

            IObj?.Paragraphs.Add(String.Format("<h2>Input Parameters</h2>"))

            IObj?.Paragraphs.Add(String.Format("Temperature: {0} K", T))
            IObj?.Paragraphs.Add(String.Format("Pressure: {0} Pa", P))
            IObj?.Paragraphs.Add(String.Format("Compounds: {0}", pp.RET_VNAMES.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Mole Fractions: {0}", Vz.ToMathArrayString))

            WriteDebugInfo("Starting Liquid Phase Stability Test @ T = " & T & " K & P = " & P & " Pa for the following trial phases:")

            IObj?.Paragraphs.Add("Starting Liquid Phase Stability Test @ T = " & T & " K & P = " & P & " Pa for the following trial phases:")

            Dim i, j, n, o, l, nt, maxits As Integer
            n = Vz.Length - 1
            nt = n

            Dim Vtrials As New List(Of Double())
            Dim idx(nt) As Integer

            For j = 0 To n
                Vtrials.Add(pp.RET_NullVector)
            Next

            For Each vector In Vtrials
                For i = 0 To n
                    vector(i) = 0.000001
                Next
            Next

            For j = 0 To Vtrials.Count - 1
                Vtrials(j)(j) = 1.0
            Next

            Dim tol As Double
            Dim fcv(n), fcl(n) As Double

            tol = 0.001
            maxits = 200

            Dim h(n), lnfi_z(n) As Double

            Dim gl, gv As Double

            ' Work in log fugacity coefficients: a high segment-number polymer's coefficient underflows
            ' to zero, and the old -500 sentinel for Log(0) discarded its true (large negative) chemical
            ' potential, so the tangent-plane test could never see a polymer-rich second liquid. The
            ' default DW_CalcLnFugCoeff reproduces the old -500 clamp, so non-underflowing packages are
            ' unaffected.
            Dim lnfcv(n), lnfcl(n) As Double

            If Settings.EnableParallelProcessing Then

                Dim task1 = TaskHelper.Run(Sub()
                                               lnfcv = pp.DW_CalcLnFugCoeff(Vz, T, P, State.Vapor)
                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                Dim task2 = TaskHelper.Run(Sub()
                                               lnfcl = pp.DW_CalcLnFugCoeff(Vz, T, P, State.Liquid)
                                           End Sub, Settings.TaskCancellationTokenSource.Token)
                Task.WaitAll(task1, task2)

            Else
                IObj?.SetCurrent
                lnfcv = pp.DW_CalcLnFugCoeff(Vz, T, P, State.Vapor)
                IObj?.SetCurrent
                lnfcl = pp.DW_CalcLnFugCoeff(Vz, T, P, State.Liquid)
            End If

            gv = 0.0#
            gl = 0.0#
            For i = 0 To n
                If Vz(i) > 0.0# Then
                    gv += Vz(i) * (lnfcv(i) + Log(Vz(i)))
                    gl += Vz(i) * (lnfcl(i) + Log(Vz(i)))
                End If
            Next

            If gl <= gv Then
                lnfi_z = lnfcl.Clone()
            Else
                lnfi_z = lnfcv.Clone()
            End If

            i = 0
            Do
                If Vz(i) > 0.0# Then
                    h(i) = Log(Vz(i)) + lnfi_z(i)
                Else
                    h(i) = -500.0
                End If
                i = i + 1
            Loop Until i = n + 1

            Vtrials.Add(pp.RET_NullVector)
            Vtrials.Add(pp.RET_NullVector)

            i = 0
            Do
                If Vz(i) <> 0.0# Then
                    Vtrials(n + 1)(i) = Exp(h(i))
                Else
                    Vtrials(n + 1)(i) = 0.0#
                End If
                Vtrials(n + 2)(i) = 1.0 / (n + 1)
                i = i + 1
            Loop Until i = n + 1

            Vtrials.Add(Vz.Clone)


            Dim random As New Random(0)

            For i = 0 To n
                Dim vnew = pp.RET_NullVector()
                For j = 0 To n
                    If i = j Then
                        If Vz(j) > 0.0 Then vnew(j) = 1.0
                    End If
                Next
                If vnew.Sum > 0.0 Then Vtrials.Add(vnew)
            Next

            For i = 0 To Vtrials.Count - 1
                Vtrials(i) = Vtrials(i).NormalizeY()
            Next

            Dim m As Integer = Vtrials.Count - 1 '+ 2

            Dim g_(m), beta(m), r(m), r_ant(m) As Double
            Dim excidx As New Concurrent.ConcurrentBag(Of Integer)

            'One slot per trial phase. The trials run in parallel, and collecting them in the order the
            'threads finished made the kept duplicate and the selected estimate change from call to call.
            Dim Vestimates(m)() As Double

            Dim prevstatus = GlobalSettings.Settings.InspectorEnabled

            GlobalSettings.Settings.InspectorEnabled = False

            'start stability test for each one of the initial estimate vectors
            Parallel.For(0, m + 1, Sub(xi)

                                       Dim jj, cc As Integer
                                       Dim vector = Vtrials(xi)

                                       Dim lnfi(n), Y(n), Y_ant(n) As Double
                                       Dim currcomp(n) As Double
                                       Dim dgdY(n), tmpfug(n), dY(n), sum3, ffcv(), ffcl(), ttmpfug() As Double
                                       Dim finish As Boolean = True

                                       Y = vector.Clone

                                       cc = 0
                                       Do

                                           jj = 0
                                           sum3 = 0
                                           Do
                                               If Y(jj) > 0 Then sum3 += Y(jj)
                                               jj = jj + 1
                                           Loop Until jj = n + 1
                                           If sum3 < 1.0E-30 Then sum3 = 1.0E-30
                                           jj = 0
                                           Do
                                               If Y(jj) > 0 Then currcomp(jj) = Y(jj) / sum3 Else currcomp(jj) = 0
                                               jj = jj + 1
                                           Loop Until jj = n + 1

                                           Dim lnffcv = pp.DW_CalcLnFugCoeff(currcomp, T, P, State.Vapor)
                                           Dim lnffcl = pp.DW_CalcLnFugCoeff(currcomp, T, P, State.Liquid)

                                           Dim ggv As Double = 0.0#
                                           Dim ggl As Double = 0.0#
                                           For jj = 0 To n
                                               If currcomp(jj) > 0.0# Then
                                                   ggv += currcomp(jj) * (lnffcv(jj) + Log(currcomp(jj)))
                                                   ggl += currcomp(jj) * (lnffcl(jj) + Log(currcomp(jj)))
                                               End If
                                           Next

                                           Dim lnsel = If(ggl <= ggv, lnffcl, lnffcv)
                                           For jj = 0 To n
                                               lnfi(jj) = lnsel(jj)
                                           Next
                                           jj = 0
                                           Do
                                               If Y(jj) > 0.0# Then
                                                   dgdY(jj) = Log(Y(jj)) + lnfi(jj) - h(jj)
                                               Else
                                                   dgdY(jj) = -500.0 + lnfi(jj) - h(jj)
                                               End If
                                               jj = jj + 1
                                           Loop Until jj = n + 1
                                           jj = 0
                                           beta(xi) = 0
                                           Do
                                               beta(xi) += (Y(jj) - Vz(jj)) * dgdY(jj)
                                               jj = jj + 1
                                           Loop Until jj = n + 1
                                           g_(xi) = 1
                                           jj = 0
                                           Do
                                               If Y(jj) > 0.0# Then
                                                   g_(xi) += Y(jj) * (Log(Y(jj)) + lnfi(jj) - h(jj) - 1)
                                               End If
                                               jj = jj + 1
                                           Loop Until jj = n + 1
                                           If xi > 0 Then r_ant(xi) = r(xi) Else r_ant(xi) = 0
                                           If Math.Abs(beta(xi)) > 1.0E-30 Then
                                               r(xi) = 2 * g_(xi) / beta(xi)
                                           Else
                                               r(xi) = 1.0
                                           End If

                                           jj = 0
                                           Do
                                               Y_ant(jj) = Y(jj)
                                               Y(jj) = Exp(h(jj) - lnfi(jj))
                                               dY(jj) = Y(jj) - Y_ant(jj)
                                               If Y(jj) < 0 Then Y(jj) = 0
                                               If Double.IsNaN(Y(jj)) OrElse Double.IsInfinity(Y(jj)) Then Y(jj) = 0
                                               jj = jj + 1
                                           Loop Until jj = n + 1

                                           'check convergence

                                           finish = True
                                           jj = 0
                                           Do
                                               If Abs(dY(jj)) > tol Then
                                                   finish = False
                                               End If
                                               jj = jj + 1
                                           Loop Until jj = n + 1

                                           cc = cc + 1

                                           If cc > maxits Then Exit Do

                                           If finish Then
                                               ' check if trivial solution (Michelsen criterion)
                                               Dim isTrivial = (Math.Abs(g_(xi)) < 0.0000000001 AndAlso r(xi) > 0.9 AndAlso r(xi) < 1.1)
                                               If Not isTrivial Then Vestimates(xi) = Y
                                           End If

                                           If Double.IsNaN(Y.SumY) Then Exit Do

                                       Loop Until finish = True

                                   End Sub)

            GlobalSettings.Settings.InspectorEnabled = prevstatus

            IObj?.SetCurrent

            ' The converged trials in trial order
            Dim VestList As List(Of Double()) = Vestimates.Where(Function(v) v IsNot Nothing).ToList()
            Dim excludeSet As New HashSet(Of Integer)

            ' Remove solutions that are too close to the feed composition (trivial)
            For i = 0 To VestList.Count - 1
                Dim sumDiff As Double = 0.0
                For j = 0 To n
                    sumDiff += Abs(VestList(i)(j) - Vz(j))
                Next
                If sumDiff < 0.001 Then
                    excludeSet.Add(i)
                End If
            Next

            ' Remove NaN/Infinity solutions
            For i = 0 To VestList.Count - 1
                If excludeSet.Contains(i) Then Continue For
                Dim sumY As Double = 0.0
                For j = 0 To n
                    sumY += VestList(i)(j)
                Next
                If Double.IsNaN(sumY) OrElse Double.IsInfinity(sumY) Then
                    excludeSet.Add(i)
                End If
            Next

            ' Join similar solutions (remove duplicates)
            For i = 0 To VestList.Count - 1
                If excludeSet.Contains(i) Then Continue For
                For o = i + 1 To VestList.Count - 1
                    If excludeSet.Contains(o) Then Continue For
                    Dim similar As Boolean = True
                    For j = 0 To n
                        If Abs(VestList(i)(j) - VestList(o)(j)) > 0.00001 Then
                            similar = False
                            Exit For
                        End If
                    Next
                    If similar Then
                        excludeSet.Add(o)
                    End If
                Next
            Next

            Dim validCount As Integer = VestList.Count - excludeSet.Count
            Dim sum2 As Double
            Dim isStable As Boolean

            If validCount > 0 Then

                'the phase is unstable

                isStable = False

                'normalize initial estimates

                IObj?.Paragraphs.Add("Liquid Phase Stability Test finished. Phase is NOT stable. Initial estimates for incipient liquid phase composition:")
                IObj?.Close()

                WriteDebugInfo("Liquid Phase Stability Test finished. Phase is NOT stable. Initial estimates for incipient liquid phase composition:")

                Dim inest(validCount - 1, n) As Double
                l = 0
                For i = 0 To VestList.Count - 1
                    If Not excludeSet.Contains(i) Then
                        Dim text As String = "{"
                        sum2 = 0
                        For j = 0 To n
                            sum2 += VestList(i)(j)
                        Next
                        If sum2 < 1.0E-30 Then sum2 = 1.0E-30
                        For j = 0 To n
                            inest(l, j) = VestList(i)(j) / sum2
                            text += inest(l, j).ToString & vbTab
                        Next
                        text = text.TrimEnd(New Char() {vbTab})
                        text += "}"
                        IObj?.Paragraphs.Add(text)
                        WriteDebugInfo(text)
                        l = l + 1
                    End If
                Next
                Return New Object() {isStable, inest}
            Else

                'the phase is stable

                WriteDebugInfo("Liquid Phase Stability Test finished. Phase is stable.")

                IObj?.Paragraphs.Add("Liquid Phase Stability Test finished. Phase is stable.")
                IObj?.Close()

                isStable = True

                Return New Object() {isStable, Nothing}

            End If

        End Function

        Function StabTest2(ByVal T As Double, ByVal P As Double, ByVal Vz As Double(), ByVal VTc As Double(), ByVal pp As PropertyPackage) As List(Of Double())

            Dim stresult = StabTest(T, P, Vz, VTc, pp)

            Dim results As New List(Of Double())

            If stresult(0) = False Then

                Dim m As Double = UBound(stresult(1), 1)

                For i As Integer = 0 To m
                    Dim Vx As Double() = pp.RET_NullVector()
                    For j As Integer = 0 To Vz.Length - 1
                        Vx(j) = stresult(1)(i, j)
                    Next
                    Dim lnfcv = pp.DW_CalcLnFugCoeff(Vx, T, P, State.Vapor)
                    Dim lnfcl = pp.DW_CalcLnFugCoeff(Vx, T, P, State.Liquid)
                    Dim gv = 0.0#
                    Dim gl = 0.0#
                    For j = 0 To Vz.Length - 1
                        If Vx(j) > 0.0# Then
                            gv += Vx(j) * (lnfcv(j) + Log(Vx(j)))
                            gl += Vx(j) * (lnfcl(j) + Log(Vx(j)))
                        End If
                    Next
                    If gl <= gv Then
                        results.Add(Vx)
                    End If
                Next

            End If

            Return results

        End Function

        Function GetPhaseSplitEstimates(T As Double, P As Double, L As Double, Vx As Double(), pp As PropertyPackage) As Object()

            Return GetPhaseSplitEstimates(T, P, L, Vx, pp, Nothing)

        End Function

        ''' <summary>
        ''' Second-liquid estimates for the liquid Vx of a vapour-liquid result whose vapour is Vy.
        ''' </summary>
        ''' <remarks>
        ''' With Vy given, a stability-test candidate with the composition of Vx (the trivial solution) or of
        ''' Vy (the vapour already there) is dropped before one is chosen. StabTest2 calls a candidate a liquid
        ''' when its liquid-root Gibbs energy is not above its vapour-root one, and where the equation of state
        ''' has a single root the two are equal, so near a bubble point the vapour passed as the second liquid.
        ''' </remarks>
        Function GetPhaseSplitEstimates(T As Double, P As Double, L As Double, Vx As Double(), pp As PropertyPackage, Vy As Double()) As Object()

            If pp.UseImmiscibleListForLiquid2InitialEstimates And pp.ImmiscibleLiquids.Count > 0 Then

                Return ProcessImmiscibleLiquids(pp, L, 0.0, Vx, pp.RET_NullVector())

            Else

                Dim stresult = StabTest2(T, P, Vx, pp.RET_VTC, pp)

                If Vy IsNot Nothing AndAlso Vy.Length = Vx.Length Then
                    Dim same = Function(w As Double(), ref As Double()) w.SubtractY(ref).Select(Function(d) Math.Abs(d)).Max < 0.005
                    stresult = stresult.Where(Function(w) Not same(w, Vx) AndAlso Not (Vy.Sum > 0.0 AndAlso same(w, Vy))).ToList()
                End If

                Dim n = Vx.Length - 1

                Dim L1, L2 As Double
                Dim Vx1, Vx2 As Double()

                If L = 0 Then L = 1
                L1 = L
                L2 = 0.0

                Vx1 = Vx.Clone()
                Vx2 = Vx.Clone()

                'if a second liquid phase is detected, estimate composition
                If stresult.Count > 0 Then

                    Dim validsolutions = stresult.Where(Function(s) s.Max > 0.05).ToList()

                    Dim fcl(n), fcv(n) As Double

                    If validsolutions.Count > 1 Then
                        ' select the solution which has the highest amount of a single compound.
                        ' Take this solution as composition of phase 2
                        Vx2 = validsolutions.OrderByDescending(Function(vec) vec.Max).First()
                    Else
                        Vx2 = stresult(0)
                    End If

                    'calculate L2
                    Dim maxL2 = Vx2.Max
                    Dim maxL2i = Vx2.ToList().IndexOf(maxL2)
                    L2 = Vx(maxL2i) * maxL2

                    'calculate L1
                    L1 = 1 - L2
                    Vx1 = Vx.SubtractY(Vx2.MultiplyConstY(L2)).MultiplyConstY(1 / L1)

                End If

                'adjust sum of L1 and L2 to specified total liquid fraction L 
                L1 *= L
                L2 *= L

                Return New Object() {L1, Vx1, L2, Vx2}

            End If

        End Function

#End Region

#Region "Phase Type Verification"

        ''' <summary>
        ''' This algorithm returns the state of a fluid given its composition and system conditions.
        ''' </summary>
        ''' <param name="Vx">Vector of mole fractions</param>
        ''' <param name="P">Pressure in Pa</param>
        ''' <param name="T">Temperature in K</param>
        ''' <param name="pp">Property Package instance</param>
        ''' <returns>A string indicating the phase: 'V' or 'L'.</returns>
        Public Shared Function IdentifyPhaseByLookupTable(Vx As Double(), P As Double, T As Double, pp As PropertyPackage) As String

            If pp.CurrentMaterialStream IsNot Nothing Then
                Dim ms = TryCast(pp.CurrentMaterialStream, Streams.MaterialStream)
                If ms IsNot Nothing AndAlso ms.PhaseEnvelopeLookup IsNot Nothing AndAlso ms.PhaseEnvelopeLookup.IsReady Then
                    Dim region = ms.QueryPhaseRegion(T, P)
                    If region = PropertyPackages.PhaseRegion.LiquidLike OrElse region = PropertyPackages.PhaseRegion.Liquid OrElse region = PropertyPackages.PhaseRegion.SolidLiquid Then
                        Return "L"
                    ElseIf region = PropertyPackages.PhaseRegion.VaporLike OrElse region = PropertyPackages.PhaseRegion.Vapor Then
                        Return "V"
                    Else
                        'fallback to old method if phase envelope lookup is inconclusive
                        Return IdentifyPhase(Vx, P, T, pp, "PR")
                    End If
                Else
                    'fallback to old method if no material stream or phase envelope lookup is available
                    Return IdentifyPhase(Vx, P, T, pp, "PR")
                End If
            Else
                'fallback to old method if no material stream or phase envelope lookup is available
                Return IdentifyPhase(Vx, P, T, pp, "PR")
            End If

        End Function

        ''' <summary>
        ''' This algorithm returns the state of a fluid given its composition and system conditions.
        ''' </summary>
        ''' <param name="Vx">Vector of mole fractions</param>
        ''' <param name="P">Pressure in Pa</param>
        ''' <param name="T">Temperature in K</param>
        ''' <param name="pp">Property Package instance</param>
        ''' <param name="eos">Equation of State: 'PR' or 'SRK'.</param>
        ''' <returns>A string indicating the phase: 'V' or 'L'.</returns>
        ''' <remarks>This algorithm is based on the method present in the following paper:
        ''' G. Venkatarathnam, L.R. Oellrich, Identification of the phase of a fluid using partial derivatives of pressure, volume, and temperature
        ''' without reference to saturation properties: Applications in phase equilibria calculations, Fluid Phase Equilibria, Volume 301, Issue 2, 
        ''' 25 February 2011, Pages 225-233, ISSN 0378-3812, http://dx.doi.org/10.1016/j.fluid.2010.12.001.
        ''' (http://www.sciencedirect.com/science/article/pii/S0378381210005935)
        ''' Keywords: Phase identification; Multiphase equilibria; Process simulators</remarks>
        Public Shared Function IdentifyPhase(Vx As Double(), P As Double, T As Double, pp As PropertyPackage, ByVal eos As String) As String

            Dim PIP, Tinv As Double, newphase As String, tmp As Double()

            tmp = CalcPIP(Vx, P, T, pp, eos)

            PIP = tmp(0)
            Tinv = tmp(1)

            If Tinv < 500 Then
                Dim fx, fx2, dfdx As Double
                Dim i As Integer = 0
                Do
                    If Settings.EnableParallelProcessing Then

                        Dim task1 As Task = TaskHelper.Run(Sub()
                                                               fx = 1 - CalcPIP(Vx, P, Tinv, pp, eos)(0)
                                                           End Sub)
                        Dim task2 As Task = TaskHelper.Run(Sub()
                                                               fx2 = 1 - CalcPIP(Vx, P, Tinv - 1, pp, eos)(0)
                                                           End Sub)
                        Task.WaitAll(task1, task2)

                    Else
                        fx = 1 - CalcPIP(Vx, P, Tinv, pp, eos)(0)
                        fx2 = 1 - CalcPIP(Vx, P, Tinv - 1, pp, eos)(0)
                    End If
                    dfdx = (fx - fx2)
                    Tinv = Tinv - fx / dfdx
                    i += 1
                Loop Until Math.Abs(fx) < 0.000001 Or i = 25
            End If

            If Double.IsNaN(Tinv) Or Double.IsInfinity(Tinv) Then Tinv = 2000

            If T > Tinv Then
                If PIP > 1 Then newphase = "V" Else newphase = "L"
            Else
                If PIP > 1 Then newphase = "L" Else newphase = "V"
            End If

            Return newphase

        End Function

        ''' <summary>
        ''' This algorithm returns the Phase Identification (PI) parameter for a fluid given its composition and system conditions.
        ''' </summary>
        ''' <param name="Vx">Vector of mole fractions</param>
        ''' <param name="P">Pressure in Pa</param>
        ''' <param name="T">Temperature in K</param>
        ''' <param name="pp">Property Package instance</param>
        ''' <param name="eos">Equation of State: 'PR' or 'SRK'.</param>
        ''' <returns>A string indicating the phase: 'V' or 'L'.</returns>
        ''' <remarks>This algorithm is based on the method present in the following paper:
        ''' G. Venkatarathnam, L.R. Oellrich, Identification of the phase of a fluid using partial derivatives of pressure, volume, and temperature
        ''' without reference to saturation properties: Applications in phase equilibria calculations, Fluid Phase Equilibria, Volume 301, Issue 2, 
        ''' 25 February 2011, Pages 225-233, ISSN 0378-3812, http://dx.doi.org/10.1016/j.fluid.2010.12.001.
        ''' (http://www.sciencedirect.com/science/article/pii/S0378381210005935)
        ''' Keywords: Phase identification; Multiphase equilibria; Process simulators</remarks>
        Private Shared Function CalcPIP(Vx As Double(), P As Double, T As Double, pp As PropertyPackage, ByVal eos As String) As Double()

            Dim g1, g2, g3, g4, g5, g6, t1, t2, v, a, b, dadT, R As Double, tmp As Double()

            If eos = "SRK" Then
                t1 = 1
                t2 = 0
                tmp = ThermoPlugs.SRK.ReturnParameters(T, P, Vx, pp.RET_VKij, pp.RET_VTC, pp.RET_VPC, pp.RET_VW)
            Else
                t1 = 1 + 2 ^ 0.5
                t2 = 1 - 2 ^ 0.5
                tmp = ThermoPlugs.PR.ReturnParameters(T, P, Vx, pp.RET_VKij, pp.RET_VTC, pp.RET_VPC, pp.RET_VW)
            End If

            a = tmp(0)
            b = tmp(1)
            v = tmp(2)
            dadT = tmp(3)

            g1 = 1 / (v - b)
            g2 = 1 / (v + t1 * b)
            g3 = 1 / (v + t2 * b)
            g4 = g2 + g3
            g5 = dadT
            g6 = g2 * g3

            R = 8.314

            Dim d2PdvdT, dPdT, d2Pdv2, dPdv As Double

            d2PdvdT = -R * g1 ^ 2 + g4 * g5 * g6
            dPdT = R * g1 - g5 * g6
            d2Pdv2 = 2 * R * T * g1 ^ 3 - 2 * a * g6 * (g2 ^ 2 + g6 + g3 ^ 2)
            dPdv = -R * T * g1 ^ 2 + a * g4 * g6

            Dim PIP As Double

            PIP = v * (d2PdvdT / dPdT - d2Pdv2 / dPdv)

            Dim Tinv As Double

            Tinv = 2 * a * (v - b) ^ 2 / (R * b * v ^ 2)

            Return New Double() {PIP, Tinv}

        End Function

        Public Shared Function CalcPIPressure(Vx As Double(), Pest As Double, T As Double, pp As PropertyPackage, ByVal eos As String) As String

            Dim P, PIP As Double

            Dim brent As New MathEx.BrentOpt.Brent
            brent.DefineFuncDelegate(AddressOf PIPressureF)

            P = brent.BrentOpt(1, Pest, 100, 0.001, 1000, New Object() {Vx, T, pp, eos})

            PIP = CalcPIP(Vx, P, T, pp, eos)(0)

            If P < 0 Or Abs(P - Pest) <= (Pest - 101325) / 1000 Then P = 0.0#

            Return P

        End Function

        Private Shared Function PIPressureF(x As Double, otherargs As Object)

            Return 1 - CalcPIP(otherargs(0), x, otherargs(1), otherargs(2), otherargs(3))(0)

        End Function

#End Region

#Region "Phases and Compounds Heuristics Check"

        Public Function PerformHeuristicsTest(Vz As Double(), T As Double, P As Double, pp As PropertyPackage) As HeuristicsTestResult

            Dim hres As New HeuristicsTestResult

            Dim i As Integer
            Dim n As Integer = Vz.Count

            Dim names = pp.RET_VNAMES().Select(Function(x) x.ToLower()).ToList()
            Dim props = pp.DW_GetConstantProperties()

            If T = 0.0 Then T = 298.15
            If P = 0.0 Then P = 101325

            'solids check

            Dim Tf = pp.RET_VTF

            For i = 0 To n - 1
                If Tf(i) > T And Tf(i) > 1.0 And Vz(i) > 0.000001 Then
                    hres.SolidPhase = True
                    hres.SolidFraction += Vz(i)
                End If
            Next

            For i = 0 To n - 1
                If props(i).IsSolid Then
                    hres.SolidPhase = True
                    hres.SolidFraction += Vz(i)
                End If
            Next

            If pp.ForcedSolids.Count > 0 Then
                'has solids
                For Each solid In pp.ForcedSolids
                    If Vz(names.IndexOf(solid.ToLower())) > 0 Then
                        hres.SolidPhase = True
                        hres.SolidFraction += Vz(names.IndexOf(solid.ToLower()))
                    End If
                Next
            End If

            If T > pp.RET_VTC.Max Then

                'no liquids.
                Return hres

            End If

            'liquid phase split check

            If names.Contains("water") And names.Where(Function(x) x.EndsWith("ane") Or x.EndsWith("ene") Or x.EndsWith("ine")).Count > 0 Then
                'Water + Hydrocarbons
                If Vz(names.IndexOf("water")) > 0.01 And Vz(names.IndexOf("water")) < 1.0 Then
                    Dim hcs = names.Where(Function(x) x.EndsWith("ane") Or x.EndsWith("ene") Or x.EndsWith("ine")).ToList()
                    For Each hc In hcs
                        If Vz(names.IndexOf(hc)) > 0.000001 And props(names.IndexOf(hc)).Critical_Temperature > T Then
                            hres.LiquidPhaseSplit = True
                            Exit For
                        End If
                    Next
                End If
            ElseIf names.Contains("water") And props.Where(Function(x) x.IsBlackOil Or x.IsHYPO Or x.IsPF).Count > 0 Then
                'Water + Hydrocarbons
                If Vz(names.IndexOf("water")) > 0.01 And Vz(names.IndexOf("water")) < 1.0 Then
                    Dim hcs = props.Where(Function(x) x.IsBlackOil Or x.IsHYPO Or x.IsPF).ToList()
                    For Each hc In hcs
                        If Vz(props.IndexOf(hc)) > 0.000001 And hc.Critical_Temperature > T Then
                            hres.LiquidPhaseSplit = True
                            Exit For
                        End If
                    Next
                End If
            ElseIf names.Where(Function(x) x.EndsWith("al")).Count > 0 And names.Where(Function(x) x.Contains("ane") Or x.Contains("ene") Or x.Contains("ine")).Count > 0 Then
                'Aldehydes + Hydrocarbons
                Dim alds = names.Where(Function(x) x.EndsWith("al")).ToList()
                For Each ald In alds
                    If Vz(names.IndexOf(ald)) > 0.01 And Vz(names.IndexOf(ald)) < 1.0 Then
                        Dim hcs = names.Where(Function(x) x.EndsWith("ane") Or x.EndsWith("ene") Or x.EndsWith("ine")).ToList()
                        For Each hc In hcs
                            If Vz(names.IndexOf(hc)) > 0.000001 And props(names.IndexOf(hc)).Critical_Temperature > T Then
                                hres.LiquidPhaseSplit = True
                                Exit For
                            End If
                        Next
                    End If
                Next
            ElseIf names.Where(Function(x) x.EndsWith("ol")).Count > 0 And names.Where(Function(x) x.EndsWith("ane") Or x.EndsWith("ene") Or x.EndsWith("ine")).Count > 0 Then

                'Alcohols + Hydrocarbons
                Dim alcs = names.Where(Function(x) x.EndsWith("ol")).ToList()
                For Each alc In alcs
                    If Vz(names.IndexOf(alc)) > 0.01 And Vz(names.IndexOf(alc)) < 1.0 Then
                        Dim hcs = names.Where(Function(x) x.EndsWith("ane") Or x.EndsWith("ene") Or x.EndsWith("ine")).ToList()
                        For Each hc In hcs
                            If Vz(names.IndexOf(hc)) > 0.000001 And props(names.IndexOf(hc)).Critical_Temperature > T Then
                                hres.LiquidPhaseSplit = True
                                Exit For
                            End If
                        Next
                    End If
                Next
            ElseIf names.Contains("water") And names.Where(Function(x) x.EndsWith("ol")).Count > 0 Then
                'Water + C4+ Alcohols
                If Vz(names.IndexOf("water")) > 0.01 And Vz(names.IndexOf("water")) < 1.0 Then
                    'get alcohols
                    Dim alcohols = names.Where(Function(x) x.EndsWith("ol")).ToList()
                    For Each alcohol In alcohols
                        If Vz(names.IndexOf(alcohol)) > 0.000001 And Vz(names.IndexOf(alcohol)) < 1.0 And props(names.IndexOf(alcohol)).Molar_Weight > 70 Then
                            hres.LiquidPhaseSplit = True
                            Exit For
                        End If
                    Next
                End If
            End If

            If Not hres.LiquidPhaseSplit AndAlso pp.UsesGibbsMinimizationForLLE Then
                'An equation-of-state package that does polymer liquid-liquid equilibrium (PC-SAFT): a polymer
                'pseudo-compound dissolved in a solvent can demix into a polymer-rich and a solvent-rich liquid
                '(cloud point). The name-based heuristics above never match a polymer, so route it to the
                'liquid-split flash explicitly. That flash self-seeds from the spinodal and falls back to a
                'plain vapour-liquid flash when there is no split, so a non-demixing mixture is unaffected.
                For Each poly In props.Where(Function(x) x.IsHYPO)
                    If Vz(props.IndexOf(poly)) > 0.000001 And Vz(props.IndexOf(poly)) < 1.0 Then
                        hres.LiquidPhaseSplit = True
                        Exit For
                    End If
                Next
            End If

            Dim FlashType As String = FlashSettings(ForceEquilibriumCalculationType)
            Dim HandleSolids As Boolean = FlashSettings(HandleSolidsInDefaultEqCalcMode)

            If FlashType = "Default" And hres.SolidPhase And Not HandleSolids And pp.ForcedSolids.Count = 0 Then
                'pp.Flowsheet.ShowMessage(String.Format(pp.Flowsheet.GetTranslatedString("FoundSolidsWarning") + " (P = {0:N2} Pa, T = {1:N2} K)", P, T), Interfaces.IFlowsheet.MessageType.Warning)
                hres.SolidPhase = False
                hres.SolidFraction = 0.0
            End If

            Return hres

        End Function


#End Region

#Region "XML Serialization"

        Public Overridable Function LoadData(data As List(Of XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            Dim el = (From xel As XElement In data Select xel Where xel.Name = "FlashSettings").SingleOrDefault

            If Not el Is Nothing Then

                FlashSettings.Clear()

                For Each xel3 In el.Elements
                    Try
                        Dim esname = [Enum].Parse(Interfaces.Enums.Helpers.GetEnumType("DWSIM.Interfaces.Enums.FlashSetting"), xel3.@Name)
                        FlashSettings.Add(esname, xel3.@Value)
                    Catch ex As Exception
                    End Try
                Next

                If Not FlashSettings.ContainsKey(PVFlash_FixedDampingFactor) Then
                    FlashSettings.Add(PVFlash_FixedDampingFactor, 1.0.ToString(Globalization.CultureInfo.InvariantCulture))
                End If
                If Not FlashSettings.ContainsKey(PVFlash_MaximumTemperatureChange) Then
                    FlashSettings.Add(PVFlash_MaximumTemperatureChange, 10.0.ToString(Globalization.CultureInfo.InvariantCulture))
                End If
                If Not FlashSettings.ContainsKey(PVFlash_TemperatureDerivativeEpsilon) Then
                    FlashSettings.Add(PVFlash_TemperatureDerivativeEpsilon, 0.1.ToString(Globalization.CultureInfo.InvariantCulture))
                End If
                If Not FlashSettings.ContainsKey(ST_Number_of_Random_Tries) Then
                    FlashSettings.Add(ST_Number_of_Random_Tries, 20)
                End If
                If Not FlashSettings.ContainsKey(CheckIncipientLiquidForStability) Then
                    FlashSettings.Add(CheckIncipientLiquidForStability, False)
                End If
                If Not FlashSettings.ContainsKey(PHFlash_MaximumTemperatureChange) Then
                    FlashSettings.Add(PHFlash_MaximumTemperatureChange, 30.0.ToString(Globalization.CultureInfo.InvariantCulture))
                End If
                If Not FlashSettings.ContainsKey(PTFlash_DampingFactor) Then
                    FlashSettings.Add(PTFlash_DampingFactor, 1.0.ToString(Globalization.CultureInfo.InvariantCulture))
                End If
                If Not FlashSettings.ContainsKey(ForceEquilibriumCalculationType) Then
                    FlashSettings.Add(ForceEquilibriumCalculationType, "Default")
                End If
                If Not FlashSettings.ContainsKey(ImmiscibleWaterOption) Then
                    FlashSettings.Add(ImmiscibleWaterOption, False)
                End If
                If Not FlashSettings.ContainsKey(HandleSolidsInDefaultEqCalcMode) Then
                    FlashSettings.Add(HandleSolidsInDefaultEqCalcMode, False)
                End If
                If Not FlashSettings.ContainsKey(UseIOFlash) Then
                    FlashSettings.Add(UseIOFlash, False)
                End If
                If Not FlashSettings.ContainsKey(FailSafeCalculationMode) Then
                    FlashSettings.Add(FailSafeCalculationMode, 1)
                End If
                If Not FlashSettings.ContainsKey(FailSafeCalculationMode) Then
                    FlashSettings.Add(PVFlash_FivePointStencilNumericalDerivative, False)
                End If
                If Not FlashSettings.ContainsKey(FailSafeCalculationMode) Then
                    FlashSettings.Add(NestedLoops_v2, False)
                End If
            End If

            Return XMLSerializer.XMLSerializer.Deserialize(Me, data)

        End Function

        Public Overridable Function SaveData() As List(Of XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = XMLSerializer.XMLSerializer.Serialize(Me)

            elements.Add(New XElement("FlashSettings"))

            For Each item In FlashSettings
                elements(elements.Count - 1).Add(New XElement("Setting", New XAttribute("Name", item.Key), New XAttribute("Value", item.Value)))
            Next

            Return elements

        End Function

#End Region

        Public Function Clone() As Interfaces.IFlashAlgorithm Implements Interfaces.IFlashAlgorithm.Clone

            Dim clonedobj As FlashAlgorithm = Me.MemberwiseClone()
            clonedobj.FlashSettings = New Dictionary(Of Interfaces.Enums.FlashSetting, String)
            For Each item In Me.FlashSettings
                clonedobj.FlashSettings.Add(item.Key, item.Value)
            Next
            Return clonedobj

        End Function

        Public Overridable Function GetNewInstance() As IFlashAlgorithm Implements IFlashAlgorithm.GetNewInstance

            Return Nothing

        End Function

        Public MustOverride ReadOnly Property AlgoType As Interfaces.Enums.FlashMethod Implements Interfaces.IFlashAlgorithm.AlgoType

        Public MustOverride ReadOnly Property Description As String Implements Interfaces.IFlashAlgorithm.Description

        Public MustOverride ReadOnly Property Name As String Implements Interfaces.IFlashAlgorithm.Name

        Public Overridable ReadOnly Property InternalUseOnly As Boolean Implements Interfaces.IFlashAlgorithm.InternalUseOnly
            Get
                Return False
            End Get
        End Property

        Public Property Tag As String = "" Implements Interfaces.IFlashAlgorithm.Tag

        Public MustOverride ReadOnly Property MobileCompatible As Boolean Implements Interfaces.IFlashAlgorithm.MobileCompatible

        Public Function ProcessImmiscibleLiquids(pp As PropertyPackage, L1 As Double, L2 As Double, Vx1 As Double(), Vx2 As Double()) As Object()

            If L1 > 0.0 And L2 = 0.0 And pp.ImmiscibleLiquids.Count > 0 Then

                Dim Vn = pp.RET_VNAMES()

                Dim L1n = L1, L2n = L2
                Dim Vx1n = Vx1.NormalizeY()
                Dim Vx2n = Vx2.NormalizeY()

                Dim i = 0
                For Each n In Vn
                    If pp.ImmiscibleLiquids.Contains(n) And Vx1(i) > 0.0 Then
                        Vx2n(i) = Vx1(i)
                        Vx1n(i) = 0.0
                        L2n += Vx1n(i) * L1
                        L1n -= Vx1n(i) * L1
                    End If
                    i += 1
                Next

                If L1n > 0.0 And L2n > 0.0 Then

                    Vx1n = Vx1n.NormalizeY()
                    Vx2n = Vx2n.NormalizeY()

                    Return New Object() {L1n, Vx1n, L2n, Vx2n}
                Else

                    Return New Object() {L1, Vx1, L2, Vx2}

                End If

            Else

                Return New Object() {L1, Vx1, L2, Vx2}

            End If

        End Function


    End Class

    Public Class HeuristicsTestResult

        Public Property LiquidPhaseSplit As Boolean = False
        Public Property SolidPhase As Boolean = False
        Public Property SolidFraction As Double = 0.0

    End Class

    ''' <summary>
    ''' Class to store flash calculation results.
    ''' </summary>
    ''' <remarks></remarks>
    ''' 
    <System.Serializable> Public Class FlashCalculationResult

        Implements Interfaces.IFlashCalculationResult

        ''' <summary>
        ''' Defines the base mole amount for determination of phase/compound fractions. Default is 1.
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Property BaseMoleAmount As Double = 1.0# Implements Interfaces.IFlashCalculationResult.BaseMoleAmount
        Public Property Kvalues As New List(Of Double) Implements Interfaces.IFlashCalculationResult.Kvalues
        Public Property MixtureMoleAmounts As New List(Of Double) Implements Interfaces.IFlashCalculationResult.MixtureMoleAmounts
        Public Property VaporPhaseMoleAmounts As New List(Of Double) Implements Interfaces.IFlashCalculationResult.VaporPhaseMoleAmounts
        Public Property LiquidPhase1MoleAmounts As New List(Of Double) Implements Interfaces.IFlashCalculationResult.LiquidPhase1MoleAmounts
        Public Property LiquidPhase2MoleAmounts As New List(Of Double) Implements Interfaces.IFlashCalculationResult.LiquidPhase2MoleAmounts
        Public Property SolidPhaseMoleAmounts As New List(Of Double) Implements Interfaces.IFlashCalculationResult.SolidPhaseMoleAmounts
        Public Property CalculatedTemperature As Nullable(Of Double) Implements Interfaces.IFlashCalculationResult.CalculatedTemperature
        Public Property CalculatedPressure As Nullable(Of Double) Implements Interfaces.IFlashCalculationResult.CalculatedPressure
        Public Property CalculatedEnthalpy As Nullable(Of Double) Implements Interfaces.IFlashCalculationResult.CalculatedEnthalpy
        Public Property CalculatedEntropy As Nullable(Of Double) Implements Interfaces.IFlashCalculationResult.CalculatedEntropy
        Public Property CompoundProperties As List(Of Interfaces.ICompoundConstantProperties) Implements Interfaces.IFlashCalculationResult.CompoundProperties
        Public Property FlashAlgorithmType As String = "" Implements Interfaces.IFlashCalculationResult.FlashAlgorithmType
        Public Property FlashSpecification1 As PropertyPackages.FlashSpec
        Public Property FlashSpecification2 As PropertyPackages.FlashSpec
        Public Property ResultException As Exception Implements Interfaces.IFlashCalculationResult.ResultException
        Public Property IterationsTaken As Integer = 0 Implements Interfaces.IFlashCalculationResult.IterationsTaken
        Public Property TimeTaken As New TimeSpan() Implements Interfaces.IFlashCalculationResult.TimeTaken

        Sub New()

        End Sub

        Sub New(constprop As List(Of Interfaces.ICompoundConstantProperties))

            CompoundProperties = constprop

        End Sub

        Public Function GetVaporPhaseMoleFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetVaporPhaseMoleFractions

            Dim collection As List(Of Double) = VaporPhaseMoleAmounts

            Dim total As Double = collection.ToArray.SumY

            If total = 0.0# Then total = 1.0#

            Dim molefracs As New List(Of Double)

            For Each value As Double In collection
                molefracs.Add(value / total)
            Next

            Return molefracs.ToArray

        End Function

        Public Function GetLiquidPhase1MoleFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetLiquidPhase1MoleFractions

            Dim collection As List(Of Double) = LiquidPhase1MoleAmounts

            Dim total As Double = collection.ToArray.SumY

            If total = 0.0# Then total = 1.0#

            Dim molefracs As New List(Of Double)

            For Each value As Double In collection
                molefracs.Add(value / total)
            Next

            Return molefracs.ToArray

        End Function

        Public Function GetLiquidPhase2MoleFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetLiquidPhase2MoleFractions

            Dim collection As List(Of Double) = LiquidPhase2MoleAmounts

            Dim total As Double = collection.ToArray.SumY

            If total = 0.0# Then total = 1.0#

            Dim molefracs As New List(Of Double)

            For Each value As Double In collection
                molefracs.Add(value / total)
            Next

            Return molefracs.ToArray

        End Function

        Public Function GetSolidPhaseMoleFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetSolidPhaseMoleFractions

            Dim collection As List(Of Double) = SolidPhaseMoleAmounts

            Dim total As Double = collection.ToArray.SumY

            If total = 0.0# Then total = 1.0#

            Dim molefracs As New List(Of Double)

            For Each value As Double In collection
                molefracs.Add(value / total)
            Next

            Return molefracs.ToArray

        End Function

        Public Function GetVaporPhaseMoleFraction() As Double Implements Interfaces.IFlashCalculationResult.GetVaporPhaseMoleFraction

            Dim collection As List(Of Double) = VaporPhaseMoleAmounts

            Return collection.ToArray.SumY

        End Function

        Public Function GetLiquidPhase1MoleFraction() As Double Implements Interfaces.IFlashCalculationResult.GetLiquidPhase1MoleFraction

            Dim collection As List(Of Double) = LiquidPhase1MoleAmounts

            Return collection.ToArray.SumY

        End Function

        Public Function GetLiquidPhase2MoleFraction() As Double Implements Interfaces.IFlashCalculationResult.GetLiquidPhase2MoleFraction

            Dim collection As List(Of Double) = LiquidPhase2MoleAmounts

            Return collection.ToArray.SumY

        End Function

        Public Function GetSolidPhaseMoleFraction() As Double Implements Interfaces.IFlashCalculationResult.GetSolidPhaseMoleFraction

            Dim collection As List(Of Double) = SolidPhaseMoleAmounts

            Return collection.ToArray.SumY

        End Function

        Public Function GetVaporPhaseMassFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetVaporPhaseMassFractions

            Return ConvertToMassFractions(GetVaporPhaseMoleFractions())

        End Function

        Public Function GetLiquidPhase1MassFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetLiquidPhase1MassFractions

            Return ConvertToMassFractions(GetLiquidPhase1MoleFractions())

        End Function

        Public Function GetLiquidPhase2MassFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetLiquidPhase2MassFractions

            Return ConvertToMassFractions(GetLiquidPhase2MoleFractions())

        End Function

        Public Function GetSolidPhaseMassFractions() As Double() Implements Interfaces.IFlashCalculationResult.GetSolidPhaseMassFractions

            Return ConvertToMassFractions(GetSolidPhaseMoleFractions())

        End Function

        Private Function ConvertToMassFractions(ByVal Vz As Double()) As Double() Implements Interfaces.IFlashCalculationResult.ConvertToMassFractions

            Dim Vwe(Vz.Length - 1) As Double
            Dim mol_x_mm As Double = 0
            Dim i As Integer = 0
            For Each sub1 As Interfaces.ICompoundConstantProperties In CompoundProperties
                mol_x_mm += Vz(i) * sub1.Molar_Weight
                i += 1
            Next

            If mol_x_mm = 0.0# Then mol_x_mm = 1.0#

            i = 0
            For Each sub1 As Interfaces.ICompoundConstantProperties In CompoundProperties
                If mol_x_mm <> 0 Then
                    Vwe(i) = Vz(i) * sub1.Molar_Weight / mol_x_mm
                Else
                    Vwe(i) = 0.0#
                End If
                i += 1
            Next

            Return Vwe

        End Function

        Private Function CalcMolarWeight(ByVal Vz() As Double) As Double Implements Interfaces.IFlashCalculationResult.CalcMolarWeight

            Dim val As Double

            Dim i As Integer = 0

            For Each subst As Interfaces.ICompoundConstantProperties In CompoundProperties
                val += Vz(i) * subst.Molar_Weight
                i += 1
            Next

            Return val

        End Function

        Public Function GetVaporPhaseMassFraction() As Double Implements Interfaces.IFlashCalculationResult.GetVaporPhaseMassFraction

            Dim mw, vw As Double

            mw = MixtureMoleAmounts.Sum * CalcMolarWeight(MixtureMoleAmounts.ToArray.MultiplyConstY(1 / BaseMoleAmount))
            vw = VaporPhaseMoleAmounts.Sum * CalcMolarWeight(GetVaporPhaseMoleFractions())

            Return vw / mw

        End Function

        Public Function GetLiquidPhase1MassFraction() As Double Implements Interfaces.IFlashCalculationResult.GetLiquidPhase1MassFraction

            Dim mw, l1w As Double

            mw = MixtureMoleAmounts.Sum * CalcMolarWeight(MixtureMoleAmounts.ToArray.MultiplyConstY(1 / BaseMoleAmount))
            l1w = LiquidPhase1MoleAmounts.Sum * CalcMolarWeight(GetLiquidPhase1MoleFractions())

            Return l1w / mw

        End Function

        Public Function GetLiquidPhase2MassFraction() As Double Implements Interfaces.IFlashCalculationResult.GetLiquidPhase2MassFraction

            Dim mw, l2w As Double

            mw = MixtureMoleAmounts.Sum * CalcMolarWeight(MixtureMoleAmounts.ToArray.MultiplyConstY(1 / BaseMoleAmount))
            l2w = LiquidPhase2MoleAmounts.Sum * CalcMolarWeight(GetLiquidPhase2MoleFractions())

            Return l2w / mw

        End Function

        Public Function GetSolidPhaseMassFraction() As Double Implements Interfaces.IFlashCalculationResult.GetSolidPhaseMassFraction

            Dim mw, sw As Double

            mw = MixtureMoleAmounts.Sum * CalcMolarWeight(MixtureMoleAmounts.ToArray.MultiplyConstY(1 / BaseMoleAmount))
            sw = SolidPhaseMoleAmounts.Sum * CalcMolarWeight(GetSolidPhaseMoleFractions())

            Return sw / mw

        End Function

    End Class

End Namespace
