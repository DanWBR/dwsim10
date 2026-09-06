Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Interfaces.Enums.GraphicObjects

Namespace GraphicObjects.Shapes

    ''' <summary>
    ''' Graphic object for the free-radical polymerization reactor. It reuses the stirred-tank geometry,
    ''' connectors and icon of the CSTR (it is a mixed vessel) and only overrides the object type so the
    ''' flowsheet factory, palette and paste path route to the polymerization unit operation.
    ''' </summary>
    Public Class PolymerizationGraphic

        Inherits CSTRGraphic

        Private Sub Retype()
            Me.ObjectType = ObjectType.RCT_Polymerization
            Me.Description = "Polymerization Reactor"
        End Sub

        Public Sub New()
            MyBase.New()
            Retype()
        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint)
            MyBase.New(graphicPosition)
            Retype()
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer)
            MyBase.New(posX, posY)
            Retype()
        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint, ByVal graphicSize As SKSize)
            MyBase.New(graphicPosition, graphicSize)
            Retype()
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer, ByVal graphicSize As SKSize)
            MyBase.New(posX, posY, graphicSize)
            Retype()
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer, ByVal width As Integer, ByVal height As Integer)
            MyBase.New(posX, posY, width, height)
            Retype()
        End Sub

    End Class

End Namespace
