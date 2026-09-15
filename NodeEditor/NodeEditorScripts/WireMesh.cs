using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NodeEditorMod
{
    /// <summary>
    /// Every wire on a board, as one mesh: quads rather than an `Image` per wire
    /// piece, which was seven hundred objects on a big board. On the board's own
    /// canvas: a nested canvas under panned, scaled content was not trusted to clip
    /// with `RectMask2D`.
    /// </summary>
    public class WireMesh : MaskableGraphic
    {
        private readonly List<Vector2> starts = new List<Vector2>();
        private readonly List<Vector2> ends = new List<Vector2>();
        private readonly List<Color32> inks = new List<Color32>();

        /// <summary>How thick a wire is, in the board's own units.</summary>
        public float Thickness = 2f;

        /// <summary>A mesh holds 65000 vertices, four to a piece.</summary>
        private const int MostPieces = 16000;

        public void Clear()
        {
            starts.Clear();
            ends.Clear();
            inks.Clear();
        }

        /// <summary>One straight piece of wire, in the coordinates of the content
        /// the board is drawn on.</summary>
        public void Add(Vector2 from, Vector2 to, Color colour)
        {
            if (starts.Count >= MostPieces)
            {
                return;
            }
            starts.Add(from);
            ends.Add(to);
            inks.Add(colour);
        }

        /// <summary>Puts what has been added since the last <see cref="Clear"/> on
        /// screen.</summary>
        public void Shown()
        {
            SetVerticesDirty();
        }

        /// <summary>Each piece a quad along its line, a wire's width
        /// across.</summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float half = Thickness * 0.5f;
            for (int i = 0; i < starts.Count; i++)
            {
                Vector2 span = ends[i] - starts[i];
                float length = span.magnitude;
                if (length < 0.0001f)
                {
                    continue;
                }
                Vector2 side = new Vector2(-span.y, span.x) * (half / length);
                int at = vh.currentVertCount;
                Color32 ink = inks[i];
                vh.AddVert(starts[i] - side, ink, Vector2.zero);
                vh.AddVert(starts[i] + side, ink, Vector2.zero);
                vh.AddVert(ends[i] + side, ink, Vector2.zero);
                vh.AddVert(ends[i] - side, ink, Vector2.zero);
                vh.AddTriangle(at, at + 1, at + 2);
                vh.AddTriangle(at + 2, at + 3, at);
            }
        }
    }
}
