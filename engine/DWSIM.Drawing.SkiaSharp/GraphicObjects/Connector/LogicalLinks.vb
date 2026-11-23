Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point

Namespace GraphicObjects

    ''' <summary>
    ''' The lines from the logical blocks (controllers, adjusts, specifications, gauges, inputs and
    ''' switches) to the objects they read or move. A line leaves the side of the block that faces the
    ''' object and reaches the middle of the side of the object that faces the block, in one or two
    ''' bends; when that path would cross the icon of a third object, it goes around it with the same
    ''' router as the connectors. The points are in flowsheet coordinates: draw them on a canvas
    ''' without the block's own rotation, flip and zoom (UnTransformCanvas).
    ''' </summary>
    Public Module LogicalLinks

        Private ReadOnly _cache As New Dictionary(Of String, KeyValuePair(Of Long, List(Of Point)))
        Private ReadOnly _cacheLock As New Object

        ''' <summary>Draws the line from <paramref name="block"/> to <paramref name="target"/>.</summary>
        Public Sub Draw(canvas As Global.SkiaSharp.SKCanvas, block As IGraphicObject, target As IGraphicObject, paint As Global.SkiaSharp.SKPaint)
            If block Is Nothing OrElse target Is Nothing Then Exit Sub
            Dim pts = PathTo(block, target)
            If pts.Count < 2 Then Exit Sub
            Using path As New Global.SkiaSharp.SKPath()
                path.MoveTo(CSng(pts(0).X), CSng(pts(0).Y))
                For i = 1 To pts.Count - 1
                    path.LineTo(CSng(pts(i).X), CSng(pts(i).Y))
                Next
                canvas.DrawPath(path, paint)
            End Using
        End Sub

        ''' <summary>The points of the line from <paramref name="block"/> to <paramref name="target"/>, in flowsheet coordinates.</summary>
        Public Function PathTo(block As IGraphicObject, target As IGraphicObject) As List(Of Point)

            Dim b = ConnectorGraphic.IconBox(block), t = ConnectorGraphic.IconBox(target)
            Dim bcx = (b.Left + b.Right) / 2, bcy = (b.Top + b.Bottom) / 2
            Dim tcx = (t.Left + t.Right) / 2, tcy = (t.Top + t.Bottom) / 2

            ' the gaps between the two icons along each axis (negative when they overlap on it)
            Dim gapX = Math.Max(t.Left - b.Right, b.Left - t.Right)
            Dim gapY = Math.Max(t.Top - b.Bottom, b.Top - t.Bottom)

            Dim s, e As Point
            Dim sd, ed As ConDir
            Dim pts As New List(Of Point)

            If gapY >= 0 AndAlso (gapX < 0 OrElse gapY >= gapX) Then
                ' one above the other: from the facing edges, bending at mid height
                Dim down = tcy > bcy
                sd = If(down, ConDir.Down, ConDir.Up) : ed = sd
                s = New Point(CSng(bcx), CSng(If(down, b.Bottom, b.Top)))
                e = New Point(CSng(tcx), CSng(If(down, t.Top, t.Bottom)))
                Dim my = (s.Y + e.Y) / 2
                pts.Add(s)
                If Math.Abs(s.X - e.X) > 0.5 Then
                    pts.Add(New Point(s.X, CSng(my)))
                    pts.Add(New Point(e.X, CSng(my)))
                End If
                pts.Add(e)
            ElseIf gapX >= 0 Then
                ' side by side: from the facing sides, bending half way
                Dim right = tcx > bcx
                sd = If(right, ConDir.Right, ConDir.Left) : ed = sd
                s = New Point(CSng(If(right, b.Right, b.Left)), CSng(bcy))
                e = New Point(CSng(If(right, t.Left, t.Right)), CSng(tcy))
                Dim mx = (s.X + e.X) / 2
                pts.Add(s)
                If Math.Abs(s.Y - e.Y) > 0.5 Then
                    pts.Add(New Point(CSng(mx), s.Y))
                    pts.Add(New Point(CSng(mx), e.Y))
                End If
                pts.Add(e)
            Else
                ' the icons overlap (a column's box takes in its condenser and reboiler): across from the
                ' centre of the block to the axis of the object, then to its edge on the block's side
                Dim ey = If(bcy > tcy, Math.Min(t.Bottom, bcy), Math.Max(t.Top, bcy))
                pts.Add(New Point(CSng(bcx), CSng(bcy)))
                If Math.Abs(bcx - tcx) > 0.5 Then pts.Add(New Point(CSng(tcx), CSng(bcy)))
                If Math.Abs(ey - bcy) > 0.5 Then pts.Add(New Point(CSng(tcx), CSng(ey)))
                Return pts
            End If

            If Not ConnectorGraphic.AvoidIcons Then Return pts

            ' around the icons of the other objects
            Dim fs As IFlowsheet = Nothing
            Try
                fs = block.Flowsheet
                If fs Is Nothing OrElse fs.GraphicObjects Is Nothing OrElse Not fs.GraphicObjects.ContainsKey(block.Name) Then Return pts
            Catch
                Return pts
            End Try

            Dim pad = ConnectorGraphic.RouteSearchPad
            Dim area As New ConnectorGraphic.Box(Math.Min(s.X, e.X) - pad, Math.Min(s.Y, e.Y) - pad, Math.Max(s.X, e.X) + pad, Math.Max(s.Y, e.Y) + pad)
            Dim others As New List(Of ConnectorGraphic.Box)
            Dim own As New List(Of ConnectorGraphic.Box) From {b, t}
            For Each kv In ConnectorGraphic.Snapshot(fs)
                If kv.Key Is block OrElse kv.Key Is target Then Continue For
                If kv.Value.Inflated(ConnectorGraphic.RouteMargin).Overlaps(area) Then others.Add(kv.Value)
            Next
            If others.Count = 0 OrElse Not ConnectorGraphic.PathHits(pts, others, 1) Then Return pts

            Dim name = block.Name & "|" & target.Name
            Dim key = ConnectorGraphic.RouteKey(s, sd, e, ed, others, own)
            SyncLock _cacheLock
                Dim known As KeyValuePair(Of Long, List(Of Point)) = Nothing
                If _cache.TryGetValue(name, known) AndAlso known.Key = key Then
                    Return If(known.Value, pts)
                End If
            End SyncLock
            Dim route = ConnectorGraphic.FindRoute(s, sd, e, ed, others, own, area)
            SyncLock _cacheLock
                If _cache.Count > 4000 Then _cache.Clear()
                _cache(name) = New KeyValuePair(Of Long, List(Of Point))(key, route)
            End SyncLock
            Return If(route, pts)

        End Function

    End Module

End Namespace
