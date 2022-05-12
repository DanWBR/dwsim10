'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


''' <summary>
''' This interface defines an event raised when a new message is sent to the flowsheet log, 
''' to be catched by an object when the automation is being done through a COM association.
''' </summary>

Public Interface IFlowsheetNewMessageSentEvent

    Sub NewMessageSent(message As String)

End Interface
