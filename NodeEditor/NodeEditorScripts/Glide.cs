using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>
    /// Fades a panel up and lets it drift the last few pixels into place, and
    /// fades it away again when it is no longer wanted.
    ///
    /// Besiege's own tooltips do this through
    /// <c>Besiege.UI.Bridge.Tooltip</c>, which fades and slides the panel it is
    /// pointed at -- but that handler goes on the hovered control and owns one
    /// panel per control, which is the arrangement <see cref="Tip"/> explains it
    /// cannot use. This is the part of it worth having on a shared panel:
    /// appearing is a movement rather than an event, so a pointer crossing a row
    /// of icons does not flash a hard-edged box on and off at each one.
    /// </summary>
    public class Glide : MonoBehaviour
    {
        /// <summary>How fast it fades, in the same units as <see cref="Swell"/>'s
        /// so a tooltip arrives at the pace the control under it grows.</summary>
        private const float Speed = 14f;

        private const float Settled = 0.004f;

        private CanvasGroup group;
        private RectTransform rect;

        private Vector2 home;
        private Vector2 approach;
        private float at;
        private bool wanted;

        /// <summary>Where the panel belongs, and where it comes in from -- an
        /// offset it holds at nothing and gives up as it fades.</summary>
        public void Settle(Vector2 where, Vector2 from)
        {
            Parts();
            home = where;
            approach = from;
            wanted = true;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            Apply();
        }

        /// <summary>Fade away. The object switches itself off once it has.</summary>
        public void Out()
        {
            wanted = false;
        }

        private void Parts()
        {
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = gameObject.AddComponent<CanvasGroup>();
                }
                // The panel hangs over the window and everything in it; nothing it
                // covers should stop answering the pointer, the control it explains
                // least of all.
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            if (rect == null)
            {
                rect = transform as RectTransform;
            }
        }

        private void Update()
        {
            float target = wanted ? 1f : 0f;
            if (Mathf.Abs(at - target) < Settled)
            {
                if (at == target)
                {
                    return;
                }
                at = target;
            }
            else
            {
                // Unscaled: the build menu is open at any time scale, pause
                // included.
                at = Mathf.Lerp(at, target, Time.unscaledDeltaTime * Speed);
            }
            Apply();
            if (!wanted && at == 0f)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Gone while faded halfway -- next time it is wanted it should come up
            // from nothing rather than from wherever it was left.
            at = 0f;
            wanted = false;
        }

        private void Apply()
        {
            Parts();
            group.alpha = at;
            if (rect != null)
            {
                rect.anchoredPosition = home + approach * (1f - at);
            }
        }
    }
}
