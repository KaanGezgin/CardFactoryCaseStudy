using CardFactory.Core;
using CardFactory.Data;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Shipping container: fixed target color + capacity. A leaning, visibly slotted container
    /// body. Empty slots use a dark shade of the target color; as cards arrive each slot is
    /// painted the full color. When full it notifies BinManager.
    /// </summary>
    public class Bin : MonoBehaviour
    {
        public CardColor Color { get; private set; }
        public int Capacity { get; private set; }
        public int Fill { get; private set; }
        public bool Active { get; private set; }
        public float TriggerDist { get; set; }   // capture point along the path

        public bool HasRoom => Active && Fill < Capacity;

        GameManager gm;
        BinManager mgr;
        int slotIndex;
        int inFlight;
        int landed;

        Material fillCellMat;      // filled cell (bright target color)
        Material emptyCellMat;     // empty cell (dark/hollow)

        Renderer[] cells;          // DISCRETE fill cells (bottom to top); each empty/filled
        Transform cellsRoot;
        Renderer[] frameRends;     // closed frame (left/right/top/bottom) → painted the bin color
        int shownFill;             // number of filled cells

        Transform marker;          // target-color status sphere (lamp) on top
        Renderer markerRend;
        Renderer bodyRend;

        const float BodyHeight = 1.35f;
        const float SlotFrontZ = -0.28f;
        const float LeanBack = 28f;     // container leans back slightly
        const float LampSize = 0.22f;   // status sphere diameter

        // Channel geometry for the discrete fill cells.
        const float FillBottomY = 0.16f;
        const float FillH = BodyHeight - 0.26f;   // fillable height
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

            // Main container body — a vertical rectangular case.
            var bodyGo = ProcMesh.RoundedCube("ContainerBody");
            DestroyCollider(bodyGo);
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.7304f, 0.02f);
            bodyGo.transform.localScale = new Vector3(0.92f, 1.23916f, 0.48f);
            bodyRend = bodyGo.GetComponent<Renderer>();
            bodyRend.sharedMaterial = GameBootstrap.NewLitMaterial(frameColor);

            // Top LID — a glossy white lid (container lid + handle, like the reference).
            var topRail = ProcMesh.RoundedCube("ContainerLid");
            DestroyCollider(topRail);
            topRail.transform.SetParent(transform, false);
            topRail.transform.localPosition = new Vector3(0f, BodyHeight + 0.07f, 0.0f);
            topRail.transform.localScale = new Vector3(1.04f, 0.2f, 0.62f);
            var lidMat = GameBootstrap.NewLitMaterial(new Color(0.95f, 0.96f, 0.99f));
            lidMat.SetFloat("_Smoothness", 0.8f);
            topRail.GetComponent<Renderer>().sharedMaterial = lidMat;

            // (ContainerBase + ribbed strips + corner posts removed → clean container.)

            // Target-color status lamp — a bright SPHERE on top (like the reference).
            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerGo.name = "ContainerLamp";
            DestroyCollider(markerGo);
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.localPosition = new Vector3(0f, 1.53f, -0.441f);
            markerGo.transform.localScale = Vector3.one * LampSize;
            markerRend = markerGo.GetComponent<Renderer>();
            marker = markerGo.transform;

            BuildFrame();   // closed frame surrounding the cells (left/right/top/bottom)

            // Ground contact shadow (soft AO feel). Since the container leans, the shadow is
            // kept flat in world space (SpawnContactShadow sets the world rotation).
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
            fillCellMat = GameBootstrap.NewLitMaterial(full);          // FILLED cell: bright, saturated
            fillCellMat.SetFloat("_Smoothness", 0.6f);
            fillCellMat.EnableKeyword("_EMISSION");
            fillCellMat.SetColor("_EmissionColor", full * 0.3f);
            emptyCellMat = GameBootstrap.NewLitMaterial(full * 0.32f); // EMPTY cell: dark shade of the bin color
            emptyCellMat.SetFloat("_Smoothness", 0.18f);

            // Frame (left/right/top/bottom) = bin color (slightly tinted → distinct from a filled cell).
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

            // Rebuild the cells (per capacity), all EMPTY.
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
            // The status sphere pulses gently.
            if (marker != null && Active)
            {
                float p = 0.9f + Mathf.Sin(Time.time * 4.5f) * 0.1f;
                marker.localScale = Vector3.one * (LampSize * p);
            }
        }

        // CLOSED frame surrounding the cells (left/right wall + top/bottom) — container feel,
        // closed on the sides. Independent of capacity (fixed FillH); built once in Init.
        void BuildFrame()
        {
            frameRends = new Renderer[4];
            int fi = 0;
            float midY = FillBottomY + FillH * 0.5f;
            float frontZ = SlotFrontZ - 0.06f;   // sits in front of the cells → frames them

            foreach (var sx in new[] { -1f, 1f })   // left/right wall
            {
                var wall = ProcMesh.RoundedCube("CellWall");
                DestroyCollider(wall);
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = new Vector3(sx * (FillWidth * 0.5f + 0.06f), midY, frontZ);
                wall.transform.localScale = new Vector3(0.1f, FillH + 0.12f, 0.24f);
                frameRends[fi++] = wall.GetComponent<Renderer>();
            }

            float[] barY = { FillBottomY + FillH + 0.04f, FillBottomY - 0.04f };   // top/bottom frame
            foreach (var yy in barY)
            {
                var bar = ProcMesh.RoundedCube("CellFrameBar");
                DestroyCollider(bar);
                bar.transform.SetParent(transform, false);
                bar.transform.localPosition = new Vector3(0f, yy, frontZ);
                bar.transform.localScale = new Vector3(FillWidth + 0.26f, 0.1f, 0.24f);
                frameRends[fi++] = bar.GetComponent<Renderer>();
            }
            // Color is set in Configure (per target color).
        }

        // DISCRETE fill cells (bottom to top). All start empty; when a card lands the matching cell fills.
        void BuildCells(int capacity)
        {
            if (cellsRoot != null) Object.Destroy(cellsRoot.gameObject);
            var rootGo = new GameObject("Cells");
            cellsRoot = rootGo.transform;
            cellsRoot.SetParent(transform, false);

            cells = new Renderer[capacity];
            float pitch = capacity > 0 ? FillH / capacity : FillH;
            float cellH = pitch * 0.8f;            // small gap between cells
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

                // Ship normally when full; or if the color is fully exhausted, ship a partial bin too.
                if (inFlight <= 0 && Active &&
                    (Fill >= Capacity || (mgr != null && mgr.IsColorExhausted(Color))))
                    Ship();
            });
        }

        void FillSlot(int idx)
        {
            shownFill = Mathf.Min(Capacity, shownFill + 1);
            // The matching cell goes EMPTY→FILLED: painted bright + a satisfying pop.
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
