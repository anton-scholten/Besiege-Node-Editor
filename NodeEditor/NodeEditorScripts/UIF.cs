using System;
using UnityEngine;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// Thin wrapper over UI Factory 3 (Workshop 2913469777), which ships Besiege's
    /// real interface as prefabs. A soft dependency: every mention of `Besiege.UI`
    /// lives in this file, behind the one guarded check, <see cref="Available"/>.
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

        /// <summary>The tooltip panel alone, with no behaviour (see <see
        /// cref="Tip"/>). Registered as "Vis Only", though the asset is named
        /// "(Visual)".</summary>
        public const string TooltipPrefab = "Text Tooltip (Vis Only)";

        /// <summary>The lettering colour for an answer.</summary>
        public static readonly Color Ink = Color.white;

        /// <summary>For text that is not an answer: a heading, a ghost prompt, a
        /// row that is following something else.</summary>
        public static readonly Color QuietInk = new Color(0.72f, 0.72f, 0.74f, 1f);

        /// <summary>Besiege's red, for the option in force.</summary>
        public static readonly Color Hot = new Color(0.92f, 0.13f, 0.29f, 1f);

        /// <summary>The panel's dark plate, for something drawn over a
        /// window.</summary>
        public static readonly Color Shade = new Color(0.10f, 0.13f, 0.17f, 0.97f);

        /// <summary>The cyan Besiege uses for a reset, and here for a row that is
        /// running.</summary>
        public static readonly Color Live = new Color(0.012f, 1f, 0.847f, 1f);

        private static bool asked;
        private static bool available;

        /// <summary>Whether UI Factory is loaded. A missing `Besiege.UI` fails as
        /// the calling method compiles, so the try has to be here.</summary>
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

        /// <summary>Whether tooltips are on, by the game's own switch
        /// (`Besiege.UI.Bridge.Tooltip.TooltipsActive`).</summary>
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

        /// <summary>Besiege's lettering; null without UI Factory, where callers use
        /// Arial.
        /// </summary>
        public static Font Font
        {
            get
            {
                try { return Besiege.UI.Make.Font; }
                catch (Exception) { return null; }
            }
        }

        /// <summary>How many prefabs have been spawned, logged with the first
        /// window's build time.</summary>
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

        /// <summary>Switches off every Translator in a spawned prefab: its `Start`
        /// calls `Recaption`, which throws on a label with no localisation key.
        /// Disabling is enough to stop `Start`, and cheaper than
        /// destroying.</summary>
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

        /// <summary>Removes a control's hover swell, which pushes a table cell's
        /// lettering into the next column. Destroyed, so re-enabling swells leaves
        /// it gone.</summary>
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

        /// <summary>A control's label: pinned to the control, off the pointer,
        /// styled, and shrunk to fit if asked.</summary>
        /// <param name="floor">The smallest it may shrink to; zero leaves the size
        /// alone.
        /// </param>
        /// <param name="make">Whether to build a label where the control has
        /// none.</param>
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

        /// <summary>Grows a control's lettering while hovered, as Besiege's buttons
        /// do. The lettering, not the control, so a row keeps its
        /// spacing.</summary>
        public static void Grow(GameObject control, Component grows, float by)
        {
            if (control == null || grows == null)
            {
                return;
            }
            Swell swell = control.AddComponent<Swell>();
            swell.grows = grows.transform;
            swell.grown = by;
        }

        public static void Grow(GameObject control, Component grows)
        {
            Grow(control, grows, 1.12f);
        }

        /// <summary>A time as the tables show one: up to two decimals.</summary>
        public static string Seconds(float value)
        {
            return value.ToString("0.##",
                                  System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A plain coloured rectangle, placed in its parent.</summary>
        public static GameObject Plate(Transform host, float x, float y, float w,
                                       float h, Color colour)
        {
            GameObject go = new GameObject("Plate");
            go.transform.SetParent(host, false);
            go.AddComponent<RectTransform>();
            Image image = go.AddComponent<Image>();
            image.color = colour;
            Fit(go.GetComponent<RectTransform>(), x, y, w, h);
            return go;
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

        /// <summary>Styles a label, giving it Besiege's font if it has none: the
        /// Input Field prefab's labels come without one and draw nothing.</summary>
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

        /// <summary>Lets a label shrink to fit its box rather than spill into the
        /// next column.</summary>
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
