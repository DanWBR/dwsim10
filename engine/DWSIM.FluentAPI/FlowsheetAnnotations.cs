using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// The annotations of a flowsheet: chart objects, property tables, master tables, text and pictures.
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
    }

    public sealed class ChartObjectBuilder : AnnotationBuilder<OxyPlotGraphic, ChartObjectBuilder>
    {
        internal ChartObjectBuilder(Flowsheet flowsheet, OxyPlotGraphic graphic) : base(flowsheet, graphic) { }

        /// <summary>Shows the monitored variables of a dynamics integrator, as the integrator window plots them.</summary>
        public ChartObjectBuilder ForIntegrator(string integratorName)
        {
            var known = Flowsheet.Inner.DynamicsManager.IntegratorList.Values.Select(i => i.Description).ToList();
            if (!known.Contains(integratorName))
                throw new KeyNotFoundException("No integrator named '" + integratorName + "'. Defined: " + (known.Count == 0 ? "none" : string.Join(", ", known)) + ".");
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
                throw new KeyNotFoundException("'" + objectTag + "' has no chart '" + chartName + "'. Available: " + (names.Count == 0 ? "none" : string.Join(", ", names)) + ".");
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
                throw new KeyNotFoundException("'" + objectTag + "' has no property " + string.Join(", ", unknown.Select(u => "'" + u + "'")) + ".");
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
    }

    public sealed class ImageBuilder : AnnotationBuilder<EmbeddedImageGraphic, ImageBuilder>
    {
        internal ImageBuilder(Flowsheet flowsheet, EmbeddedImageGraphic graphic) : base(flowsheet, graphic) { }
    }
}
