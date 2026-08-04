Public Interface IANNModel

    Property Data As List(Of List(Of Single))

    Property MetaData As IConvergenceHelperMetaData

    Property Parameters As IModelParameters

    Property SerializedModelData As String

    Property TensorName_Output As String

    Property TensorName_X As String

    Property TensorName_Y As String

    Property session As Object

    Sub Dispose()

    Function LoadData(ByVal data As List(Of XElement)) As Boolean

    Function Predict(ByVal inputdata As List(Of Single)) As List(Of Single)

    Sub PrepareData()

    Function SaveData() As List(Of XElement)

    Function Summary() As String

    Sub Train(Optional ByVal flowsheet As IFlowsheet = Nothing)

End Interface

Public Interface IModelParameters

    Property AbsoluteMSETolerance As Single

    Property BatchSize As Integer

    Property Labels As List(Of String)

    Property Labels_Outputs As List(Of String)

    Property LearningRate As Single

    Property MaxScale As Single

    Property MaxValues As List(Of Single)

    Property MinScale As Single

    Property MinValues As List(Of Single)

    Property NumberOfEpochs As Integer

    Property NumberOfLayers As Integer

    Property NumberOfNeuronsOnFirstLayer As Integer

    Property RelativeMSETolerance As Single

    Property SplitFactor As Single

    Property TensorName_Output As String

    Property TensorName_X As String

    Property TensorName_Y As String

    Function LoadData(ByVal data As List(Of XElement)) As Boolean

    Function SaveData() As List(Of XElement)

End Interface