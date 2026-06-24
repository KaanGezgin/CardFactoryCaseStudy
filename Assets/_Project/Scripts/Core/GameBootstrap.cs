using System.Collections.Generic;
using CardFactory.Data;
using CardFactory.Feedback;
using CardFactory.Gameplay;
using CardFactory.InputSys;
using CardFactory.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardFactory.Core
{
    /// <summary>
    /// Boş sahnede Play'e basınca tüm oyunu KODDAN kurar. U-şekilli konveyör,
    /// ortadaki kutular, dock tepsisi, alttaki desteler ve dünya konumlu UI.
    /// </summary>
    public static class GameBootstrap
    {
        const float GroundSize = 18f;

        // U-yol geometrisi
        const float LegX = 2.8f;
        const float ZTop = 5.5f;
        const float ZBot = 1.0f;
        const float Bulge = 1.7f;
        const float BeltY = 0.15f;

        const float StackZ = -6.0f;
        const float DockZ = -3.0f;

        public const float CameraPitch = 62f;   // kamera eğimi; 3B etiketler bununla kameraya bakar

        static int? pendingBuild;

        // Kalıcı ortam (her level'de yeniden yaratılmaz) referansları.
        static GameObject levelRoot;
        static Dock dock;
        static FactoryGate gate;
        static BeltPath path;
        static HudController hud;
        static Transform[] stackAnchors;   // kartların (destelerin) yaratılacağı yerler
        static Transform[] binAnchors;     // ortadaki kutuların yaratılacağı yerler

        static readonly Vector3[] BinSlots =
        {
            new Vector3(-0.95f, 0f, 0.3f),
            new Vector3(0.95f, 0f, 0.3f),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var runnerGo = new GameObject("GameRunner");
            runnerGo.AddComponent<GameRunner>();
            Object.DontDestroyOnLoad(runnerGo);
            BuildWorld();
            BuildLevel(0);
        }

        public static void RequestRebuild(int levelIndex) => pendingBuild = levelIndex;

        internal static void TickRebuild()
        {
            if (!pendingBuild.HasValue) return;
            int idx = pendingBuild.Value;
            pendingBuild = null;
            BuildLevel(idx);
        }

        /// <summary>
        /// KALICI ortamı bir kez kurar: kamera, ışık, zemin, U-yol + baş/son objeler,
        /// kapı (0/20 ekranı), dock tepsisi + slotları + teklif + fail ışığı. Bunlar
        /// level değiştikçe yeniden yaratılmaz.
        /// </summary>
        static void BuildWorld()
        {
            var existing = GameObject.Find("CardFactoryWorld");
            if (existing != null)
            {
                var marker = existing.GetComponent<WorldPersistence>();
                if (marker == null || !marker.rebuildOnPlay)
                {
                    ReuseWorld(existing);   // mevcut sahne objeleri korunur (yeniden yaratılmaz)
                    Debug.Log("[GameBootstrap] Mevcut dünya korundu (yeniden yaratılmadı).");
                    return;
                }
                Object.DestroyImmediate(existing);  // rebuildOnPlay açık → baştan kur
            }

            CleanScene();
            BuildWorldObjects();
            Debug.Log("[GameBootstrap] Kalıcı ortam (runtime) kuruldu.");
        }

        /// <summary>
        /// Sahnede var olan dünyayı yeniden YARATMADAN, GÖRÜNÜŞÜNÜ DEĞİŞTİRMEDEN kullanır:
        /// sadece referansları yeniden bağlar ve anchor'ları bulur. Inspector düzenlemeleri korunur.
        /// </summary>
        static void ReuseWorld(GameObject world)
        {
            path = new BeltPath(BuildUPath());   // yol verisi (serialize edilmez)
            dock = world.GetComponentInChildren<Dock>(true);
            gate = world.GetComponentInChildren<FactoryGate>(true);
            hud = world.GetComponentInChildren<HudController>(true);
            if (dock != null) dock.Rebind();
            if (gate != null) gate.Rebind(GameConfig.Default);
            if (hud != null) hud.Rebind();

            stackAnchors = CollectAnchors(world, "StackAnchor_");
            if (stackAnchors.Length == 0) stackAnchors = BuildStackAnchors(world.transform);
            binAnchors = CollectAnchors(world, "BinAnchor_");
            if (binAnchors.Length == 0) binAnchors = BuildBinAnchors(world.transform);

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam != null && !cam.transform.IsChildOf(world.transform))
                    Object.DestroyImmediate(cam.gameObject);
            foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (al != null && !al.transform.IsChildOf(world.transform))
                    Object.DestroyImmediate(al.gameObject);

            ApplyPolish(world);   // cila runtime'da da uygulanır (re-bake gerekmeden)
        }

        static void ClearAnchorChildren(Transform[] anchors)
        {
            if (anchors == null) return;
            foreach (var a in anchors)
            {
                if (a == null) continue;
                for (int i = a.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(a.GetChild(i).gameObject);
            }
        }

        static Transform[] CollectAnchors(GameObject world, string prefix)
        {
            var list = new List<Transform>();
            for (int i = 0; ; i++)
            {
                var t = world.transform.Find(prefix + i);
                if (t == null) break;
                list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Kalıcı ortam objelerini oluşturur (CleanScene/runner YOK). Hem runtime
        /// BuildWorld hem de Editör 'Bake World' aracı bunu kullanır.
        /// </summary>
        public static GameObject BuildWorldObjects()
        {
            var config = GameConfig.Default;
            var world = new GameObject("CardFactoryWorld");

            BuildCamera(config, world.transform);
            BuildLight(world.transform);
            BuildGround(world.transform);

            var pathPts = BuildUPath();
            path = new BeltPath(pathPts);
            BuildBeltVisual(world.transform, pathPts);

            gate = BuildGate(world.transform, config, pathPts[0]);
            BuildEndCap(world.transform, pathPts[pathPts.Count - 1]);

            dock = new GameObject("Dock").AddComponent<Dock>();
            dock.transform.SetParent(world.transform, false);
            dock.Init(config.dockCapacity, DockZ);

            // Kazan/kaybet paneli de KALICI (sahnede kalır).
            hud = new GameObject("HudController").AddComponent<HudController>();
            hud.transform.SetParent(world.transform, false);
            hud.Init();

            // Kart desteleri ve ortadaki kutular için ANCHOR'lar (boş, taşınabilir).
            stackAnchors = BuildStackAnchors(world.transform);
            binAnchors = BuildBinAnchors(world.transform);

            world.AddComponent<WorldPersistence>();
            ApplyPolish(world);
            return world;
        }

        static Transform[] BuildStackAnchors(Transform parent)
        {
            const int n = 4;
            float width = Mathf.Min(7.6f, (n - 1) * 2.5f);
            float x0 = -width * 0.5f;
            float step = width / (n - 1);
            var arr = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"StackAnchor_{i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x0 + i * step, 0f, StackZ);
                arr[i] = go.transform;
            }
            return arr;
        }

        static Transform[] BuildBinAnchors(Transform parent)
        {
            var arr = new Transform[BinSlots.Length];
            for (int i = 0; i < BinSlots.Length; i++)
            {
                var go = new GameObject($"BinAnchor_{i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = BinSlots[i];
                arr[i] = go.transform;
            }
            return arr;
        }

        /// <summary>
        /// Editör 'Bake World' için: eski baked dünyayı + fazla kamera/listener'ı
        /// temizler ve kalıcı ortamı SAHNEYE gerçek obje olarak kurar (edit mode).
        /// </summary>
        public static GameObject BakeWorld()
        {
            DestroyByName("CardFactory");
            DestroyByName("CardFactoryWorld");
            DestroyByName("CardFactoryLevel");
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (al != null) Object.DestroyImmediate(al.gameObject);
            return BuildWorldObjects();
        }

        /// <summary>
        /// PER-LEVEL içeriği (yeniden) kurar: yöneticiler, kutular, konveyör+kartlar,
        /// desteler, input, ghost pointer, HUD paneli. Kalıcı ortam korunur; dock/kapı
        /// durumu sıfırlanır.
        /// </summary>
        public static void BuildLevel(int levelIndex)
        {
            if (dock == null || gate == null || path == null) BuildWorld();
            if (levelRoot != null) Object.DestroyImmediate(levelRoot);

            // Eski kart/kutu içeriğini anchor'lardan temizle (anchor'lar kalır).
            ClearAnchorChildren(stackAnchors);
            ClearAnchorChildren(binAnchors);

            dock.ResetForNewLevel();
            gate.ResetVisual();

            var config = GameConfig.Default;
            var level = DefaultLevels.Get(levelIndex);
            levelRoot = new GameObject("CardFactoryLevel");

            dock.SetTension(config.dockTensionPulse);   // reklam için saklı (şimdilik kapalı)

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.transform.SetParent(levelRoot.transform, false);
            gm.Init(config, levelIndex, level);
            dock.Bind(gm);

            var binMgr = new GameObject("BinManager").AddComponent<BinManager>();
            binMgr.transform.SetParent(levelRoot.transform, false);
            binMgr.Init(config, level, gm, binAnchors, path);   // kutular bin anchor'ları altında

            var conveyor = new GameObject("Conveyor").AddComponent<Conveyor>();
            conveyor.transform.SetParent(levelRoot.transform, false);
            conveyor.Init(config, gm, binMgr, dock, gate, path);

            var stacks = BuildStacks(level, conveyor);   // kartlar stack anchor'ları altında
            gm.SetSystems(stacks, conveyor);

            var input = new GameObject("InputController").AddComponent<InputController>();
            input.transform.SetParent(levelRoot.transform, false);
            input.Init(Camera.main, gm);

            // Ghost el-pointer (reklam için saklı; şimdilik kapalı)
            if (config.showHandPointer)
            {
                var firstColor = level.binColorOrder.Count > 0 ? level.binColorOrder[0] : (CardColor?)null;
                CardStack target = null;
                foreach (var s in stacks)
                    if (firstColor.HasValue && s.TopColor == firstColor.Value) { target = s; break; }
                if (target == null && stacks.Count > 0) target = stacks[0];
                if (target != null)
                {
                    var hp = new GameObject("HandPointer").AddComponent<HandPointer>();
                    hp.transform.SetParent(levelRoot.transform, false);
                    hp.Init(target.transform.position, input);
                }
            }

            // (HUD kalıcı dünyada; durumu GameManager.Instance'tan okur.)

            Debug.Log($"[GameBootstrap] Level {levelIndex + 1} içeriği kuruldu (kart {level.TotalCards}).");
        }

        static List<Vector3> BuildUPath()
        {
            var pts = new List<Vector3>();
            // Sağ bacak: üst → alt
            for (float z = ZTop; z > ZBot; z -= 0.4f)
                pts.Add(new Vector3(LegX, BeltY, z));
            // Alt yay: sağ → sol
            const int seg = 14;
            for (int i = 0; i <= seg; i++)
            {
                float t = Mathf.PI * i / seg;
                float x = LegX * Mathf.Cos(t);
                float z = ZBot - Bulge * Mathf.Sin(t);
                pts.Add(new Vector3(x, BeltY, z));
            }
            // Sol bacak: alt → üst
            for (float z = ZBot; z <= ZTop; z += 0.4f)
                pts.Add(new Vector3(-LegX, BeltY, z));
            return pts;
        }

        static void BuildBeltVisual(Transform parent, List<Vector3> pts)
        {
            var mat = NewLitMaterial(new Color(0.40f, 0.45f, 0.55f));
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.001f) continue;

                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "BeltSeg";
                var col = seg.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                seg.transform.SetParent(parent, false);
                Vector3 mid = (a + b) * 0.5f;
                seg.transform.position = new Vector3(mid.x, 0.05f, mid.z);
                seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                seg.transform.localScale = new Vector3(1.5f, 0.2f, len + 0.12f);
                seg.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        static FactoryGate BuildGate(Transform parent, GameConfig config, Vector3 startPt)
        {
            // Makine gövdesi
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "FactoryGate";
            var col = body.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            body.transform.SetParent(parent, false);
            body.transform.position = new Vector3(startPt.x, 0.6f, startPt.z);
            body.transform.localScale = new Vector3(1.9f, 1.2f, 0.85f);

            var gateColor = new Color(0.24f, 0.28f, 0.48f);
            body.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(gateColor);

            // Ekran (öne dönük, koyu) — sayaç UI'ı bunun üstüne oturur
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "GateScreen";
            var scol = screen.GetComponent<Collider>();
            if (scol != null) Object.Destroy(scol);
            screen.transform.SetParent(parent, false);
            screen.transform.position = new Vector3(startPt.x, 1.05f, startPt.z - 0.42f);
            screen.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            screen.transform.localScale = new Vector3(1.15f, 0.66f, 0.12f);
            screen.GetComponent<Renderer>().sharedMaterial =
                NewLitMaterial(new Color(0.05f, 0.06f, 0.10f));

            var gate = body.AddComponent<FactoryGate>();

            // Ekrana GÖMÜLÜ 3B sayaç: ekranla aynı konumda/derinlikte, kameraya dönük.
            var counterAnchor = new GameObject("GateCounter");
            counterAnchor.transform.SetParent(parent, false);
            counterAnchor.transform.position = screen.transform.position + new Vector3(0f, 0.16f, -0.16f);
            // Billboard YOK: rotasyon bake'te kamera açısıyla sabitlenir (Inspector'da değiştirilebilir, Play'de kaymaz).
            counterAnchor.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            var counter = MakeWorldText("0/20", counterAnchor.transform, Vector3.zero,
                0.07f, Color.white, 90);

            gate.Init(body.GetComponent<Renderer>(), gateColor, config, counter);
            return gate;
        }

        /// <summary>Dünya-uzayı 3B yazı (TextMesh) üretir. Objelere gömülü etiketler için.</summary>
        public static TextMesh MakeWorldText(string text, Transform parent, Vector3 localPos,
                                             float characterSize, Color color, int fontSize)
        {
            var go = new GameObject("WorldText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = fontSize;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        /// <summary>Yumuşak radyal hale (merkez parlak → kenar şeffaf) sprite'ı üretir.</summary>
        public static Sprite MakeRadialSprite(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            float maxR = c;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;   // kareli düşüş → daha yerel/yumuşak hale (geniş yayılmaz)
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static void BuildEndCap(Transform parent, Vector3 endPt)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BeltEndCap";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(endPt.x, 0.45f, endPt.z);
            go.transform.localScale = new Vector3(1.7f, 0.9f, 0.95f);
            go.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.55f, 0.57f, 0.60f));
        }

        // Desteleri (kartlar) stack anchor'larının ALTINA kurar; anchor konumu kullanılır.
        static List<CardStack> BuildStacks(LevelData level, Conveyor conveyor)
        {
            var result = new List<CardStack>();
            if (stackAnchors == null) return result;
            int n = Mathf.Min(level.stacks.Count, stackAnchors.Length);

            for (int i = 0; i < n; i++)
            {
                var anchor = stackAnchors[i];
                var go = new GameObject($"Stack_{i}");
                go.transform.SetParent(anchor, false);
                var stack = go.AddComponent<CardStack>();
                stack.Init(level.stacks[i], anchor.position, conveyor);
                result.Add(stack);
            }
            return result;
        }

        static void CleanScene()
        {
            DestroyByName("CardFactory");
            DestroyByName("CardFactoryWorld");
            DestroyByName("CardFactoryLevel");

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam != null) Object.DestroyImmediate(cam.gameObject);

            foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (al != null) Object.DestroyImmediate(al.gameObject);
        }

        static void DestroyByName(string n)
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }

        static void BuildCamera(GameConfig config, Transform parent)
        {
            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(parent, false);
            camGo.tag = "MainCamera";

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.backgroundColor;
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            // Daha yukarıdan, daha dik (kuş bakışı) açı.
            camGo.transform.position = new Vector3(0f, 18f, -9f);
            camGo.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);

            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraRig>();
        }

        static void BuildLight(Transform parent)
        {
            var lightGo = new GameObject("DirectionalLight");
            lightGo.transform.SetParent(parent, false);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.61f, 0.66f);
        }

        static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);
            ground.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.78f, 0.84f, 0.90f));
        }

        /// <summary>
        /// Görsel cila (idempotent; hem bake hem reuse'da çalışır): ışık ayarı,
        /// grid zemin, hafif parlaklık, post-processing volume, kamera post-fx.
        /// </summary>
        static void ApplyPolish(GameObject world)
        {
            // Işık
            var light = world.GetComponentInChildren<Light>(true);
            if (light != null)
            {
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.25f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.45f;
                light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.72f);

            // Grid zemin
            var ground = world.transform.Find("Ground");
            if (ground != null)
            {
                var r = ground.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = GroundMaterial();
            }

            // Hafif parlaklık — dünyadaki tüm materyaller
            foreach (var rend in world.GetComponentsInChildren<Renderer>(true))
            {
                var m = rend.sharedMaterial;
                if (m != null && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
            }

            // Post-processing volume (her açılışta taze kur → serialize sorunlarından bağımsız)
            var oldFx = world.transform.Find("PostFX");
            if (oldFx != null) Object.DestroyImmediate(oldFx.gameObject);
            BuildPostFX(world.transform);

            // Kamera post-processing aç
            var cam = world.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
        }

        static void BuildPostFX(Transform parent)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;

            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.Override(0.5f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.6f);

            var vig = profile.Add<Vignette>();
            vig.active = true;
            vig.intensity.Override(0.27f);
            vig.smoothness.Override(0.5f);

            var ca = profile.Add<ColorAdjustments>();
            ca.active = true;
            ca.saturation.Override(18f);
            ca.contrast.Override(8f);
            ca.postExposure.Override(0.05f);

            var tone = profile.Add<Tonemapping>();
            tone.active = true;
            tone.mode.Override(TonemappingMode.Neutral);
        }

        static Material GroundMaterial()
        {
            var mat = NewLitMaterial(new Color(0.85f, 0.90f, 0.96f));
            var tex = MakeGridTexture();
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetTextureScale("_BaseMap", new Vector2(12f, 12f));
            mat.mainTextureScale = new Vector2(12f, 12f);
            mat.SetFloat("_Smoothness", 0.2f);
            return mat;
        }

        static Texture2D MakeGridTexture()
        {
            const int s = 64;
            const int lineW = 3;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var baseC = (Color32)new Color(0.86f, 0.91f, 0.97f);
            var lineC = (Color32)new Color(0.74f, 0.81f, 0.91f);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = (x < lineW || y < lineW) ? lineC : baseC;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Material NewLitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[GameBootstrap] 'Universal Render Pipeline/Lit' shader'ı " +
                               "bulunamadı! Project Settings > Graphics > Always Included " +
                               "Shaders listesine ekle.");
                shader = Shader.Find("Standard");
            }
            var mat = new Material(shader) { color = color };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.32f);   // hafif parlaklık (cila)
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }

    public class GameRunner : MonoBehaviour
    {
        void Update() => GameBootstrap.TickRebuild();
    }
}
