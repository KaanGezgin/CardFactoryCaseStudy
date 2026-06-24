using CardFactory.Core;
using CardFactory.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardFactory.UI
{
    /// <summary>
    /// Kazan/kaybet paneli (LEVEL COMPLETE / FAILED). Artık KALICI dünyanın
    /// parçası: bir kez kurulur, sahnede kalır. Durumu GameManager.Instance'tan
    /// okur (per-level bağ yok), butonlar Instance üzerinden çalışır. Baked
    /// sahneden tekrar kullanılınca Rebind ile referanslar/dinleyiciler yenilenir.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        Dock dock;
        Font font;
        Sprite roundedSprite;

        GameObject panel;
        Image bannerFront;
        Text bannerText;
        GameObject nextButton;
        GameObject closeButton;

        GameObject failGlowGo;
        RectTransform failGlowRt;
        Image failGlowImg;
        float failGlowPulse;

        // --- Kurulum (bake / taze) ---

        public void Init()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            roundedSprite = MakeRoundedSprite(64, 22);

            EnsureEventSystem();
            BuildCanvas();
            dock = Object.FindFirstObjectByType<Dock>();
        }

        // --- Baked sahneden tekrar kullanım: referansları ve dinleyicileri yenile ---

        public void Rebind()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvas = transform.Find("HUDCanvas");
            if (canvas != null)
            {
                var panelT = canvas.Find("EndPanel");
                if (panelT != null)
                {
                    panel = panelT.gameObject;
                    bannerFront = panelT.Find("BannerFront")?.GetComponent<Image>();
                    bannerText = panelT.Find("Banner")?.GetComponent<Text>();
                    nextButton = panelT.Find("NextBtn")?.gameObject;
                    closeButton = panelT.Find("CloseBtn")?.gameObject;
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
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            MakePanelImage("BannerBack", panel.transform, Color.white,
                new Vector2(880, 360), new Vector2(0, 220));
            bannerFront = MakePanelImage("BannerFront", panel.transform,
                new Color(0.93f, 0.27f, 0.30f), new Vector2(820, 300), new Vector2(0, 220));

            bannerText = MakeText("Banner", panel.transform, 100, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(820, 300));
            bannerText.color = Color.white;

            nextButton = MakeButton("NextBtn", panel.transform, "NEXT LEVEL", new Vector2(0, -260),
                () => GameManager.Instance?.NextLevel());
            closeButton = MakeCloseButton(panel.transform, () => GameManager.Instance?.Restart());

            panel.SetActive(false);
        }

        /// <summary>
        /// Fail ekranı karartmasının ÜSTÜNDE çizilen boş çember (3B ışık değil).
        /// EndPanel ile kardeş; yanıp sönerek dock teklifini işaret eder.
        /// </summary>
        void BuildFailOfferGlow(Transform canvasRoot)
        {
            failGlowGo = new GameObject("FailOfferGlow");
            failGlowGo.transform.SetParent(canvasRoot, false);
            failGlowRt = failGlowGo.AddComponent<RectTransform>();
            failGlowRt.anchorMin = failGlowRt.anchorMax = new Vector2(0.5f, 0.5f);
            failGlowRt.pivot = new Vector2(0.5f, 0.5f);
            failGlowRt.sizeDelta = new Vector2(440, 440);

            failGlowImg = failGlowGo.AddComponent<Image>();
            failGlowImg.raycastTarget = false;
            failGlowImg.sprite = MakeRingSprite();
            failGlowImg.color = new Color(1f, 0.92f, 0.35f, 0.9f);
            failGlowGo.SetActive(false);
        }

        /// <summary>İçi boş halka — merkez şeffaf, kenar parlak.</summary>
        static Sprite MakeRingSprite(int size = 256, float inner = 0.58f, float outer = 0.78f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            float mid = (inner + outer) * 0.5f;
            float half = (outer - inner) * 0.5f + 0.04f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = 1f - Mathf.Abs(d - mid) / half;
                    a = Mathf.Clamp01(a);
                    a *= a;
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

            if (dock == null) dock = Object.FindFirstObjectByType<Dock>();
            var cam = Camera.main;
            if (dock == null || cam == null)
            {
                failGlowGo.SetActive(false);
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(dock.OfferWorldPos);
            if (screen.z < 0f)
            {
                failGlowGo.SetActive(false);
                return;
            }

            failGlowRt.position = screen;
            failGlowPulse += Time.deltaTime;
            float blink = Mathf.Sin(failGlowPulse * 3.4f) * 0.5f + 0.5f;
            float alpha = Mathf.Lerp(0.18f, 0.95f, blink);
            float scale = Mathf.Lerp(0.88f, 1.12f, blink);
            failGlowRt.localScale = Vector3.one * scale;
            failGlowImg.color = new Color(1f, 0.92f, 0.35f, alpha);
            failGlowGo.SetActive(true);
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
            rt.anchoredPosition = new Vector2(90, -120);
            rt.sizeDelta = new Vector2(160, 160);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            MakeText("Close_Label", go.transform, 90, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 160)).text = "X";
            return go;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            bool ended = gm.State == GameState.Won || gm.State == GameState.Lost;
            if (panel != null && panel.activeSelf != ended) panel.SetActive(ended);
            UpdateFailOfferGlow(ended && gm.State == GameState.Lost);
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
