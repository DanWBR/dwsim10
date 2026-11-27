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
using System.Globalization;
using System.Linq;
using System.Text;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// A short text that changes whenever something the 3D scene shows changes: an object added,
    /// removed, renamed, moved, turned or flipped on the canvas, a connection made or taken apart,
    /// a new result in a stream, or a new place in the saved 3D layout. The link reads it twice a
    /// second, which costs far less than building the plant model, and builds the scene again only
    /// when it changed. It catches the changes the drawing surface raises no event for (an object
    /// moved by code, a layout command, a load).
    /// </summary>
    public static class FlowsheetFingerprint
    {
        const string LayoutKey = "SolidCraftLayout";

        /// <summary>
        /// The structure only: what changes the geometry (objects, tags, places, turns, connections,
        /// the 3D layout), and not the results.
        /// </summary>
        public static string Structure(IFlowsheet fs) => Compute(fs, false);

        /// <summary>
        /// The results only: stream conditions, energy flows, calculated flags, errors and alarms.
        /// </summary>
        public static string Results(IFlowsheet fs)
        {
            var sb = new StringBuilder();
            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.Name, StringComparer.Ordinal))
            {
                sb.Append(obj.Name).Append(obj.Calculated ? 'c' : '-').Append(obj.ErrorMessage);
                var ms = obj as IMaterialStream;
                if (ms != null)
                    sb.Append('|').Append(Round(ms.GetTemperature())).Append(',').Append(Round(ms.GetPressure()))
                      .Append(',').Append(Round(ms.GetMassFlow()));
                var es = obj as IEnergyStream;
                if (es != null) sb.Append('|').Append(Round(es.GetEnergyFlow()));
                var g = obj as IIndicator;
                if (g != null) sb.Append('|').Append(g.HighAlarmActive || g.VeryHighAlarmActive || g.LowAlarmActive || g.VeryLowAlarmActive ? 'A' : '-');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static string Compute(IFlowsheet fs) => Compute(fs, true);

        public static string Compute(IFlowsheet fs, bool results)
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            foreach (var go in fs.GraphicObjects.Values.Where(g => g != null && !g.IsConnector).OrderBy(g => g.Name, StringComparer.Ordinal))
            {
                sb.Append(go.Name).Append('|').Append(go.Tag).Append('|').Append((int)go.ObjectType).Append('|')
                  .Append(go.X.ToString("R", inv)).Append(',').Append(go.Y.ToString("R", inv)).Append(',')
                  .Append(go.Width).Append(',').Append(go.Height).Append(',').Append(go.Rotation).Append(',')
                  .Append(go.FlippedH ? 'h' : '-').Append(go.FlippedV ? 'v' : '-');
                Ports(sb, "<", go.InputConnectors, true);
                Ports(sb, ">", go.OutputConnectors, false);
                if (go.EnergyConnector != null && go.EnergyConnector.IsAttached && go.EnergyConnector.AttachedConnector != null)
                    sb.Append("~").Append(Other(go.EnergyConnector.AttachedConnector, go));

                ISimulationObject obj;
                if (results && fs.SimulationObjects.TryGetValue(go.Name, out obj) && obj != null)
                {
                    sb.Append('|').Append(obj.Calculated ? 'c' : '-');
                    var ms = obj as IMaterialStream;
                    if (ms != null)
                        sb.Append('|').Append(Round(ms.GetTemperature())).Append(',').Append(Round(ms.GetPressure()))
                          .Append(',').Append(Round(ms.GetMassFlow()));
                }
                sb.Append('\n');
            }

            var props = fs.ExtraProperties as IDictionary<string, object>;
            object layout;
            if (props != null && props.TryGetValue(LayoutKey, out layout) && layout != null) sb.Append("layout:").Append(layout);
            object site;
            if (props != null && props.TryGetValue(SiteLayout.Key, out site) && site != null) sb.Append("site:").Append(site);
            return sb.ToString();
        }

        static void Ports(StringBuilder sb, string mark, IEnumerable<IConnectionPoint> ports, bool input)
        {
            if (ports == null) return;
            var k = 0;
            foreach (var cp in ports)
            {
                if (cp != null && cp.IsAttached && cp.AttachedConnector != null)
                {
                    var c = cp.AttachedConnector;
                    var other = input ? c.AttachedFrom : c.AttachedTo;
                    sb.Append(mark).Append(k).Append(':').Append(other != null ? other.Name : "?");
                }
                k++;
            }
        }

        static string Other(IConnectorGraphicObject c, IGraphicObject self)
        {
            var other = c.AttachedFrom == self ? c.AttachedTo : c.AttachedFrom;
            return other != null ? other.Name : "?";
        }

        /// <summary>Six significant digits: enough to see a new result, not the noise of a converged one.</summary>
        static string Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return "n";
            return x.ToString("G6", CultureInfo.InvariantCulture);
        }
    }
}
