Imports DWSIMCore.Foundation.Enums

'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


''' <summary>
''' This interface defines the flowsheet settings and other properties.
''' </summary>
Public Interface IFlowsheetOptions

    Property NumberFormat As String
    Property FractionNumberFormat As String

    Property SimulationName As String
    Property SimulationAuthor As String
    Property SimulationComments As String

    Property FilePath As String

    Property BackupFileName As String

    <Xml.Serialization.XmlIgnore> Property Password As String
    <Xml.Serialization.XmlIgnore> Property UsePassword As Boolean

    Property FlowsheetSnapToGrid As Boolean
    Property FlowsheetDisplayGrid As Boolean
    Property FlowsheetQuickConnect As Boolean
    Property FlowsheetShowConsoleWindow As Boolean
    Property FlowsheetShowCOReportsWindow As Boolean
    Property FlowsheetShowCalculationQueue As Boolean
    Property FlowsheetShowWatchWindow As Boolean

    Property FlowsheetControlPanelMode As Boolean

    Property Key As String

    Property SelectedUnitSystem As IUnitsOfMeasure

    Property VisibleProperties As Dictionary(Of String, List(Of String))

    Property SimultaneousAdjustSolverEnabled As Boolean

    Property SpreadsheetUseRegionalSeparator As Boolean

    Property SpreadsheetUnitLockingMode As Boolean

    Property MassBalanceCheck As WarningType

    Property EnergyBalanceCheck As WarningType

    Property MassBalanceRelativeTolerance As Double

    Property EnergyBalanceRelativeTolerance As Double

    Property DisplayFloatingPropertyTables As Boolean

    Property DisplayCornerPropertyList As Boolean

    Property DisplayCornerPropertyListPosition As ListPosition

    Property DisplayCornerPropertyListFontName As String

    Property DisplayCornerPropertyListFontSize As Integer

    Property DisplayCornerPropertyListFontColor As String

    Property DisplayCornerPropertyListPadding As Integer

    Property DefaultFloatingTableCompoundAmountBasis As Enums.CompositionBasis

    Property DisplayFloatingTableCompoundAmounts As Boolean

    Property FlowsheetMultiSelectMode As Boolean

    Property CompoundOrderingMode As CompoundOrdering

    Property SkipEquilibriumCalculationOnDefinedStreams As Boolean

    Property ForceStreamPhase As Enums.ForcedPhase

    Property DisplayUserDefinedPropertiesEditor As Boolean

    Property LabelFontSize As Double

    Property RegularFontName As String

    Property BoldFontName As String

    Property ItalicFontName As String

    Property FlowsheetColorTheme As Integer

    Property BoldItalicFontName As String

    Property DisplayEnergyStreamPowerValue As Boolean

    Property DisplayMaterialStreamMassFlowValue As Boolean

    Property DisplayMaterialStreamMolarFlowValue As Boolean

    Property DisplayMaterialStreamVolFlowValue As Boolean

    Property DisplayMaterialStreamTemperatureValue As Boolean

    Property DisplayMaterialStreamPressureValue As Boolean

    Property DisplayMaterialStreamEnergyFlowValue As Boolean

    Property DisplayDynamicPropertyValues As Boolean

    Property AddObjectsWithStreams As Integer

    Property Simulate365FileID As String

End Interface
