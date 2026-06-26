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

        Material fillCellMat;      // dolu hücre (parlak hedef renk)
        Material emptyCellMat;     // boş hücre (koyu/oyuk)

        Renderer[] cells;          // AYRIK dolum hücreleri (alttan üste); her biri boş/dolu
        Transform cellsRoot;
        Renderer[] frameRends;     // kapalı çerçeve (sol/sağ/üst/alt) → kutu rengine boyanır
        int shownFill;             // dolu hücre sayısı

        Transform marker;          // üstte hedef-renk durum küresi (lamba)
        Renderer markerRend;
        Renderer bodyRend;

        const float BodyHeight = 1.35f;
        const float SlotFrontZ = -0.28f;
        const float LeanBack = 28f;     // konteyner hafif geriye yatar
        const float LampSize = 0.22f;   // durum küresi çapı

        // Ayrık dolum hücreleri için kanal geometrisi.
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

            // Üst KAPAK — parlak beyaz lid (referanstaki gibi konteyner kapağı + tutamak).
            var topRail = ProcMesh.RoundedCube("ContainerLid");
            DestroyCollider(topRail);
            topRail.transform.SetParent(transform, false);
            topRail.transform.localPosition = new Vector3(0f, BodyHeight + 0.07f, 0.0f);
            topRail.transform.localScale = new Vector3(1.04f, 0.2f, 0.62f);
            var lidMat = GameBootstrap.NewLitMaterial(new Color(0.95f, 0.96f, 0.99f));
            lidMat.SetFloat("_Smoothness", 0.8f);
            topRail.GetComponent<Renderer>().sharedMaterial = lidMat;

            // Alt taban şeridi.
            var baseRail = ProcMesh.RoundedCube("ContainerBase");
            DestroyCollider(baseRail);
            baseRail.transform.SetParent(transform, false);
            baseRail.transform.localPosition = new Vector3(0f, 0.05f, 0.0f);
            baseRail.transform.localScale = new Vector3(1.0f, 0.12f, 0.56f);
            baseRail.GetComponent<Renderer>().sharedMaterial = GameBootstrap.NewLitMaterial(trimColor);
            // (Oluklu şeritler + köşe direkleri kaldırıldı → uzaktan temiz konteyner okunur.)

            // Hedef-renk durum lambası — üstte parlak KÜRE (referans gibi).
            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerGo.name = "ContainerLamp";
            DestroyCollider(markerGo);
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.localPosition = new Vector3(0f, BodyHeight + 0.18f, -0.06f);
            markerGo.transform.localScale = Vector3.one * LampSize;
            markerRend = markerGo.GetComponent<Renderer>();
            marker = markerGo.transform;

            BuildFrame();   // hücreleri çevreleyen kapalı çerçeve (sol/sağ/üst/alt)

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
            fillCellMat = GameBootstrap.NewLitMaterial(full);          // DOLU hücre: parlak, doygun
            fillCellMat.SetFloat("_Smoothness", 0.6f);
            fillCellMat.EnableKeyword("_EMISSION");
            fillCellMat.SetColor("_EmissionColor", full * 0.3f);
            emptyCellMat = GameBootstrap.NewLitMaterial(full * 0.32f); // BOŞ hücre: kutu renginin KOYU hali
            emptyCellMat.SetFloat("_Smoothness", 0.18f);

            // Çerçeve (sol/sağ/üst/alt) = kutu rengi (hafif tonlu → dolu hücreden ayrışsın).
            var frameMat = GameBootstrap.NewLitMaterial(full * 0.7f);
            if (frameRends != null)
                foreach (var r in frameRends)
                    if (r != null) r.sharedMaterial = frameMat;

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

            // Hücreleri (kapasiteye göre) yeniden kur, hepsi BOŞ.
            shownFill = 0;
            BuildCells(capacity);

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
        }

        // Hücreleri çevreleyen KAPALI çerçeve (sol/sağ duvar + üst/alt) — konteyner hissi,
        // yandan kapalı. Kapasiteden bağımsız (sabit FillH), Init'te bir kez kurulur.
        void BuildFrame()
        {
            frameRends = new Renderer[4];
            int fi = 0;
            float midY = FillBottomY + FillH * 0.5f;
            float frontZ = SlotFrontZ - 0.06f;   // hücrelerin önünde durur → çerçeveler

            foreach (var sx in new[] { -1f, 1f })   // sol/sağ duvar
            {
                var wall = ProcMesh.RoundedCube("CellWall");
                DestroyCollider(wall);
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = new Vector3(sx * (FillWidth * 0.5f + 0.06f), midY, frontZ);
                wall.transform.localScale = new Vector3(0.1f, FillH + 0.12f, 0.24f);
                frameRends[fi++] = wall.GetComponent<Renderer>();
            }

            float[] barY = { FillBottomY + FillH + 0.04f, FillBottomY - 0.04f };   // üst/alt çerçeve
            foreach (var yy in barY)
            {
                var bar = ProcMesh.RoundedCube("CellFrameBar");
                DestroyCollider(bar);
                bar.transform.SetParent(transform, false);
                bar.transform.localPosition = new Vector3(0f, yy, frontZ);
                bar.transform.localScale = new Vector3(FillWidth + 0.26f, 0.1f, 0.24f);
                frameRends[fi++] = bar.GetComponent<Renderer>();
            }
            // Renk Configure'da (hedef renge göre) verilir.
        }

        // AYRIK dolum hücreleri (alttan üste). Hepsi boş başlar; kart inince ilgili hücre dolu olur.
        void BuildCells(int capacity)
        {
            if (cellsRoot != null) Object.Destroy(cellsRoot.gameObject);
            var rootGo = new GameObject("Cells");
            cellsRoot = rootGo.transform;
            cellsRoot.SetParent(transform, false);

            cells = new Renderer[capacity];
            float pitch = capacity > 0 ? FillH / capacity : FillH;
            float cellH = pitch * 0.8f;            // hücreler arası küçük boşluk
            for (int i = 0; i < capacity; i++)
            {
                float y = FillBottomY + (i + 0.5f) * pitch;
                var c = ProcMesh.RoundedCube("Cell_" + i);
                DestroyCollider(c);
                c.transform.SetParent(cellsRoot, false);
                c.transform.localPosition = new Vector3(0f, y, SlotFrontZ - 0.03f);
                c.transform.localScale = new Vector3(FillWidth, cellH, FillDepth * 0.5f);
                var r = c.GetComponent<Renderer>();
                r.sharedMaterial = emptyCellMat;
                cells[i] = r;
            }
        }

        Vector3 SlotWorldPos(int idx)
        {
            float pitch = Capacity > 0 ? FillH / Capacity : FillH;
            float y = FillBottomY + (idx + 0.5f) * pitch;
            return transform.TransformPoint(new Vector3(0f, y, SlotFrontZ - 0.03f));
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
                FillSlot(idx);
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

        void FillSlot(int idx)
        {
            shownFill = Mathf.Min(Capacity, shownFill + 1);
            // İlgili hücre BOŞ→DOLU: parlak renge boyanır + tatmin edici pop.
            if (cells != null && idx >= 0 && idx < cells.Length && cells[idx] != null)
            {
                cells[idx].sharedMaterial = fillCellMat;
                Juice.PunchScale(cells[idx].transform, cells[idx].transform.localScale, 0.22f, 0.12f);
            }
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
