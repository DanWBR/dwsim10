//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    public static partial class ProcessFlowDiagram
    {
        /// <summary>Radius of an instrument bubble, mm on the sheet.</summary>
        const double Bubble = 5;

        /// <summary>The P&amp;ID line number and number alone of each material stream, by tag, as the P&amp;ID shows them.</summary>
        internal static Dictionary<string, (string Number, string Id)> LineNumbers(IFlowsheet fs, Dictionary<string, PipeRun> data)
        {
            var map = new Dictionary<string, (string, string)>();
            foreach (var l in Lines(fs).Where(l => !l.Energy && l.Tag != null))
                map[l.Tag] = (LineNumber(l, data), l.Id);
            return map;
        }

        /// <summary>A line number: nominal size, fluid code (P, process), number, and H or C for hot or cold insulation.</summary>
        static string LineNumber(Line l, Dictionary<string, PipeRun> data)
        {
            data.TryGetValue(l.Tag ?? "", out var run);
            var size = run?.Size ?? "";
            var ins = run == null ? "" : run.InsulationService == "hot" ? "-H" : run.InsulationService == "cold" ? "-C" : "";
            return (size.Length > 0 ? size + "-" : "") + "P-" + l.Id + ins;
        }

        /// <summary>
        /// The instruments, after ISA 5.1: for each controller, a transmitter (a field bubble) at
        /// what it measures, tapped off the line or the item, the controller itself (a shared
        /// display bubble, in a square) where its icon is on the flowsheet, and dashed signal lines
        /// from the transmitter to the controller and from the controller to what it moves (the
        /// actuator of a control valve, tagged with the loop); for each gauge, a local indicator.
        /// Bubbles move off the items, their texts and each other; signals take clear routes.
        /// </summary>
        static void DrawInstruments(IFlowsheet fs, List<SolidCraftScript.Command> cmds, List<Unit> units, List<Line> lines,
            Dictionary<Line, List<Pt>> routes, Dictionary<string, string> actuated, Dictionary<string, Pt> outline,
            List<Router.Box> items, List<Router.Box> texts, Router router, Func<double, double, Pt> T, double k)
        {
            void C(string name, JObject p, string what) => cmds.Add(new SolidCraftScript.Command { Name = name, Params = p, Subject = what });
            var unitOf = units.GroupBy(u => u.Obj.Name).ToDictionary(g => g.Key, g => g.First());
            var lineOf = lines.Where(l => l.Icon?.Owner != null).GroupBy(l => l.Icon.Owner.Name).ToDictionary(g => g.Key, g => g.First());
            var onLine = new Dictionary<Line, int>();
            var onUnit = new Dictionary<string, int>();
            var bubbles = new List<Pt>();
            var loops = InstrumentLoops(fs).ToDictionary(x => x.Obj.Name, x => x.Loop);

            // the bubbles are placed first and drawn, then the signals are routed round them
            var toDraw = new List<(Pt At, string Letters, string Loop, bool Shared, string What)>();
            var signals = new List<(Pt From, Pt To, bool ToBubble, bool FromAbove, string What)>();
            var setpoints = new List<(Pt At, string Text, string What)>();
            var alarms = new List<(Pt At, string Text, string What)>();

            Pt Center(Unit u) => T(u.Go.X + u.Go.Width / 2.0, u.Go.Y + u.Go.Height / 2.0);
            Pt Half(Unit u) => outline.TryGetValue(u.Obj.Name, out var h) ? h : new Pt(u.Go.Width * k / 2, u.Go.Height * k / 2);

            // a place for a bubble clear of the items, their texts and the other bubbles
            bool Free(Pt p) =>
                !items.Any(b => b.Grown(Bubble + 1).Inside(p.X, p.Y)) &&
                !texts.Any(b => b.Grown(Bubble).Inside(p.X, p.Y)) &&
                !bubbles.Any(q => Dist(p, q) < 2 * Bubble + 2);
            Pt Place(Pt first, double dx, double dy, int tries)
            {
                for (var i = 0; i < tries; i++)
                {
                    var p = new Pt(first.X + dx * i, first.Y + dy * i);
                    if (Free(p)) return p;
                }
                return first;
            }

            // where a measurement is taken: on its stream's line, or on the side of its item with
            // fewer nozzles; the bubble stands off it, joined by a thin tap line
            bool Measure(string id, string what, out Pt bubble)
            {
                bubble = new Pt(0, 0);
                if (string.IsNullOrEmpty(id)) return false;
                Pt tap, end;
                if (lineOf.TryGetValue(id, out var line) && routes.TryGetValue(line, out var route) && route.Count >= 2)
                {
                    onLine.TryGetValue(line, out var n);
                    onLine[line] = n + 1;
                    tap = Along(route, Math.Min(0.85, 0.35 + 0.2 * n), out var horizontal);
                    bubble = horizontal ? Place(new Pt(tap.X, tap.Y + 13), 0, 12, 6) : Place(new Pt(tap.X + 13, tap.Y), 12, 0, 6);
                    end = horizontal ? new Pt(tap.X, bubble.Y - Bubble) : new Pt(bubble.X - Bubble, tap.Y);
                }
                else if (unitOf.TryGetValue(id, out var u))
                {
                    onUnit.TryGetValue(id, out var n);
                    onUnit[id] = n + 1;
                    var c = Center(u);
                    var half = Half(u);
                    double h = u.Go.Height * k;
                    var right = u.Go.InputConnectors.Concat(u.Go.OutputConnectors).Where(p => p.IsAttached).Count(p => T(p.Position.X, p.Position.Y).X > c.X);
                    var left = u.Go.InputConnectors.Concat(u.Go.OutputConnectors).Where(p => p.IsAttached).Count(p => T(p.Position.X, p.Position.Y).X < c.X);
                    var side = right <= left ? 1 : -1;
                    tap = new Pt(c.X + side * half.X, c.Y + h * 0.3 - n * (2 * Bubble + 3));
                    bubble = Place(new Pt(tap.X + side * (u.Go.Width * k / 2 - half.X + 13), tap.Y), side * 12, 0, 6);
                    end = new Pt(bubble.X - side * Bubble, tap.Y);
                    if (Math.Abs(bubble.Y - tap.Y) > 0.01) end = new Pt(bubble.X - side * Bubble, bubble.Y);
                }
                else return false;
                bubbles.Add(bubble);
                C("layer.current", new JObject { ["name"] = "INSTRUMENT" }, what);
                C("pline", new JObject { ["vertices"] = Math.Abs(end.Y - tap.Y) < 0.01 || Math.Abs(end.X - tap.X) < 0.01
                    ? new JArray(A(tap), A(end))
                    : new JArray(A(tap), A(new Pt(tap.X, end.Y)), A(end)) }, what);
                return true;
            }

            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                var go = obj.GraphicObject;
                if (go == null) continue;
                var isController = go.ObjectType.ToString().StartsWith("Controller_") && obj is IAdjust;
                var gauge = obj as IIndicator;
                if (!isController && gauge == null) continue;

                string measured, letter;
                if (isController)
                {
                    var c = (IAdjust)obj;
                    measured = c.ControlledObjectData?.ID;
                    letter = Instruments.Letter(fs, c.ControlledObjectData?.UnitsType ?? UnitOfMeasure.none, c.ControlledObjectData?.PropertyName, go.Tag);
                }
                else
                {
                    measured = gauge.SelectedObjectID;
                    letter = Instruments.Letter(fs, gauge.SelectedPropertyType, gauge.SelectedProperty, go.Tag);
                }
                var loop = loops[obj.Name];
                var what = "instrument " + go.Tag;
                if (!Measure(measured, what, out var at)) continue;

                if (!isController)
                {
                    toDraw.Add((at, letter + "I", loop, false, what));
                    // the alarms set on the gauge: the high ones over its right, the low ones under it
                    var gu = string.IsNullOrEmpty(gauge.SelectedPropertyUnits) ? "" : " " + gauge.SelectedPropertyUnits;
                    var highs = new List<string>(); var lows = new List<string>();
                    if (gauge.VeryHighAlarmEnabled) highs.Add(letter + "AHH " + Fmt(gauge.VeryHighAlarmValue) + gu);
                    if (gauge.HighAlarmEnabled) highs.Add(letter + "AH " + Fmt(gauge.HighAlarmValue) + gu);
                    if (gauge.LowAlarmEnabled) lows.Add(letter + "AL " + Fmt(gauge.LowAlarmValue) + gu);
                    if (gauge.VeryLowAlarmEnabled) lows.Add(letter + "ALL " + Fmt(gauge.VeryLowAlarmValue) + gu);
                    for (var i = 0; i < highs.Count; i++) alarms.Add((new Pt(at.X + Bubble + 1, at.Y + Bubble * 0.5 + (highs.Count - 1 - i) * 2.4), highs[i], what));
                    for (var i = 0; i < lows.Count; i++) alarms.Add((new Pt(at.X + Bubble + 1, at.Y - Bubble * 0.5 - i * 2.4), lows[i], what));
                    continue;
                }

                // the transmitter, the controller near its icon, and the signals
                toDraw.Add((at, letter + "T", loop, false, what));
                var icon = T(go.X + go.Width / 2.0, go.Y + go.Height / 2.0);
                var ctl = Place(icon, 0, -12, 8);
                if (!Free(ctl)) ctl = Place(icon, 0, 12, 8);
                bubbles.Add(ctl);
                toDraw.Add((ctl, letter + "IC", loop, true, what));

                // the set point beside the controller, in the units the controller is set in
                var adjust = (IAdjust)obj;
                var sp = adjust.AdjustValue;
                if (!double.IsNaN(sp) && !double.IsInfinity(sp))
                {
                    var spUnits = adjust.ControlledObjectData?.Units ?? "";
                    setpoints.Add((new Pt(ctl.X + Bubble + 1.5, ctl.Y), "SP " + Fmt(sp) + (spUnits.Length > 0 ? " " + spUnits : ""), what));
                }
                signals.Add((at, ctl, true, true, what));

                var mv = ((IAdjust)obj).ManipulatedObjectData?.ID;
                if (string.IsNullOrEmpty(mv)) continue;
                Pt target;
                var fromAbove = true;
                if (unitOf.TryGetValue(mv, out var u))
                {
                    var c = Center(u);
                    double w = u.Go.Width * k, h = u.Go.Height * k;
                    if (u.Symbol == Symbol.Valve && actuated.ContainsKey(mv))
                    {
                        ActuatorTop(w, h, out var stemTop, out var r);
                        target = new Pt(c.X, c.Y + stemTop + r);
                        var tag = letter + "V-" + loop;
                        if (!string.Equals(tag, u.Go.Tag, StringComparison.OrdinalIgnoreCase))
                        {
                            C("layer.current", new JObject { ["name"] = "TEXT" }, what);
                            C("text", new JObject { ["at"] = new JArray(Math.Round(c.X + r + 1.5, 2), Math.Round(c.Y + stemTop, 2)), ["text"] = tag, ["height"] = 2.0, ["justify"] = "ML" }, what);
                        }
                    }
                    else target = new Pt(c.X, c.Y + Half(u).Y);
                }
                else if (lineOf.TryGetValue(mv, out var line) && routes.TryGetValue(line, out var route) && route.Count >= 2)
                {
                    target = Along(route, 0.6, out var horizontal);
                    fromAbove = horizontal;
                }
                else continue;
                signals.Add((ctl, target, false, fromAbove, what));
            }

            // the switches: a hand switch on the control system, through an interlock to what it
            // acts on, with the values it sets
            var interlocks = new List<(Pt At, string Number, string What)>();
            var switchNotes = new List<(Pt At, string Text, string What)>();
            var iseq = 0;
            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                if (!(obj is ISwitch sw) || obj.GraphicObject == null) continue;
                var go = obj.GraphicObject;
                var what = "switch " + go.Tag;
                var number = Loop(go.Tag, ref iseq);
                var icon = T(go.X + go.Width / 2.0, go.Y + go.Height / 2.0);
                var hs = Place(icon, 0, -12, 8);
                if (!Free(hs)) hs = Place(icon, 0, 12, 8);
                bubbles.Add(hs);
                var dia = Place(new Pt(hs.X, hs.Y - 2 * Bubble - 4), 12, 0, 6);
                bubbles.Add(dia);
                toDraw.Add((hs, "HS", number, true, what));
                interlocks.Add((dia, number, what));
                var su = string.IsNullOrEmpty(sw.SelectedPropertyUnits) ? "" : " " + sw.SelectedPropertyUnits;
                string property = sw.SelectedProperty ?? "";
                try { property = fs.GetTranslatedString(property, "en") ?? property; } catch (Exception) { }
                switchNotes.Add((new Pt(dia.X, dia.Y - Bubble - 1), "ON " + Fmt(sw.OnValue) + " / OFF " + Fmt(sw.OffValue) + su, what));
                signals.Add((hs, dia, true, false, what));
                var id = sw.SelectedObjectID;
                if (string.IsNullOrEmpty(id)) continue;
                if (unitOf.TryGetValue(id, out var u))
                {
                    var c = Center(u);
                    double w = u.Go.Width * k, h = u.Go.Height * k;
                    Pt target;
                    if (u.Symbol == Symbol.Valve && actuated.ContainsKey(id)) { ActuatorTop(w, h, out var stemTop, out var r); target = new Pt(c.X, c.Y + stemTop + r); }
                    else target = new Pt(c.X, c.Y + Half(u).Y);
                    signals.Add((dia, target, false, true, what));
                }
                else if (lineOf.TryGetValue(id, out var line) && routes.TryGetValue(line, out var route) && route.Count >= 2)
                {
                    var target = Along(route, 0.6, out var horizontal);
                    signals.Add((dia, target, false, horizontal, what));
                }
            }

            foreach (var b in interlocks)
            {
                // the interlock: a diamond with I over its number
                const double r = Bubble;
                C("layer.current", new JObject { ["name"] = "INSTRUMENT" }, b.What);
                C("pline", new JObject { ["vertices"] = new JArray(A(new Pt(b.At.X - r, b.At.Y)), A(new Pt(b.At.X, b.At.Y + r)), A(new Pt(b.At.X + r, b.At.Y)), A(new Pt(b.At.X, b.At.Y - r))), ["closed"] = true }, b.What);
                C("layer.current", new JObject { ["name"] = "TEXT" }, b.What);
                C("text", new JObject { ["at"] = new JArray(b.At.X, Math.Round(b.At.Y + 1.2, 2)), ["text"] = "I", ["height"] = 1.8, ["justify"] = "MC" }, b.What);
                C("text", new JObject { ["at"] = new JArray(b.At.X, Math.Round(b.At.Y - 1.6, 2)), ["text"] = b.Number, ["height"] = b.Number.Length > 3 ? 1.4 : 1.6, ["justify"] = "MC" }, b.What);
                router.Block(new Router.Box(b.At.X - r, b.At.Y - r, b.At.X + r, b.At.Y + r));
            }
            foreach (var t in switchNotes)
            {
                const double h = 1.8;
                C("layer.current", new JObject { ["name"] = "TEXT" }, t.What);
                C("text", new JObject { ["at"] = A(t.At), ["text"] = t.Text, ["height"] = h, ["justify"] = "TC" }, t.What);
                var half = t.Text.Length * h * 0.45;
                router.Block(new Router.Box(t.At.X - half, t.At.Y - h, t.At.X + half, t.At.Y));
            }
            foreach (var t in alarms)
            {
                const double h = 1.8;
                C("layer.current", new JObject { ["name"] = "TEXT" }, t.What);
                C("text", new JObject { ["at"] = A(t.At), ["text"] = t.Text, ["height"] = h, ["justify"] = "ML" }, t.What);
                router.Block(new Router.Box(t.At.X, t.At.Y - h / 2, t.At.X + t.Text.Length * h * 0.9, t.At.Y + h / 2));
            }

            foreach (var b in toDraw)
            {
                DrawBubble(cmds, b.At, b.Letters, b.Loop, b.Shared, b.What);
                router.Block(new Router.Box(b.At.X - Bubble, b.At.Y - Bubble, b.At.X + Bubble, b.At.Y + Bubble));
            }
            foreach (var t in setpoints)
            {
                const double h = 2.0;
                C("layer.current", new JObject { ["name"] = "TEXT" }, t.What);
                C("text", new JObject { ["at"] = A(t.At), ["text"] = t.Text, ["height"] = h, ["justify"] = "ML" }, t.What);
                router.Block(new Router.Box(t.At.X, t.At.Y - h, t.At.X + t.Text.Length * h * 0.9, t.At.Y + h));
            }
            foreach (var s in signals) Signal(cmds, router, s.From, s.To, s.ToBubble, s.What, s.FromAbove);
        }

        /// <summary>
        /// The controllers and gauges of the flowsheet in tag order, with the first letter of what
        /// they measure and their loop number (the digits of their tag, else numbered from 101).
        /// </summary>
        internal static List<(ISimulationObject Obj, string Letter, string Loop, bool Controller)> InstrumentLoops(IFlowsheet fs)
        {
            var list = new List<(ISimulationObject, string, string, bool)>();
            var seq = 100;
            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                var go = obj.GraphicObject;
                if (go == null) continue;
                var isController = go.ObjectType.ToString().StartsWith("Controller_") && obj is IAdjust;
                var gauge = obj as IIndicator;
                if (!isController && gauge == null) continue;
                var letter = isController
                    ? Instruments.Letter(fs, ((IAdjust)obj).ControlledObjectData?.UnitsType ?? UnitOfMeasure.none, ((IAdjust)obj).ControlledObjectData?.PropertyName, go.Tag)
                    : Instruments.Letter(fs, gauge.SelectedPropertyType, gauge.SelectedProperty, go.Tag);
                list.Add((obj, letter, Loop(go.Tag, ref seq), isController));
            }
            return list;
        }

        /// <summary>The loop number: the digits of the instrument's tag, or the next free number.</summary>
        static string Loop(string tag, ref int seq)
        {
            var digits = Regex.Match(tag ?? "", @"\d+").Value;
            return digits.Length > 0 ? digits : (++seq).ToString();
        }

        /// <summary>The point a fraction of the way along a route, and whether the run it is on is horizontal.</summary>
        static Pt Along(List<Pt> route, double fraction, out bool horizontal)
        {
            double total = 0;
            for (var i = 1; i < route.Count; i++) total += Dist(route[i - 1], route[i]);
            var target = fraction * total;
            for (var i = 1; i < route.Count; i++)
            {
                var l = Dist(route[i - 1], route[i]);
                horizontal = Math.Abs(route[i].Y - route[i - 1].Y) < 0.01;
                if (target <= l)
                {
                    var f = l > 0 ? target / l : 0;
                    return new Pt(route[i - 1].X + (route[i].X - route[i - 1].X) * f, route[i - 1].Y + (route[i].Y - route[i - 1].Y) * f);
                }
                target -= l;
            }
            horizontal = true;
            return route[route.Count - 1];
        }

        /// <summary>An ISA bubble: letters over the loop number; a shared display (control room) bubble sits in a square with a line across.</summary>
        static void DrawBubble(List<SolidCraftScript.Command> cmds, Pt c, string letters, string loop, bool shared, string what)
        {
            void C(string name, JObject p) => cmds.Add(new SolidCraftScript.Command { Name = name, Params = p, Subject = what });
            C("layer.current", new JObject { ["name"] = "INSTRUMENT" });
            C("circle", new JObject { ["center"] = A(c), ["radius"] = Bubble });
            if (shared)
            {
                C("pline", new JObject { ["vertices"] = new JArray(A(new Pt(c.X - Bubble, c.Y - Bubble)), A(new Pt(c.X + Bubble, c.Y - Bubble)), A(new Pt(c.X + Bubble, c.Y + Bubble)), A(new Pt(c.X - Bubble, c.Y + Bubble))), ["closed"] = true });
                C("line", new JObject { ["points"] = new JArray(A(new Pt(c.X - Bubble, c.Y)), A(new Pt(c.X + Bubble, c.Y))) });
            }
            C("layer.current", new JObject { ["name"] = "TEXT" });
            C("text", new JObject { ["at"] = new JArray(c.X, Math.Round(c.Y + 1.6, 2)), ["text"] = letters, ["height"] = letters.Length > 3 ? 1.6 : 2.0, ["justify"] = "MC" });
            C("text", new JObject { ["at"] = new JArray(c.X, Math.Round(c.Y - 2.0, 2)), ["text"] = loop, ["height"] = loop.Length > 3 ? 1.6 : 2.0, ["justify"] = "MC" });
        }

        /// <summary>
        /// A dashed signal line from a bubble to another bubble (<paramref name="toBubble"/>) or to
        /// a point (an actuator or an item from above, a line from the side): the router's clear
        /// way where it finds one, else across and then up or down.
        /// </summary>
        static void Signal(List<SolidCraftScript.Command> cmds, Router router, Pt from, Pt to, bool toBubble, string what, bool fromAbove = true)
        {
            double dx = to.X - from.X, dy = to.Y - from.Y;
            List<Pt> path = null;
            try
            {
                var c = router.Clearance;
                int Toward(double x, double y) => Math.Abs(x) >= Math.Abs(y) ? (x >= 0 ? 0 : 2) : (y >= 0 ? 1 : 3);
                // out of the bubble towards the target first, then the other ways round
                var first = Toward(dx, dy);
                var starts = new[] { first, Toward(Math.Abs(dx) >= Math.Abs(dy) ? 0 : dx, Math.Abs(dx) >= Math.Abs(dy) ? dy : 0), (first + 1) % 4, (first + 3) % 4, (first + 2) % 4 }.Distinct();
                var ends = toBubble ? new[] { first, (first + 1) % 4, (first + 3) % 4, (first + 2) % 4 } : new[] { fromAbove ? 3 : (dx >= 0 ? 0 : 2) };
                foreach (var sd in starts)
                {
                    var start = new Pt(from.X + Dirs[sd].X * Bubble, from.Y + Dirs[sd].Y * Bubble);
                    var s = new Pt(start.X + Dirs[sd].X * c, start.Y + Dirs[sd].Y * c);
                    foreach (var ed in ends)
                    {
                        var end = toBubble ? new Pt(to.X - Dirs[ed].X * Bubble, to.Y - Dirs[ed].Y * Bubble) : to;
                        var e = new Pt(end.X - Dirs[ed].X * c, end.Y - Dirs[ed].Y * c);
                        path = router.Route(start, s, sd, e, end, ed, !toBubble);
                        if (path != null) break;
                    }
                    if (path != null) break;
                }
                if (path != null) router.Add(path);
            }
            catch (Exception) { path = null; }

            if (path == null)
            {
                var sy = Math.Sign(dy);
                var end = toBubble ? new Pt(to.X, to.Y - sy * Bubble) : to;
                if (Math.Abs(dx) < 0.5) path = new List<Pt> { new Pt(from.X, from.Y + sy * Bubble), end };
                else
                {
                    var start = new Pt(from.X + Math.Sign(dx) * Bubble, from.Y);
                    path = Math.Abs(dy) < 0.5
                        ? new List<Pt> { start, new Pt(to.X - Math.Sign(dx) * (toBubble ? Bubble : 0), to.Y) }
                        : new List<Pt> { start, new Pt(to.X, from.Y), end };
                }
            }
            cmds.Add(new SolidCraftScript.Command { Name = "layer.current", Params = new JObject { ["name"] = "SIGNAL" }, Subject = what });
            cmds.Add(new SolidCraftScript.Command { Name = "pline", Params = new JObject { ["vertices"] = new JArray(path.Select(A)) }, Subject = what });
        }

        static readonly Pt[] Dirs = { new Pt(1, 0), new Pt(0, 1), new Pt(-1, 0), new Pt(0, -1) };
    }
}
