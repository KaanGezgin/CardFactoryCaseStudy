using CardFactory.Core;
using CardFactory.Data;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Sevkiyat konteyneri: sabit hedef renk + kapasite. Geriye yatık duran, görünür
    /// slotlu konteyner gövdesi. Boş slotlar hedef rengin koyu tonunda; kart geldikçe
    /// slot tam renge boyanır. Dolunca BinManager'a haber verir.
    /// </summary>
    public class Bin : MonoBehaviour
    {
        public CardColor Color { get; private set; }
        public int Capacity { get; private set; }
        public int Fill { get; private set; }
        public bool Active { get; private set; }
        public float TriggerDist { get; set; }   // yol üzerinde yakalama noktası

        public bool HasRoom => Active && Fill < Capacity;

        GameManager gm;
        BinManager mgr;
        int slotIndex;
        int inFlight;
        int landed;

        Material fillMat;          // hedef renk (dolan bar)
        Material wellMat;          // boş kanal (koyu)

        Transform fillBar, fillCap, well;
        Renderer fillBarRend, fillCapRend, wellRend;
        float barT;                // gösterilen dolum oranı (0..1), yumuşak lerp
        int shownFill;             // banda yansımış (inmiş) kart sayısı

        Transform marker;          // üstte hedef-renk durum küresi (lamba)
        Renderer markerRend;
        Renderer bodyRend;
        Transform grooveRoot;      // segment yivleri (referans gibi rung'lar)

        const float BodyHeight = 1.35f;
        const float SlotFrontZ = -0.28f;
        const float LeanBack = 28f;     // konteyner hafif geriye yatar
        const float LampSize = 0.22f;   // durum küresi çapı

        // Sürekli dolum barı geometrisi (slot yerine yükselen renk sütunu).
        const float FillBottomY = 0.16f;
        const float FillH = BodyHeight - 0.26f;   // doldurulabilir yükseklik
        const float FillWidth = 0.7f;
        const float FillDepth = 0.34f;

        public void Init(GameManager gameManager, BinManager manager, int slot, Vector3 pos)
        {
            gm = gameManager;
            mgr = manager;
            slotIndex = slot;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(LeanBack, 0f, 0f);

            var frameColor = new Color(0.14f, 0.16f, 0.19f);
            var trimColor = new Color(0.22f, 0.24f, 0.28f);

            // Ana konteyner gövdesi — dikey dikdörtgen kasa.
            var bodyGo = ProcMesh.RoundedCube("ContainerBody");
            DestroyCollider(bodyGo);
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, BodyHeight * 0.5f, 0.02f);
            bodyGo.transform.localScale = new Vector3(0.92f, BodyHeight, 0.48f);
            bodyRend = bodyGo.GetComponent<Renderer>();
            bodyRend.sharedMaterial = GameBootstrap.NewLitMaterial(frameColor);

            // Üst çerçeve / kapak şeridi.
            var topRail = ProcMesh.RoundedCube("ContainerTop");
            DestroyCollider(topRail);
            topRail.transform.SetParent(transform, false);
            topRail.transform.localPosition = new Vector3(0f, BodyHeight + 0.06f, 0.02f);
            topRail.transform.localScale = new Vector3(0.98f, 0.12f, 0.54f);
            topRail.GetComponent<Renderer>().sharedMaterial = GameBootstrap.NewLitMaterial(trimColor);

            // Alt taban şeridi.
            var baseRail = ProcMesh.RoundedCube("ContainerBase");
            DestroyCollider(baseRail);
            baseRail.transform.SetParent(transform, false);
            baseRail.transform.localPosition = new Vector3(0f, 0.06f, 0.02f);
            baseRail.transform.localScale = new Vector3(0.98f, 0.12f, 0.54f);
            baseRail.GetComponent<Renderer>().sharedMaterial = GameBootstrap.NewLitMaterial(trimColor);

            // Ön yüzde oluklu (corrugated) dikey şeritler — konteyner hissi.
            var ribMat = GameBootstrap.NewLitMaterial(new Color(0.10f, 0.11f, 0.13f));
            for (int i = -2; i <= 2; i++)
            {
                var rib = ProcMesh.RoundedCube("ContainerRib_" + i);
                DestroyCollider(rib);
                rib.transform.SetParent(transform, false);
                rib.transform.localPosition = new Vector3(i * 0.17f, BodyHeight * 0.5f, -0.24f);
                rib.transform.localScale = new Vector3(0.06f, BodyHeight * 0.92f, 0.06f);
                rib.GetComponent<Renderer>().sharedMaterial = ribMat;
            }

            // Köşe direkleri.
            var postMat = GameBootstrap.NewLitMaterial(new Color(0.08f, 0.09f, 0.11f));
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                var post = ProcMesh.RoundedCube("ContainerPost");
                DestroyCollider(post);
                post.transform.SetParent(transform, false);
                post.transform.localPosition = new Vector3(sx * 0.44f, BodyHeight * 0.5f, sz * 0.22f);
                post.transform.localScale = new Vector3(0.08f, BodyHeight * 0.98f, 0.08f);
                post.GetComponent<Renderer>().sharedMaterial = postMat;
            }

            // Hedef-renk durum lambası — üstte parlak KÜRE (referans gibi).
            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerGo.name = "ContainerLamp";
            DestroyCollider(markerGo);
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.localPosition = new Vector3(0f, BodyHeight + 0.18f, -0.06f);
            markerGo.transform.localScale = Vector3.one * LampSize;
            markerRend = markerGo.GetComponent<Renderer>();
            marker = markerGo.transform;

            BuildFillColumn();

            // Zemin kontakt gölgesi (yumuşak AO hissi). Konteyner eğik durduğundan
            // gölge world-space'te flat tutulur (SpawnContactShadow world rotasyon ayarlar).
            GameBootstrap.SpawnContactShadow(transform, new Vector3(pos.x, 0.04f, pos.z + 0.12f), 1.25f, 1.25f);
        }

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        public void Configure(CardColor color, int capacity)
        {
            Color = color;
            Capacity = capacity;
            Fill = 0;
            inFlight = 0;
            landed = 0;
            Active = true;

            var full = CardPalette.Get(color);
            fillMat = GameBootstrap.NewLitMaterial(full);
            fillMat.SetFloat("_Smoothness", 0.62f);                 // parlak, doygun fill (referans)
            fillMat.EnableKeyword("_EMISSION");
            fillMat.SetColor("_EmissionColor", full * 0.35f);
            wellMat = GameBootstrap.NewLitMaterial(full * 0.28f);   // koyu boş kanal
            if (bodyRend != null)
            {
                var shell = GameBootstrap.NewLitMaterial(UnityEngine.Color.Lerp(full * 0.35f, new UnityEngine.Color(0.12f, 0.13f, 0.15f), 0.55f));
                bodyRend.sharedMaterial = shell;
            }
            if (markerRend != null)
            {
                var lightMat = GameBootstrap.NewLitMaterial(full);
                lightMat.EnableKeyword("_EMISSION");
                lightMat.SetColor("_EmissionColor", full * 2.0f);
                markerRend.sharedMaterial = lightMat;
            }

            // Bar/kanal renklendir + sıfırla.
            if (wellRend != null) wellRend.sharedMaterial = wellMat;
            if (fillBarRend != null) fillBarRend.sharedMaterial = fillMat;
            shownFill = 0;
            barT = 0f;
            UpdateBar(0f);
            BuildGrooves(capacity);

            gameObject.SetActive(true);
            transform.localScale = Vector3.one;
            Juice.PopIn(transform, Vector3.one, 0.22f);
        }

        public void Deactivate()
        {
            Active = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            // Durum küresi hafif nabız atar.
            if (marker != null && Active)
            {
                float p = 0.9f + Mathf.Sin(Time.time * 4.5f) * 0.1f;
                marker.localScale = Vector3.one * (LampSize * p);
            }

            // Dolum barını hedef orana doğru yumuşak yükselt.
            if (fillBar != null && Capacity > 0)
            {
                float targetT = (float)shownFill / Capacity;
                if (Mathf.Abs(barT - targetT) > 0.0005f)
                {
                    barT = Mathf.MoveTowards(barT, targetT, Time.deltaTime * 2.5f);
                    UpdateBar(barT);
                }
            }
        }

        // Slot dizisi yerine: koyu boş kanal + yükselen renk barı + parlak kapak.
        void BuildFillColumn()
        {
            var wellGo = ProcMesh.RoundedCube("FillWell");
            DestroyCollider(wellGo);
            wellGo.transform.SetParent(transform, false);
            wellGo.transform.localPosition = new Vector3(0f, FillBottomY + FillH * 0.5f, SlotFrontZ + 0.03f);
            wellGo.transform.localScale = new Vector3(FillWidth + 0.04f, FillH, FillDepth * 0.7f);
            wellRend = wellGo.GetComponent<Renderer>();

            var barGo = ProcMesh.RoundedCube("FillBar");
            DestroyCollider(barGo);
            barGo.transform.SetParent(transform, false);
            fillBar = barGo.transform;
            fillBarRend = barGo.GetComponent<Renderer>();

            var capGo = ProcMesh.RoundedCube("FillCap");
            DestroyCollider(capGo);
            capGo.transform.SetParent(transform, false);
            capGo.transform.localScale = new Vector3(FillWidth + 0.05f, 0.06f, FillDepth + 0.03f);
            fillCap = capGo.transform;
            fillCapRend = capGo.GetComponent<Renderer>();
            var capMat = GameBootstrap.NewLitMaterial(new UnityEngine.Color(0.96f, 0.97f, 1f));
            capMat.SetFloat("_Smoothness", 0.85f);   // glossy beyaz kapak
            fillCapRend.sharedMaterial = capMat;

            UpdateBar(0f);
        }

        // Fill kanalı boyunca yatay segment yivleri (referanstaki rung'lar). Kapasiteye
        // göre capacity-1 koyu çizgi → fill segmentli görünür. Configure'da yeniden kurulur.
        void BuildGrooves(int capacity)
        {
            if (grooveRoot != null) Object.Destroy(grooveRoot.gameObject);
            if (capacity < 2) return;

            var rootGo = new GameObject("FillGrooves");
            grooveRoot = rootGo.transform;
            grooveRoot.SetParent(transform, false);

            var grooveMat = GameBootstrap.NewLitMaterial(new UnityEngine.Color(0.05f, 0.06f, 0.08f));
            for (int k = 1; k < capacity; k++)
            {
                float y = FillBottomY + (k / (float)capacity) * FillH;
                var g = ProcMesh.RoundedCube("Groove_" + k);
                DestroyCollider(g);
                g.transform.SetParent(grooveRoot, false);
                g.transform.localPosition = new Vector3(0f, y, SlotFrontZ - 0.015f);
                g.transform.localScale = new Vector3(FillWidth + 0.02f, 0.03f, 0.05f);
                g.GetComponent<Renderer>().sharedMaterial = grooveMat;
            }
        }

        void UpdateBar(float t)
        {
            t = Mathf.Clamp01(t);
            float h = Mathf.Max(0.0001f, t * FillH);
            if (fillBar != null)
            {
                fillBar.localScale = new Vector3(FillWidth, h, FillDepth);
                fillBar.localPosition = new Vector3(0f, FillBottomY + h * 0.5f, SlotFrontZ);
                fillBar.gameObject.SetActive(t > 0.001f);
            }
            if (fillCap != null)
            {
                fillCap.localPosition = new Vector3(0f, FillBottomY + h, SlotFrontZ - 0.01f);
                fillCap.gameObject.SetActive(t > 0.001f);
            }
        }

        Vector3 SlotWorldPos(int idx)
        {
            float frac = Capacity > 0 ? (idx + 1f) / Capacity : 0f;
            float y = FillBottomY + frac * FillH;
            return transform.TransformPoint(new Vector3(0f, y, SlotFrontZ));
        }

        public void Accept(Card card)
        {
            int idx = Fill;
            Fill++;
            inFlight++;
            card.State = CardState.Bin;

            float dur = gm != null && gm.Config != null ? gm.Config.binFillDuration : 0.2f;
            Vector3 target = SlotWorldPos(idx);
            card.MoveTo(target, dur, () =>
            {
                if (card != null) Object.Destroy(card.gameObject);
                inFlight--;
                landed++;
                FillSlot();
                Juice.PunchScale(transform, Vector3.one, 0.05f, 0.1f);
                Sfx.Play("fill");
                if (gm != null) gm.OnCardShipped();
                if (mgr != null) mgr.NotifyCaptured(Color);

                // Dolunca normal sevk; ya da renk tamamen tükendiyse yarım kutuyu da sevk et.
                if (inFlight <= 0 && Active &&
                    (Fill >= Capacity || (mgr != null && mgr.IsColorExhausted(Color))))
                    Ship();
            });
        }

        void FillSlot()
        {
            shownFill = Mathf.Min(Capacity, shownFill + 1);
            // Kapakta tatmin edici "doluyor" vuruşu.
            if (fillCap != null)
                Juice.PunchScale(fillCap, fillCap.localScale, 0.2f, 0.12f);
        }

        void Ship()
        {
            Juice.Burst(transform.position + Vector3.up * (BodyHeight + 0.2f), CardPalette.Get(Color));
            Sfx.Play("ship");
            Sfx.Haptic();
            Juice.CameraPunch(0.22f);
            Juice.ShrinkOut(transform, 0.2f, () => mgr.OnBinShipped(slotIndex));
        }
    }
}
