using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWSIM.Drawing.SkiaSharp.Appearance;
using DWSIM.Drawing.SkiaSharp.GraphicObjects;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes;
using DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using SkiaSharp;
using FB = DWSIM.FlowsheetBase.FlowsheetBase;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// The annotations of a flowsheet: chart objects, property tables, master tables, text, formatted text,
    /// rectangles and pictures.
    /// They are graphics with no simulation object behind them; each builder places one on the drawing
    /// surface and wires what it shows.
    /// </summary>
    public sealed partial class Flowsheet
    {
        private IGraphicObject AddAnnotation(ObjectType type, string tag, int x, int y, SKImage image = null)
        {
            var fb = Inner as FB;
            if (fb == null) throw new InvalidOperationException("Annotations need a FlowsheetBase host.");
            var g = fb.AddGraphicObject(type, x, y, tag ?? "", image);
            if (g == null) throw new InvalidOperationException("'" + type + "' is not an annotation type.");
            return g;
        }

        /// <summary>A chart object on the PFD. Bind it with <see cref="ChartObjectBuilder.ForIntegrator"/> (the
        /// monitored variables of a dynamics integrator), <see cref="ChartObjectBuilder.ForObject"/> (a chart a
        /// unit operation offers, such as a column's temperature profile) or <see cref="ChartObjectBuilder.ForChart"/>
        /// (a chart of the flowsheet's chart collection).</summary>
        public ChartObjectBuilder AddChart(string tag, int x, int y, int width = 500, int height = 400)
        {
            var g = (OxyPlotGraphic)AddAnnotation(ObjectType.GO_Chart, tag, x, y);
            g.Width = width; g.Height = height;
            return new ChartObjectBuilder(this, g);
        }

        /// <summary>A property table on the PFD, showing chosen properties of one or more objects.</summary>
        public PropertyTableBuilder AddPropertyTable(string tag, int x, int y)
            => new PropertyTableBuilder(this, (TableGraphic)AddAnnotation(ObjectType.GO_Table, tag, x, y));

        /// <summary>A master table on the PFD: the same properties for every object of one family (all the
        /// material streams, say), one column per object.</summary>
        public MasterTableBuilder AddMasterTable(string tag, int x, int y)
            => new MasterTableBuilder(this, (MasterTableGraphic)AddAnnotation(ObjectType.GO_MasterTable, tag, x, y));

        /// <summary>A text note on the PFD.</summary>
        public TextBuilder AddText(string tag, string text, int x, int y)
        {
            var g = (TextGraphic)AddAnnotation(ObjectType.GO_Text, tag, x, y);
            g.Text = text;
            return new TextBuilder(this, g);
        }

        /// <summary>A picture on the PFD, read from an image file (PNG, JPEG, BMP, ...) and embedded in the
        /// simulation file. The size defaults to the image's own.</summary>
        public ImageBuilder AddImage(string tag, string imagePath, int x, int y, int width = 0, int height = 0)
        {
            if (!File.Exists(imagePath)) throw new FileNotFoundException("Image not found.", imagePath);
            var image = SKImage.FromEncodedData(imagePath);
            if (image == null) throw new InvalidOperationException("'" + imagePath + "' is not an image the flowsheet can embed.");
            var g = (EmbeddedImageGraphic)AddAnnotation(ObjectType.GO_Image, tag, x, y, image);
            if (width > 0) g.Width = width;
            if (height > 0) g.Height = height;
            return new ImageBuilder(this, g);
        }

        /// <summary>A rectangle on the PFD, to frame a section of the drawing, with optional text inside.</summary>
        public RectangleBuilder AddRectangle(string tag, int x, int y, int width, int height)
        {
            var g = (RectangleGraphic)AddAnnotation(ObjectType.GO_Rectangle, tag, x, y);
            g.Width = width; g.Height = height;
            g.Text = "";
            return new RectangleBuilder(this, g);
        }

        /// <summary>Formatted text on the PFD, written as HTML (headings, bold, lists). Its width and height bound the
        /// area the text is laid out in.</summary>
        public FormattedTextBuilder AddFormattedText(string tag, string html, int x, int y, int width = 400, int height = 200)
        {
            var g = (HTMLTextGraphic)AddAnnotation(ObjectType.GO_HTMLText, tag, x, y);
            g.Text = html;
            g.Width = width; g.Height = height;
            return new FormattedTextBuilder(this, g);
        }

        /// <summary>The annotations of the flowsheet, by tag.</summary>
        public IReadOnlyList<IGraphicObject> Annotations
            => ((DWSIM.Drawing.SkiaSharp.GraphicsSurface)Inner.GetSurface()).DrawingObjects.Cast<IGraphicObject>()
                .Where(o => o.ObjectType == ObjectType.GO_Chart || o.ObjectType == ObjectType.GO_Table ||
                            o.ObjectType == ObjectType.GO_MasterTable || o.ObjectType == ObjectType.GO_Text ||
                            o.ObjectType == ObjectType.GO_Image || o.ObjectType == ObjectType.GO_Rectangle ||
                            o.ObjectType == ObjectType.GO_HTMLText || o.ObjectType == ObjectType.GO_SpreadsheetTable)
                .ToList();
    }

    /// <summary>What every annotation builder shares: position, size and the graphic itself.</summary>
    public abstract class AnnotationBuilder<TGraphic, TSelf>
        where TGraphic : class, IGraphicObject
        where TSelf : AnnotationBuilder<TGraphic, TSelf>
    {
        protected AnnotationBuilder(Flowsheet flowsheet, TGraphic graphic) { Flowsheet = flowsheet; Object = graphic; }
        public Flowsheet Flowsheet { get; }
        public TGraphic Object { get; }
        public string Tag => Object.Tag;
        protected TSelf Self => (TSelf)this;
        public TSelf PositionAt(int x, int y) { Object.X = x; Object.Y = y; return Self; }
        public TSelf WithSize(int width, int height) { Object.Width = width; Object.Height = height; return Self; }

        /// <summary>Keeps the annotation where it is: dragging, aligning, snapping and the automatic layouts leave it
        /// in place.</summary>
        public TSelf LockPosition(bool locked = true) { Object.PositionLocked = locked; return Self; }

        /// <summary>Clockwise rotation about the center, in degrees. Offered by the builders whose annotation is drawn
        /// rotated (text, formatted text, pictures and charts).</summary>
        protected TSelf RotateBy(int degrees) { Object.Rotation = ((degrees % 360) + 360) % 360; return Self; }
    }

    public sealed class ChartObjectBuilder : AnnotationBuilder<OxyPlotGraphic, ChartObjectBuilder>
    {
        internal ChartObjectBuilder(Flowsheet flowsheet, OxyPlotGraphic graphic) : base(flowsheet, graphic) { }

        /// <summary>Clockwise rotation about the center, in degrees.</summary>
        public ChartObjectBuilder Rotate(int degrees) => RotateBy(degrees);

        /// <summary>Shows the monitored variables of a dynamics integrator, as the integrator window plots them.</summary>
        public ChartObjectBuilder ForIntegrator(string integratorName)
        {
            var known = Flowsheet.Inner.DynamicsManager.IntegratorList.Values.Select(i => i.Description).ToList();
            if (!known.Contains(integratorName))
                throw new KeyNotFoundException("There isn't an integrator named '" + integratorName + "'. Defined: " + (known.Count == 0 ? "none" : string.Join(", ", known)) + ".");
            Object.OwnerID = "Dynamic Mode Integrators";
            Object.ModelName = integratorName;
            return this;
        }

        /// <summary>Shows one of the charts a simulation object offers (a column's "Temperature Profile", say).</summary>
        public ChartObjectBuilder ForObject(string objectTag, string chartName)
        {
            var obj = Flowsheet.ResolveByTag(objectTag);
            var names = obj.GetChartModelNames() ?? new List<string>();
            if (!names.Contains(chartName))
                throw new KeyNotFoundException("'" + objectTag + "' doesn't have a chart '" + chartName + "'. Available: " + (names.Count == 0 ? "none" : string.Join(", ", names)) + ".");
            Object.OwnerID = obj.Name;
            Object.ModelName = chartName;
            return this;
        }

        /// <summary>Shows a chart of the flowsheet's chart collection, by its display name.</summary>
        public ChartObjectBuilder ForChart(string chartDisplayName)
        {
            Object.OwnerID = "Chart Objects";
            Object.ModelName = chartDisplayName;
            return this;
        }
    }

    public sealed class PropertyTableBuilder : AnnotationBuilder<TableGraphic, PropertyTableBuilder>
    {
        internal PropertyTableBuilder(Flowsheet flowsheet, TableGraphic graphic) : base(flowsheet, graphic) { }

        /// <summary>Adds an object to the table with the properties to show (property ids as the object's
        /// GetProperties lists them, "PROP_MS_2" for a stream's mass flow, say). With no ids, every property
        /// the object reports is shown.</summary>
        public PropertyTableBuilder Show(string objectTag, params string[] propertyIds)
        {
            var obj = Flowsheet.ResolveByTag(objectTag);
            var all = obj.GetProperties(Interfaces.Enums.PropertyType.ALL) ?? new string[0];
            var ids = propertyIds == null || propertyIds.Length == 0 ? all.ToList() : propertyIds.ToList();
            var unknown = ids.Where(id => !all.Contains(id)).ToList();
            if (unknown.Count > 0)
                throw new KeyNotFoundException("'" + objectTag + "' doesn't have the property " + string.Join(", ", unknown.Select(u => "'" + u + "'")) + ".");
            Object.VisibleProperties[obj.Name] = ids;
            return this;
        }

        public PropertyTableBuilder WithHeader(string text) { Object.HeaderText = text; return this; }
    }

    public sealed class MasterTableBuilder : AnnotationBuilder<MasterTableGraphic, MasterTableBuilder>
    {
        internal MasterTableBuilder(Flowsheet flowsheet, MasterTableGraphic graphic) : base(flowsheet, graphic) { }

        /// <summary>The family of objects the table lists (material streams, energy streams, one kind of unit
        /// operation). Setting a new family clears the objects and properties chosen before.</summary>
        public MasterTableBuilder OfFamily(ObjectType family) { Object.ObjectFamily = family; return this; }

        /// <summary>The objects to list, by tag; with none, every object of the family is listed.</summary>
        public MasterTableBuilder Including(params string[] objectTags)
        {
            var tags = objectTags == null || objectTags.Length == 0
                ? Flowsheet.Inner.SimulationObjects.Values.Where(o => o.GraphicObject.ObjectType == Object.ObjectFamily).Select(o => o.GraphicObject.Tag).ToList()
                : objectTags.ToList();
            foreach (var t in tags)
            {
                Flowsheet.ResolveByTag(t);
                Object.ObjectList[t] = true;
            }
            return this;
        }

        /// <summary>The properties to show for every object, by property id.</summary>
        public MasterTableBuilder WithProperties(params string[] propertyIds)
        {
            foreach (var id in propertyIds) Object.PropertyList[id] = true;
            return this;
        }

        public MasterTableBuilder WithHeader(string text) { Object.HeaderText = text; return this; }

        /// <summary>The column order: "Name | ASC", "Name | DESC" or a property description with " | ASC"/" | DESC".</summary>
        public MasterTableBuilder SortBy(string sortBy) { Object.SortBy = sortBy; return this; }
    }

    public sealed class TextBuilder : AnnotationBuilder<TextGraphic, TextBuilder>
    {
        internal TextBuilder(Flowsheet flowsheet, TextGraphic graphic) : base(flowsheet, graphic) { }
        public TextBuilder WithText(string text) { Object.Text = text; return this; }
        public TextBuilder WithFontSize(double size) { Object.Size = size; return this; }
        public TextBuilder WithColor(byte red, byte green, byte blue) { Object.Color = new SKColor(red, green, blue); return this; }
        /// <summary>The text color as "#RRGGBB". On a dark background the text is drawn light.</summary>
        public TextBuilder WithColor(string color) { Object.Color = Flowsheet.ParseColor(color); return this; }
        public TextBuilder WithFontStyle(FontStyle style) { Object.FontStyle = style; return this; }
        /// <summary>Clockwise rotation about the center, in degrees (90 runs the text down the page).</summary>
        public TextBuilder Rotate(int degrees) => RotateBy(degrees);
    }

    public sealed class FormattedTextBuilder : AnnotationBuilder<HTMLTextGraphic, FormattedTextBuilder>
    {
        internal FormattedTextBuilder(Flowsheet flowsheet, HTMLTextGraphic graphic) : base(flowsheet, graphic) { }
        public FormattedTextBuilder WithHtml(string html) { Object.Text = html; return this; }
        /// <summary>The size of text that has no size of its own in the HTML, in points.</summary>
        public FormattedTextBuilder WithFontSize(double size) { Object.Size = size; return this; }
        /// <summary>The color of text that has no color of its own in the HTML, as "#RRGGBB".</summary>
        public FormattedTextBuilder WithColor(string color) { Object.Color = Flowsheet.ParseColor(color); return this; }
        public FormattedTextBuilder WithFontStyle(FontStyle style) { Object.FontStyle = style; return this; }
        /// <summary>Clockwise rotation about the center, in degrees.</summary>
        public FormattedTextBuilder Rotate(int degrees) => RotateBy(degrees);
    }

    public sealed class RectangleBuilder : AnnotationBuilder<RectangleGraphic, RectangleBuilder>
    {
        internal RectangleBuilder(Flowsheet flowsheet, RectangleGraphic graphic) : base(flowsheet, graphic) { }
        /// <summary>Text written inside the rectangle, at its top.</summary>
        public RectangleBuilder WithText(string text, double fontSize = 0)
        {
            Object.Text = text;
            if (fontSize > 0) Object.FontSize = fontSize;
            return this;
        }
        /// <summary>The color of the text inside the rectangle, as "#RRGGBB".</summary>
        public RectangleBuilder WithTextColor(string color) { Object.FontColor = Flowsheet.ParseColor(color); return this; }
        /// <summary>A plain fill, as "#RRGGBB" (drawn semi-transparent).</summary>
        public RectangleBuilder WithFill(string color) { Object.GradientMode = false; Object.FillColor = Flowsheet.ParseColor(color); return this; }
        /// <summary>A vertical gradient fill from <paramref name="top"/> to <paramref name="bottom"/>, as "#RRGGBB";
        /// the border is drawn in the bottom color.</summary>
        public RectangleBuilder WithGradient(string top, string bottom)
        {
            Object.GradientMode = true;
            Object.GradientColor1 = Flowsheet.ParseColor(top);
            Object.GradientColor2 = Flowsheet.ParseColor(bottom);
            return this;
        }
        /// <summary>Opacity of the gradient fill and the border, 0 (transparent) to 255 (opaque).</summary>
        public RectangleBuilder WithOpacity(int opacity) { Object.Opacity = Math.Max(0, Math.Min(255, opacity)); return this; }
        /// <summary>Border thickness, in pixels.</summary>
        public RectangleBuilder WithBorderWidth(int width) { Object.LineWidth = Math.Max(1, width); return this; }
        public RectangleBuilder WithRoundedCorners(bool rounded = true) { Object.RoundEdges = rounded; return this; }
    }

    public sealed class ImageBuilder : AnnotationBuilder<EmbeddedImageGraphic, ImageBuilder>
    {
        internal ImageBuilder(Flowsheet flowsheet, EmbeddedImageGraphic graphic) : base(flowsheet, graphic) { }

        /// <summary>
        /// Whether a new width or height takes the other dimension along at the picture's current proportions (on by
        /// default; applies to <see cref="WithWidth"/> and <see cref="WithHeight"/> and to the appearance editors).
        /// </summary>
        public ImageBuilder KeepProportions(bool keep = true) { Object.KeepAspectRatio = keep; return this; }

        /// <summary>A new width; with proportions kept, the height follows.</summary>
        public ImageBuilder WithWidth(int width) { AppearanceDescriptors.Find("Width").SetValue(Object, width); return this; }

        /// <summary>A new height; with proportions kept, the width follows.</summary>
        public ImageBuilder WithHeight(int height) { AppearanceDescriptors.Find("Height").SetValue(Object, height); return this; }

        /// <summary>The size of the picture itself, one flowsheet pixel per picture pixel.</summary>
        public ImageBuilder OriginalSize() { AppearanceDescriptors.SetOriginalSize(Object); return this; }

        public ImageBuilder FlipHorizontal(bool flipped = true) { Object.FlippedH = flipped; return this; }
        public ImageBuilder FlipVertical(bool flipped = true) { Object.FlippedV = flipped; return this; }

        /// <summary>Clockwise rotation about the center, in degrees.</summary>
        public ImageBuilder Rotate(int degrees) => RotateBy(degrees);
    }
}
