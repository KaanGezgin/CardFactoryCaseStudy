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
        Image endTint;          // tam ekran karartma/renk tint (reklamda kırmızı/yeşil)
        Image bannerFront;
        Text bannerText;
        Text adSub;             // reklam alt yazısı ("Can you do it?" / "So satisfying!")
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
                    endTint = panel.GetComponent<Image>();
                    bannerFront = panelT.Find("BannerFront")?.GetComponent<Image>();
                    bannerText = panelT.Find("Banner")?.GetComponent<Text>();
                    nextButton = panelT.Find("NextBtn")?.gameObject;
                    closeButton = panelT.Find("CloseBtn")?.gameObject;
                    adSub = panelT.Find("AdSub")?.GetComponent<Text>();
                    if (adSub == null)
                    {
                        adSub = MakeText("AdSub", panel.transform, 64, TextAnchor.MiddleCenter,
                            new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(900, 160));
                        adSub.color = Color.white;
                    }
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
                    // Baked eski sprite'ı yeni donut glow ile değiştir; KONUM/BOYUT'a dokunma
                    // (Inspector'dan ayarladığın değerler korunur).
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

            MakePanelImage("BannerBack", panel.transform, Color.white,
                new Vector2(880, 360), new Vector2(0, 220));
            bannerFront = MakePanelImage("BannerFront", panel.transform,
                new Color(0.93f, 0.27f, 0.30f), new Vector2(820, 300), new Vector2(0, 220));

            bannerText = MakeText("Banner", panel.transform, 100, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(820, 300));
            bannerText.color = Color.white;

            adSub = MakeText("AdSub", panel.transform, 64, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(900, 160));
            adSub.color = Color.white;
            adSub.gameObject.SetActive(false);

            nextButton = MakeButton("NextBtn", panel.transform, "NEXT LEVEL", new Vector2(0, -260),
                () => GameManager.Instance?.NextLevel());
            closeButton = MakeCloseButton(panel.transform, () => GameManager.Instance?.Restart());

            panel.SetActive(false);
        }

        // Yumuşak ışık havuzu rengi (image 3 gibi: nötr-serin beyaz, bloom'la parlar).
        static readonly Color GlowColor = new Color(0.92f, 0.96f, 1f);

        /// <summary>
        /// Fail ekranı karartmasının ÜSTÜNDE çizilen, dock teklifini "aydınlatan"
        /// yumuşak radyal ışık havuzu (dolu glow; image 3 gibi). EndPanel ile kardeş.
        /// </summary>
        void BuildFailOfferGlow(Transform canvasRoot)
        {
            failGlowGo = new GameObject("FailOfferGlow");
            failGlowGo.transform.SetParent(canvasRoot, false);
            failGlowRt = failGlowGo.AddComponent<RectTransform>();
            failGlowRt.anchorMin = failGlowRt.anchorMax = new Vector2(0.5f, 0.5f);
            failGlowRt.pivot = new Vector2(0.5f, 0.5f);
            // Varsayılan konum/boyut (Inspector'dan ayarlanıp bake edilince bunlar korunur).
            failGlowRt.sizeDelta = new Vector2(309f, 337f);
            failGlowRt.anchoredPosition3D = new Vector3(340f, -321.5837f, 16.68921f);

            failGlowImg = failGlowGo.AddComponent<Image>();
            failGlowImg.raycastTarget = false;
            failGlowImg.sprite = MakeSoftGlowSprite();
            failGlowImg.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.5f);
            failGlowGo.SetActive(false);
        }

        /// <summary>
        /// Yumuşak halka (donut) glow — MERKEZ BOŞ (arkadaki teklif görünür), kenarda yumuşak
        /// parlama. mid = tepe yarıçapı, w = halkanın yumuşaklık genişliği.
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
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0=merkez
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / w);            // halka
                    a = a * a * (3f - 2f * a);   // smoothstep → yumuşak kenar
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

            // Konum/boyut/scale Inspector'dan gelir; KOD EZMEZ. Sadece hafif alpha nefesi.
            failGlowPulse += Time.deltaTime;
            float breath = Mathf.Sin(failGlowPulse * 1.6f) * 0.5f + 0.5f;
            float alpha = Mathf.Lerp(0.32f, 0.5f, breath);
            if (failGlowImg != null)
                failGlowImg.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, alpha);
            failGlowGo.SetActive(true);
        }

        /// <summary>
        /// REKLAM (AdDirector) için: mevcut yuvarlatılmış banner panelini kullanır. NEXT/X butonları
        /// gizli; alt yazı + tam ekran renk tint (kırmızı fail / yeşil success). Timing AdDirector'da.
        /// </summary>
        public void ShowAdMessage(string banner, string sub, Color front, Color tint)
        {
            if (panel == null) return;
            panel.SetActive(true);
            if (endTint != null) endTint.color = tint;
            if (bannerText != null) bannerText.text = banner;
            if (bannerFront != null) bannerFront.color = front;
            if (nextButton != null) nextButton.SetActive(false);
            if (closeButton != null) closeButton.SetActive(false);
            if (adSub != null)
            {
                adSub.text = sub;
                adSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            }
        }

        public void HideAdMessage()
        {
            if (panel != null) panel.SetActive(false);
            if (endTint != null) endTint.color = new Color(0f, 0f, 0f, 0.22f);
            if (adSub != null) adSub.gameObject.SetActive(false);
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
            // Reklam modunda paneli AdDirector açıkça yönetir (ShowAdMessage); otomatik akışı atla.
            if (GameBootstrap.AdMode)
            {
                if (failGlowGo != null && failGlowGo.activeSelf) failGlowGo.SetActive(false);
                return;
            }

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
