using CardFactory.Core;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Taşma tamponu: sabit kapasiteli görünür slot dizisi (yatay tepsi).
    /// Eşleşmeden yolun sonuna ulaşan kartlar buraya düşüp slota oturur.
    /// Dolunca GameManager.OnDockFull → FAILED. Genişletme yok.
    /// </summary>
    public class Dock : MonoBehaviour
    {
        public int Capacity { get; private set; }
        public int Count { get; private set; }

        /// <summary>"+4 slots" UI'ının üstüne oturacağı 3B kaidenin dünya konumu.</summary>
        public Vector3 SlotsAnchor { get; private set; }

        GameManager gm;
        Vector3[] slots;

        const float Spacing = 0.42f;
        const float SlotY = 0.05f;
        const float CardY = 0.32f;

        public void Init(int capacity, GameManager gameManager, float centerZ)
        {
            Capacity = capacity;
            Count = 0;
            gm = gameManager;
            slots = new Vector3[capacity];

            float width = (capacity - 1) * Spacing;
            float xStart = -width * 0.5f;

            // Tepsi tabanı (açık renk bar)
            var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "DockTray";
            var tcol = tray.GetComponent<Collider>();
            if (tcol != null) Object.Destroy(tcol);
            tray.transform.SetParent(transform, false);
            tray.transform.position = new Vector3(0f, -0.02f, centerZ);
            tray.transform.localScale = new Vector3(width + 0.7f, 0.14f, 0.75f);
            tray.GetComponent<Renderer>().sharedMaterial =
                GameBootstrap.NewLitMaterial(new Color(0.88f, 0.90f, 0.93f));

            // Boş slot işaretçileri
            var slotMat = GameBootstrap.NewLitMaterial(new Color(0.30f, 0.33f, 0.38f));
            for (int i = 0; i < capacity; i++)
            {
                float x = xStart + i * Spacing;
                slots[i] = new Vector3(x, CardY, centerZ);

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "DockSlot_" + i;
                var mcol = marker.GetComponent<Collider>();
                if (mcol != null) Object.Destroy(mcol);
                marker.transform.SetParent(transform, false);
                marker.transform.position = new Vector3(x, SlotY, centerZ);
                marker.transform.localScale = new Vector3(0.32f, 0.06f, 0.5f);
                marker.GetComponent<Renderer>().sharedMaterial = slotMat;
            }

            // "+4 slots" kaidesi (dock'un sağ ucunda, yükseltilmiş koyu mavi blok).
            float pedX = width * 0.5f + 1.15f;
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "SlotsPedestal";
            var pcol = pedestal.GetComponent<Collider>();
            if (pcol != null) Object.Destroy(pcol);
            pedestal.transform.SetParent(transform, false);
            pedestal.transform.position = new Vector3(pedX, 0.3f, centerZ);
            pedestal.transform.localScale = new Vector3(1.7f, 0.7f, 1.15f);
            pedestal.GetComponent<Renderer>().sharedMaterial =
                GameBootstrap.NewLitMaterial(new Color(0.14f, 0.22f, 0.40f));

            SlotsAnchor = new Vector3(pedX, 0.75f, centerZ);
        }

        public void Receive(Card card)
        {
            if (Count >= Capacity)
            {
                if (card != null) Object.Destroy(card.gameObject);
                return;
            }

            Vector3 target = slots[Count];
            Count++;
            card.State = CardState.Dock;
            // Slota dik oturan ince kart görünümü
            card.transform.localScale = new Vector3(0.30f, 0.5f, 0.08f);
            card.MoveTo(target, 0.25f, () =>
            {
                if (card != null) Juice.PunchScale(card.transform, card.transform.localScale, 0.18f, 0.12f);
            });
            Sfx.Play("fill");

            if (Count >= Capacity && gm != null)
                gm.OnDockFull();
        }
    }
}
