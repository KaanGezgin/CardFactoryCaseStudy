using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Arka plan dekoru: verilen item'ları yerel X ekseninde kaydırır ve uçtan
    /// uca sarar (sonsuz akan bant üstünde giden kutular hissi). Kendi item
    /// listesinden çalışır (serialize edilir) → bake edilip kaydedildiğinde de çalışır.
    /// </summary>
    public class DecorMover : MonoBehaviour
    {
        [SerializeField] Transform[] items;
        [SerializeField] float speed = 1.6f;   // işaret yönü = hareket yönü
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
