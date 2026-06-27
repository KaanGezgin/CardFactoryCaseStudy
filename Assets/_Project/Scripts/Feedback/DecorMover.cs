using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Background decor: slides the given items along their local X axis and wraps them
    /// end to end (the feel of boxes riding an endless conveyor). Runs off its own
    /// serialized item list → still works once baked and saved.
    /// </summary>
    public class DecorMover : MonoBehaviour
    {
        [SerializeField] Transform[] items;
        [SerializeField] float speed = 1.6f;   // sign = direction of travel
        [SerializeField] float minX = -12f;
        [SerializeField] float maxX = 12f;

        public void Setup(Transform[] it, float spd, float min, float max)
        {
            items = it;
            speed = spd;
            minX = min;
            maxX = max;
        }

        void Update()
        {
            if (items == null || items.Length == 0) return;
            float span = maxX - minX;
            if (span <= 0.001f) return;

            float dx = speed * Time.deltaTime;
            for (int i = 0; i < items.Length; i++)
            {
                var t = items[i];
                if (t == null) continue;
                var p = t.localPosition;
                p.x += dx;
                if (p.x > maxX) p.x -= span;
                else if (p.x < minX) p.x += span;
                t.localPosition = p;
            }
        }
    }
}
