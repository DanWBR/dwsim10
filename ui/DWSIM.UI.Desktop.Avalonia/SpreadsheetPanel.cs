using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.SharedClasses.Spreadsheet;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using unvell.ReoGrid;
using unvell.ReoGrid.DataFormat;
using unvell.ReoGrid.Formula;
using Cell = unvell.ReoGrid.Cell;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Avalonia wrapper around ReoGrid spreadsheet control.
/// Mirrors the integration done in DWSIM.UI.Desktop.Editors.Spreadsheet (Eto)
/// but uses the Avalonia-native ReoGridControl from ReoGrid.Avalonia.
/// </summary>
public sealed class SpreadsheetPanel
{
    private readonly IFlowsheet _flowsheet;

    /// <summary>The underlying ReoGrid control (Avalonia UserControl).</summary>
    public ReoGridControl Grid { get; }

    /// <summary>
    /// Object list kept in sync with the flowsheet.
    /// Used by custom formula functions to resolve names/IDs.
    /// </summary>
    public Dictionary<string, ISimulationObject>? ObjList { get; set; }

    /// <summary>Gate for SETPROPVAL: only writes when true.</summary>
    public bool Loaded { get; set; } = true;

    private readonly List<string> _columns = new()
    {
        "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P",
        "Q","R","S","T","U","V","W","X","Y","Z"
    };

    public SpreadsheetPanel(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        Grid = new ReoGridControl();
        Grid.CurrentWorksheet.Name = "MAIN";

        RegisterCustomFunctions();

        // Wire up the flowsheet callback so the engine can reach the grid.
        // GetSpreadsheetObjectFunc is defined on FlowsheetBase, not IFlowsheet.
        if (flowsheet is DWSIM.FlowsheetBase.FlowsheetBase fb)
            fb.GetSpreadsheetObjectFunc = () => Grid;
    }

    // -----------------------------------------------------------------
    // Custom DWSIM formula functions
    // -----------------------------------------------------------------

