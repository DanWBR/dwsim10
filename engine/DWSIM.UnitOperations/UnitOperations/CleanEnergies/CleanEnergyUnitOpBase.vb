Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums

Namespace UnitOperations

    ''' <summary>
    ''' Abstract base class for clean-energy power-source unit operations (solar panel, wind turbine,
    ''' hydroelectric turbine, water electrolyser). Provides shared properties for weather input,
    ''' connector creation, drawing, and the external-unit-operation interface.
    ''' </summary>
    Public MustInherit Class CleanEnergyUnitOpBase

        Inherits DWSIM.UnitOperations.UnitOperations.UnitOpBaseClass

        Implements DWSIM.Interfaces.IExternalUnitOperation

        ''' <summary>Gets or sets whether user-defined weather parameters are used instead of flowsheet-level weather.</summary>
        Public Property UseUserDefinedWeather As Boolean = False

        ''' <summary>Gets a value indicating this unit operation acts as a source (generates energy).</summary>
        Public Overrides ReadOnly Property IsSource As Boolean = True

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        Private ReadOnly Property IExternalUnitOperation_Name As String = GetDisplayName() Implements IExternalUnitOperation.Name

        ''' <summary>When overridden, gets or sets the default name prefix for this unit operation on the flowsheet.</summary>
        Public MustOverride Property Prefix As String Implements IExternalUnitOperation.Prefix

        ''' <summary>Gets the description of this external unit operation.</summary>
        Public ReadOnly Property Description As String = GetDisplayDescription() Implements IExternalUnitOperation.Description

        ''' <summary>Gets or sets the simulation object class category (CleanPowerSources).</summary>
        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.CleanPowerSources

        ''' <summary>Gets a value indicating this unit operation is not compatible with mobile/cross-platform interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean = False

        ''' <summary>When overridden, creates and returns a new instance of this unit operation type for deserialization.</summary>
        Public MustOverride Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance

        ''' <summary>When overridden, draws the unit operation icon on the given SkiaSharp canvas.</summary>
        Public MustOverride Sub Draw(g As Object) Implements IExternalUnitOperation.Draw

        ''' <summary>When overridden, creates the graphic connector (port) definitions on the flowsheet.</summary>
        Public MustOverride Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors

        ''' <summary>
        ''' Initializes a new instance of the clean-energy unit operation with a name and description.
        ''' </summary>
        ''' <param name="Name">The display name.</param>
        ''' <param name="Description">A brief description.</param>
        Public Sub New(ByVal Name As String, ByVal Description As String)

            MyBase.CreateNew()
            Me.ComponentName = Name
            Me.ComponentDescription = Description

        End Sub

        ''' <summary>Initializes a new default instance of the clean-energy unit operation.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>Performs post-calculation validation (no-op for clean energy unit operations).</summary>
        Public Overrides Sub PerformPostCalcValidation()

        End Sub

        ''' <summary>When overridden, populates the cross-platform editor panel with controls for this unit operation.</summary>
        Public MustOverride Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

        Private Sub CallSolverIfNeeded()
            If GlobalSettings.Settings.CallSolverOnEditorPropertyChanged Then
                FlowSheet.RequestCalculation()
            End If
        End Sub

    End Class

End Namespace
