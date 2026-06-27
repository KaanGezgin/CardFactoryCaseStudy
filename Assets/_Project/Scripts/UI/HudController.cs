using System.Collections;
using CardFactory.Core;
using CardFactory.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardFactory.UI
{
    /// <summary>
    /// Win/lose panel (LEVEL COMPLETE / FAILED). Now part of the PERSISTENT world: built once,
    /// stays in the scene. Reads state from GameManager.Instance (no per-level binding); buttons
    /// act through Instance. When reused from a baked scene, Rebind refreshes references/listeners.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        Dock dock;
        Font font;
        Sprite roundedSprite;

        GameObject panel;
        Image endTint;          // full-screen dim/color tint (red/green in the ad)
        Image bannerBack;
        Image bannerFront;
        Text bannerText;
        Text adSub;             // ad subtitle ("Can you do it?" / "So satisfying!")
        Coroutine adPopCo;
        bool adGlowOn;          // show DockGlow (FailOfferGlow) on the ad fail beat
        Image flashImg;         // full-screen flash (belt full/blocked → red)
        Coroutine flashCo;
        GameObject nextButton;
        GameObject closeButton;

        GameObject failGlowGo;
        RectTransform failGlowRt;
        Image failGlowImg;
        float failGlowPulse;

        // --- Setup (bake / fresh) ---

        public void Init()
        {
            font = LoadUiFont();
            roundedSprite = MakeRoundedSprite(64, 22);

            EnsureEventSystem();
            BuildCanvas();
            dock = Object.FindFirstObjectByType<Dock>();
        }

        // --- Reuse from a baked scene: refresh references and listeners ---

        public void Rebind()
        {
            font = LoadUiFont();

            var canvas = transform.Find("HUDCanvas");
            if (canvas != null)
            {
                var panelT = canvas.Find("EndPanel");
                if (panelT != null)
                {
                    panel = panelT.gameObject;
                    endTint = panel.GetComponent<Image>();
                    bannerBack = panelT.Find("BannerBack")?.GetComponent<Image>();
                    bannerFront = panelT.Find("BannerFront")?.GetComponent<Image>();
                    bannerText = panelT.Find("Banner")?.GetComponent<Text>();
                    nextButton = panelT.Find("NextBtn")?.gameObject;
                    closeButton = panelT.Find("CloseBtn")?.gameObject;
                    adSub = panelT.Find("AdSub")?.GetComponent<Text>();
                    if (adSub == null)
                    {
                        adSub = MakeText("AdSub", panel.transform, 64, TextAnchor.MiddleCenter,
                            new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 160));
                        adSub.color = Color.white;
                    }
                    ApplyTextFx(bannerText);
                    ApplyTextFx(adSub);
                    adSub.gameObject.SetActive(false);
                    WireButton(nextButton, () => GameManager.Instance?.NextLevel());
                    WireButton(closeButton, () => GameManager.Instance?.Restart());
                    panel.SetActive(false);
                }

                var glowT = canvas.Find("FailOfferGlow");
                if (glowT != null)
                {
                    failGlowGo = glowT.gameObject;
                    failGlowRt = glowT.GetComponent<RectTransform>();
                    failGlowImg = glowT.GetComponent<Image>();
                    // Replace the old baked sprite with the new donut glow; don't touch POSITION/SIZE
                    // (the values you set in the Inspector are preserved).
                    if (failGlowImg != null)
                    {
                        failGlowImg.sprite = MakeSoftGlowSprite();
                        failGlowImg.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.5f);
                    }
                    failGlowGo.SetActive(false);
                }
                else if (panel != null)
                {
                    BuildFailOfferGlow(canvas);
                }
            }

            EnsureEventSystem();
            dock = Object.FindFirstObjectByType<Dock>();
        }

        void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("HUDCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildPanel(canvasGo.transform);
            BuildFailOfferGlow(canvasGo.transform);
        }

        void BuildPanel(Transform root)
        {
            panel = new GameObject("EndPanel");
            panel.transform.SetParent(root, false);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.22f);
            endTint = img;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            bannerBack = MakePanelImage("BannerBack", panel.transform, Color.white,
                new Vector2(880, 360), new Vector2(0, 220));
            bannerFront = MakePanelImage("BannerFront", panel.transform,
                new Color(0.93f, 0.27f, 0.30f), new Vector2(820, 300), new Vector2(0, 220));

            bannerText = MakeText("Banner", panel.transform, 100, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(820, 300));
            bannerText.color = Color.white;
            ApplyTextFx(bannerText);

            adSub = MakeText("AdSub", panel.transform, 64, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 160));
            adSub.color = Color.white;
            ApplyTextFx(adSub);
            adSub.gameObject.SetActive(false);

            nextButton = MakeButton("NextBtn", panel.transform, "NEXT LEVEL", new Vector2(0, -260),
                () => GameManager.Instance?.NextLevel());
            closeButton = MakeCloseButton(panel.transform, () => GameManager.Instance?.Restart());

            panel.SetActive(false);
        }

        // Soft light-pool color (like image 3: neutral-cool white, glows with bloom).
        static readonly Color GlowColor = new Color(0.92f, 0.96f, 1f);

        /// <summary>
        /// A soft radial light pool drawn ABOVE the fail-screen dim that "lights up" the dock
        /// offer (filled glow; like image 3). A sibling of EndPanel.
        /// </summary>
        void BuildFailOfferGlow(Transform canvasRoot)
        {
            failGlowGo = new GameObject("FailOfferGlow");
            failGlowGo.transform.SetParent(canvasRoot, false);
            failGlowRt = failGlowGo.AddComponent<RectTransform>();
            failGlowRt.anchorMin = failGlowRt.anchorMax = new Vector2(0.5f, 0.5f);
            failGlowRt.pivot = new Vector2(0.5f, 0.5f);
            // Default position/size (preserved once tuned in the Inspector and baked).
            failGlowRt.sizeDelta = new Vector2(309f, 337f);
            failGlowRt.anchoredPosition3D = new Vector3(340f, -321.5837f, 16.68921f);

            failGlowImg = failGlowGo.AddComponent<Image>();
            failGlowImg.raycastTarget = false;
            failGlowImg.sprite = MakeSoftGlowSprite();
            failGlowImg.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.5f);
            failGlowGo.SetActive(false);
        }

        /// <summary>
        /// Soft ring (donut) glow — CENTER EMPTY (the offer behind shows through), soft glow at
        /// the edge. mid = peak radius, w = softness width of the ring.
        /// </summary>
        static Sprite MakeSoftGlowSprite(int size = 256, float mid = 0.66f, float w = 0.40f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0=center
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / w);            // ring
                    a = a * a * (3f - 2f * a);   // smoothstep → soft edge
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        void UpdateFailOfferGlow(bool show)
        {
            if (failGlowGo == null) return;
            if (!show)
            {
                failGlowGo.SetActive(false);
                return;
            }

            // Position/size/scale come from the Inspector; CODE DOES NOT override them. Only a soft alpha breath.
            failGlowPulse += Time.deltaTime;
            float breath = Mathf.Sin(failGlowPulse * 1.6f) * 0.5f + 0.5f;
            float alpha = Mathf.Lerp(0.32f, 0.5f, breath);
            if (failGlowImg != null)
                failGlowImg.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, alpha);
            failGlowGo.SetActive(true);
        }

        /// <summary>
        /// For the AD (AdDirector): reuses the existing rounded banner panel. NEXT/X buttons hidden;
        /// subtitle + full-screen color tint (red fail / green success). Timing lives in AdDirector.
        /// </summary>
        public void ShowAdMessage(string banner, string sub, Color front, Color tint,
                                  bool showClose = false, bool failGlow = false)
        {
            if (panel == null) return;
            panel.SetActive(true);
            if (bannerText != null) bannerText.text = banner;
            if (bannerFront != null) bannerFront.color = front;
            if (nextButton != null) nextButton.SetActive(false);
            if (closeButton != null) closeButton.SetActive(showClose);
            adGlowOn = failGlow;
            if (adSub != null)
            {
                adSub.text = sub;
                adSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            }
            if (adPopCo != null) StopCoroutine(adPopCo);
            adPopCo = StartCoroutine(AdPop(tint));
        }

        public void HideAdMessage()
        {
            if (adPopCo != null) { StopCoroutine(adPopCo); adPopCo = null; }
            SetBannerScale(1f);
            adGlowOn = false;
            if (panel != null) panel.SetActive(false);
            if (endTint != null) endTint.color = new Color(0f, 0f, 0f, 0.22f);
            if (adSub != null) adSub.gameObject.SetActive(false);
            if (failGlowGo != null) failGlowGo.SetActive(false);
        }

        /// <summary>Banner pop (ease-out-back) + tint fade, then a soft breath. Fully code-driven effect.</summary>
        IEnumerator AdPop(Color tint)
        {
            float t = 0f, dur = 0.34f;
            while (t < dur)
            {
                float k = t / dur;
                SetBannerScale(Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(k)));
                if (endTint != null)
                    endTint.color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.SmoothStep(0f, 1f, k));
                t += Time.deltaTime;
                yield return null;
            }
            SetBannerScale(1f);
            if (endTint != null) endTint.color = tint;

            float b = 0f;
            while (true)   // soft breath while it stays on screen
            {
                b += Time.deltaTime;
                SetBannerScale(1f + Mathf.Sin(b * 2.4f) * 0.02f);
                yield return null;
            }
        }

        void SetBannerScale(float s)
        {
            var v = new Vector3(s, s, 1f);
            if (bannerBack != null) bannerBack.rectTransform.localScale = v;
            if (bannerFront != null) bannerFront.rectTransform.localScale = v;
            if (bannerText != null) bannerText.rectTransform.localScale = v;
            if (adSub != null) adSub.rectTransform.localScale = v;
        }

        static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float p = k - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        /// <summary>
        /// Custom font: use `Assets/Resources/UIFont.ttf` (or .otf) if present; otherwise builtin.
        /// Hypercasual Google Fonts: Fredoka / Lilita One / Luckiest Guy / Titan One / Baloo 2.
        /// </summary>
        static Font LoadUiFont()
        {
            var f = Resources.Load<Font>("UIFont");
            if (f != null) return f;
            f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        /// <summary>Chunky look like the reference: dark outline + soft shadow (in code, no assets).</summary>
        static void ApplyTextFx(Text t)
        {
            if (t == null || t.GetComponent<Outline>() != null) return;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.30f, 0.16f, 0.13f, 0.95f);
            o.effectDistance = new Vector2(5f, -5f);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
            sh.effectDistance = new Vector2(0f, -8f);
        }

        /// <summary>Briefly flashes the full screen with a color (belt full/blocked feedback). Code-driven.</summary>
        public void FlashScreen(Color color, float duration)
        {
            EnsureFlash();
            if (flashImg == null) return;
            if (flashCo != null) StopCoroutine(flashCo);
            flashCo = StartCoroutine(FlashRoutine(color, duration));
        }

        void EnsureFlash()
        {
            if (flashImg != null) return;
            var canvas = transform.Find("HUDCanvas");
            if (canvas == null) return;
            var go = new GameObject("ScreenFlash");
            go.transform.SetParent(canvas, false);
            flashImg = go.AddComponent<Image>();
            flashImg.raycastTarget = false;
            var rt = flashImg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            flashImg.color = new Color(1f, 0f, 0f, 0f);
        }

        IEnumerator FlashRoutine(Color color, float duration)
        {
            flashImg.transform.SetAsLastSibling();
            float t = 0f;
            while (t < duration)
            {
                float k = 1f - t / duration;        // fade from peak → 0
                flashImg.color = new Color(color.r, color.g, color.b, color.a * k);
                t += Time.deltaTime;
                yield return null;
            }
            flashImg.color = new Color(color.r, color.g, color.b, 0f);
        }

        void WireButton(GameObject go, UnityAction action)
        {
            if (go == null) return;
            var btn = go.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    float cx = x < radius ? radius : (x > size - 1 - radius ? size - 1 - radius : x);
                    float cy = y < radius ? radius : (y > size - 1 - radius ? size - 1 - radius : y);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > radius) a = Mathf.Clamp01(1f - (d - radius));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        Image MakePanelImage(string name, Transform parent, Color color, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (roundedSprite != null) { img.sprite = roundedSprite; img.type = Image.Type.Sliced; }
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return img;
        }

        Text MakeText(string name, Transform parent, int size, TextAnchor anchor,
                      Vector2 anchorPivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = anchorPivot;
            rt.anchorMax = anchorPivot;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return t;
        }

        GameObject MakeButton(string name, Transform parent, string label, Vector2 anchoredPos,
                              UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.55f, 0.95f);
            if (roundedSprite != null) { img.sprite = roundedSprite; img.type = Image.Type.Sliced; }
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(520, 150);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            MakeText(name + "_Label", go.transform, 48, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 150));
            return go;
        }

        GameObject MakeCloseButton(Transform parent, UnityAction onClick)
        {
            var go = new GameObject("CloseBtn");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.93f, 0.27f, 0.30f);
            if (roundedSprite != null) { img.sprite = roundedSprite; img.type = Image.Type.Sliced; }
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(14f, -603.6f);
            rt.sizeDelta = new Vector2(160, 160);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            MakeText("Close_Label", go.transform, 90, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 160)).text = "X";
            return go;
        }

        void Update()
        {
            // In ad mode AdDirector drives the panel explicitly (ShowAdMessage); skip the automatic flow.
            if (GameBootstrap.AdMode)
            {
                UpdateFailOfferGlow(adGlowOn);   // show DockGlow (donut) on the fail beat
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null) return;

            bool ended = gm.State == GameState.Won || gm.State == GameState.Lost;
            if (panel != null && panel.activeSelf != ended) panel.SetActive(ended);
            UpdateFailOfferGlow(false);   // FailOfferGlow off (disliked)
            if (!ended || panel == null) return;

            bool won = gm.State == GameState.Won;
            if (bannerText != null) bannerText.text = won ? "LEVEL COMPLETE" : "LEVEL FAILED";
            if (bannerFront != null)
                bannerFront.color = won ? new Color(0.25f, 0.72f, 0.35f) : new Color(0.93f, 0.27f, 0.30f);

            if (nextButton != null && nextButton.activeSelf != won) nextButton.SetActive(won);
            if (closeButton != null && closeButton.activeSelf != !won) closeButton.SetActive(!won);

            if (!won && dock != null) dock.ShowFailOffer();
        }
    }
}