    private void RegisterCustomFunctions()
    {
        FormulaExtension.CustomFunctions["GETNAME"] = (cell, args) =>
        {
            try
            {
                return _flowsheet.SimulationObjects[args[0].ToString()!].GraphicObject.Tag;
            }
            catch (Exception ex) { return "ERROR: " + ex.Message; }
        };

        FormulaExtension.CustomFunctions["GETPROPVAL"] = (cell, args) =>
        {
            if (args.Length == 2)
            {
                try
                {
                    return _flowsheet.SimulationObjects[args[0].ToString()!]
                        .GetPropertyValue(args[1].ToString()!);
                }
                catch (Exception ex) { return "ERROR: " + ex.Message; }
            }
            if (args.Length == 3)
            {
                try
                {
                    var obj = _flowsheet.SimulationObjects[args[0].ToString()!];
                    var val = obj.GetPropertyValue(args[1].ToString()!);
                    var fromUnits = obj.GetPropertyUnit(args[1].ToString()!);
                    var toUnits = args[2].ToString()!;
                    var dval = double.Parse(val!.ToString()!);
                    return cv.ConvertFromSI(toUnits, cv.ConvertToSI(fromUnits, dval));
                }
                catch (Exception ex) { return "ERROR: " + ex.Message; }
            }
            return "INVALID ARGS";
        };

        FormulaExtension.CustomFunctions["SETPROPVAL"] = (cell, args) =>
        {
            if (args.Length == 3)
            {
                try
                {
                    if (!Loaded) return "NOT READY";
                    var ws = cell.Worksheet;
                    int tr = ws.RowCount - 1, tc = ws.ColumnCount - 1;
                    var wcell = ws.Cells[tr, tc];
                    wcell.Data = null;
                    wcell.Formula = args[2].ToString()!.Trim('"');
                    ws.RecalcCell(tr, tc);
                    var val = wcell.Data ?? (object)wcell.Formula;
                    _flowsheet.SimulationObjects[args[0].ToString()!]
                        .SetPropertyValue(args[1].ToString()!, val);
                    wcell.Formula = null;
                    wcell.Data = null;
                    return $"EXPORT OK [{_flowsheet.SimulationObjects[args[0].ToString()!].GraphicObject.Tag}, {args[1]} = {val}]";
                }
                catch (Exception ex) { return "ERROR: " + ex.Message; }
            }
            if (args.Length == 4)
            {
                try
                {
                    if (!Loaded) return "NOT READY";
                    var obj = _flowsheet.SimulationObjects[args[0].ToString()!];
                    var prop = args[1].ToString()!;
                    var ws = cell.Worksheet;
                    int tr = ws.RowCount - 1, tc = ws.ColumnCount - 1;
                    var wcell = ws.Cells[tr, tc];
                    wcell.Formula = args[2].ToString()!.Trim('"');
                    ws.RecalcCell(tr, tc);
                    var val = wcell.Data;
                    wcell.Formula = "";
                    wcell.Data = "";
                    var units = args[3].ToString()!;
                    var newval = cv.ConvertFromSI(obj.GetPropertyUnit(prop),
                        cv.ConvertToSI(units, double.Parse(val!.ToString()!)));
                    obj.SetPropertyValue(prop, newval);
                    return $"EXPORT OK [{obj.GraphicObject.Tag}, {prop} = {val} {units}]";
                }
                catch (Exception ex) { return "ERROR: " + ex.Message; }
            }
            return "INVALID ARGS";
        };

        FormulaExtension.CustomFunctions["GETPROPUNITS"] = (cell, args) =>
        {
            if (args.Length == 2)
            {
                try
                {
                    return _flowsheet.SimulationObjects[args[0].ToString()!]
                        .GetPropertyUnit(args[1].ToString()!);
                }
                catch (Exception ex) { return "ERROR: " + ex.Message; }
            }
            return "INVALID ARGS";
        };

        FormulaExtension.CustomFunctions["GETOBJID"] = (cell, args) =>
        {
            if (args.Length == 1)
            {
                try
                {
                    return _flowsheet.GetFlowsheetSimulationObject(args[0].ToString()!).Name;
                }
                catch (Exception ex) { return "ERROR: " + ex.Message; }
            }
            return "INVALID ARGS";
        };

        FormulaExtension.CustomFunctions["GETOBJNAME"] = (cell, args) =>
        {
            if (args.Length == 1)
            {
                try
                {
                    return _flowsheet.SimulationObjects[args[0].ToString()!].GraphicObject.Tag;
                }
                catch (Exception ex) { return "ERROR: " + ex.Message; }
            }
            return "INVALID ARGS";
        };
    }

    // -----------------------------------------------------------------
    // Recalculate all worksheets
    // -----------------------------------------------------------------

    public void WriteAll()
    {
        foreach (var ws in Grid.Worksheets)
            ws.Recalculate();
    }

    public void EvaluateAll() => WriteAll();

    // -----------------------------------------------------------------
    // Data retrieval (used by flowsheet callbacks)
    // -----------------------------------------------------------------

    public List<string[]> GetDataFromRange(string range)
    {
        var list = new List<string[]>();
        var rdata = Grid.Worksheets[0].GetRangeData(new RangePosition(range));
        for (var i = 0; i < rdata.GetLength(0); i++)
        {
            var row = new List<string>();
            for (var j = 0; j < rdata.GetLength(1); j++)
                row.Add(rdata[i, j] != null ? rdata[i, j].ToString()! : "");
            list.Add(row.ToArray());
        }
        return list;
    }

    public List<string[]> GetFormatFromRange(string range)
    {
        var list = new List<string[]>();
        var rdata = Grid.Worksheets[0].GetRangeData(new RangePosition(range));
        for (var i = 0; i < rdata.GetLength(0); i++)
        {
            var row = new List<string>();
            for (var j = 0; j < rdata.GetLength(1); j++)
            {
                var format = Grid.Worksheets[0].Cells[i, j].DataFormat;
                if (format == CellDataFormatFlag.Number)
                {
                    var args = (NumberDataFormatter.NumberFormatArgs)Grid.Worksheets[0].Cells[i, j].DataFormatArgs;
                    row.Add("N" + args.DecimalPlaces);
                }
                else
                {
                    row.Add("");
                }
            }
            list.Add(row.ToArray());
        }
        return list;
    }

