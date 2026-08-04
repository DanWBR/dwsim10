''' <summary>
''' Binary interaction parameter result returned by
''' <see cref="IFlowsheet.CallDataRegressionUtility"/>. The Parameters
''' dictionary keys are model-specific (kij, kji, A12, A21, B12, B21,
''' C12, C21, alpha12).
''' </summary>
Public Interface IInteractionParameter
    Property Comp1 As String
    Property Comp2 As String
    Property Model As String
    Property Parameters As Dictionary(Of String, Object)
End Interface
