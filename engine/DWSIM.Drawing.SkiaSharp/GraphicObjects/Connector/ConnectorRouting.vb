Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point

Namespace GraphicObjects

    ''' <summary>
    ''' Keeps the orthogonal path of a connector off the icons of the other objects on the flowsheet
    ''' (streams, unit operations, logical blocks). The path from the direction templates is kept when
    ''' it clears them; when it runs through one, the connector takes the shortest orthogonal route
    ''' with the fewest bends around them, found on the grid of the obstacle edges near the two ends.
    ''' The route is kept until the ends or an obstacle near them move.
    ''' </summary>
    Partial Class ConnectorGraphic

        ''' <summary>Whether the connectors go around the icons of the other objects (on by default).</summary>
        Public Shared Property AvoidIcons As Boolean = True

        Private _routeKey As Long
        Private _routeKnown As Boolean
        Private _route As List(Of Point)

        ''' <summary>Clearance kept around an icon, px.</summary>
        Friend Const RouteMargin As Double = 6
        ''' <summary>Straight run out of a port before the first bend, px.</summary>
        Private Const RouteStub As Double = 12
        ''' <summary>Cost of a bend, as px of length.</summary>
        Private Const RouteBendCost As Double = 30
        ''' <summary>How far beyond the two ends the route may go, px.</summary>
        Friend Const RouteSearchPad As Double = 160

        Friend Structure Box
            Public Left, Top, Right, Bottom As Double
            Public Sub New(l As Double, t As Double, r As Double, b As Double)
                Left = l : Top = t : Right = r : Bottom = b
            End Sub
            Public Function Inflated(m As Double) As Box
                Return New Box(Left - m, Top - m, Right + m, Bottom + m)
            End Function
            Public Function Contains(x As Double, y As Double) As Boolean
                Return x > Left AndAlso x < Right AndAlso y > Top AndAlso y < Bottom
            End Function
            Public Function Overlaps(o As Box) As Boolean
                Return Left < o.Right AndAlso o.Left < Right AndAlso Top < o.Bottom AndAlso o.Top < Bottom
            End Function
        End Structure

        ' the icons of the flowsheet, read once per short interval for all its connectors
        Private Shared _snapshotOwner As IFlowsheet
        Private Shared _snapshotTick As Integer
        Private Shared _snapshot As New List(Of KeyValuePair(Of IGraphicObject, Box))
        Private Shared ReadOnly _snapshotLock As New Object

        Friend Shared Function IconBox(go As IGraphicObject) As Box
            Dim w As Double = go.Width, h As Double = go.Height
            Dim r = ((go.Rotation Mod 360) + 360) Mod 360
            If (r >= 45 AndAlso r < 135) OrElse (r >= 225 AndAlso r < 315) Then
                Dim cx = go.X + w / 2, cy = go.Y + h / 2
                Return New Box(cx - h / 2, cy - w / 2, cx + h / 2, cy + w / 2)
            End If
            Return New Box(go.X, go.Y, go.X + w, go.Y + h)
        End Function

        Private Shared Function IsObstacle(go As IGraphicObject) As Boolean
            If go Is Nothing OrElse TypeOf go Is IConnectorGraphicObject Then Return False
            If go.Width <= 0 OrElse go.Height <= 0 Then Return False
            Dim t = go.ObjectType
            If t = ObjectType.Nenhum Then Return False
            ' decorations: frames, texts, tables, charts, pictures
            Return Not t.ToString().StartsWith("GO_")
        End Function

        Friend Shared Function Snapshot(fs As IFlowsheet) As List(Of KeyValuePair(Of IGraphicObject, Box))
            SyncLock _snapshotLock
                Dim now = Environment.TickCount
                If fs IsNot _snapshotOwner OrElse Math.Abs(now - _snapshotTick) > 40 Then
                    Dim list As New List(Of KeyValuePair(Of IGraphicObject, Box))
                    For Each go In fs.GraphicObjects.Values.ToArray()
                        If IsObstacle(go) Then list.Add(New KeyValuePair(Of IGraphicObject, Box)(go, IconBox(go)))
                    Next
                    _snapshot = list
                    _snapshotOwner = fs
                    _snapshotTick = now
                End If
                Return _snapshot
            End SyncLock
        End Function

        ''' <summary>Called at the end of SetupPositioning with the ports and directions it used.</summary>
        Private Sub AvoidObstacles(startPos As Point, startDir As ConDir, endPos As Point, endDir As ConDir)

            If Not AvoidIcons Then Return

            Dim fs As IFlowsheet = Nothing
            Try
                fs = AttachedFrom?.Flowsheet
                ' only on the flowsheet that holds the objects (other diagrams reuse the connector)
                If fs Is Nothing OrElse fs.GraphicObjects Is Nothing OrElse Not fs.GraphicObjects.ContainsKey(AttachedFrom.Name) Then Return
            Catch
                Return
            End Try

            Dim area As New Box(Math.Min(startPos.X, endPos.X) - RouteSearchPad, Math.Min(startPos.Y, endPos.Y) - RouteSearchPad,
                                Math.Max(startPos.X, endPos.X) + RouteSearchPad, Math.Max(startPos.Y, endPos.Y) + RouteSearchPad)

            Dim others As New List(Of Box)
            Dim own As New List(Of Box)
            For Each kv In Snapshot(fs)
                If kv.Key Is AttachedFrom OrElse kv.Key Is AttachedTo Then
                    own.Add(kv.Value)
                ElseIf kv.Value.Inflated(RouteMargin).Overlaps(area) Then
                    others.Add(kv.Value)
                End If
            Next
            If others.Count = 0 Then
                _routeKnown = False
                Return
            End If

            ' the path from the templates stays when it clears the other icons
            If Not PathHits(PointList, others, 1) Then
                _routeKnown = False
                Return
            End If

            Dim key = RouteKey(startPos, startDir, endPos, endDir, others, own)
            If Not _routeKnown OrElse key <> _routeKey Then
                _routeKey = key
                _routeKnown = True
                _route = FindRoute(startPos, startDir, endPos, endDir, others, own, area)
            End If
            If _route IsNot Nothing Then PointList = _route.Select(Function(p) New Point(CSng(p.X), CSng(p.Y))).ToList()

        End Sub

        Friend Shared Function RouteKey(s As Point, sd As ConDir, e As Point, ed As ConDir, others As List(Of Box), own As List(Of Box)) As Long
            Dim h As Long = 17
            ' kept below 2^31 before each step, so the product can't overflow
            Dim Add = Sub(v As Double)
                          h = (h * 31 + CLng(Math.Round(v))) Mod 2147483647L
                      End Sub
            Add(s.X) : Add(s.Y) : Add(CDbl(sd)) : Add(e.X) : Add(e.Y) : Add(CDbl(ed))
            For Each b In others.Concat(own)
                Add(b.Left) : Add(b.Top) : Add(b.Right) : Add(b.Bottom)
            Next
            Return h
        End Function

        ''' <summary>Whether a path runs through one of the boxes (shrunk by <paramref name="slack"/> px).</summary>
        Friend Shared Function PathHits(path As List(Of Point), boxes As List(Of Box), slack As Double) As Boolean
            For i = 1 To path.Count - 1
                Dim a = path(i - 1), b = path(i)
                For Each bx In boxes
                    Dim r = bx.Inflated(-slack)
                    If Math.Abs(a.Y - b.Y) < 0.5 Then
                        If a.Y > r.Top AndAlso a.Y < r.Bottom AndAlso Math.Max(a.X, b.X) > r.Left AndAlso Math.Min(a.X, b.X) < r.Right Then Return True
                    ElseIf Math.Abs(a.X - b.X) < 0.5 Then
                        If a.X > r.Left AndAlso a.X < r.Right AndAlso Math.Max(a.Y, b.Y) > r.Top AndAlso Math.Min(a.Y, b.Y) < r.Bottom Then Return True
                    Else
                        For k = 1 To 19
                            Dim t = k / 20.0
                            If r.Contains(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y)) Then Return True
                        Next
                    End If
                Next
            Next
            Return False
        End Function

        Private Shared Function DirStep(d As ConDir) As (dx As Integer, dy As Integer)
            Select Case d
                Case ConDir.Right : Return (1, 0)
                Case ConDir.Left : Return (-1, 0)
                Case ConDir.Down : Return (0, 1)
                Case Else : Return (0, -1)
            End Select
        End Function

        ''' <summary>
        ''' The cheapest orthogonal route (length plus a cost per bend) from the start stub to the end
        ''' stub on the grid of the obstacle edges, or Nothing when there isn't one inside the area.
        ''' </summary>
        Friend Shared Function FindRoute(s As Point, sd As ConDir, e As Point, ed As ConDir,
                                          others As List(Of Box), own As List(Of Box), area As Box) As List(Of Point)

            Dim sv = DirStep(sd), ev = DirStep(ed)
            ' the line leaves the start port along its direction and arrives at the end port along the end direction
            Dim s1x = s.X + sv.dx * RouteStub, s1y = s.Y + sv.dy * RouteStub
            Dim e1x = e.X - ev.dx * RouteStub, e1y = e.Y - ev.dy * RouteStub

            Dim blocks As New List(Of Box)
            For Each b In others
                blocks.Add(b.Inflated(RouteMargin))
            Next
            ' the two own icons too, unless a stub lies inside one (a port within the icon)
            For Each b In own
                Dim r = b.Inflated(2)
                If Not r.Contains(s1x, s1y) AndAlso Not r.Contains(e1x, e1y) Then blocks.Add(r)
            Next

            Dim xs As New SortedSet(Of Double) From {s1x, e1x, area.Left, area.Right}
            Dim ys As New SortedSet(Of Double) From {s1y, e1y, area.Top, area.Bottom}
            For Each b In blocks
                For Each v In {b.Left, b.Right}
                    If v > area.Left AndAlso v < area.Right Then xs.Add(Math.Round(v))
                Next
                For Each v In {b.Top, b.Bottom}
                    If v > area.Top AndAlso v < area.Bottom Then ys.Add(Math.Round(v))
                Next
            Next
            Dim gx = xs.ToArray(), gy = ys.ToArray()
            Dim nx = gx.Length, ny = gy.Length
            If nx * ny > 40000 Then Return Nothing

            Dim si = Array.IndexOf(gx, s1x), sj = Array.IndexOf(gy, s1y)
            Dim ei = Array.IndexOf(gx, e1x), ej = Array.IndexOf(gy, e1y)
            If si < 0 OrElse sj < 0 OrElse ei < 0 OrElse ej < 0 Then Return Nothing

            Dim Free = Function(x As Double, y As Double) As Boolean
                           For Each b In blocks
                               If b.Contains(x, y) Then Return False
                           Next
                           Return True
                       End Function

            ' directions: 0 right, 1 left, 2 down, 3 up
            Dim dxs = {1, -1, 0, 0}, dys = {0, 0, 1, -1}
            Dim DirIndex = Function(d As ConDir) As Integer
                               Select Case d
                                   Case ConDir.Right : Return 0
                                   Case ConDir.Left : Return 1
                                   Case ConDir.Down : Return 2
                                   Case Else : Return 3
                               End Select
                           End Function

            Dim n = nx * ny * 4
            Dim cost(n - 1) As Double
            Dim prev(n - 1) As Integer
            For k = 0 To n - 1 : cost(k) = Double.MaxValue : prev(k) = -1 : Next
            Dim State = Function(i As Integer, j As Integer, d As Integer) (j * nx + i) * 4 + d

            Dim heap As New List(Of (f As Double, st As Integer))
            Dim Push = Sub(f As Double, st As Integer)
                           heap.Add((f, st))
                           Dim c = heap.Count - 1
                           While c > 0
                               Dim p = (c - 1) \ 2
                               If heap(p).f <= heap(c).f Then Exit While
                               Dim t = heap(p) : heap(p) = heap(c) : heap(c) = t
                               c = p
                           End While
                       End Sub
            Dim Pop = Function() As Integer
                          Dim top = heap(0).st
                          heap(0) = heap(heap.Count - 1)
                          heap.RemoveAt(heap.Count - 1)
                          Dim c = 0
                          Do
                              Dim l = 2 * c + 1, r = l + 1, m = c
                              If l < heap.Count AndAlso heap(l).f < heap(m).f Then m = l
                              If r < heap.Count AndAlso heap(r).f < heap(m).f Then m = r
                              If m = c Then Exit Do
                              Dim t = heap(m) : heap(m) = heap(c) : heap(c) = t
                              c = m
                          Loop
                          Return top
                      End Function
            Dim H = Function(i As Integer, j As Integer) Math.Abs(gx(i) - gx(ei)) + Math.Abs(gy(j) - gy(ej))

            Dim startState = State(si, sj, DirIndex(sd))
            cost(startState) = 0
            Push(H(si, sj), startState)
            Dim goalDir = DirIndex(ed)
            Dim best = -1, bestCost = Double.MaxValue

            While heap.Count > 0
                Dim st = Pop()
                Dim d = st Mod 4, cell = st \ 4
                Dim i = cell Mod nx, j = cell \ nx
                Dim c = cost(st)
                If c >= bestCost Then Exit While
                If i = ei AndAlso j = ej Then
                    Dim total = c + If(d = goalDir, 0, RouteBendCost)
                    If total < bestCost Then bestCost = total : best = st
                    Continue While
                End If
                For nd = 0 To 3
                    ' never straight back
                    If dxs(nd) = -dxs(d) AndAlso dys(nd) = -dys(d) AndAlso (dxs(nd) <> 0 OrElse dys(nd) <> 0) Then Continue For
                    Dim ni = i + dxs(nd), nj = j + dys(nd)
                    If ni < 0 OrElse nj < 0 OrElse ni >= nx OrElse nj >= ny Then Continue For
                    Dim mx = (gx(i) + gx(ni)) / 2, my = (gy(j) + gy(nj)) / 2
                    If Not Free(mx, my) Then Continue For
                    If Not (ni = ei AndAlso nj = ej) AndAlso Not Free(gx(ni), gy(nj)) Then Continue For
                    Dim nc = c + Math.Abs(gx(ni) - gx(i)) + Math.Abs(gy(nj) - gy(j)) + If(nd = d, 0, RouteBendCost)
                    Dim ns = State(ni, nj, nd)
                    If nc < cost(ns) Then
                        cost(ns) = nc
                        prev(ns) = st
                        Push(nc + H(ni, nj), ns)
                    End If
                Next
            End While

            If best < 0 Then Return Nothing

            Dim cells As New List(Of Point)
            Dim cur = best
            While cur >= 0
                Dim cl = cur \ 4
                cells.Add(New Point(CSng(gx(cl Mod nx)), CSng(gy(cl \ nx))))
                cur = prev(cur)
            End While
            cells.Reverse()

            Dim pts As New List(Of Point)
            pts.Add(New Point(CSng(s.X), CSng(s.Y)))
            pts.AddRange(cells)
            pts.Add(New Point(CSng(e.X), CSng(e.Y)))
            Return Simplified(pts)

        End Function

        ''' <summary>The path without repeated points and without the middle points of straight runs.</summary>
        Private Shared Function Simplified(pts As List(Of Point)) As List(Of Point)
            Dim res As New List(Of Point)
            For Each p In pts
                If res.Count > 0 AndAlso Math.Abs(res.Last.X - p.X) < 0.5 AndAlso Math.Abs(res.Last.Y - p.Y) < 0.5 Then Continue For
                If res.Count >= 2 Then
                    Dim a = res(res.Count - 2), b = res(res.Count - 1)
                    Dim straightX = Math.Abs(a.X - b.X) < 0.5 AndAlso Math.Abs(b.X - p.X) < 0.5
                    Dim straightY = Math.Abs(a.Y - b.Y) < 0.5 AndAlso Math.Abs(b.Y - p.Y) < 0.5
                    If straightX OrElse straightY Then res.RemoveAt(res.Count - 1)
                End If
                res.Add(p)
            Next
            Return res
        End Function

    End Class

End Namespace
