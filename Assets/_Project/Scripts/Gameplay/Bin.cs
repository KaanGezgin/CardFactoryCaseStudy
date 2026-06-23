using CardFactory.Core;
using CardFactory.Data;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Sevkiyat kutusu: sabit hedef renk + kapasite. Geriye yatık duran, görünür
    /// slotlu kasa. Boş slotlar hedef rengin koyu tonunda; kart geldikçe slot tam
    /// renge boyanır. Dolunca BinManager'a haber verir.
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

        const float BodyHeight = 1.3f;
        const float SlotFrontZ = -0.30f;
        const float LeanBack = 22f;     // geriye yatma açısı (derece)

        public void Init(GameManager gameManager, BinManager manager, int slot, Vector3 pos)
        {
            gm = gameManager;
            mgr = manager;
            slotIndex = slot;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(LeanBack, 0f, 0f);

            // Kasa gövdesi — koyu çerçeve.
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyGo.name = "BinBody";
            DestroyCollider(bodyGo);
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, BodyHeight * 0.5f, 0.06f);
            bodyGo.transform.localScale = new Vector3(1.05f, BodyHeight + 0.08f, 0.55f);
            bodyGo.GetComponent<Renderer>().sharedMaterial =
                GameBootstrap.NewLitMaterial(new Color(0.12f, 0.13f, 0.15f));
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
            emptyMat = GameBootstrap.NewLitMaterial(full * 0.45f);  // koyu ton

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

        void BuildSlots(int capacity)
        {
            if (slotRends != null)
                foreach (var r in slotRends)
                    if (r != null) Object.Destroy(r.gameObject);

            slotRends = new Renderer[capacity];
            float slotH = BodyHeight / capacity;
            for (int i = 0; i < capacity; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Slot_" + i;
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
                if (Fill >= Capacity && inFlight <= 0)
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
