Public Interface IConvergenceHelperTrainingData

    Property RequestType As ConvergenceHelperRequestType

    Property Reactions As List(Of IReaction)

    Property ModelName As String

    Property NumberOfCompounds As Integer

    Property CompoundNames As String()

    Property Temperature As String

    Property Temperature2 As String

    Property Pressure As String

    Property MassEnthalpy As String

    Property MassEntropy As String

    Property VaporMolarFraction As String

    Property MixtureMolarFlows As String()

    Property MixtureMolarFlows2 As String()

    Property VaporMolarFlows As String()

    Property Liquid1MolarFlows As String()

    Property Liquid2MolarFlows As String()

    Property SolidMolarFlows As String()

    Property KValuesVL1 As String()

    Property KValuesVL2 As String()

    Property ReactionExtents As String()

    Property Hash As String

    Function GetBase64StringHash() As String

End Interface

Public Interface IConvergenceHelperRequest

    Property RequestType As ConvergenceHelperRequestType

    Property ModelName As String

    Property NumberOfCompounds As Integer

    Property CompoundNames As String()

    Property Temperature As Double?

    Property Pressure As Double?

    Property MassEnthalpy As Double?

    Property MassEntropy As Double?

    Property VaporMolarFraction As Double?

    Property MixtureMolarFlows As Double()

End Interface

Public Interface IConvergenceHelperResponse

    Property RequestType As ConvergenceHelperRequestType

    Property MetaData As IConvergenceHelperMetaData

    Property ModelName As String

    Property IsValid As Boolean

    Property Reason As String

    Property InnerException As Exception

    Property Temperature As Double?

    Property Temperature2 As Double?

    Property Pressure As Double?

    Property MassEnthalpy As Double?

    Property MassEntropy As Double?

    Property VaporMolarFraction As Double?

    Property MixtureMolarFlows As Double()

    Property VaporMolarFlows As Double()

    Property Liquid1MolarFlows As Double()

    Property Liquid2MolarFlows As Double()

    Property SolidMolarFlows As Double()

    Property KValuesVL1 As Double()

    Property KValuesVL2 As Double()

    Property MixtureMolarFlows2 As Double()

    Property ReactionExtents As Double()

End Interface

Public Interface IConvergenceHelperMetaData

    Property RequestType As ConvergenceHelperRequestType

    Property ModelName As String

    Property PropertyPackageName As String

    Property CreatedOn As DateTime

    Property LastUpdatedOn As DateTime

    Property NumberOfSamples As Integer

    Property NumberOfCompounds As Integer

    Property NumberOfReactions As Integer

    Property CompoundNames As String()

    Property TemperatureRange As Single()

    Property PressureRange As Single()

    Property MassEnthalpyRange As Single()

    Property MassEntropyRange As Single()

    Property VaporMolarFractionRange As Single()

    Property MolarCompositionRange As List(Of Single())

    Property TrainingDataMSE As Single

    Property TestingDataMSE As Single

End Interface

Public Enum ConvergenceHelperRequestType

    PVFlash = 0
    TVFlash = 1
    PTFlash = 2
    PHFlash = 3
    PSFlash = 4

    GibbsReactorIsothermic = 5
    GibbsReactorAdiabatic = 6

    EquilibriumReactorIsothermic = 8
    EquilibriumReactorAdiabatic = 9

End Enum

Public Interface IAIAssistedConvergenceManager

    Property Database As IFileDatabaseProvider

    Property Settings As IManagerSettings

    Sub DisplayEditor(Flowsheet As IFlowsheet)

    Sub AddToSummary(mdata As IConvergenceHelperMetaData)

    Function GetModel(request As IConvergenceHelperRequest) As IANNModel

    Sub Initialize()

    Function LoadModelFromFile(modelfilepath As String) As IANNModel

    Sub LoadSettings()

    Sub SaveDatabaseToFile()

    Sub SaveModelToFile(model As IANNModel)

    Sub SaveSettings()

    Sub StoreData(data As IConvergenceHelperTrainingData)

    Sub UpdateModels()

    Sub UpdateModelList()

    Property Initialized As Boolean

    ''' <summary>
    ''' The models the manager has on disk, with the data each one was trained on.
    ''' </summary>
    ReadOnly Property ModelsSummary As List(Of IConvergenceHelperMetaData)

    ''' <summary>
    ''' Creates a generator that sweeps a flowsheet to produce training data.
    ''' </summary>
    Function CreateDataGenerator() As IConvergenceDataGenerator

End Interface

''' <summary>
''' Sweeps a flowsheet through a range of operating conditions, solving at every point, so the
''' convergence enhancer has data to learn from.
''' </summary>
Public Interface IConvergenceDataGenerator

    ''' <summary>How far along the whole job is, from 0 to 1.</summary>
    Property Progress1 As Action(Of Single)

    ''' <summary>How far along the current step is, from 0 to 1.</summary>
    Property Progress2 As Action(Of Single)

    ''' <summary>What the generator is doing right now.</summary>
    Property StatusUpdate As Action(Of String)

    ''' <summary>Called when the sweep is over, whether it finished or was cancelled.</summary>
    Property JobFinished As Action

    Sub GenerateData(flowsheet As IFlowsheet, tokens As Threading.CancellationTokenSource)

End Interface

Public Interface IAIAssistedSolutionProvider

    Function GetSolutionEstimate(request As IConvergenceHelperRequest) As IConvergenceHelperResponse

    Function GetPhaseEnvelope(request As IPhaseEnvelopeRequest) As IPhaseEnvelopeResult

End Interface

Public Interface IManagerSettings

    Property UniqueID As String

    Property UploadToServer As Boolean

    Property StoreDataInLocalDatabase As Boolean

    Property AssistanceLevel As Integer

    Property AutoUpdateEnabled As Boolean

    Property DatabaseSaveThreshold As Integer

    Property EATrainThreshold As Integer

    Property EITrainThreshold As Integer

    Property GATrainThreshold As Integer

    Property GITrainThreshold As Integer

    Property PHFlashTrainThreshold As Integer

    Property PSFlashTrainThreshold As Integer

    Property PTFlashTrainThreshold As Integer

    Property PVFlashTrainThreshold As Integer

    Property TVFlashTrainThreshold As Integer

    Property HomeDirectory As String

    Property UpdateTimerInterval As Integer

    Property ProcessDataDeltaPercentage As Integer

    Property ProcessDataNumberOfPoints As Integer

    Property ModelTrainingIterations As Integer

    Property EnableSolutionProvider1 As Boolean

    Property EnableSolutionProvider2 As Boolean

    Property EnableSolutionProvider3 As Boolean

    Property EnableSolutionProvider4 As Boolean

    Property EnableSolutionProvider5 As Boolean

    Property PTFlashEnable As Boolean

    Property PHFlashEnable As Boolean

    Property PSFlashEnable As Boolean

    Property PVFlashEnable As Boolean

    Property TVFlashEnable As Boolean

    Property GibbsReactorEnable As Boolean

    Property EquilibriumReactorEnable As Boolean

    Property EnablePhaseEnvelopeEnhancements As Boolean

End Interface

Public Interface IPhaseEnvelopeResult

    Property BubbleTemperatures As Double()

    Property BubblePressures As Double()

    Property DewTemperatures As Double()

    Property DewPressures As Double()

    Property CriticalPoints As List(Of Double())

End Interface

Public Interface IPhaseEnvelopeRequest

    Property CompoundNames As String()

    Property MolarComposition As Double()

    Property ModelName As String

    Property ModelParameters As List(Of Tuple(Of String, String, Double()))

End Interface