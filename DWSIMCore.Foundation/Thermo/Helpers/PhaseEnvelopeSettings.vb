Namespace PropertyPackages
    <Serializable> Public Class PhaseEnvelopeOptions

        Implements IPhaseEnvelopeOptions, ICloneable

        Public Property BubbleCurveDeltaP As Double = 101325.0 Implements IPhaseEnvelopeOptions.BubbleCurveDeltaP

        Public Property BubbleCurveDeltaT As Double = 5.0 Implements IPhaseEnvelopeOptions.BubbleCurveDeltaT

        Public Property BubbleCurveInitialFlash As String = "PVF" Implements IPhaseEnvelopeOptions.BubbleCurveInitialFlash

        Public Property BubbleCurveInitialPressure As Double = 101325.0# Implements IPhaseEnvelopeOptions.BubbleCurveInitialPressure

        Public Property BubbleCurveInitialTemperature As Double = 200.0# Implements IPhaseEnvelopeOptions.BubbleCurveInitialTemperature

        Public Property BubbleCurveMaximumPoints As Integer = 300 Implements IPhaseEnvelopeOptions.BubbleCurveMaximumPoints

        Public Property CheckLiquidInstability As Boolean Implements IPhaseEnvelopeOptions.CheckLiquidInstability

        Public Property DewCurveDeltaP As Double = 101325.0 Implements IPhaseEnvelopeOptions.DewCurveDeltaP

        Public Property DewCurveDeltaT As Double = 5.0 Implements IPhaseEnvelopeOptions.DewCurveDeltaT

        Public Property DewCurveInitialFlash As String = "PVF" Implements IPhaseEnvelopeOptions.DewCurveInitialFlash

        Public Property DewCurveInitialPressure As Double = 101325.0# Implements IPhaseEnvelopeOptions.DewCurveInitialPressure

        Public Property DewCurveInitialTemperature As Double = 250.0# Implements IPhaseEnvelopeOptions.DewCurveInitialTemperature

        Public Property DewCurveMaximumPoints As Integer = 300 Implements IPhaseEnvelopeOptions.DewCurveMaximumPoints

        Public Property Hydrate As Boolean = False Implements IPhaseEnvelopeOptions.Hydrate

        Public Property HydrateModel As Integer = 0 Implements IPhaseEnvelopeOptions.HydrateModel

        Public Property HydrateVaporOnly As Boolean = False Implements IPhaseEnvelopeOptions.HydrateVaporOnly

        Public Property OperatingPoint As Boolean = True Implements IPhaseEnvelopeOptions.OperatingPoint

        Public Property PhaseIdentificationCurve As Boolean = False Implements IPhaseEnvelopeOptions.PhaseIdentificationCurve

        Public Property QualityLine As Boolean = False Implements IPhaseEnvelopeOptions.QualityLine

        Public Property QualityValue As Double = 0.5# Implements IPhaseEnvelopeOptions.QualityValue

        Public Property StabilityCurve As Boolean = False Implements IPhaseEnvelopeOptions.StabilityCurve

        Public Property BubbleCurveMaximumTemperature As Double = 1000.0# Implements IPhaseEnvelopeOptions.BubbleCurveMaximumTemperature

        Public Property BubbleUseCustomParameters As Boolean = False Implements IPhaseEnvelopeOptions.BubbleUseCustomParameters

        Public Property DewCurveMaximumTemperature As Double = 1000.0# Implements IPhaseEnvelopeOptions.DewCurveMaximumTemperature

        Public Property DewUseCustomParameters As Boolean = False Implements IPhaseEnvelopeOptions.DewUseCustomParameters

        Public Function Clone() As Object Implements ICloneable.Clone
            Return Me.MemberwiseClone
        End Function

    End Class

End Namespace

