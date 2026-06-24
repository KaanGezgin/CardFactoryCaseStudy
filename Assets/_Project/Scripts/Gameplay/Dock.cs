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
        float centerZ;

        TextMesh offerLabel;
        TextMesh offerAmount;
        Transform offerRoot;
        Renderer offerPanel;
        SpriteRenderer glow;
        bool failShown;

        Renderer trayRend;          // tansiyon nabzı için
        static readonly Color TrayBase = new Color(0.88f, 0.90f, 0.93f);

        static readonly Color OfferGreen = new Color(0.10f, 0.42f, 0.18f);   // koyu yeşil

        const float Spacing = 0.42f;
        const float SlotY = 0.05f;
        const float CardY = 0.32f;

        public void Init(int capacity, GameManager gameManager, float centerZ)
        {
            Capacity = capacity;
            Count = 0;
            gm = gameManager;
            this.centerZ = centerZ;
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
            trayRend = tray.GetComponent<Renderer>();
            trayRend.sharedMaterial = GameBootstrap.NewLitMaterial(TrayBase);

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

            BuildOfferWidget(new Vector3(pedX, 1.0f, centerZ - 0.25f));
        }

        // Kaideye GÖMÜLÜ yeşil teklif widget'ı (yazı + buton + coin), kameraya dönük.
        void BuildOfferWidget(Vector3 pos)
        {
            var anchor = new GameObject("DockOffer");
            anchor.transform.SetParent(transform, false);
            anchor.transform.position = pos;
            anchor.AddComponent<CardFactory.Feedback.Billboard>();
            offerRoot = anchor.transform;

            // Işıksız (unlit) radyal hale: teklifin ARKASINDA, kameradan uzakta.
            // Opak panel ortayı derinlik testiyle gizler → buton/yazı temiz, çevresi hale.
            var glowGo = new GameObject("DockGlow");
            glowGo.transform.SetParent(anchor.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            glowGo.transform.localScale = Vector3.one * 2.6f;
            glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = GameBootstrap.MakeRadialSprite();
            glow.color = new Color(1f, 0.95f, 0.7f, 0f);
            glow.enabled = false;

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "OfferPanel";
            var pcol = panel.GetComponent<Collider>();
            if (pcol != null) Object.Destroy(pcol);
            panel.transform.SetParent(anchor.transform, false);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localScale = new Vector3(1.7f, 1.05f, 0.08f);
            offerPanel = panel.GetComponent<Renderer>();
            offerPanel.sharedMaterial = GameBootstrap.NewLitMaterial(OfferGreen);

            offerLabel = GameBootstrap.MakeWorldText("+4 slots", anchor.transform,
                new Vector3(0f, 0.28f, -0.07f), 0.05f, Color.white, 80);

            // Coin (basit sarı disk)
            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "Coin";
            var ccol = coin.GetComponent<Collider>();
            if (ccol != null) Object.Destroy(ccol);
            coin.transform.SetParent(anchor.transform, false);
            coin.transform.localPosition = new Vector3(-0.42f, -0.26f, -0.07f);
            coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coin.transform.localScale = new Vector3(0.28f, 0.02f, 0.28f);
            coin.GetComponent<Renderer>().sharedMaterial =
                GameBootstrap.NewLitMaterial(new Color(1f, 0.82f, 0.10f));

            offerAmount = GameBootstrap.MakeWorldText("400", anchor.transform,
                new Vector3(0.12f, -0.26f, -0.07f), 0.05f, new Color(1f, 0.95f, 0.6f), 80);
        }

        /// <summary>
        /// Level fail olunca: teklif "New Dock / 1000" olur ve dock'a özel bir spot
        /// ışığı yanar (alana dikkat çeker).
        /// </summary>
        public void ShowFailOffer()
        {
            if (failShown) return;
            failShown = true;

            if (offerLabel != null) offerLabel.text = "New Dock";
            if (offerAmount != null) offerAmount.text = "1000";
            if (offerPanel != null)
                offerPanel.sharedMaterial = GameBootstrap.NewLitMaterial(OfferGreen);
            if (offerRoot != null) Juice.PunchScale(offerRoot, Vector3.one, 0.25f, 0.3f);

            if (glow != null)
            {
                glow.enabled = true;
                glow.color = new Color(1f, 0.95f, 0.7f, 0.95f);
            }
        }

        // Tansiyon nabzı: dock doldukça tepsi kırmızıya doğru, hızlanan bir nabızla yanar.
        void Update()
        {
            if (trayRend == null) return;
            float ratio = Capacity > 0 ? (float)Count / Capacity : 0f;
            if (ratio < 0.5f)
            {
                SetTray(TrayBase);
                return;
            }
            float intensity = Mathf.InverseLerp(0.5f, 1f, ratio);
            float speed = Mathf.Lerp(3f, 11f, ratio);
            float k = (Mathf.Sin(Time.time * speed) * 0.5f + 0.5f) * intensity;
            SetTray(Color.Lerp(TrayBase, new Color(0.95f, 0.18f, 0.18f), k));
        }

        void SetTray(Color c)
        {
            trayRend.sharedMaterial.color = c;
            trayRend.sharedMaterial.SetColor("_BaseColor", c);
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
