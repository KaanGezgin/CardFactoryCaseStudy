using CardFactory.Core;
using CardFactory.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardFactory.UI
{
    /// <summary>
    /// uGUI HUD (koddan). Sayaç ve dock teklifi 3B/objelere gömülü olduğundan burada
    /// yalnızca kazan/kaybet paneli yönetilir: beyaz+renkli iki katmanlı banner,
    /// kazanmada NEXT, kaybetmede sağda X (kapat=yeniden başlat). Kaybetme anında
    /// dock teklifini ve spot ışığını tetikler.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        GameManager gm;
        Dock dock;

        Font font;
        GameObject panel;
        Image bannerBack;     // beyaz (en arkada)
        Image bannerFront;    // renkli (kırmızı/yeşil)
        Text bannerText;
        GameObject nextButton;
        GameObject closeButton;

        Sprite roundedSprite;

        public void Init(GameManager gameManager, Dock dockRef, Transform parent)
        {
            gm = gameManager;
            dock = dockRef;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            roundedSprite = MakeRoundedSprite(64, 22);

            EnsureEventSystem(parent);
            BuildCanvas(parent);
        }

        void EnsureEventSystem(Transform parent)
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.transform.SetParent(parent, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        void BuildCanvas(Transform parent)
        {
            var canvasGo = new GameObject("HUDCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildPanel(canvasGo.transform);
        }

        void BuildPanel(Transform root)
        {
            panel = new GameObject("EndPanel");
            panel.transform.SetParent(root, false);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.38f);   // hafif karartma (3B sahne ve spot görünür kalsın)
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // İki katmanlı banner: en arkada beyaz, üstünde renkli (kırmızı/yeşil).
            bannerBack = MakePanelImage("BannerBack", panel.transform, Color.white,
                new Vector2(880, 360), new Vector2(0, 220));
            bannerFront = MakePanelImage("BannerFront", panel.transform,
                new Color(0.93f, 0.27f, 0.30f), new Vector2(820, 300), new Vector2(0, 220));

            bannerText = MakeText("Banner", panel.transform, 100, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(820, 300));
            bannerText.color = Color.white;

            nextButton = MakeButton("NextBtn", panel.transform, "NEXT LEVEL", new Vector2(0, -260),
                () => gm.NextLevel());

            closeButton = MakeCloseButton(panel.transform, () => gm.Restart());

            panel.SetActive(false);
        }

        // Runtime'da yuvarlak köşeli (9-slice) beyaz sprite üretir. Builtin kaynak yok.
        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    // En yakın köşe merkezine uzaklığa göre kenarları yuvarla (AA'lı).
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
            if (roundedSprite != null)
            {
                img.sprite = roundedSprite;
                img.type = Image.Type.Sliced;
            }
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
                              UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.55f, 0.95f);
            if (roundedSprite != null)
            {
                img.sprite = roundedSprite;
                img.type = Image.Type.Sliced;
            }
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(520, 150);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            MakeText(name + "_Label", go.transform, 48, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 150));
            return go;
        }

        // Sol tarafta kırmızı, X sembollü kapat butonu (kaybetme ekranında çıkar).
        GameObject MakeCloseButton(Transform parent, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("CloseBtn");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.93f, 0.27f, 0.30f);
            if (roundedSprite != null)
            {
                img.sprite = roundedSprite;
                img.type = Image.Type.Sliced;
            }
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
            if (gm == null) return;

            bool ended = gm.State == GameState.Won || gm.State == GameState.Lost;
            if (panel.activeSelf != ended) panel.SetActive(ended);
            if (!ended) return;

            bool won = gm.State == GameState.Won;
            bannerText.text = won ? "LEVEL COMPLETE" : "LEVEL FAILED";
            bannerFront.color = won ? new Color(0.25f, 0.72f, 0.35f) : new Color(0.93f, 0.27f, 0.30f);

            if (nextButton.activeSelf != won) nextButton.SetActive(won);
            if (closeButton.activeSelf != !won) closeButton.SetActive(!won);

            if (!won && dock != null) dock.ShowFailOffer();
        }
    }
}
