Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Interfaces

Namespace GraphicObjects.Shapes

    Public Class ExternalUnitOperationGraphic

        Inherits ShapeGraphic

#Region "Constructors"

        Public Sub New()

            Me.ObjectType = ObjectType.External

            'Me.Description = TryCast(Owner, IExternalUnitOperation)?.Description

        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint)
            Me.New()
            Me.SetPosition(graphicPosition)
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer)
            Me.New(New SKPoint(posX, posY))
        End Sub

        Public Sub New(ByVal graphicPosition As SKPoint, ByVal graphicSize As SKSize)
            Me.New(graphicPosition)
            Me.SetSize(graphicSize)
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer, ByVal graphicSize As SKSize)
            Me.New(New SKPoint(posX, posY), graphicSize)
        End Sub

        Public Sub New(ByVal posX As Integer, ByVal posY As Integer, ByVal width As Integer, ByVal height As Integer)
            Me.New(New SKPoint(posX, posY), New SKSize(width, height))
        End Sub

        Public Overrides Function LoadData(data As List(Of XElement)) As Boolean
            Return XMLSerializer.XMLSerializer.Deserialize(Me, data)
        End Function

        Public Overrides Function SaveData() As List(Of XElement)
            Dim data = XMLSerializer.XMLSerializer.Serialize(Me)
            data.AddRange(MyBase.SaveData)
            Return data
        End Function

#End Region

        ''' <summary>
        ''' Adds ports until the graphic has as many inlets and outlets as the saved element <paramref name="xel"/>.
        ''' On load the ports are built by the palette instance of the unit, which has its default configuration;
        ''' a unit saved with more feeds or products needs room for every saved port before the connections are
        ''' restored, and every saved connection needs the port type (material or energy) it was saved with. The
        ''' unit trims or retypes the spare ports the next time it builds its ports.
        ''' </summary>
        Public Shared Sub PadSavedConnectors(obj As IGraphicObject, xel As XElement)
            Dim pos As New Point(obj.X, obj.Y)
            PadConnectors(obj.InputConnectors, xel.Element("InputConnectors"), ConType.ConIn, pos)
            PadConnectors(obj.OutputConnectors, xel.Element("OutputConnectors"), ConType.ConOut, pos)
        End Sub

        Private Shared Sub PadConnectors(ports As List(Of IConnectionPoint), saved As XElement, materialType As ConType, pos As Point)
            If saved Is Nothing Then Return
            Dim items = saved.Elements.ToList()
            While ports.Count < items.Count
                ports.Add(New ConnectionPoint With {.Type = materialType, .Position = New Point(pos.X, pos.Y)})
            End While
            'a port the default layout has as an energy port may hold a material stream in the saved one, and
            'the other way round: every saved connection keeps the type it was saved with
            For i = 0 To items.Count - 1
                Dim st = items(i).@ConnType
                Dim t As ConType
                If st IsNot Nothing AndAlso [Enum].TryParse(st, t) Then
                    ports(i).Type = t
                    ports(i).IsEnergyConnector = (t = ConType.ConEn)
                End If
            Next
        End Sub

        Public Overrides Sub CreateConnectors(InCount As Integer, OutCount As Integer)

            TryCast(Owner, IExternalUnitOperation)?.CreateConnectors()

        End Sub

        Public Overrides Sub Draw(ByVal g As Object)

            Me.Description = TryCast(Owner, IExternalUnitOperation)?.Description

            Me.AdditionalInfo = TryCast(Owner, IExternalUnitOperation)?.Name

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            CreateConnectors(0, 0)
            UpdateStatus()

            MyBase.Draw(g)

            TryCast(Owner, IExternalUnitOperation)?.Draw(g)

        End Sub

        Public Overrides Sub PositionConnectors()

            CreateConnectors(0, 0)

        End Sub

    End Class

End Namespace
