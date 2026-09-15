using UnityEngine;

namespace NodeEditorMod
{
    /// <summary>Fades a panel in, drifting its last few pixels, and fades it out:
    /// what Besiege's own tooltip handler does, for a shared panel.</summary>
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
                // The panel must not block the pointer, least of all over what it
                // explains.
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
