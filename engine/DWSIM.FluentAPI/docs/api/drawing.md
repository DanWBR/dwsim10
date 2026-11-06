# Drawing: Appearance and Annotations

The flowsheet drawing (the PFD) has a background, a grid and a layout lock of its own, and carries
annotations: text, formatted text, rectangles, pictures, charts and property tables. Everything set here
is saved with the simulation file and is what the Flowsheet Appearance and Edit Appearance windows of
both interfaces edit.

## Background, grid and layout lock

```csharp
fs.WithBackground("#F0F0F0");                       // a plain color, #RRGGBB
fs.WithBackground(22, 34, 58);                      // or red, green, blue
fs.WithBackgroundGradient("#DCE8F4", "#FFFFFF");    // vertical: top of the view to its bottom
fs.WithThemeBackground();                           // back to white, or dark grey with the dark theme

fs.WithGrid(show: true, snap: true);                // show the grid, snap moved objects to it
fs.LockLayout();                                    // nothing can be moved; pan and zoom still work
```

Lines and text turn light on a dark background and dark on a light one. A gradient stays put when
the view is panned or zoomed, and lines and text follow the mean of its two ends, so keep both ends
light or both dark. Drawing a gradient costs the same as a plain color.

## Annotations

Every annotation builder has `PositionAt(x, y)`, `WithSize(width, height)` and `LockPosition()`.
Text, formatted text, pictures and charts also take `Rotate(degrees)`, clockwise about the center.

```csharp
fs.AddText("note", "Feed from tank farm", 40, 40)
  .WithFontSize(14).WithColor("#336699").WithFontStyle(FontStyle.Bold);

fs.AddFormattedText("intro", "<h2>Propane refrigeration</h2><p>Four-stage cycle.</p>", 40, 80, 400, 120)
  .WithFontSize(12).WithColor("#202020");

fs.AddRectangle("area-100", 20, 200, 600, 400)
  .WithText("Section 100", 14).WithGradient("#EEF3F8", "#B0C4DE").WithOpacity(80);

fs.AddImage("logo", "logo.png", 900, 20)            // embedded in the file, at its own size
  .WithWidth(240)                                   // the height follows: proportions are kept
  .FlipHorizontal();
```

| Builder | What it adds |
|---|---|
| `AddText` | plain text: `WithText`, `WithFontSize`, `WithColor`, `WithFontStyle`, `Rotate` |
| `AddFormattedText` | HTML text laid out in its width and height: `WithHtml`, `WithFontSize`, `WithColor`, `WithFontStyle`, `Rotate` |
| `AddRectangle` | a frame with optional text: `WithText`, `WithTextColor`, `WithFill`, `WithGradient`, `WithOpacity`, `WithBorderWidth`, `WithRoundedCorners` |
| `AddImage` | a picture from a file: `WithWidth`, `WithHeight`, `KeepProportions`, `OriginalSize`, `FlipHorizontal`, `FlipVertical`, `Rotate` |
| `AddChart` | a chart: `ForIntegrator`, `ForObject`, `ForChart`, `Rotate` |
| `AddPropertyTable` | chosen properties of objects: `Show`, `WithHeader` |
| `AddMasterTable` | the same properties for a family of objects: `OfFamily`, `Including`, `WithProperties`, `SortBy` |

A picture keeps its proportions by default: `WithWidth` and `WithHeight` change the other dimension at
the ratio the picture has on the flowsheet, so a stretched picture stays stretched.
`KeepProportions(false)` turns that off, and `OriginalSize()` goes back to one flowsheet pixel per
picture pixel. `WithSize` always sets both dimensions as given.

`fs.Annotations` lists the annotations of the flowsheet.
