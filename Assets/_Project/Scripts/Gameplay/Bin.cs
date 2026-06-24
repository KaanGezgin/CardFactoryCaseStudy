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

        Renderer[] slotRends;
        Material emptyMat;
        Material fillMat;

        Transform marker;          // üstte hedef-renk durum lambası
        Renderer markerRend;
        Renderer bodyRend;

        const float BodyHeight = 1.35f;
        const float SlotFrontZ = -0.28f;
        const float LeanBack = 28f;     // konteyner hafif geriye yatar

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

            // Hedef-renk durum lambası — konteynerin üst önünde parlak panel.
            var markerGo = ProcMesh.RoundedCube("ContainerLight");
            DestroyCollider(markerGo);
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.localPosition = new Vector3(0f, BodyHeight + 0.02f, -0.22f);
            markerGo.transform.localScale = new Vector3(0.36f, 0.14f, 0.08f);
            markerRend = markerGo.GetComponent<Renderer>();
            marker = markerGo.transform;
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
            emptyMat = GameBootstrap.NewLitMaterial(full * 0.45f);
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

            BuildSlots(capacity);

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
            // Durum lambası hafif nabız atar.
            if (marker != null && Active)
            {
                float p = 0.88f + Mathf.Sin(Time.time * 4.5f) * 0.12f;
                marker.localScale = new Vector3(0.36f * p, 0.14f, 0.08f);
            }
        }

        void BuildSlots(int capacity)
        {
            if (slotRends != null)
                foreach (var r in slotRends)
                    if (r != null) Object.Destroy(r.gameObject);

            slotRends = new Renderer[capacity];
            float slotH = BodyHeight / capacity;
            for (int i = 0; i < capacity; i++)
            {
                var go = ProcMesh.RoundedCube("Slot_" + i);
                DestroyCollider(go);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, (i + 0.5f) * slotH, SlotFrontZ);
                go.transform.localScale = new Vector3(0.82f, slotH * 0.82f, 0.34f);
                var rend = go.GetComponent<Renderer>();
                rend.sharedMaterial = emptyMat;
                slotRends[i] = rend;
            }
        }

        Vector3 SlotWorldPos(int idx)
        {
            float slotH = BodyHeight / Capacity;
            return transform.TransformPoint(new Vector3(0f, (idx + 0.5f) * slotH, SlotFrontZ));
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
            if (slotRends != null && idx >= 0 && idx < slotRends.Length && slotRends[idx] != null)
            {
                slotRends[idx].sharedMaterial = fillMat;
                Juice.PunchScale(slotRends[idx].transform,
                    slotRends[idx].transform.localScale, 0.25f, 0.12f);
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
