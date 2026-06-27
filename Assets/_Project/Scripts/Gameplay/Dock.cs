using System.Collections;
using System.Collections.Generic;
using CardFactory.Core;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Overflow buffer: a fixed-capacity visible slot array (horizontal tray).
    /// Cards that reach the end of the path without matching fall here and settle into a slot.
    /// When full, GameManager.OnDockFull → FAILED. No expansion.
    /// </summary>
    public class Dock : MonoBehaviour
    {
        public int Capacity { get; private set; }
        public int Count { get; private set; }

        public Vector3 SlotsAnchor { get; private set; }
        public Vector3 OfferWorldPos =>
            offerRoot != null ? offerRoot.position : transform.position + Vector3.up;

        GameManager gm;
        Vector3[] slots;
        float centerZ;

        // Dock layout (left→right). Grouped by color: an incoming card inserts to the right of
        // its color group, and the cards to its right shift one slot over.
        readonly List<Card> placed = new();

        TextMesh offerLabel;
        TextMesh offerAmount;
        Transform offerRoot;
        Renderer offerPanel;

        Transform glowRoot;
        SpriteRenderer glowSr;
        float glowBaseAlpha = 0.88f;
        bool glowActive;
        bool failShown;

        Renderer trayRend;
        Renderer trayInnerRend;
        Renderer[] slotPockets;
        bool tensionEnabled;

        static readonly Color TrayOuter = new Color(0.96f, 0.97f, 0.99f);
        static readonly Color TrayInner = new Color(0.84f, 0.87f, 0.92f);
        static readonly Color SlotPocket = new Color(0.76f, 0.80f, 0.86f);
        static readonly Color OfferGreen = new Color(0.12f, 0.68f, 0.30f);
        static readonly Color PedestalBlue = new Color(0.11f, 0.17f, 0.32f);

        const float Spacing = 0.28f;   // tighter; slot positions can be tuned in the Inspector
        const float SlotPocketY = 0.17f;
        const float CardY = 0.30f;

        // Scale of a card dropped into the dock (user tuning): X=thin face, Y=long edge, Z=depth.
        static readonly Vector3 DockCardScale = new Vector3(0.2f, 1.72f, 1f);
        static readonly Quaternion DockCardRot = Quaternion.Euler(-12f, 0f, 0f);
        static readonly Vector3 PocketScale = new Vector3(0.24f, 0.38f, 0.40f);

        static readonly UnityEngine.Color GlowTint = new UnityEngine.Color(1f, 0.90f, 0.32f);
        const float GlowAlphaOnFail = 0.92f;

        public void Init(int capacity, float centerZ)
        {
            Capacity = capacity;
            Count = 0;
            this.centerZ = centerZ;
            slots = new Vector3[capacity];
            slotPockets = new Renderer[capacity];

            float width = (capacity - 1) * Spacing;
            float xStart = -width * 0.5f;
            float trayW = width + 1.05f;

            var tray = ProcMesh.RoundedCube("DockTray");
            DestroyCol(tray);
            tray.transform.SetParent(transform, false);
            tray.transform.position = new Vector3(0f, 0.02f, centerZ);
            tray.transform.localScale = new Vector3(trayW, 0.22f, 0.90f);
            trayRend = tray.GetComponent<Renderer>();
            var trayMat = GameBootstrap.NewLitMaterial(TrayOuter);
            trayMat.SetFloat("_Smoothness", 0.42f);
            trayRend.sharedMaterial = trayMat;

            var inner = ProcMesh.RoundedCube("DockTrayInner");
            DestroyCol(inner);
            inner.transform.SetParent(transform, false);
            inner.transform.position = new Vector3(0f, 0.06f, centerZ);
            inner.transform.localScale = new Vector3(trayW - 0.28f, 0.07f, 0.74f);
            trayInnerRend = inner.GetComponent<Renderer>();
            trayInnerRend.sharedMaterial = GameBootstrap.NewLitMaterial(TrayInner);

            var pocketMat = GameBootstrap.NewLitMaterial(SlotPocket);
            pocketMat.SetFloat("_Smoothness", 0.28f);
            for (int i = 0; i < capacity; i++)
            {
                float x = xStart + i * Spacing;
                slots[i] = new Vector3(x, CardY, centerZ - 0.02f);

                var pocket = ProcMesh.RoundedCube("DockSlot_" + i);
                DestroyCol(pocket);
                pocket.transform.SetParent(transform, false);
                pocket.transform.position = new Vector3(x, SlotPocketY, centerZ);
                pocket.transform.localScale = PocketScale;
                slotPockets[i] = pocket.GetComponent<Renderer>();
                slotPockets[i].sharedMaterial = pocketMat;
            }

            float pedX = width * 0.5f + 1.05f;
            var pedestal = ProcMesh.RoundedCube("SlotsPedestal");
            DestroyCol(pedestal);
            pedestal.transform.SetParent(transform, false);
            pedestal.transform.position = new Vector3(pedX, 0.20f, centerZ);
            pedestal.transform.localScale = new Vector3(1.55f, 0.82f, 0.90f);
            pedestal.GetComponent<Renderer>().sharedMaterial = GameBootstrap.NewLitMaterial(PedestalBlue);

            SlotsAnchor = new Vector3(pedX, 0.72f, centerZ);

            BuildOfferWidget(new Vector3(pedX, 0.95f, centerZ - 0.22f));
        }

        static void DestroyCol(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        static TextMesh FindChildTextMesh(Transform root, string childName)
        {
            foreach (var tm in root.GetComponentsInChildren<TextMesh>(true))
                if (tm.gameObject.name == childName) return tm;
            return null;
        }

        static Renderer FindChildRenderer(Transform root, string childName)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name == childName) return r;
            return null;
        }

        void BuildOfferWidget(Vector3 pos)
        {
            var anchor = new GameObject("DockOffer");
            anchor.transform.SetParent(transform, false);
            anchor.transform.position = pos;
            anchor.transform.rotation = Quaternion.Euler(GameBootstrap.CameraPitch, 0f, 0f);
            offerRoot = anchor.transform;

            EnsureGlowHalo(anchor.transform, createIfMissing: true);

            var panel = ProcMesh.RoundedCube("OfferPanel");
            DestroyCol(panel);
            panel.transform.SetParent(anchor.transform, false);
            panel.transform.localPosition = new Vector3(0f, -0.20f, -0.07f);
            panel.transform.localScale = new Vector3(1.35f, 0.50f, 0.09f);
            offerPanel = panel.GetComponent<Renderer>();
            var btnMat = GameBootstrap.NewLitMaterial(OfferGreen);
            btnMat.SetFloat("_Smoothness", 0.38f);
            offerPanel.sharedMaterial = btnMat;

            offerLabel = GameBootstrap.MakeWorldText("+4 slots", anchor.transform,
                new Vector3(0f, 0.30f, -0.07f), 0.048f, Color.white, 80);
            offerLabel.gameObject.name = "OfferLabel";

            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "Coin";
            DestroyCol(coin);
            coin.transform.SetParent(anchor.transform, false);
            coin.transform.localPosition = new Vector3(-0.36f, -0.20f, -0.08f);
            coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coin.transform.localScale = new Vector3(0.24f, 0.018f, 0.24f);
            coin.GetComponent<Renderer>().sharedMaterial =
                GameBootstrap.NewLitMaterial(new Color(1f, 0.82f, 0.10f));

            offerAmount = GameBootstrap.MakeWorldText("400", anchor.transform,
                new Vector3(0.14f, -0.20f, -0.08f), 0.048f, Color.white, 80);
            offerAmount.gameObject.name = "OfferAmount";
        }

        /// <summary>
        /// Round halo (sprite). Position/scale are set in the Inspector; at runtime ONLY the
        /// alpha pulses gently — the transform is never overwritten.
        /// </summary>
        void EnsureGlowHalo(Transform offerParent, bool createIfMissing)
        {
            var glowT = offerParent.Find("DockGlow");
            if (glowT == null)
            {
                if (!createIfMissing) return;
                var go = new GameObject("DockGlow");
                go.transform.SetParent(offerParent, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.12f);
                go.transform.localScale = Vector3.one * 4.8f;
                glowSr = go.AddComponent<SpriteRenderer>();
                glowSr.sprite = GameBootstrap.MakeGlowSprite();
                glowSr.color = new UnityEngine.Color(GlowTint.r, GlowTint.g, GlowTint.b, 0f);
                glowSr.sortingOrder = 10;
                glowRoot = go.transform;
            }
            else
            {
                glowRoot = glowT;
                RemoveLegacyQuadLayers(glowT);
                glowSr = glowT.GetComponent<SpriteRenderer>();
                if (glowSr == null)
                    glowSr = glowT.GetComponentInChildren<SpriteRenderer>(true);
                if (glowSr == null && createIfMissing)
                {
                    glowSr = glowT.gameObject.AddComponent<SpriteRenderer>();
                    glowSr.sprite = GameBootstrap.MakeGlowSprite();
                    glowSr.color = new UnityEngine.Color(GlowTint.r, GlowTint.g, GlowTint.b, 0f);
                }
            }

            CaptureGlowBaseline();
            SetGlowVisible(false);
        }

        /// <summary>Removes the old square quad layers; the root transform is kept.</summary>
        static void RemoveLegacyQuadLayers(Transform glowT)
        {
            for (int i = glowT.childCount - 1; i >= 0; i--)
            {
                var ch = glowT.GetChild(i);
                if (ch.name.StartsWith("GlowLayer_"))
                    Object.Destroy(ch.gameObject);
            }
            var mf = glowT.GetComponent<MeshFilter>();
            if (mf != null && glowT.GetComponent<SpriteRenderer>() == null)
                Object.Destroy(mf);
            var mr = glowT.GetComponent<MeshRenderer>();
            if (mr != null && glowT.GetComponent<SpriteRenderer>() == null)
                Object.Destroy(mr);
        }

        void CaptureGlowBaseline()
        {
            if (glowSr == null) return;
            glowBaseAlpha = glowSr.color.a > 0.05f ? glowSr.color.a : GlowAlphaOnFail;
        }

        void ResolveGlowRef()
        {
            if (offerRoot != null && glowRoot == null)
                glowRoot = offerRoot.Find("DockGlow");
            if (glowRoot == null) return;
            if (glowSr == null)
                glowSr = glowRoot.GetComponent<SpriteRenderer>()
                         ?? glowRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (glowSr != null && glowSr.sprite == null)
                glowSr.sprite = GameBootstrap.MakeGlowSprite();
        }

        void SetGlowVisible(bool on)
        {
            glowActive = on;
            ResolveGlowRef();

            if (offerRoot != null)
            {
                foreach (var sr in offerRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.gameObject.SetActive(on);
                    sr.enabled = on;
                    if (on && sr.color.a < 0.5f)
                        sr.color = new UnityEngine.Color(GlowTint.r, GlowTint.g, GlowTint.b, glowBaseAlpha);
                }
            }

            if (glowRoot != null)
                glowRoot.gameObject.SetActive(on);
            if (glowSr != null)
            {
                glowSr.enabled = on;
                if (on)
                    glowSr.color = new UnityEngine.Color(GlowTint.r, GlowTint.g, GlowTint.b, glowBaseAlpha);
            }
        }

        /// <summary>Alpha pulse only — position/scale are NOT touched (Inspector setting preserved).</summary>
        void PulseGlow()
        {
            if (!glowActive) return;
            ResolveGlowRef();
            if (glowSr == null || !glowSr.enabled) return;
            float pulse = 0.96f + Mathf.Sin(Time.time * 2.8f) * 0.04f;
            float a = glowBaseAlpha * pulse;
            glowSr.color = new UnityEngine.Color(GlowTint.r * 1.1f, GlowTint.g * 1.05f, GlowTint.b, a);
        }

        public void Rebind()
        {
            var trayT = transform.Find("DockTray");
            trayRend = trayT != null ? trayT.GetComponent<Renderer>() : null;
            var innerT = transform.Find("DockTrayInner");
            trayInnerRend = innerT != null ? innerT.GetComponent<Renderer>() : null;

            var pocketList = new List<Renderer>();
            var slotList = new List<Vector3>();
            for (int i = 0; ; i++)
            {
                var sl = transform.Find("DockSlot_" + i);
                if (sl == null) break;
                pocketList.Add(sl.GetComponent<Renderer>());
                slotList.Add(sl.position + Vector3.up * (CardY - SlotPocketY));
            }
            slotPockets = pocketList.ToArray();
            slots = slotList.ToArray();
            Capacity = slotList.Count;
            Count = 0;
            failShown = false;
            placed.Clear();

            var offer = transform.Find("DockOffer");
            if (offer != null)
            {
                offerRoot = offer;
                var bb = offer.GetComponent<Billboard>();
                if (bb != null) Destroy(bb);
                EnsureGlowHalo(offer, createIfMissing: true);
                offerPanel = FindChildRenderer(offer, "OfferPanel");
                offerLabel = FindChildTextMesh(offer, "OfferLabel");
                offerAmount = FindChildTextMesh(offer, "OfferAmount");
            }

            // The old DockGlowFloor is no longer used.
            var oldFloor = transform.Find("DockGlowFloor");
            if (oldFloor != null) oldFloor.gameObject.SetActive(false);

            SetGlowVisible(false);
        }

        public void ShowFailOffer()
        {
            if (failShown) return;
            failShown = true;

            if (offerLabel != null) offerLabel.text = "New Dock";
            if (offerAmount != null) offerAmount.text = "1000";
            // (The offer button's "punch/bounce" effect on fail was removed at the user's request.)

            ResolveGlowRef();
            CaptureGlowBaseline();
            if (glowBaseAlpha < 0.75f) glowBaseAlpha = GlowAlphaOnFail;
            // Instead of a 3D sprite, HudController.FailOfferGlow (UI, above the dim overlay).
            SetGlowVisible(false);
        }

        public void Bind(GameManager gameManager) => gm = gameManager;

        public void ResetForNewLevel()
        {
            Count = 0;
            failShown = false;
            placed.Clear();

            if (slotPockets != null)
                foreach (var p in slotPockets)
                    if (p != null) p.enabled = true;

            if (tensionEnabled)
            {
                ResetTrayColors();
            }

            if (offerLabel != null) offerLabel.text = "+4 slots";
            if (offerAmount != null) offerAmount.text = "400";
            SetGlowVisible(false);
        }

        public void SetTension(bool on) => tensionEnabled = on;

        void Update()
        {
            if (glowActive) PulseGlow();
            UpdateTensionPulse();
        }

        void UpdateTensionPulse()
        {
            if (!tensionEnabled || Capacity <= 0) return;

            float ratio = (float)Count / Capacity;
            if (ratio < 0.5f)
            {
                ResetTrayColors();
                return;
            }

            float intensity = Mathf.InverseLerp(0.5f, 1f, ratio);
            float speed = Mathf.Lerp(3f, 11f, ratio);
            float k = (Mathf.Sin(Time.time * speed) * 0.5f + 0.5f) * intensity;
            var red = new UnityEngine.Color(0.95f, 0.18f, 0.18f);

            if (trayInnerRend != null)
                SetRendererColor(trayInnerRend, UnityEngine.Color.Lerp(TrayInner, red, k));
            if (trayRend != null)
                SetRendererColor(trayRend, UnityEngine.Color.Lerp(TrayOuter, red, k * 0.55f));
        }

        void ResetTrayColors()
        {
            if (trayInnerRend != null) SetRendererColor(trayInnerRend, TrayInner);
            if (trayRend != null) SetRendererColor(trayRend, TrayOuter);
        }

        static void SetRendererColor(Renderer rend, UnityEngine.Color c)
        {
            rend.sharedMaterial.color = c;
            rend.sharedMaterial.SetColor("_BaseColor", c);
        }

        public void Receive(Card card)
        {
            if (card == null) return;
            if (Count >= Capacity)
            {
                Object.Destroy(card.gameObject);
                return;
            }

            // Insertion point: RIGHT AFTER the last card of the same color; if absent, at the end.
            int insertion = placed.Count;
            for (int i = placed.Count - 1; i >= 0; i--)
                if (placed[i] != null && placed[i].Color == card.Color) { insertion = i + 1; break; }

            // Cards to the right of insertion shift one slot over (slight lift, staggered).
            for (int i = placed.Count - 1; i >= insertion; i--)
            {
                var c = placed[i];
                if (c == null) continue;
                float delay = (i - insertion) * 0.04f;
                StartCoroutine(ShiftCard(c, slots[i + 1], delay));
            }

            placed.Insert(insertion, card);
            Count = placed.Count;
            card.State = CardState.Dock;

            // Hide the pocket of the newly filled slot at the right end (filled region is always 0..Count-1).
            int lastIdx = Count - 1;
            if (slotPockets != null && lastIdx < slotPockets.Length && slotPockets[lastIdx] != null)
                slotPockets[lastIdx].enabled = false;

            card.transform.localScale = DockCardScale;
            card.transform.rotation = DockCardRot;
            // From the belt end, it FLIES (arcs) to land in the slot next to its group.
            Vector3 target = slots[insertion];
            card.MoveArc(target, 1.3f, 0.42f, () =>
            {
                if (card != null)
                {
                    card.transform.rotation = DockCardRot;
                    Juice.PunchScale(card.transform, DockCardScale, 0.12f, 0.12f);
                }
            });
            Sfx.Play("dock");

            if (Count >= Capacity && gm != null)
                gm.OnDockFull();
        }

        /// <summary>Shifts an existing dock card to its new slot with a slight lift (staggered delay).</summary>
        IEnumerator ShiftCard(Card card, Vector3 target, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (card == null) yield break;
            card.MoveArc(target, 0.35f, 0.26f, () =>
            {
                if (card != null) card.transform.rotation = DockCardRot;
            });
        }
    }
}
