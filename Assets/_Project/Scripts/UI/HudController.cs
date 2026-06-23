using CardFactory.Core;
using CardFactory.Feedback;
using CardFactory.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardFactory.UI
{
    /// <summary>
    /// uGUI HUD (koddan). Belt sayacı kapının üstünde, "+4 slots/400" dock
    /// tepsisinin ucunda — dünya konumlarına kilitlenir (her kare ekrana yansıtılır).
    /// Kazan/kaybet paneli + butonlar.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        GameManager gm;
        Conveyor conveyor;
        FactoryGate gate;
        Camera cam;
        Vector3 beltAnchor;
        Vector3 slotsAnchor;

        Font font;
        RectTransform beltGroup;
        Text beltText;
        RectTransform slotsGroup;
        GameObject panel;
        Text bannerText;
        GameObject nextButton;

        public void Init(GameManager gameManager, Conveyor conv, FactoryGate gateRef,
                         Camera camera, Vector3 beltWorld, Vector3 slotsWorld, Transform parent)
        {
            gm = gameManager;
            conveyor = conv;
            gate = gateRef;
            cam = camera;
            beltAnchor = beltWorld;
            slotsAnchor = slotsWorld;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

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
            var root = canvasGo.transform;

            BuildBeltCounter(root);
            BuildSlotsWidget(root);
            BuildPanel(root);
        }

        // Kapının üstünde "X/20" makine göstergesi
        void BuildBeltCounter(Transform root)
        {
            var go = new GameObject("BeltCounter");
            go.transform.SetParent(root, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.12f, 0.18f, 0.92f);
            beltGroup = img.rectTransform;
            beltGroup.sizeDelta = new Vector2(150, 90);

            beltText = MakeText("BeltText", go.transform, 46, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 90));
        }

        // Dock kaidesinin üstünde yeşil "+4 slots / 400" butonu (DEKORATİF).
        // Kaide 3B obje; bu yeşil onun üstüne oturup birleşik görünür.
        void BuildSlotsWidget(Transform root)
        {
            var holder = new GameObject("SlotsWidget");
            holder.transform.SetParent(root, false);
            var img = holder.AddComponent<Image>();
            img.color = new Color(0.20f, 0.66f, 0.32f);     // yeşil buton
            slotsGroup = img.rectTransform;
            slotsGroup.sizeDelta = new Vector2(200, 130);

            var btn = holder.AddComponent<Button>();
            btn.onClick.AddListener(() => Sfx.Play("warn"));

            MakeText("SlotsLabel", holder.transform, 34, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(190, 60)).text = "+4 slots";

            var coin = new GameObject("Coin");
            coin.transform.SetParent(holder.transform, false);
            var coinImg = coin.AddComponent<Image>();
            coinImg.color = new Color(1f, 0.82f, 0.10f);
            var crt = coinImg.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(-48f, -34f);
            crt.sizeDelta = new Vector2(44f, 44f);

            MakeText("CoinText", holder.transform, 38, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(-8, -34), new Vector2(150, 56)).text = "400";
        }

        void BuildPanel(Transform root)
        {
            panel = new GameObject("EndPanel");
            panel.transform.SetParent(root, false);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            bannerText = MakeText("Banner", panel.transform, 110, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1000, 200));

            MakeButton("RestartBtn", panel.transform, "RESTART", new Vector2(0, -60),
                () => gm.Restart());
            nextButton = MakeButton("NextBtn", panel.transform, "NEXT LEVEL", new Vector2(0, -260),
                () => gm.NextLevel());

            panel.SetActive(false);
        }

        Text MakeText(string name, Transform parent, int size, TextAnchor anchor,
                      Vector2 anchorPivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
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

        void PlaceAtWorld(RectTransform rt, Vector3 world)
        {
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            rt.gameObject.SetActive(sp.z > 0f);
            if (sp.z > 0f) rt.position = new Vector3(sp.x, sp.y, 0f);
        }

        void Update()
        {
            if (gm == null) return;

            bool warn = gate != null && gate.WarningActive;
            beltText.text = $"{conveyor.BeltCount}/{gm.Config.beltMaxCards}";
            beltText.color = warn ? new Color(1f, 0.3f, 0.3f) : Color.white;

            PlaceAtWorld(beltGroup, beltAnchor);
            PlaceAtWorld(slotsGroup, slotsAnchor);

            bool ended = gm.State == GameState.Won || gm.State == GameState.Lost;
            if (panel.activeSelf != ended) panel.SetActive(ended);
            if (ended)
            {
                bool won = gm.State == GameState.Won;
                bannerText.text = won ? "LEVEL COMPLETE" : "LEVEL FAILED";
                bannerText.color = won ? new Color(0.3f, 0.95f, 0.4f) : new Color(0.95f, 0.3f, 0.3f);
                if (nextButton.activeSelf != won) nextButton.SetActive(won);
            }
        }
    }
}