    // -----------------------------------------------------------------
    // Saved-data loading (DT1 = values/formulas, DT2 = cell metadata)
    // -----------------------------------------------------------------

    public List<List<object>> dt1 = new();
    public List<List<object>> dt2 = new();

    public void CopyDT1FromString(string text)
    {
        dt1.Clear();
        var ci = CultureInfo.InvariantCulture;
        var format = NumberStyles.Any & ~NumberStyles.AllowThousands;
        var rows = text.Split('|');
        int n = rows.Length - 1;
        if (n <= 0) return;

        int m = 0;
        for (int i = 0; i <= n; i++)
        {
            var cols = rows[i].Split(';');
            if (cols.Length - 1 > m) m = cols.Length - 1;
            var rowData = new List<object>();
            for (int j = 0; j < cols.Length; j++)
            {
                var value = cols[j].TrimStart();
                if (string.IsNullOrEmpty(value))
                    rowData.Add("");
                else if (double.TryParse(value, format, ci, out double dval))
                    rowData.Add(dval);
                else
                    rowData.Add(value);
            }
            dt1.Add(rowData);
        }
    }

    public void CopyDT2FromString(string text)
    {
        dt2.Clear();
        var rows = text.Split('|');
        int n = rows.Length - 1;
        if (n <= 0) return;

        for (int i = 0; i <= n; i++)
        {
            var cols = rows[i].Split(';');
            var rowData = new List<object>();
            for (int j = 0; j < cols.Length; j++)
                rowData.Add(cols[j].TrimStart());
            dt2.Add(rowData);
        }
    }

    /// <summary>
    /// Populate the grid from dt1 (values/formulas) and dt2 (cell metadata/formatting).
    /// Matches the Eto Spreadsheet.CopyFromDT() logic.
    /// </summary>
    public void CopyFromDT()
    {
        int n1 = dt1.Count - 1;
        if (n1 < 0) return;

        int m1 = dt1[0].Count - 1;
        int n2 = dt2.Count - 1;

        var ws = Grid.Worksheets[0];
        int maxrow = ws.RowCount - 1;
        int maxcol = ws.ColumnCount - 1;

        for (int i = 0; i <= n1; i++)
        {
            for (int j = 0; j <= m1; j++)
            {
                if (i <= maxrow && j <= dt1[i].Count - 1)
                {
                    if (dt1[i][j] != null)
                        ws.Cells[i, j].Data = dt1[i][j];
                }

                // Apply dt2 metadata (SpreadsheetCellParameters or plain strings)
                if (n2 >= 0 && i <= n2 && j < dt2[i].Count)
                {
                    if (dt2[i][j] == null)
                    {
                        ws.Cells[i, j].Tag = new SpreadsheetCellParameters();
                    }
                    else if (dt2[i][j] is SpreadsheetCellParameters scp)
                    {
                        ws.Cells[i, j].Tag = scp;
                    }
                    else if (dt2[i][j] is string sval && !string.IsNullOrWhiteSpace(sval))
                    {
                        var cellparam = new SpreadsheetCellParameters();
                        try
                        {
                            cellparam.Expression = sval;
                            if (cellparam.Expression.StartsWith(":"))
                            {
                                cellparam.CellType = VarType.Read;
                                var str = cellparam.Expression.Split(',');
                                cellparam.ObjectID = str[0].Substring(1);
                                cellparam.PropID = str[1];
                            }
                            else
                            {
                                cellparam.CellType = VarType.Expression;
                            }
                        }
                        catch { }
                        ws.Cells[i, j].Tag = cellparam;
                    }
                }
            }
        }
    }

}
