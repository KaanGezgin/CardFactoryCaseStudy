using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Flows the chevron (‹‹‹) arrows along the belt → conveyor feel. Runs off its own
    /// waypoint list (not dependent on BeltPath), so it still works correctly once baked
    /// and saved (it is serialized).
    /// </summary>
    public class BeltFlow : MonoBehaviour
    {
        [SerializeField] Vector3[] waypoints;
        [SerializeField] Transform[] chevrons;
        [SerializeField] float speed = 1.4f;
        [SerializeField] float surfaceY = 0.17f;

        float[] cum;
        float length;
        float phase;

        public void Setup(Vector3[] wp, Transform[] chev, float spd, float y)
        {
            waypoints = wp;
            chevrons = chev;
            speed = spd;
            surfaceY = y;
            Recompute();
        }

        void OnEnable() => Recompute();

        void Recompute()
        {
            if (waypoints == null || waypoints.Length < 2) { length = 0f; return; }
            cum = new float[waypoints.Length];
            cum[0] = 0f;
            for (int i = 1; i < waypoints.Length; i++)
                cum[i] = cum[i - 1] + Vector3.Distance(waypoints[i - 1], waypoints[i]);
            length = cum[waypoints.Length - 1];
        }

        Vector3 PointAt(float d)
        {
            if (length <= 0f) return Vector3.zero;
            d = Mathf.Repeat(d, length);
            int i = 1;
            while (i < cum.Length && cum[i] < d) i++;
            if (i >= cum.Length) i = cum.Length - 1;
            float segL = cum[i] - cum[i - 1];
            float t = segL > 1e-5f ? (d - cum[i - 1]) / segL : 0f;
            return Vector3.Lerp(waypoints[i - 1], waypoints[i], t);
        }

        void Update()
        {
            if (length <= 0f)
            {
                if (cum == null) Recompute();
                return;
            }
            if (chevrons == null || chevrons.Length == 0) return;

            phase = Mathf.Repeat(phase + speed * Time.deltaTime, length);
            float gap = length / chevrons.Length;

            for (int i = 0; i < chevrons.Length; i++)
            {
                var ch = chevrons[i];
                if (ch == null) continue;

                float d = phase + i * gap;
                Vector3 p = PointAt(d);
                Vector3 tan = PointAt(d + 0.06f) - p;
                ch.localPosition = new Vector3(p.x, surfaceY, p.z);
                tan.y = 0f;
                if (tan.sqrMagnitude > 1e-6f)
                    ch.localRotation = Quaternion.LookRotation(tan.normalized, Vector3.up);
            }
        }
    }
}
