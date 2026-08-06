Imports DWSIM.Interfaces

''' <summary>
''' Where the control panel mode editors for Input and PID objects come from. The dialogs
''' themselves belong to the UI host, which assigns the delegates at startup; with nothing
''' assigned, double-clicking one of these objects in control panel mode does nothing.
''' </summary>
Public Class GraphicObjectControlPanelModeEditors

    ''' <summary>Wires the editor of an Input object. Set by the UI host.</summary>
    Public Shared Property InputEditor As Action(Of IGraphicObject, ISimulationObject)

    ''' <summary>Wires the editor of a PID controller. Set by the UI host.</summary>
    Public Shared Property PIDEditor As Action(Of IGraphicObject, ISimulationObject)

    Public Shared Sub SetInputDelegate(gobj As IGraphicObject, myObj As ISimulationObject)

        InputEditor?.Invoke(gobj, myObj)

    End Sub

    Public Shared Sub SetPIDDelegate(gobj As IGraphicObject, myObj As ISimulationObject)

        PIDEditor?.Invoke(gobj, myObj)

    End Sub

End Class
