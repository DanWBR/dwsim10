'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


Imports Enums.GraphicObjects

''' <summary>
''' This interface defines the basic properties for a graphical representation of an object in the flowsheet PFD.
''' </summary>
Public Interface IExternalUnitOperation

    Function ReturnInstance(typename As String) As Object

    Sub Draw(g As Object)

    Sub CreateConnectors()

    ReadOnly Property Name As String

    ReadOnly Property Description As String

    ReadOnly Property Prefix As String

    Sub PopulateEditorPanel(container As Object)

End Interface
