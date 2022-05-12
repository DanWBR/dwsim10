'    DWSIM Interface definitions
'    Copyright 2010-2017 Daniel Wagner O. de Medeiros


'Revision history:
'27/09/2010 - added new property - Display Mode 
'20/08/2010 - initial release

Public Interface IUtilityPlugin

    ReadOnly Property Name() As String
    ReadOnly Property Description() As String
    ReadOnly Property Author() As String
    ReadOnly Property ContactInfo() As String
    ReadOnly Property WebSite() As String
    ReadOnly Property UniqueID() As String

    ReadOnly Property UtilityForm() As Object
    ReadOnly Property CurrentFlowsheet() As IFlowsheet

    Function SetFlowsheet(form As IFlowsheet) As Boolean

    ReadOnly Property DisplayMode() As DispMode

    Enum DispMode
        Modal = 0
        Normal = 1
        Dockable = 2
    End Enum

End Interface

Public Interface IUtilityPlugin2

    Inherits IUtilityPlugin

    Function Run(args As Object) As Object

End Interface

Public Interface IUtilityPlugin5

    ReadOnly Property Name() As String
    ReadOnly Property Description() As String
    ReadOnly Property Author() As String
    ReadOnly Property ContactInfo() As String
    ReadOnly Property WebSite() As String
    ReadOnly Property UniqueID() As String

    ''' <summary>
    ''' This must be an instance of Eto.Forms.Form
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    ReadOnly Property UtilityForm() As Object

    ReadOnly Property CurrentFlowsheet() As IFlowsheet

    Function SetFlowsheet(form As IFlowsheet) As Boolean

    Function Run(args As Object) As Object

End Interface

Public Interface IUtilityPlugin6

    Inherits IUtilityPlugin5

    Property AutoStart As Boolean

    Property Hidden As Boolean

End Interface