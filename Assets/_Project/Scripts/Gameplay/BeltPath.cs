using System.Collections.Generic;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Konveyör yolu: dünya-uzayı waypoint dizisi. Kartlar yay-uzunluğu (arc
    /// length) ile ilerler. U/üçgen şekli için yeterince sık vertex verilir.
    /// </summary>
    public class BeltPath
    {
        readonly List<Vector3> pts;
        readonly float[] cum;
        public float Length { get; }

        public BeltPath(List<Vector3> points)
        {
            pts = points;
            cum = new float[pts.Count];
            cum[0] = 0f;
            for (int i = 1; i < pts.Count; i++)
                cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            Length = cum[pts.Count - 1];
        }

        public Vector3 PointAt(float d)
        {
            if (d <= 0f) return pts[0];
            if (d >= Length) return pts[pts.Count - 1];

            int i = 1;
            while (i < pts.Count && cum[i] < d) i++;
            float segLen = cum[i] - cum[i - 1];
            float t = segLen > 0f ? (d - cum[i - 1]) / segLen : 0f;
            return Vector3.Lerp(pts[i - 1], pts[i], t);
        }

        /// <summary>İlerleme (artan d) yönündeki birim teğet → kart bu yöne döner (viraj takibi).</summary>
        public Vector3 TangentAt(float d)
        {
            if (pts.Count < 2) return Vector3.forward;
            d = Mathf.Clamp(d, 0f, Length);
            int i = 1;
            while (i < pts.Count - 1 && cum[i] < d) i++;   // d'nin düştüğü segment [i-1, i]
            Vector3 dir = pts[i] - pts[i - 1];
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
        }

        /// <summary>Verilen dünya noktasına en yakın vertex'in yay-uzunluğu.</summary>
        public float NearestDist(Vector3 world)
        {
            float best = float.MaxValue;
            float bestDist = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                float dd = (pts[i] - world).sqrMagnitude;
                if (dd < best) { best = dd; bestDist = cum[i]; }
            }
            return bestDist;
        }
    }
}
