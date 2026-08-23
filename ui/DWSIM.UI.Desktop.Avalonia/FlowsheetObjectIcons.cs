using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using SkiaSharp;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Gives the two flowsheet colour themes distinct artwork instead of leaving "Default" on the
/// schematic outline (a diamond with a "C", a circle with a zigzag):
///
///   Default     -> the flat icons the Objects palette shows, so the block you drop looks like the
///                  thumbnail you dragged.
///   Color Icons -> the photorealistic images the drawing assembly embeds, matching what the
///                  external biochemical units already do in that theme.
///
/// Both use the engine's existing image path: <c>DrawMode = 2</c> makes ShapeGraphic.DrawPhoto blit
/// whatever is in the object's photo fields. This class only decides which image goes in there.
/// The other themes (black-and-white PFD, the gradient modes) are left untouched.
///
/// The two fields are Friend to the drawing assembly, hence the reflection. The lookups are cached
/// and the class degrades to a no-op if a future engine version renames them, which just restores
/// the stock rendering.
/// </summary>
internal static class FlowsheetObjectIcons
{
    private const int DefaultTheme = 0;
    private const int ColorIconsTheme = 2;
    private const int PhotoDrawMode = 2;

    private static readonly Type GraphicObjectType =
        typeof(DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject);

    private static readonly FieldInfo? PhotoImageField =
        GraphicObjectType.GetField("PhotoImage", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? PhotoNameField =
        GraphicObjectType.GetField("EmbeddedResourcePhotoName", BindingFlags.NonPublic | BindingFlags.Instance);

    public static bool Available => PhotoImageField != null && PhotoNameField != null;

    /// <summary>
    /// Per-object artwork. Keyed weakly on the graphic object, so an object removed from the
    /// flowsheet takes its entry with it and a re-added one starts clean.
    /// </summary>
    private sealed class State
    {
        /// <summary>The embedded photo the object was built with, captured before it is overwritten.</summary>
        public string OriginalPhotoName = "";
        public SKImage? PaletteIcon;
        public SKImage? Photo;
        public bool PaletteTried;
        public bool PhotoTried;
    }

    private static readonly ConditionalWeakTable<IGraphicObject, State> _state = new();

    /// <summary>
    /// Streams are left alone: their graphic IS the connecting arrow, so an icon would hide where
    /// the material goes.
    /// </summary>
    private static bool IsExcluded(Interfaces.Enums.GraphicObjects.ObjectType type) =>
        type == Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream ||
        type == Interfaces.Enums.GraphicObjects.ObjectType.EnergyStream;

    private static bool IsExternal(Interfaces.Enums.GraphicObjects.ObjectType type) =>
        type == Interfaces.Enums.GraphicObjects.ObjectType.External;

    /// <summary>
    /// The private SKImage cache each external unit operation passes ByRef to
    /// BioOpsDrawHelper.TryDrawPhotorealistic, looked up once per concrete type.
    /// </summary>
    private static readonly Dictionary<Type, FieldInfo?> _externalPhotoFields = new();

    private static FieldInfo? ExternalPhotoField(Type type)
    {
        lock (_externalPhotoFields)
        {
            if (_externalPhotoFields.TryGetValue(type, out var cached)) return cached;

            FieldInfo? found = null;
            for (var t = type; t != null && found == null; t = t.BaseType)
                found = t.GetField("_photoImage", BindingFlags.NonPublic | BindingFlags.Instance);

            _externalPhotoFields[type] = found;
            return found;
        }
    }

    /// <summary>
    /// Points every block at the artwork its theme calls for, and reports the theme value that has
    /// to be restored once the frame is drawn.
    ///
    /// DesignSurface.UpdateCanvas opens every frame with UpdateColorTheme(), which overwrites each
    /// object's DrawMode with FlowsheetOptions.FlowsheetColorTheme. Setting DrawMode here would
    /// therefore be undone before anything is painted - under "Default" the blocks would keep
    /// falling back to the outline. So the option itself is switched to the image mode for the
    /// duration of the (synchronous) draw and put back in <see cref="EndDraw"/>, leaving the value
    /// Simulation Settings reads untouched.
    ///
    /// Returns the theme to restore, or null when nothing was overridden.
    /// </summary>
    public static int? BeginDraw(IFlowsheet? flowsheet)
    {
        if (!Available || flowsheet == null) return null;
        if (!UiPreferences.UsePaletteIconsOnCanvas) return null;

        int theme;
        try { theme = flowsheet.FlowsheetOptions.FlowsheetColorTheme; } catch { return null; }
        if (theme != DefaultTheme && theme != ColorIconsTheme) return null;

        List<ISimulationObject> objects;
        try { objects = new List<ISimulationObject>(flowsheet.SimulationObjects.Values); }
        catch { return null; }

        foreach (var so in objects)
        {
            // Per object, so one type that misbehaves cannot abort the whole pass and leave the
            // rest of the flowsheet on the outline rendering.
            try
            {
                var gobj = so?.GraphicObject;
                if (gobj == null || IsExcluded(gobj.ObjectType)) continue;

                if (!_state.TryGetValue(gobj, out var state))
                {
                    state = new State();
                    try { state.OriginalPhotoName = PhotoNameField!.GetValue(gobj) as string ?? ""; }
                    catch { }
                    _state.Add(gobj, state);
                }

                if (IsExternal(gobj.ObjectType))
                    UseExternalArtwork(so!, state, wantPalette: theme == DefaultTheme);
                else if (theme == DefaultTheme)
                    UsePaletteIcon(so!, gobj, state);
                else
                    UseEmbeddedPhoto(gobj, state);
            }
            catch { }
        }

        if (theme == ColorIconsTheme) return null;   // already the mode UpdateColorTheme will set

        try
        {
            flowsheet.FlowsheetOptions.FlowsheetColorTheme = PhotoDrawMode;
            return theme;
        }
        catch { return null; }
    }

    /// <summary>Puts back the theme <see cref="BeginDraw"/> overrode. Safe to call with null.</summary>
    public static void EndDraw(IFlowsheet? flowsheet, int? original)
    {
        if (flowsheet == null || original == null) return;
        try { flowsheet.FlowsheetOptions.FlowsheetColorTheme = original.Value; } catch { }
    }

    /// <summary>
    /// External unit operations (the biochemical blocks and friends) paint themselves in
    /// IExternalUnitOperation.Draw, so the graphic object's photo fields never come into play.
    /// What they do read, in the photo mode this class puts them into, is their own private SKImage
    /// cache - which BioOpsDrawHelper only fills when it is empty. Writing the palette icon into
    /// that cache is therefore enough to give them the same Default-theme look as every other block.
    /// </summary>
    private static void UseExternalArtwork(ISimulationObject so, State state, bool wantPalette)
    {
        var field = ExternalPhotoField(so.GetType());
        if (field == null) return;

        // Whatever the helper loaded for itself is the artwork to put back under Color Icons.
        var current = field.GetValue(so) as SKImage;
        if (current != null && !ReferenceEquals(current, state.PaletteIcon)) state.Photo = current;

        if (wantPalette)
        {
            EnsurePaletteIcon(so, state);
            if (state.PaletteIcon != null) field.SetValue(so, state.PaletteIcon);
            return;
        }

        // Null lets the helper reload its own image and cache it; the branch above captures it on
        // the next frame, so the swap back is a plain assignment from then on.
        if (!ReferenceEquals(current, state.Photo)) field.SetValue(so, state.Photo);
    }

    private static void EnsurePaletteIcon(ISimulationObject so, State state)
    {
        if (state.PaletteTried) return;
        state.PaletteTried = true;
        byte[]? bytes = null;
        try { bytes = so.GetIconBitmapBytes(); } catch { }
        state.PaletteIcon = Decode(bytes);
    }

    private static void UsePaletteIcon(ISimulationObject so, IGraphicObject gobj, State state)
    {
        EnsurePaletteIcon(so, state);

        // No palette artwork for this type: leave the outline the theme would have drawn.
        if (state.PaletteIcon == null) return;

        // A non-empty name is what tells DrawPhoto an image exists; the image is already set, so it
        // never resolves the name against the drawing assembly's resources.
        PhotoNameField!.SetValue(gobj, "palette-icon");
        PhotoImageField!.SetValue(gobj, state.PaletteIcon);
    }

    private static void UseEmbeddedPhoto(IGraphicObject gobj, State state)
    {
        // Nothing was ever swapped in, or the object has no photo of its own: stock behaviour.
        if (state.OriginalPhotoName.Length == 0) return;

        if (!state.PhotoTried)
        {
            state.PhotoTried = true;
            try
            {
                using var stream = GraphicObjectType.Assembly.GetManifestResourceStream(
                    "DWSIM.Drawing.SkiaSharp." + state.OriginalPhotoName);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    state.Photo = Decode(ms.ToArray());
                }
            }
            catch { }
        }

        if (state.Photo == null) return;

        PhotoNameField!.SetValue(gobj, state.OriginalPhotoName);
        PhotoImageField!.SetValue(gobj, state.Photo);
    }

    private static SKImage? Decode(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        try
        {
            using var bitmap = SKBitmap.Decode(bytes);
            return bitmap == null ? null : SKImage.FromBitmap(bitmap);
        }
        catch { return null; }
    }
}
