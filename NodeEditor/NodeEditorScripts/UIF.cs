using System;
using UnityEngine;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// Thin wrapper over UI Factory 3 (https://gitlab.com/dagriefaa/ui-factory-3),
    /// Workshop item 2913469777, which ships Besiege's real interface as Unity
    /// prefabs. Instantiating those is the only way a mod panel can look like part
    /// of the game: Besiege's own panel materials are reachable only from inside a
    /// custom mapper selector, and `InternalModding` is blacklisted.
    ///
    /// UI Factory is a **soft** dependency. Without it the block falls back to
    /// Besiege's own mapper and the panel simply never appears. That costs one
    /// rule: every mention of `Besiege.UI` in this mod lives in this file. A type
    /// that cannot be resolved fails as the method mentioning it is compiled, so
    /// confining the mentions here means one guarded call decides whether the
    /// panel can exist at all -- see <see cref="Available"/>.
    /// </summary>
    public static class UIF
    {
        /// <summary>The name UI Factory registers its own prefabs under.</summary>
        public const string Package = "UIFactory3";

        public const string WindowPrefab = "Window";
        public const string TextPrefab = "Text";

        /// <summary>A real toggle, not a button painted to look like one.</summary>
        public const string TogglePrefab = "Text Toggle";

        /// <summary>A button with a word on it, as Besiege's own dialogs use.</summary>
        public const string ButtonPrefab = "Text Button";

        /// <summary>Besiege's own text box. It carries the behaviour that stops the
        /// game's hotkeys firing at whatever is being typed into it.</summary>
        public const string InputPrefab = "Input Field";

        /// <summary>A slider. The node editor's colour bands are drawn over one.
        /// </summary>
        public const string SliderPrefab = "Slider";

        /// <summary>
        /// The tooltip panel on its own -- background, label and pointing triangle,
        /// with no behaviour on it. UI Factory splits the two halves; the other one,
        /// `Besiege.UI.Bridge.Tooltip`, decides when to show a panel and is not used
        /// here (see <see cref="Tip"/> for why). Note the registered name really is
        /// "Vis Only" while the asset it comes from is named "(Visual)".
        /// </summary>
        public const string TooltipPrefab = "Text Tooltip (Vis Only)";

        /// <summary>The lettering colour for an answer.</summary>
        public static readonly Color Ink = Color.white;

        /// <summary>For text that is not an answer: a heading, a ghost prompt, a
        /// row that is following something else.</summary>
        public static readonly Color QuietInk = new Color(0.72f, 0.72f, 0.74f, 1f);

        /// <summary>Besiege's red, which is what the game paints the option in
        /// force. Kept here rather than read from `Besiege.UI.Consts` so the
        /// panel's colours are not another thing that has to resolve first.</summary>
        public static readonly Color Hot = new Color(0.92f, 0.13f, 0.29f, 1f);

        /// <summary>The panel's own dark plate, for something drawn on top of the
        /// window rather than in it -- the variable list. Near enough the window's
        /// own background to belong to it, opaque enough to read words on.</summary>
        public static readonly Color Shade = new Color(0.10f, 0.13f, 0.17f, 0.97f);

        /// <summary>The cyan Besiege uses for a reset, and here for a row that is
        /// running.</summary>
        public static readonly Color Live = new Color(0.012f, 1f, 0.847f, 1f);

        private static bool asked;
        private static bool available;

        /// <summary>
        /// Whether UI Factory is installed and has finished loading its bundle.
        ///
        /// This is the guarded call site the whole soft dependency rests on.
        /// Touching `Besiege.UI` at all is what fails when the mod is absent, and
        /// it fails as the *calling* method is compiled -- so the try has to be
        /// here, one call away from any of the panel's own code.
        /// </summary>
        public static bool Available
        {
            get
            {
                if (asked)
                {
                    return available;
                }
                try
                {
                    available = Besiege.UI.Make.Instance != null
                             && Modding.ModResource.AllResourcesLoaded;
                }
                catch (Exception)
                {
                    available = false;
                }
                // Only remembered once it is true: UI Factory loads its bundle a
                // moment after the mod does, and "not yet" is not "not installed".
                asked = available;
                return available;
            }
        }

        /// <summary>
        /// Whether tooltips are wanted at all.
        ///
        /// `Besiege.UI.Bridge.Tooltip.TooltipsActive` is a static `Func&lt;bool&gt;`
        /// that switches every tooltip in the game off at once. This panel does not
        /// use that handler, but it should answer to the same switch -- a player who
        /// has turned tooltips off has turned them off.
        /// </summary>
        public static bool TooltipsWanted
        {
            get
            {
                try
                {
                    return Besiege.UI.Bridge.Tooltip.TooltipsActive == null
                        || Besiege.UI.Bridge.Tooltip.TooltipsActive();
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        /// <summary>Besiege's own lettering, for the controls this mod builds
        /// itself. Null without UI Factory, which every caller answers with Unity's
        /// built-in Arial.</summary>
        public static Font Font
        {
            get
            {
                try { return Besiege.UI.Make.Font; }
                catch (Exception) { return null; }
            }
        }

        /// <summary>
        /// Instantiates one of UI Factory's prefabs, with its translators already
        /// stripped. Returns null and says why rather than throwing into a caller
        /// that is halfway through building a window.
        /// </summary>
        /// <summary>How many prefabs have been spawned, for the one line this mod
        /// logs about how long its first window took to build. A prefab is the
        /// expensive part of building one, and knowing how many were made is what
        /// says whether a stall is this mod's or the game's.</summary>
        public static int Spawned;

        public static GameObject Spawn(string prefab, Transform parent)
        {
            try
            {
                GameObject made = Besiege.UI.Make.Prefab(Package, prefab, parent);
                Spawned++;
                Untranslate(made);
                return made;
            }
            catch (Exception e)
            {
                Log.Warn("UI Factory has no prefab '" + prefab + "': " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Switches off every Translator in a spawned prefab.
        ///
        /// One would put the prefab's own wording back at the next language
        /// change, and -- fatally -- its `Start` calls `Recaption`, which throws on
        /// a label with no localisation key. That is every label a mod writes, and
        /// it takes the whole panel build with it. The whole hierarchy including
        /// inactive objects, since a Translator is not always on the object
        /// carrying the Text.
        ///
        /// Disabled rather than destroyed: Unity does not call `Start` on a
        /// component that is off, which is the whole of what has to be prevented,
        /// and `DestroyImmediate` on every label of every prefab is a real part of
        /// what building a window costs.
        /// </summary>
        public static void Untranslate(GameObject spawned)
        {
            if (spawned == null)
            {
                return;
            }
            try
            {
                Besiege.UI.Behaviours.Translator[] all =
                    spawned.GetComponentsInChildren<Besiege.UI.Behaviours.Translator>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                    {
                        all[i].enabled = false;
                    }
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Takes a control's own hover swell away. A hovered control grows about
        /// 15%, which is right for a button and wrong for a table cell: it carries
        /// the lettering sideways into the next column instead of lighting the cell
        /// up. Destroyed rather than disabled, so turning a row's swells back on
        /// does not bring this one with them.
        /// </summary>
        public static void NoSwell(GameObject control)
        {
            if (control == null)
            {
                return;
            }
            try
            {
                Besiege.UI.Bridge.ScaleAnimation scale =
                    control.GetComponent<Besiege.UI.Bridge.ScaleAnimation>();
                if (scale != null)
                {
                    UnityEngine.Object.DestroyImmediate(scale);
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Anchors a rect to its parent's top-left corner and puts it at x, y with
        /// y running downward, which is how the whole panel lays out.
        /// </summary>
        /// <summary>
        /// A control's own lettering, pinned to it and written.
        ///
        /// Every window in this mod wants the same four things of a prefab's label
        /// and used to do them three times over: pin it to the control it belongs
        /// to -- the prefab's caption is a fixed width, so on a narrower control it
        /// overhangs its neighbour and takes the clicks meant for it -- stop it
        /// answering the pointer, style it, and shrink it to fit.
        /// </summary>
        /// <param name="floor">The smallest the lettering may shrink to, or zero
        /// to leave its size alone.</param>
        /// <param name="make">Whether to build a label where the control has none.
        /// A drawn plate has none; a UI Factory prefab always does.</param>
        public static Text Label(GameObject control, string text, Color colour,
                                 TextAnchor align, int floor, bool make)
        {
            if (control == null)
            {
                return null;
            }
            Text label = control.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                if (!make)
                {
                    return null;
                }
                GameObject go = new GameObject("Text");
                go.transform.SetParent(control.transform, false);
                go.AddComponent<RectTransform>();
                label = go.AddComponent<Text>();
            }
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label.raycastTarget = false;
            Style(label, colour, align);
            if (floor > 0)
            {
                Shrink(label, floor);
            }
            label.text = text;
            return label;
        }

        /// <summary>
        /// Grows a control's lettering while the pointer is on it, the way
        /// Besiege's own buttons do.
        ///
        /// The lettering rather than the plate, so a row of controls keeps its
        /// spacing. UI Factory's own version of this is taken off by
        /// <see cref="NoSwell"/> -- it scales the whole control, and a control
        /// scaled inside a table overlaps its neighbour.
        /// </summary>
        public static void Grow(GameObject control, Transform grows, float by)
        {
            if (control == null || grows == null)
            {
                return;
            }
            Swell swell = control.AddComponent<Swell>();
            swell.grows = grows;
            swell.grown = by;
        }

        public static void Grow(GameObject control, Transform grows)
        {
            Grow(control, grows, 1.12f);
        }

        public static void Fit(RectTransform rect, float x, float y, float w, float h)
        {
            if (rect == null)
            {
                return;
            }
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>
        /// Gives a label UI Factory's font if it has none, and settles the rest of
        /// its drawing.
        ///
        /// The Input Field prefab's own text and placeholder come out of the bundle
        /// with no font at all, and a Text with no font draws nothing -- so the box
        /// reads as one that swallows typing rather than one that failed to paint.
        /// </summary>
        public static void Style(Text label, Color colour, TextAnchor align)
        {
            if (label == null)
            {
                return;
            }
            if (label.font == null)
            {
                Font font = Font;
                label.font = font != null
                    ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            label.color = colour;
            label.alignment = align;
            label.supportRichText = false;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>
        /// Lets a label shrink to fit its own box rather than spill out of it.
        ///
        /// A table cell holds whatever somebody typed -- a variable name is not a
        /// length this can choose -- and an overflowing Text is drawn straight
        /// across the next column, where it also reads as that column's value.
        /// Shrinking is the honest failure: a long name in a narrow cell is small
        /// rather than somewhere else.
        /// </summary>
        public static void Shrink(Text label, int floor)
        {
            if (label == null)
            {
                return;
            }
            label.resizeTextMaxSize = label.fontSize > 0 ? label.fontSize : 14;
            label.resizeTextMinSize = floor;
            label.resizeTextForBestFit = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
