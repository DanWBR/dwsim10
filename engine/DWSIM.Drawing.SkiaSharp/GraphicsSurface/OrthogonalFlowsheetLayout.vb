Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums.GraphicObjects

''' <summary>
''' Layered, left-to-right layout of a flowsheet with the streams lined up on the ports they connect, so
''' the connectors (drawn orthogonally by the surface) come out as straight lines wherever the topology
''' allows. Feeds on the left, products on the right; recycle returns run back as the only right-to-left
''' links. Steps: cycles broken at the recycle blocks (then at any remaining back edge), columns by the
''' longest path from the feeds with feeds pulled next to the unit they enter, dummy points on links that
''' skip columns, crossings reduced by barycentre sweeps that know where each port sits on its object,
''' then the vertical positions by repeated weighted-median targets solved per column with order and
''' spacing constraints (pool-adjacent-violators), which puts a stream exactly on its port whenever the
''' neighbours allow it. Energy streams with one free end, controllers, specs, adjusts, gauges and inputs
''' sit beside the object they belong to; tables, text and images move below the drawing as a group.
''' Optionally the columns are wrapped into rows (every row read left to right) to keep the drawing's
''' width between minAspect and 1/minAspect times its height with the smallest area.
''' </summary>
Public Class OrthogonalFlowsheetLayout

    Private Class LNode
        Public G As IGraphicObject
        Public Layer As Integer
        Public Pos As Double
        Public Y As Double
        Public H As Double = 20
        Public W As Double = 20
        Public Succ As New List(Of LEdge)
        Public Pred As New List(Of LEdge)
        Public Index As Integer
        Public Row As Integer
        Public ReadOnly Property IsDummy As Boolean
            Get
                Return G Is Nothing
            End Get
        End Property
    End Class

    Private Class LEdge
        Public A As LNode, B As LNode
        Public OffA As Double, OffB As Double
        Public Weight As Double = 1.0
    End Class

    Private Class Link
        Public From As IGraphicObject, [To] As IGraphicObject
        Public OffFrom As Double, OffTo As Double
        Public OffFromX As Double, OffToX As Double
    End Class

    Public Const ColumnGap As Double = 70
    Public Const RowGap As Double = 45
    Public Const WrapGap As Double = 110

    Private Shared Function IsDecorative(o As IGraphicObject) As Boolean
        Select Case o.ObjectType
            Case ObjectType.GO_Table, ObjectType.GO_Text, ObjectType.GO_Image, ObjectType.GO_FloatingTable,
                 ObjectType.GO_Animation, ObjectType.GO_MasterTable, ObjectType.GO_SpreadsheetTable,
                 ObjectType.GO_Rectangle, ObjectType.GO_Chart, ObjectType.GO_InputControl,
                 ObjectType.GO_HTMLText, ObjectType.GO_Button, ObjectType.Nenhum
                Return True
        End Select
        Return False
    End Function

    Private Shared Function IsSatelliteType(o As IGraphicObject) As Boolean
        Select Case o.ObjectType
            Case ObjectType.OT_Adjust, ObjectType.OT_Spec, ObjectType.Controller_PID, ObjectType.Controller_Python,
                 ObjectType.Controller_MPC, ObjectType.AnalogGauge, ObjectType.DigitalGauge, ObjectType.LevelGauge,
                 ObjectType.Switch, ObjectType.Input
                Return True
        End Select
        Return False
    End Function

    Private Shared Function Center(o As IGraphicObject) As Double()
        Return New Double() {o.X + o.Width / 2.0, o.Y + o.Height / 2.0}
    End Function

    ''' <summary>The object a controller, spec, adjust, gauge or input is about, when it can be found.</summary>
    Private Shared Function SatelliteTarget(o As IGraphicObject, byName As Dictionary(Of String, IGraphicObject)) As IGraphicObject
        For Each pname In {"ConnectedToCv", "ConnectedToMv", "ConnectedToSv", "ConnectedToTv"}
            Try
                Dim p = o.GetType().GetProperty(pname)
                If p IsNot Nothing Then
                    Dim t = TryCast(p.GetValue(o), IGraphicObject)
                    If t IsNot Nothing AndAlso byName.ContainsKey(t.Name) Then Return byName(t.Name)
                End If
            Catch
            End Try
        Next
        Try
            If o.Owner IsNot Nothing Then
                Dim p = o.Owner.GetType().GetProperty("SelectedObjectID")
                If p IsNot Nothing Then
                    Dim id = TryCast(p.GetValue(o.Owner), String)
                    If Not String.IsNullOrEmpty(id) AndAlso byName.ContainsKey(id) Then Return byName(id)
                End If
            End If
        Catch
        End Try
        Return Nothing
    End Function

    ''' <summary>Vertical offset of a port from the centre of its object (unrotated, unmirrored object).</summary>
    Private Shared Function PortOffset(o As IGraphicObject, cp As IConnectionPoint) As Double
        If cp Is Nothing OrElse cp.Position Is Nothing Then Return 0.0
        Return cp.Position.Y - (o.Y + o.Height / 2.0)
    End Function

    Private Shared Function PortOffsetX(o As IGraphicObject, cp As IConnectionPoint) As Double
        If cp Is Nothing OrElse cp.Position Is Nothing Then Return 0.0
        Return cp.Position.X - (o.X + o.Width / 2.0)
    End Function

    Private Shared Function ReadLinks(objects As IEnumerable(Of IGraphicObject)) As List(Of Link)
        Dim links As New List(Of Link)
        For Each obj In objects
            Dim con = TryCast(obj, IConnectorGraphicObject)
            If con Is Nothing OrElse con.AttachedFrom Is Nothing OrElse con.AttachedTo Is Nothing Then Continue For
            Dim f = con.AttachedFrom, t = con.AttachedTo
            Dim cpf As IConnectionPoint = Nothing, cpt As IConnectionPoint = Nothing
            Try
                If con.AttachedFromEnergy OrElse con.AttachedFromConnectorIndex < 0 Then
                    cpf = f.EnergyConnector
                ElseIf con.AttachedFromInput Then
                    If con.AttachedFromConnectorIndex < f.InputConnectors.Count Then cpf = f.InputConnectors(con.AttachedFromConnectorIndex)
                ElseIf con.AttachedFromConnectorIndex < f.OutputConnectors.Count Then
                    cpf = f.OutputConnectors(con.AttachedFromConnectorIndex)
                End If
                If con.AttachedToEnergy OrElse con.AttachedToConnectorIndex < 0 Then
                    cpt = t.EnergyConnector
                ElseIf con.AttachedToOutput Then
                    If con.AttachedToConnectorIndex < t.OutputConnectors.Count Then cpt = t.OutputConnectors(con.AttachedToConnectorIndex)
                ElseIf con.AttachedToConnectorIndex < t.InputConnectors.Count Then
                    cpt = t.InputConnectors(con.AttachedToConnectorIndex)
                End If
            Catch
            End Try
            links.Add(New Link With {.From = f, .[To] = t, .OffFrom = PortOffset(f, cpf), .OffTo = PortOffset(t, cpt), .OffFromX = PortOffsetX(f, cpf), .OffToX = PortOffsetX(t, cpt)})
        Next
        Return links
    End Function

    ''' <summary>Lays out the flowsheet objects. Returns the width and height of the drawing in pixels.</summary>
    Public Shared Function Arrange(drawingObjects As IList(Of IGraphicObject), Optional wrapRows As Boolean = False,
                                   Optional minAspect As Double = 0.5, Optional originX As Double = 60,
                                   Optional originY As Double = 60) As Double()

        Dim objs = drawingObjects.Where(Function(o) Not o.IsConnector).ToList()
        Dim byName As New Dictionary(Of String, IGraphicObject)
        For Each o In objs
            If Not byName.ContainsKey(o.Name) Then byName.Add(o.Name, o)
        Next
        Dim deco = objs.Where(Function(o) IsDecorative(o)).ToList()
        Dim active = objs.Where(Function(o) Not IsDecorative(o)).ToList()
        If active.Count = 0 Then Return New Double() {0, 0}
        Dim oldL As Double = active.Min(Function(o) o.X), oldT As Double = active.Min(Function(o) o.Y)
        Dim oldR As Double = active.Max(Function(o) o.X + o.Width), oldB As Double = active.Max(Function(o) o.Y + o.Height)
        ' a rectangle drawn around a group of objects follows the group
        Dim frames As New Dictionary(Of IGraphicObject, List(Of IGraphicObject))
        For Each d In deco.Where(Function(o) o.ObjectType = ObjectType.GO_Rectangle)
            Dim inside = active.Where(Function(o) o.X + o.Width / 2.0 >= d.X AndAlso o.X + o.Width / 2.0 <= d.X + d.Width AndAlso
                                                  o.Y + o.Height / 2.0 >= d.Y AndAlso o.Y + o.Height / 2.0 <= d.Y + d.Height).ToList()
            If inside.Count > 0 Then frames(d) = inside
        Next

        ' every object starts unmirrored and unrotated, so its ports are where the layout expects them
        For Each o In active
            o.FlippedH = False : o.FlippedV = False : o.Rotation = 0
            Try
                o.PositionConnectors()
            Catch
            End Try
        Next

        Dim links = ReadLinks(drawingObjects).Where(Function(l) byName.ContainsKey(l.From.Name) AndAlso byName.ContainsKey(l.[To].Name)).ToList()

        ' satellites: controllers & co., and energy streams with one free end
        Dim satellites As New Dictionary(Of IGraphicObject, IGraphicObject)
        For Each o In active
            If IsSatelliteType(o) Then
                Dim t = SatelliteTarget(o, byName)
                If t IsNot Nothing Then satellites(o) = t
            ElseIf o.ObjectType = ObjectType.EnergyStream Then
                Dim ls = links.Where(Function(l) l.From Is o OrElse l.[To] Is o).ToList()
                If ls.Count = 1 Then satellites(o) = If(ls(0).From Is o, ls(0).[To], ls(0).From)
            End If
        Next

        ' the layered part: every other object with a link
        Dim nodes As New Dictionary(Of IGraphicObject, LNode)
        For Each o In active
            If satellites.ContainsKey(o) OrElse IsSatelliteType(o) Then Continue For
            nodes(o) = New LNode With {.G = o, .W = Math.Max(o.Width, 10), .H = Math.Max(o.Height, 10)}
        Next
        For Each l In links
            If Not nodes.ContainsKey(l.From) OrElse Not nodes.ContainsKey(l.[To]) OrElse l.From Is l.[To] Then Continue For
            Dim e = New LEdge With {.A = nodes(l.From), .B = nodes(l.[To]), .OffA = l.OffFrom, .OffB = l.OffTo}
            Dim isStream = Function(g As IGraphicObject) g.ObjectType = ObjectType.MaterialStream OrElse g.ObjectType = ObjectType.EnergyStream
            e.Weight = If(isStream(l.From) OrElse isStream(l.[To]), 2.0, 1.0)
            e.A.Succ.Add(e) : e.B.Pred.Add(e)
        Next
        Dim orphans = nodes.Values.Where(Function(n) n.Succ.Count = 0 AndAlso n.Pred.Count = 0).Select(Function(n) n.G).ToList()
        For Each g In orphans : nodes.Remove(g) : Next
        Dim unplacedSat = active.Where(Function(o) IsSatelliteType(o) AndAlso Not satellites.ContainsKey(o)).ToList()
        orphans.AddRange(unplacedSat)

        ' connected components, laid out one below the other
        Dim bands As New List(Of List(Of LNode))
        Dim seen As New HashSet(Of LNode)
        For Each n In nodes.Values.OrderBy(Function(v) v.G.X).ThenBy(Function(v) v.G.Y)
            If seen.Contains(n) Then Continue For
            Dim comp As New List(Of LNode)
            Dim st As New Stack(Of LNode) : st.Push(n) : seen.Add(n)
            While st.Count > 0
                Dim v = st.Pop() : comp.Add(v)
                For Each e In v.Succ.Concat(v.Pred)
                    Dim w = If(e.A Is v, e.B, e.A)
                    If seen.Add(w) Then st.Push(w)
                Next
            End While
            bands.Add(comp)
        Next

        Dim placed As New List(Of LNode)
        Dim bandTop = originY
        Dim totalW As Double = 0, totalH As Double = 0
        For Each comp In bands.OrderByDescending(Function(c) c.Count)
            Dim wh = LayoutComponent(comp, wrapRows, minAspect, originX, bandTop)
            placed.AddRange(comp.Where(Function(v) Not v.IsDummy))
            totalW = Math.Max(totalW, wh(0)) : totalH = bandTop + wh(1) - originY
            bandTop += wh(1) + WrapGap
        Next

        ' objects placed from the node centres
        For Each v In placed
            v.G.X = CInt(Math.Round(v.Pos - v.G.Width / 2.0))
            v.G.Y = CInt(Math.Round(v.Y - v.G.Height / 2.0))
        Next

        ' orphans on a row below the drawing
        Dim yOrph = originY + totalH + WrapGap
        Dim xOrph = originX
        Dim hOrph As Double = 0
        For Each g In orphans
            g.X = CInt(xOrph) : g.Y = CInt(yOrph)
            xOrph += g.Width + ColumnGap
            hOrph = Math.Max(hOrph, g.Height)
        Next
        If orphans.Count > 0 Then totalH = yOrph + hOrph - originY : totalW = Math.Max(totalW, xOrph - originX)

        ' satellites beside their object, out of the way of everything already placed: an energy stream
        ' in line with its port (to the side of a port on the left or right edge, above or below one on
        ' the top or bottom), anything else above the object it belongs to
        ' the room an object takes with its tag (and value) written below it
        Dim boxOf = Function(o As IGraphicObject, cx As Double, cy As Double) As Double()
                        Dim lw = Math.Max(o.Width, 6.5 * If(o.Tag, "").Length)
                        If o.ObjectType = ObjectType.EnergyStream Then lw = Math.Max(lw, 90)
                        Return New Double() {cx - lw / 2.0 - 4, cy - o.Height / 2.0 - 4, cx + lw / 2.0 + 4, cy + o.Height / 2.0 + If(o.ObjectType = ObjectType.EnergyStream, 34, 22)}
                    End Function
        Dim rects As New List(Of Double())
        For Each v In placed
            rects.Add(boxOf(v.G, v.G.X + v.G.Width / 2.0, v.G.Y + v.G.Height / 2.0))
        Next
        For Each g In orphans
            rects.Add(boxOf(g, g.X + g.Width / 2.0, g.Y + g.Height / 2.0))
        Next
        Dim isFreeFor = Function(o As IGraphicObject, cx As Double, cy As Double) As Boolean
                            Dim r = boxOf(o, cx, cy)
                            Return Not rects.Any(Function(q) q(0) < r(2) AndAlso r(0) < q(2) AndAlso q(1) < r(3) AndAlso r(1) < q(3))
                        End Function
        For Each kv In satellites
            Dim s = kv.Key, t = kv.Value
            Dim tcx = t.X + t.Width / 2.0, tcy = t.Y + t.Height / 2.0
            Dim cands As New List(Of Double())   ' candidate centre, and the step to move out by
            If s.ObjectType = ObjectType.EnergyStream Then
                Dim l = links.FirstOrDefault(Function(k) k.From Is s OrElse k.[To] Is s)
                Dim ox = If(l Is Nothing, 0.0, If(l.From Is s, l.OffToX, l.OffFromX))
                Dim oy = If(l Is Nothing, 0.0, If(l.From Is s, l.OffTo, l.OffFrom))
                Dim px = tcx + ox, py = tcy + oy
                Dim gap = 40.0
                If ox > 0.25 * t.Width Then cands.Add(New Double() {px + gap + s.Width / 2.0, py, 1, 0})
                If ox < -0.25 * t.Width Then cands.Add(New Double() {px - gap - s.Width / 2.0, py, -1, 0})
                If oy <= 0 Then
                    cands.Add(New Double() {px, t.Y - gap - s.Height / 2.0, 0, -1})
                    cands.Add(New Double() {px, t.Y + t.Height + gap + s.Height / 2.0, 0, 1})
                Else
                    cands.Add(New Double() {px, t.Y + t.Height + gap + s.Height / 2.0, 0, 1})
                    cands.Add(New Double() {px, t.Y - gap - s.Height / 2.0, 0, -1})
                End If
            Else
                cands.Add(New Double() {tcx, t.Y - 30 - s.Height / 2.0, 0, -1})
                cands.Add(New Double() {tcx, t.Y + t.Height + 30 + s.Height / 2.0, 0, 1})
            End If
            Dim pick As Double() = Nothing
            For k = 0 To 12
                For Each c In cands
                    Dim cx = c(0) + c(2) * k * (s.Width + 15), cy = c(1) + c(3) * k * (s.Height + 15)
                    If isFreeFor(s, cx, cy) Then pick = New Double() {cx, cy} : Exit For
                Next
                If pick IsNot Nothing Then Exit For
            Next
            If pick Is Nothing Then pick = New Double() {cands(0)(0), cands(0)(1)}
            s.X = CInt(Math.Round(pick(0) - s.Width / 2.0)) : s.Y = CInt(Math.Round(pick(1) - s.Height / 2.0))
            rects.Add(boxOf(s, pick(0), pick(1)))
        Next

        ' frames around their group again; tables, text and images on the side of the drawing they were
        ' on (above, below, right, left), keeping their distance from it; anything that sat over the
        ' drawing goes below everything
        Dim newL As Double = active.Min(Function(o) o.X), newT As Double = active.Min(Function(o) o.Y)
        Dim newR As Double = active.Max(Function(o) o.X + o.Width), newB As Double = active.Max(Function(o) o.Y + o.Height)
        For Each kv In frames
            Dim d = kv.Key, g = kv.Value
            Dim l = g.Min(Function(o) o.X) - 30.0, tp = g.Min(Function(o) o.Y) - 30.0
            Dim r = g.Max(Function(o) o.X + o.Width) + 30.0, bt = g.Max(Function(o) o.Y + o.Height) + 45.0
            d.X = CInt(l) : d.Y = CInt(tp) : d.Width = CInt(r - l) : d.Height = CInt(bt - tp)
        Next
        ' the other decorations move as two rigid groups, keeping how they were laid out among
        ' themselves: what was above the drawing stays above it, the rest goes below it
        Dim aboveSet = deco.Where(Function(d) Not frames.ContainsKey(d) AndAlso d.Y + d.Height <= oldT).ToList()
        Dim restSet = deco.Where(Function(d) Not frames.ContainsKey(d) AndAlso d.Y + d.Height > oldT).ToList()
        Dim dxDeco = CInt(newL - oldL)
        If aboveSet.Count > 0 Then
            Dim dy = Math.Min(newT - oldT, newT - 40.0 - aboveSet.Max(Function(d) d.Y + d.Height))
            For Each d In aboveSet : d.X += dxDeco : d.Y = CInt(d.Y + dy) : Next
        End If
        If restSet.Count > 0 Then
            Dim bottomNow = Math.Max(newB, frames.Keys.Select(Function(f) CDbl(f.Y + f.Height)).DefaultIfEmpty(newB).Max())
            Dim dy = Math.Max(newB - oldB, bottomNow + 50.0 - restSet.Min(Function(d) d.Y))
            For Each d In restSet : d.X += dxDeco : d.Y = CInt(d.Y + dy) : Next
        End If

        ' everything back inside the margins
        Dim minXAll = objs.Min(Function(o) o.X), minYAll = objs.Min(Function(o) o.Y)
        Dim shX = CInt(originX - minXAll), shY = CInt(originY - minYAll)
        If shX <> 0 OrElse shY <> 0 Then
            For Each o In objs
                o.X += shX : o.Y += shY
            Next
        End If

        ' orientation: a stream (or recycle) whose flow runs right to left in its row is mirrored; an
        ' energy stream beside its unit turns to point at it
        For Each o In active
            Dim ins = links.Where(Function(l) l.[To] Is o).Select(Function(l) l.From).ToList()
            Dim outs = links.Where(Function(l) l.From Is o).Select(Function(l) l.[To]).ToList()
            Dim streamLike = o.ObjectType = ObjectType.MaterialStream OrElse o.ObjectType = ObjectType.EnergyStream OrElse
                             o.ObjectType = ObjectType.OT_Recycle OrElse o.ObjectType = ObjectType.OT_EnergyRecycle
            If Not streamLike Then Continue For
            Dim a As Double() = If(ins.Count > 0, Center(ins(0)), Center(o))
            Dim b As Double() = If(outs.Count > 0, Center(outs(0)), Center(o))
            If ins.Count = 0 AndAlso outs.Count = 0 Then Continue For
            Dim dxf = b(0) - a(0), dyf = b(1) - a(1)
            If satellites.ContainsKey(o) AndAlso Math.Abs(dyf) > Math.Abs(dxf) Then
                o.Rotation = If(dyf > 0, 90, 270)
            ElseIf dxf < 0 AndAlso Math.Abs(dyf) < 3 * Math.Max(o.Height, 20) Then
                o.FlippedH = True
            End If
        Next
        For Each o In active
            Try
                o.PositionConnectors()
            Catch
            End Try
        Next

        Return New Double() {totalW, totalH}

    End Function

    ''' <summary>Layers, orders and places one connected part. Returns its width and height.</summary>
    Private Shared Function LayoutComponent(comp As List(Of LNode), wrapRows As Boolean, minAspect As Double,
                                            originX As Double, originY As Double) As Double()

        ' ---- 1. cycles: links out of recycle blocks first, then the back edges of a depth-first walk
        Dim reversed As New HashSet(Of LEdge)
        For Each v In comp
            If v.G.ObjectType = ObjectType.OT_Recycle OrElse v.G.ObjectType = ObjectType.OT_EnergyRecycle Then
                For Each e In v.Succ : reversed.Add(e) : Next
            End If
        Next
        Dim state As New Dictionary(Of LNode, Integer)
        For Each v In comp : state(v) = 0 : Next
        Dim roots = comp.Where(Function(v) v.Pred.All(Function(e) reversed.Contains(e))).OrderBy(Function(v) v.G.Y).ThenBy(Function(v) v.G.X).ToList()
        Dim rest = comp.Where(Function(v) Not roots.Contains(v)).OrderBy(Function(v) v.G.X).ToList()
        roots.AddRange(rest)
        For Each r In roots
            If state(r) <> 0 Then Continue For
            ' iterative DFS
            Dim stack As New Stack(Of (LNode, Integer))
            stack.Push((r, 0)) : state(r) = 1
            While stack.Count > 0
                Dim frame = stack.Pop()
                Dim v = frame.Item1, i = frame.Item2
                Dim outs = v.Succ.Where(Function(e) Not reversed.Contains(e)).OrderBy(Function(e) e.OffA).ToList()
                If i < outs.Count Then
                    stack.Push((v, i + 1))
                    Dim e = outs(i)
                    If state(e.B) = 1 Then
                        reversed.Add(e)
                    ElseIf state(e.B) = 0 Then
                        state(e.B) = 1 : stack.Push((e.B, 0))
                    End If
                Else
                    state(v) = 2
                End If
            End While
        Next
        Dim fwd = Function(e As LEdge) As Boolean
                      Return Not reversed.Contains(e)
                  End Function

        ' ---- 2. layers: longest path from the sources of the DAG
        Dim dSucc As New Dictionary(Of LNode, List(Of LNode)), dPred As New Dictionary(Of LNode, List(Of LNode))
        For Each v In comp : dSucc(v) = New List(Of LNode) : dPred(v) = New List(Of LNode) : Next
        For Each v In comp
            For Each e In v.Succ
                If Not comp.Contains(e.B) Then Continue For
                If fwd(e) Then dSucc(e.A).Add(e.B) : dPred(e.B).Add(e.A) Else dSucc(e.B).Add(e.A) : dPred(e.A).Add(e.B)
            Next
        Next
        Dim indeg = comp.ToDictionary(Function(v) v, Function(v) dPred(v).Count)
        Dim q As New Queue(Of LNode)(comp.Where(Function(v) indeg(v) = 0))
        For Each v In comp : v.Layer = 0 : Next
        Dim topo As New List(Of LNode)
        While q.Count > 0
            Dim v = q.Dequeue() : topo.Add(v)
            For Each w In dSucc(v)
                w.Layer = Math.Max(w.Layer, v.Layer + 1)
                indeg(w) -= 1
                If indeg(w) = 0 Then q.Enqueue(w)
            Next
        End While
        ' a node without predecessors (a feed) sits right before the first object it enters
        For pass = 1 To 3
            For Each v In topo.AsEnumerable().Reverse()
                If dPred(v).Count = 0 AndAlso dSucc(v).Count > 0 Then v.Layer = Math.Max(0, dSucc(v).Min(Function(w) w.Layer) - 1)
            Next
        Next
        Dim minL = comp.Min(Function(v) v.Layer)
        For Each v In comp : v.Layer -= minL : Next
        Dim nL = comp.Max(Function(v) v.Layer) + 1

        ' ---- 3. dummy points on links that skip columns (both directions), for the ordering and spacing
        Dim edges As New List(Of LEdge)   ' edges between consecutive layers, oriented left to right
        Dim all As New List(Of LNode)(comp)
        For Each v In comp
            For Each e In v.Succ.ToList()
                If Not comp.Contains(e.B) Then Continue For
                Dim a = e.A, b = e.B, offA = e.OffA, offB = e.OffB
                Dim back = reversed.Contains(e)
                If a.Layer > b.Layer Then
                    Dim t = a : a = b : b = t
                    Dim to2 = offA : offA = offB : offB = to2
                End If
                If a.Layer = b.Layer Then Continue For
                Dim prev = a, prevOff = offA
                For L = a.Layer + 1 To b.Layer - 1
                    Dim d = New LNode With {.G = Nothing, .Layer = L, .W = 4, .H = 6}
                    all.Add(d)
                    edges.Add(New LEdge With {.A = prev, .B = d, .OffA = prevOff, .OffB = 0, .Weight = If(back, 0.3, 4.0)})
                    prev = d : prevOff = 0
                Next
                edges.Add(New LEdge With {.A = prev, .B = b, .OffA = prevOff, .OffB = offB, .Weight = If(back, 0.3, If(prev.IsDummy, 4.0, e.Weight))})
            Next
        Next
        Dim lp As New Dictionary(Of LNode, List(Of LEdge)), ls As New Dictionary(Of LNode, List(Of LEdge))
        For Each v In all : lp(v) = New List(Of LEdge) : ls(v) = New List(Of LEdge) : Next
        For Each e In edges : ls(e.A).Add(e) : lp(e.B).Add(e) : Next

        ' ---- 4. order within the columns: barycentre sweeps with port positions, best kept
        Dim layers As New List(Of List(Of LNode))
        For L = 0 To nL - 1 : layers.Add(New List(Of LNode)) : Next
        For Each v In all.OrderBy(Function(x) If(x.IsDummy, 0, x.G.Y)) : layers(v.Layer).Add(v) : Next
        Dim setIdx = Sub()
                         For Each lay In layers
                             For i = 0 To lay.Count - 1 : lay(i).Index = i : Next
                         Next
                     End Sub
        setIdx()
        Dim frac = Function(v As LNode, off As Double) As Double
                       Return If(v.IsDummy, 0.0, Math.Max(-0.45, Math.Min(0.45, off / Math.Max(v.H, 1.0) * 0.9)))
                   End Function
        Dim crossings = Function() As Integer
                            Dim c = 0
                            For L = 0 To nL - 2
                                Dim es = edges.Where(Function(e) e.A.Layer = L).ToList()
                                For i = 0 To es.Count - 1
                                    For j = i + 1 To es.Count - 1
                                        Dim a1 = es(i).A.Index + frac(es(i).A, es(i).OffA), b1 = es(i).B.Index + frac(es(i).B, es(i).OffB)
                                        Dim a2 = es(j).A.Index + frac(es(j).A, es(j).OffA), b2 = es(j).B.Index + frac(es(j).B, es(j).OffB)
                                        If (a1 - a2) * (b1 - b2) < 0 Then c += 1
                                    Next
                                Next
                            Next
                            Return c
                        End Function
        Dim best = layers.Select(Function(l) l.ToList()).ToList()
        Dim bestC = crossings()
        For sweep = 1 To 12
            Dim down = (sweep Mod 2 = 1)
            Dim range = If(down, Enumerable.Range(1, Math.Max(nL - 1, 0)), Enumerable.Range(0, Math.Max(nL - 1, 0)).Reverse())
            For Each L In range
                Dim lay = layers(L)
                Dim bary As New Dictionary(Of LNode, Double)
                For Each v In lay
                    Dim es = If(down, lp(v), ls(v))
                    If es.Count = 0 Then
                        bary(v) = v.Index
                    Else
                        bary(v) = es.Average(Function(e) If(down, e.A.Index + frac(e.A, e.OffA) - frac(v, e.OffB), e.B.Index + frac(e.B, e.OffB) - frac(v, e.OffA)))
                    End If
                Next
                Dim sorted = lay.OrderBy(Function(v) bary(v)).ThenBy(Function(v) v.Index).ToList()
                layers(L) = sorted
                For i = 0 To sorted.Count - 1 : sorted(i).Index = i : Next
            Next
            Dim c = crossings()
            If c < bestC Then bestC = c : best = layers.Select(Function(l) l.ToList()).ToList()
        Next
        layers = best
        setIdx()

        ' ---- 5. vertical positions: weighted-median targets, solved per column (PAV)
        For Each lay In layers
            Dim y = 0.0
            For Each v In lay : v.Y = y + v.H / 2.0 : y += v.H + RowGap : Next
        Next
        Dim sep = Function(a As LNode, b As LNode) As Double
                      Dim g = If(a.IsDummy OrElse b.IsDummy, 18.0, RowGap)
                      Return a.H / 2.0 + b.H / 2.0 + g
                  End Function
        For it = 1 To 30
            Dim mode = it Mod 3   ' 1: from the left, 2: from the right, 0: both
            Dim range = If(mode = 2, Enumerable.Range(0, nL).Reverse(), Enumerable.Range(0, nL))
            For Each L In range
                Dim lay = layers(L)
                Dim targets(lay.Count - 1) As Double, weights(lay.Count - 1) As Double
                For i = 0 To lay.Count - 1
                    Dim v = lay(i)
                    Dim cand As New List(Of (Double, Double))
                    If mode <> 2 Then
                        For Each e In lp(v) : cand.Add((e.A.Y + e.OffA - e.OffB, e.Weight)) : Next
                    End If
                    If mode <> 1 Then
                        For Each e In ls(v) : cand.Add((e.B.Y + e.OffB - e.OffA, e.Weight)) : Next
                    End If
                    If cand.Count = 0 Then
                        targets(i) = v.Y : weights(i) = 0.05
                    Else
                        targets(i) = WeightedMedian(cand) : weights(i) = cand.Sum(Function(c) c.Item2)
                    End If
                Next
                Dim gaps(Math.Max(lay.Count - 2, 0)) As Double
                For i = 0 To lay.Count - 2 : gaps(i) = sep(lay(i), lay(i + 1)) : Next
                Dim ys = SolveOrdered(targets, weights, gaps)
                For i = 0 To lay.Count - 1 : lay(i).Y = ys(i) : Next
            Next
        Next

        ' ---- 6. horizontal positions: column pitch from the widest object of each column
        Dim colW(nL - 1) As Double, colX(nL - 1) As Double
        For L = 0 To nL - 1 : colW(L) = Math.Max(10, layers(L).Select(Function(v) v.W).DefaultIfEmpty(10).Max()) : Next
        Dim x0 = 0.0
        For L = 0 To nL - 1
            colX(L) = x0 + colW(L) / 2.0
            x0 += colW(L) + ColumnGap
        Next
        Dim widthAll = x0 - ColumnGap
        For Each v In all : v.Pos = colX(v.Layer) : Next

        ' ---- 7. rows: one, or the wrap with the smallest area within the aspect limits
        Dim nRows = 1
        Dim cuts As New List(Of Integer)
        If wrapRows AndAlso nL > 2 Then
            Dim bestArea = Double.MaxValue, bestViol = Double.MaxValue, bestOk = False
            For r = 1 To Math.Min(nL, 12)
                Dim cc = ChooseCuts(r, colW, colX, edges, nL)
                Dim wh = RowsSize(cc, layers, colX, colW, nL)
                Dim ratio = wh(0) / Math.Max(wh(1), 1.0)
                Dim ok = ratio >= minAspect AndAlso ratio <= 1.0 / minAspect
                Dim viol = Math.Max(ratio, 1.0 / ratio)
                Dim area = wh(0) * wh(1)
                If (ok AndAlso (Not bestOk OrElse area < bestArea)) OrElse (Not bestOk AndAlso Not ok AndAlso viol < bestViol) Then
                    bestOk = ok : bestArea = area : bestViol = viol : cuts = cc : nRows = r
                End If
            Next
        End If

        ' place the rows (every row from the left margin), stacked
        Dim bounds As New List(Of Integer)(cuts) : bounds.Insert(0, 0) : bounds.Add(nL)
        Dim top = originY, maxW = 0.0
        For r = 0 To bounds.Count - 2
            Dim l0 = bounds(r), l1 = bounds(r + 1)
            Dim rowNodes = all.Where(Function(v) v.Layer >= l0 AndAlso v.Layer < l1).ToList()
            If rowNodes.Count = 0 Then Continue For
            Dim minY = rowNodes.Min(Function(v) v.Y - v.H / 2.0), maxY = rowNodes.Max(Function(v) v.Y + v.H / 2.0)
            Dim xStart = colX(l0) - colW(l0) / 2.0
            For Each v In rowNodes
                v.Pos = originX + v.Pos - xStart
                v.Y = top + v.Y - minY
                v.Row = r
            Next
            maxW = Math.Max(maxW, colX(l1 - 1) + colW(l1 - 1) / 2.0 - xStart)
            top += maxY - minY + WrapGap
        Next
        Return New Double() {maxW, top - WrapGap - originY}

    End Function

    Private Shared Function WeightedMedian(c As List(Of (Double, Double))) As Double
        Dim s = c.OrderBy(Function(x) x.Item1).ToList()
        Dim total = s.Sum(Function(x) x.Item2), acc = 0.0
        For Each x In s
            acc += x.Item2
            If acc >= total / 2.0 - 1.0E-9 Then Return x.Item1
        Next
        Return s.Last().Item1
    End Function

    ''' <summary>Minimises sum w (y - t)^2 with y(i+1) - y(i) >= gap(i): pool-adjacent-violators on the
    ''' gap-shifted targets.</summary>
    Private Shared Function SolveOrdered(t As Double(), w As Double(), gaps As Double()) As Double()
        Dim n = t.Length
        Dim shift(n - 1) As Double
        For i = 1 To n - 1 : shift(i) = shift(i - 1) + gaps(i - 1) : Next
        Dim vals As New List(Of Double), wts As New List(Of Double), cnt As New List(Of Integer)
        For i = 0 To n - 1
            vals.Add(t(i) - shift(i)) : wts.Add(Math.Max(w(i), 1.0E-6)) : cnt.Add(1)
            While vals.Count > 1 AndAlso vals(vals.Count - 2) > vals(vals.Count - 1)
                Dim k = vals.Count - 1
                Dim ww = wts(k - 1) + wts(k)
                vals(k - 1) = (vals(k - 1) * wts(k - 1) + vals(k) * wts(k)) / ww
                wts(k - 1) = ww : cnt(k - 1) += cnt(k)
                vals.RemoveAt(k) : wts.RemoveAt(k) : cnt.RemoveAt(k)
            End While
        Next
        Dim y(n - 1) As Double, idx = 0
        For b = 0 To vals.Count - 1
            For j = 1 To cnt(b)
                y(idx) = vals(b) + shift(idx) : idx += 1
            Next
        Next
        Return y
    End Function

    ''' <summary>Column boundaries for r rows: near equal widths, nudged to cut where fewest links cross.</summary>
    Private Shared Function ChooseCuts(r As Integer, colW As Double(), colX As Double(), edges As List(Of LEdge), nL As Integer) As List(Of Integer)
        Dim cuts As New List(Of Integer)
        If r <= 1 Then Return cuts
        Dim total = colX(nL - 1) + colW(nL - 1) / 2.0
        Dim crossAt = Function(c As Integer) edges.Where(Function(e) e.A.Layer < c AndAlso e.B.Layer >= c).Count()
        Dim last = 0
        For k = 1 To r - 1
            Dim target = total * k / r
            Dim bestC = -1, bestS = Double.MaxValue
            For c = last + 1 To nL - (r - k)
                Dim x = colX(c) - colW(c) / 2.0
                Dim s = Math.Abs(x - target) / total + 0.03 * crossAt(c)
                If s < bestS Then bestS = s : bestC = c
            Next
            If bestC < 0 Then Exit For
            cuts.Add(bestC) : last = bestC
        Next
        Return cuts
    End Function

    Private Shared Function RowsSize(cuts As List(Of Integer), layers As List(Of List(Of LNode)), colX As Double(), colW As Double(), nL As Integer) As Double()
        Dim bounds As New List(Of Integer)(cuts) : bounds.Insert(0, 0) : bounds.Add(nL)
        Dim w = 0.0, h = 0.0
        For r = 0 To bounds.Count - 2
            Dim l0 = bounds(r), l1 = bounds(r + 1)
            Dim ns = layers.Skip(l0).Take(l1 - l0).SelectMany(Function(l) l).ToList()
            If ns.Count = 0 Then Continue For
            w = Math.Max(w, colX(l1 - 1) + colW(l1 - 1) / 2.0 - (colX(l0) - colW(l0) / 2.0))
            h += ns.Max(Function(v) v.Y + v.H / 2.0) - ns.Min(Function(v) v.Y - v.H / 2.0) + If(r > 0, WrapGap, 0.0)
        Next
        Return New Double() {w, h}
    End Function

End Class
