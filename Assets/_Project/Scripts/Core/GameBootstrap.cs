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
    /// Builds the whole game FROM CODE when Play is pressed in an empty scene. U-shaped conveyor,
    /// center bins, dock tray, the stacks at the bottom, and world-positioned UI.
    /// </summary>
    public static class GameBootstrap
    {
        const float GroundSize = 18f;

        // U-path geometry
        const float LegX = 2.8f;
        const float ZTop = 5.5f;
        const float ZBot = 1.0f;
        const float Bulge = 1.7f;
        const float BeltY = 0.15f;

        const float StackZ = -6.0f;
        const float DockZ = -3.0f;

        public const float CameraPitch = 62f;   // camera tilt; 3D labels face the camera with this

        static int? pendingBuild;

        /// <summary>Ad mode (AdDirector active): demo board + automatic hand; normal input/HUD suppressed.</summary>
        public static bool AdMode;

        /// <summary>Which demo board in the ad: true → winnable (success), false → unwinnable (fail).</summary>
        public static bool AdWinnableBoard;

        // References to the persistent world (not recreated each level).
        static GameObject levelRoot;
        static Dock dock;
        static FactoryGate gate;
        static BeltPath path;
        static HudController hud;
        static Transform[] stackAnchors;   // where the cards (stacks) are created
        static Transform[] binAnchors;     // where the center bins are created

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

            AdMode = GameConfig.Default.adMode;   // auto-ad if enabled in config

            BuildWorld();
            BuildLevel(0);

            // Ad director: persistent, idles; starts with the 'A' key (or adMode).
            var adGo = new GameObject("AdDirector");
            adGo.AddComponent<AdDirector>();
            Object.DontDestroyOnLoad(adGo);
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
        /// Builds the PERSISTENT world once: camera, light, ground, U-path + start/end objects,
        /// gate (0/20 display), dock tray + slots + offer + fail glow. These are not recreated
        /// when the level changes.
        /// </summary>
        static void BuildWorld()
        {
            var existing = GameObject.Find("CardFactoryWorld");
            if (existing != null)
            {
                var marker = existing.GetComponent<WorldPersistence>();
                if (marker == null || !marker.rebuildOnPlay)
                {
                    ReuseWorld(existing);   // keep existing scene objects (don't recreate)
                    return;
                }
                Object.DestroyImmediate(existing);  // rebuildOnPlay on → rebuild from scratch
            }

            CleanScene();
            BuildWorldObjects();
        }

        /// <summary>
        /// Reuses the existing world WITHOUT recreating it and WITHOUT changing its appearance:
        /// only rebinds references and finds the anchors. Inspector edits are preserved.
        /// </summary>
        static void ReuseWorld(GameObject world)
        {
            path = new BeltPath(BuildUPath());   // path data (not serialized)
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

            ApplyPolish(world);   // polish is applied at runtime too (no re-bake needed)
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
        /// Creates the persistent world objects (NO CleanScene/runner). Used by both the runtime
        /// BuildWorld and the Editor 'Bake World' tool.
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

            // The win/lose panel is PERSISTENT too (stays in the scene).
            hud = new GameObject("HudController").AddComponent<HudController>();
            hud.transform.SetParent(world.transform, false);
            hud.Init();

            // ANCHORS for the card stacks and the center bins (empty, movable).
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
        /// For the Editor 'Bake World': clears the old baked world + extra cameras/listeners and
        /// builds the persistent world into the SCENE as real objects (edit mode).
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
        /// (Re)builds the PER-LEVEL content: managers, bins, conveyor+cards, stacks, input, ghost
        /// pointer, HUD panel. The persistent world is kept; dock/gate state is reset.
        /// </summary>
        public static void BuildLevel(int levelIndex)
        {
            if (dock == null || gate == null || path == null) BuildWorld();
            if (levelRoot != null) Object.DestroyImmediate(levelRoot);

            // Clear the old card/bin content from the anchors (anchors remain).
            ClearAnchorChildren(stackAnchors);
            ClearAnchorChildren(binAnchors);

            dock.ResetForNewLevel();
            gate.ResetVisual();

            var config = GameConfig.Default;
            var level = AdMode
                ? (AdWinnableBoard ? DefaultLevels.GetDemo() : DefaultLevels.GetDemoFail())
                : DefaultLevels.Get(levelIndex);
            levelRoot = new GameObject("CardFactoryLevel");

            dock.SetTension(config.dockTensionPulse);   // reserved for the ad (off for now)

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.transform.SetParent(levelRoot.transform, false);
            gm.Init(config, levelIndex, level);
            dock.Bind(gm);

            var binMgr = new GameObject("BinManager").AddComponent<BinManager>();
            binMgr.transform.SetParent(levelRoot.transform, false);
            binMgr.Init(config, level, gm, binAnchors, path);   // bins under the bin anchors

            var conveyor = new GameObject("Conveyor").AddComponent<Conveyor>();
            conveyor.transform.SetParent(levelRoot.transform, false);
            conveyor.Init(config, gm, binMgr, dock, gate, path);

            var stacks = BuildStacks(level, conveyor);   // cards under the stack anchors
            gm.SetSystems(stacks, conveyor);

            // No mouse input in ad mode (AdDirector drives).
            InputController input = null;
            if (!AdMode)
            {
                input = new GameObject("InputController").AddComponent<InputController>();
                input.transform.SetParent(levelRoot.transform, false);
                input.Init(Camera.main, gm);
            }

            if (config.showHandPointer || AdMode)
            {
                // With mixed stacks, binColorOrder[0] may not be on any top.
                // Show the first valid move: the stack whose top comes EARLIEST in binColorOrder.
                CardStack target = null;
                int bestIdx = int.MaxValue;
                foreach (var s in stacks)
                {
                    if (!s.TopColor.HasValue) continue;
                    int idx = level.binColorOrder.IndexOf(s.TopColor.Value);
                    if (idx < 0) idx = int.MaxValue;
                    if (idx < bestIdx) { bestIdx = idx; target = s; }
                }
                if (target == null && stacks.Count > 0) target = stacks[0];
                if (target != null)
                {
                    var hp = new GameObject("HandPointer").AddComponent<HandPointer>();
                    hp.transform.SetParent(levelRoot.transform, false);
                    hp.Init(target.transform.position, input);
                    if (AdMode) hp.SetAutoTarget(target.transform.position + Vector3.up * 0.4f);
                }
            }

        }

        static List<Vector3> BuildUPath()
        {
            var pts = new List<Vector3>();
            // Right leg: top → bottom
            for (float z = ZTop; z > ZBot; z -= 0.4f)
                pts.Add(new Vector3(LegX, BeltY, z));
            // Bottom arc: right → left
            const int seg = 14;
            for (int i = 0; i <= seg; i++)
            {
                float t = Mathf.PI * i / seg;
                float x = LegX * Mathf.Cos(t);
                float z = ZBot - Bulge * Mathf.Sin(t);
                pts.Add(new Vector3(x, BeltY, z));
            }
            // Left leg: bottom → top
            for (float z = ZBot; z <= ZTop; z += 0.4f)
                pts.Add(new Vector3(-LegX, BeltY, z));
            return pts;
        }

        const float BeltWidth = 1.85f;

        static void BuildBeltVisual(Transform parent, List<Vector3> pts)
        {
            var beltRoot = new GameObject("Belt");
            beltRoot.transform.SetParent(parent, false);

            var surfaceMat = NewLitMaterial(new Color(0.47f, 0.53f, 0.62f));   // lighter blue-gray (reference)
            var railMat = NewLitMaterial(new Color(0.93f, 0.96f, 0.99f));     // clean white side rail
            const float railW = 0.18f;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.001f) continue;

                Vector3 fwd = dir / len;
                Vector3 mid = (a + b) * 0.5f;
                Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
                Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;

                // Belt surface (kept flat; rounding is not wanted on long parts).
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "BeltSeg";
                var scol = seg.GetComponent<Collider>();
                if (scol != null) Object.Destroy(scol);
                seg.transform.SetParent(beltRoot.transform, false);
                seg.transform.position = new Vector3(mid.x, 0.05f, mid.z);
                seg.transform.rotation = rot;
                seg.transform.localScale = new Vector3(BeltWidth, 0.16f, len + 0.14f);
                seg.GetComponent<Renderer>().sharedMaterial = surfaceMat;

                // Two side rails.
                for (int s = -1; s <= 1; s += 2)
                {
                    var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rail.name = "BeltRail";
                    var rcol = rail.GetComponent<Collider>();
                    if (rcol != null) Object.Destroy(rcol);
                    rail.transform.SetParent(beltRoot.transform, false);
                    Vector3 rp = mid + side * (s * (BeltWidth * 0.5f + railW * 0.4f));
                    rail.transform.position = new Vector3(rp.x, 0.13f, rp.z);
                    rail.transform.rotation = rot;
                    rail.transform.localScale = new Vector3(railW, 0.24f, len + 0.14f);
                    rail.GetComponent<Renderer>().sharedMaterial = railMat;
                }
            }

            BuildBeltChevrons(beltRoot.transform, pts);
        }

        static void BuildBeltChevrons(Transform beltRoot, List<Vector3> pts)
        {
            var chevMat = NewLitMaterial(new Color(0.34f, 0.39f, 0.48f));   // dark/matte arrow readable on the light belt
            var flowGo = new GameObject("BeltFlow");
            flowGo.transform.SetParent(beltRoot, false);
            var flow = flowGo.AddComponent<BeltFlow>();

            var p = new BeltPath(pts);
            int count = Mathf.Max(8, Mathf.RoundToInt(p.Length / 0.8f));
            var chevrons = new Transform[count];
            for (int i = 0; i < count; i++)
                chevrons[i] = BuildChevron(flowGo.transform, chevMat);

            flow.Setup(pts.ToArray(), chevrons, 2.2f, 0.17f);
        }

        static Transform BuildChevron(Transform parent, Material mat)
        {
            var root = new GameObject("Chevron");
            root.transform.SetParent(parent, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = "ChevArm";
                var col = arm.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                arm.transform.SetParent(root.transform, false);
                arm.transform.localPosition = new Vector3(s * 0.18f, 0f, -0.08f);
                arm.transform.localRotation = Quaternion.Euler(0f, -s * 32f, 0f);
                arm.transform.localScale = new Vector3(0.09f, 0.06f, 0.6f);
                arm.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return root.transform;
        }

        static FactoryGate BuildGate(Transform parent, GameConfig config, Vector3 startPt)
        {
            // The body sits BEHIND the belt start; its front face ≈ belt start. Cards exit through
            // the MOUTH on the front face (they don't pass through it). Enlarged → counter stays above the cards.
            const float depth = 1.05f, bodyH = 1.95f;
            // The body sits so that its front face is just BEHIND the card spawn point (startPt.z)
            // → a card looks like it came out of the mouth (it doesn't get stuck in front of it).
            float gateZ = startPt.z + 0.5f;
            float frontZ = gateZ - depth * 0.5f;     // front face (just behind the card spawn point)
            var gateColor = new Color(0.24f, 0.28f, 0.48f);

            var body = ProcMesh.RoundedCube("FactoryGate");
            DestroyColliderGO(body);
            body.transform.SetParent(parent, false);
            body.transform.position = new Vector3(startPt.x, bodyH * 0.5f, gateZ);
            body.transform.localScale = new Vector3(2.15f, bodyH, depth);
            body.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(gateColor);

            // Light-blue top trim
            var trim = ProcMesh.RoundedCube("GateTrim");
            DestroyColliderGO(trim);
            trim.transform.SetParent(parent, false);
            trim.transform.position = new Vector3(startPt.x, bodyH + 0.04f, gateZ);
            trim.transform.localScale = new Vector3(2.28f, 0.2f, depth + 0.12f);
            trim.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.70f, 0.78f, 0.92f));

            // Top card entry slot (decorative): dark slot + 2 light lips.
            var slot = ProcMesh.RoundedCube("GateSlot");
            DestroyColliderGO(slot);
            slot.transform.SetParent(parent, false);
            slot.transform.position = new Vector3(startPt.x, bodyH + 0.16f, gateZ);
            slot.transform.localScale = new Vector3(1.5f, 0.14f, 0.34f);
            slot.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.04f, 0.05f, 0.08f));

            var lipMat = NewLitMaterial(new Color(0.82f, 0.88f, 0.97f));
            for (int s = -1; s <= 1; s += 2)
            {
                var lip = ProcMesh.RoundedCube("GateLip");
                DestroyColliderGO(lip);
                lip.transform.SetParent(parent, false);
                lip.transform.position = new Vector3(startPt.x, bodyH + 0.19f, gateZ + s * 0.26f);
                lip.transform.localScale = new Vector3(1.64f, 0.16f, 0.14f);
                lip.GetComponent<Renderer>().sharedMaterial = lipMat;
            }

            // FRONT EXIT MOUTH — aligned to the card SPAWN point (startPt.z): a dark recess frames
            // the card, and the card exits forward (−z) through this mouth.
            float mouthZ = startPt.z;
            var mouth = ProcMesh.RoundedCube("GateMouth");
            DestroyColliderGO(mouth);
            mouth.transform.SetParent(parent, false);
            mouth.transform.position = new Vector3(startPt.x, 0.5f, mouthZ);
            mouth.transform.localScale = new Vector3(1.5f, 0.78f, 0.18f);
            mouth.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.04f, 0.05f, 0.08f));

            // Frame (IN FRONT of the card, bounding the opening): top overhang lip + 2 side jambs.
            var mLip = ProcMesh.RoundedCube("GateMouthLip");
            DestroyColliderGO(mLip);
            mLip.transform.SetParent(parent, false);
            mLip.transform.position = new Vector3(startPt.x, 0.92f, mouthZ - 0.04f);
            mLip.transform.localScale = new Vector3(1.66f, 0.14f, 0.2f);
            mLip.GetComponent<Renderer>().sharedMaterial = lipMat;

            for (int s = -1; s <= 1; s += 2)
            {
                var jamb = ProcMesh.RoundedCube("GateMouthJamb");
                DestroyColliderGO(jamb);
                jamb.transform.SetParent(parent, false);
                jamb.transform.position = new Vector3(startPt.x + s * 0.79f, 0.5f, mouthZ - 0.04f);
                jamb.transform.localScale = new Vector3(0.12f, 0.84f, 0.2f);
                jamb.GetComponent<Renderer>().sharedMaterial = lipMat;
            }

            // Screen (top of the front face, facing the camera) — counter + progress ABOVE the cards.
            var screen = ProcMesh.RoundedCube("GateScreen");
            DestroyColliderGO(screen);
            screen.transform.SetParent(parent, false);
            screen.transform.position = new Vector3(startPt.x, 1.6f, frontZ - 0.03f);
            screen.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            screen.transform.localScale = new Vector3(1.3f, 0.62f, 0.12f);
            screen.GetComponent<Renderer>().sharedMaterial =
                NewLitMaterial(new Color(0.05f, 0.06f, 0.10f));

            var gate = body.AddComponent<FactoryGate>();

            // 3D counter embedded in the screen (camera-facing, no drift on Play).
            var counterAnchor = new GameObject("GateCounter");
            counterAnchor.transform.SetParent(parent, false);
            counterAnchor.transform.position = screen.transform.position + new Vector3(0f, 0.15f, -0.16f);
            counterAnchor.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            var counter = MakeWorldText("0/20", counterAnchor.transform, Vector3.zero,
                0.07f, Color.white, 90);

            // Progress bar under the counter: dark track + a color bar filling from the left edge.
            var progAnchor = new GameObject("GateProgress");
            progAnchor.transform.SetParent(parent, false);
            progAnchor.transform.position = screen.transform.position + new Vector3(0f, -0.16f, -0.16f);
            progAnchor.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);

            var track = ProcMesh.RoundedCube("GateProgressTrack");
            DestroyColliderGO(track);
            track.transform.SetParent(progAnchor.transform, false);
            track.transform.localPosition = Vector3.zero;
            track.transform.localScale = new Vector3(0.96f, 0.07f, 0.05f);
            track.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.04f, 0.05f, 0.08f));

            var fill = ProcMesh.RoundedCube("GateProgressFill");
            DestroyColliderGO(fill);
            fill.transform.SetParent(progAnchor.transform, false);
            fill.transform.localPosition = new Vector3(-0.44f, 0f, -0.02f);
            fill.transform.localScale = new Vector3(0.001f, 0.06f, 0.06f);
            fill.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.32f, 0.82f, 0.45f));

            gate.Init(body.GetComponent<Renderer>(), gateColor, config, counter, fill.transform);
            return gate;
        }

        /// <summary>Creates world-space 3D text (TextMesh). For labels embedded in objects.</summary>
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

        /// <summary>Wide, soft glow halo — for DockGlow / bloom (spreads wider than the narrow MakeRadialSprite).</summary>
        public static Sprite MakeGlowSprite(int size = 256)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            float maxR = c;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                    float core = Mathf.Clamp01(1f - d * 0.55f);
                    float halo = Mathf.Pow(Mathf.Clamp01(1f - d), 1.35f);
                    float a = Mathf.Clamp01(core * 0.55f + halo * 0.95f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Light-independent glow material (URP Unlit + emission → catches bloom).</summary>
        public static Material NewGlowMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 3.2f);
            }
            if (color.a < 0.99f)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return mat;
        }

        /// <summary>Creates a soft radial halo sprite (bright center → transparent edge).</summary>
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
                    a *= a;   // squared falloff → more local/soft halo (doesn't spread wide)
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---- Contact shadow (soft AO feel) ----------------------------------
        static Texture2D blobTex;
        /// <summary>Soft black blob, opaque at center → transparent at edge — contact shadow texture.</summary>
        static Texture2D MakeBlobTexture(int size = 128)
        {
            if (blobTex != null) return blobTex;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;                       // soft, local falloff
                    px[y * size + x] = new Color(0f, 0f, 0f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            blobTex = tex;
            return tex;
        }

        static Material blobMat;
        static Material BlobMaterial()
        {
            if (blobMat != null) return blobMat;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            var m = new Material(shader);
            var t = MakeBlobTexture();
            m.SetTexture("_BaseMap", t);
            m.mainTexture = t;
            m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.34f));
            m.SetFloat("_Surface", 1f);          // transparent surface
            m.SetFloat("_Blend", 0f);            // alpha blend
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 2990;                // after the ground, before the cards
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            blobMat = m;
            return m;
        }

        /// <summary>
        /// Creates a soft contact-shadow quad lying flat on the ground (its top faces the camera).
        /// Even on leaning/parented objects like the bin, the world rotation is kept flat.
        /// </summary>
        public static Transform SpawnContactShadow(Transform parent, Vector3 worldPos,
                                                   float sizeX, float sizeZ, float y = 0.03f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ContactShadow";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(worldPos.x, y, worldPos.z);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // lie flat on the ground, normal +Y
            go.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = BlobMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>
        /// Adds contact shadows to the persistent world objects (gate, end-cap). Idempotent:
        /// skips if a "GroundShadows" root exists → set up once, including in reuse.
        /// </summary>
        static void EnsureGroundShadows(GameObject world)
        {
            if (world.transform.Find("GroundShadows") != null) return;
            var root = new GameObject("GroundShadows");
            root.transform.SetParent(world.transform, false);

            var gateT = world.transform.Find("FactoryGate");
            if (gateT != null) SpawnContactShadow(root.transform, gateT.position, 2.5f, 1.5f);

            var endT = world.transform.Find("BeltEndCap");
            if (endT != null) SpawnContactShadow(root.transform, endT.position, 2.1f, 1.4f);
        }

        // ---- Background "working factory" decor ------------------------------
        /// <summary>
        /// Builds a cute working factory in the empty blue area BEHIND the play ground (z>9):
        /// back floor + flowing belts + boxes traveling left/right + a few machines.
        /// Idempotent (skips if a "BackgroundFactory" root exists) → built once in bake + reuse.
        /// Animation runs on Play (DecorMover/BeltFlow); also serialized when baked.
        /// </summary>
        static void EnsureBackgroundFactory(GameObject world)
        {
            if (world.transform.Find("BackgroundFactory") != null) return;

            var root = new GameObject("BackgroundFactory");
            root.transform.SetParent(world.transform, false);

            // Back floor (continues the grid) — the belts sit on this.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "BackFloor";
            var fcol = floor.GetComponent<Collider>();
            if (fcol != null) Object.Destroy(fcol);
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.52f, 15f);   // tuck under the main ground (seamless)
            floor.transform.localScale = new Vector3(34f, 1f, 12f);
            floor.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            // Flowing belts (alternating left/right), rising with z → clearly visible behind the
            // belt. Cute colored boxes ride on top of them.
            BuildDecorBelt(root.transform, 10.5f, 0.8f, +1.7f);
            BuildDecorBelt(root.transform, 13.5f, 1.2f, -1.5f);
            BuildDecorBelt(root.transform, 16.5f, 1.6f, +1.3f);

            // Back silos / machines (skyline flavor) — static.
            var machMat = NewLitMaterial(new Color(0.62f, 0.66f, 0.72f));
            var siloMat = NewLitMaterial(new Color(0.78f, 0.81f, 0.86f));
            foreach (var sx in new[] { -12.5f, -9.5f, 10f, 13f })
            {
                var silo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                silo.name = "BackSilo";
                var scol = silo.GetComponent<Collider>();
                if (scol != null) Object.Destroy(scol);
                silo.transform.SetParent(root.transform, false);
                silo.transform.localPosition = new Vector3(sx, 1.1f, 18.5f);
                silo.transform.localScale = new Vector3(1.5f, 1.2f, 1.5f);
                silo.GetComponent<Renderer>().sharedMaterial = siloMat;

                var cap = ProcMesh.RoundedCube("BackSiloCap");
                var ccol = cap.GetComponent<Collider>();
                if (ccol != null) Object.Destroy(ccol);
                cap.transform.SetParent(root.transform, false);
                cap.transform.localPosition = new Vector3(sx, 2.45f, 18.5f);
                cap.transform.localScale = new Vector3(1.7f, 0.4f, 1.7f);
                cap.GetComponent<Renderer>().sharedMaterial = machMat;
            }
        }

        /// <summary>A single decor belt: slab + rails + flowing chevrons + boxes riding on top.</summary>
        static void BuildDecorBelt(Transform parent, float z, float y, float speed)
        {
            const float halfLen = 13f, surfaceY = 0.16f;
            var beltRoot = new GameObject("DecorBelt");
            beltRoot.transform.SetParent(parent, false);
            beltRoot.transform.localPosition = new Vector3(0f, y, z);

            // Slab
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "DecorBeltSlab";
            DestroyColliderGO(slab);
            slab.transform.SetParent(beltRoot.transform, false);
            slab.transform.localPosition = Vector3.zero;
            slab.transform.localScale = new Vector3(halfLen * 2f, 0.22f, 1.15f);
            slab.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.38f, 0.43f, 0.52f));

            // Side rails
            var railMat = NewLitMaterial(new Color(0.6f, 0.65f, 0.74f));
            for (int s = -1; s <= 1; s += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "DecorBeltRail";
                DestroyColliderGO(rail);
                rail.transform.SetParent(beltRoot.transform, false);
                rail.transform.localPosition = new Vector3(0f, 0.06f, s * 0.62f);
                rail.transform.localScale = new Vector3(halfLen * 2f, 0.16f, 0.12f);
                rail.GetComponent<Renderer>().sharedMaterial = railMat;
            }

            // Support legs (reach the ground) → so the belt doesn't float.
            var legMat = NewLitMaterial(new Color(0.5f, 0.54f, 0.6f));
            foreach (var lx in new[] { -halfLen * 0.8f, halfLen * 0.8f })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "DecorLeg";
                DestroyColliderGO(leg);
                leg.transform.SetParent(beltRoot.transform, false);
                leg.transform.localPosition = new Vector3(lx, -y * 0.5f, 0f);
                leg.transform.localScale = new Vector3(0.25f, y, 0.25f);
                leg.GetComponent<Renderer>().sharedMaterial = legMat;
            }

            // Flowing chevrons (in the belt's direction)
            var chevMat = NewLitMaterial(new Color(0.7f, 0.75f, 0.84f));
            var flowGo = new GameObject("DecorFlow");
            flowGo.transform.SetParent(beltRoot.transform, false);
            var flow = flowGo.AddComponent<BeltFlow>();
            int chevCount = 10;
            var chevrons = new Transform[chevCount];
            for (int i = 0; i < chevCount; i++)
                chevrons[i] = BuildChevron(flowGo.transform, chevMat);
            // Direction: speed>0 → -x to +x; otherwise the reverse.
            var wp = speed >= 0f
                ? new[] { new Vector3(-halfLen, 0f, 0f), new Vector3(halfLen, 0f, 0f) }
                : new[] { new Vector3(halfLen, 0f, 0f), new Vector3(-halfLen, 0f, 0f) };
            flow.Setup(wp, chevrons, Mathf.Abs(speed) * 1.3f, surfaceY);

            // Boxes riding on top (cute colored)
            var boxesRoot = new GameObject("DecorBoxes");
            boxesRoot.transform.SetParent(beltRoot.transform, false);
            const int n = 6;
            float span = halfLen * 2f - 1f;
            float spacing = span / n;
            var boxes = new Transform[n];
            var palette = new[]
            {
                new Color(0.93f, 0.30f, 0.30f), new Color(0.30f, 0.62f, 0.95f),
                new Color(0.98f, 0.80f, 0.25f), new Color(0.35f, 0.80f, 0.45f),
                new Color(0.70f, 0.45f, 0.90f), new Color(0.98f, 0.58f, 0.25f),
            };
            for (int i = 0; i < n; i++)
            {
                var box = ProcMesh.RoundedCube("DecorBox");
                DestroyColliderGO(box);
                box.transform.SetParent(boxesRoot.transform, false);
                box.transform.localPosition = new Vector3(-halfLen + 0.5f + i * spacing, 0.42f, 0f);
                box.transform.localScale = new Vector3(0.62f, 0.6f, 0.62f);
                box.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(palette[i % palette.Length] * 0.92f);
                boxes[i] = box.transform;
            }
            var mover = boxesRoot.AddComponent<DecorMover>();
            mover.Setup(boxes, speed, -halfLen + 0.5f, halfLen - 0.5f);
        }

        static void DestroyColliderGO(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        static void BuildEndCap(Transform parent, Vector3 endPt)
        {
            // OPEN exit end: NO closed box/top trim → cards exit freely over the top and fly
            // to the dock (they don't cut through it). Low base + end roller + 2 side rails.
            var baseMat = NewLitMaterial(new Color(0.55f, 0.57f, 0.60f));

            var baseGo = ProcMesh.RoundedCube("BeltEndCap");   // (name kept → contact shadow finds it)
            DestroyColliderGO(baseGo);
            baseGo.transform.SetParent(parent, false);
            baseGo.transform.position = new Vector3(endPt.x, 0.16f, endPt.z);
            baseGo.transform.localScale = new Vector3(1.75f, 0.34f, 1.0f);
            baseGo.GetComponent<Renderer>().sharedMaterial = baseMat;

            // End roller (cylinder, X axis) — open conveyor-end feel.
            var roller = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            roller.name = "BeltEndRoller";
            DestroyColliderGO(roller);
            roller.transform.SetParent(parent, false);
            roller.transform.position = new Vector3(endPt.x, 0.4f, endPt.z + 0.42f);
            roller.transform.rotation = Quaternion.Euler(0f, 0f, 90f);   // X axis (across the belt)
            roller.transform.localScale = new Vector3(0.46f, 0.95f, 0.46f);
            roller.GetComponent<Renderer>().sharedMaterial = NewLitMaterial(new Color(0.64f, 0.68f, 0.74f));

            // 2 low side rails (open top).
            var railMat = NewLitMaterial(new Color(0.66f, 0.70f, 0.78f));
            for (int s = -1; s <= 1; s += 2)
            {
                var rail = ProcMesh.RoundedCube("BeltEndRail");
                DestroyColliderGO(rail);
                rail.transform.SetParent(parent, false);
                rail.transform.position = new Vector3(endPt.x + s * 0.8f, 0.34f, endPt.z);
                rail.transform.localScale = new Vector3(0.14f, 0.42f, 1.0f);
                rail.GetComponent<Renderer>().sharedMaterial = railMat;
            }
        }

        // Builds the stacks (cards) UNDER the stack anchors; the anchor position is used.
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
                // The stack's ground contact shadow (cleared along with the stack).
                SpawnContactShadow(go.transform, anchor.position, 1.3f, 1.6f);
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

            // Higher up, steeper (top-down) angle.
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
        /// Visual polish (idempotent; runs in both bake and reuse): light tuning, grid ground,
        /// slight gloss, post-processing volume, camera post-fx.
        /// </summary>
        static void ApplyPolish(GameObject world)
        {
            // Light
            var light = world.GetComponentInChildren<Light>(true);
            if (light != null)
            {
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.3f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.6f;
                light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.68f, 0.74f);

            // Environment decor (corner crates + warning stripe) — built if missing (in reuse too).
            EnsureDecor(world);

            // Ground contact shadows for the persistent objects (idempotent).
            EnsureGroundShadows(world);

            // "Working factory" decor in the empty background area (idempotent).
            EnsureBackgroundFactory(world);

            // Grid ground
            var ground = world.transform.Find("Ground");
            if (ground != null)
            {
                var r = ground.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = GroundMaterial();
            }

            // Gloss — only flat-colored game materials (keep textured ground/crate/belt matte)
            foreach (var rend in world.GetComponentsInChildren<Renderer>(true))
            {
                var m = rend.sharedMaterial;
                if (m == null || m.mainTexture != null) continue;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.45f);
            }

            // Post-processing volume — IDEMPOTENT: build if missing, DON'T touch if present → the
            // user's Inspector color-tint tweaks aren't overwritten. (To apply the code values, the
            // 'Rebake Belt + Color' tool rebuilds the PostFX from scratch.)
            if (world.transform.Find("PostFX") == null)
                BuildPostFX(world.transform);

            // Enable camera post-processing
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

            // Light/soft tone — easy on the eyes (high saturation/contrast/bloom removed).
            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.Override(0.28f);
            bloom.threshold.Override(0.95f);   // only very bright surfaces glow slightly
            bloom.scatter.Override(0.6f);

            var vig = profile.Add<Vignette>();
            vig.active = true;
            vig.intensity.Override(0.14f);
            vig.smoothness.Override(0.5f);

            var ca = profile.Add<ColorAdjustments>();
            ca.active = true;
            ca.saturation.Override(10f);                                  // slight vibrancy (not excessive)
            ca.contrast.Override(4f);
            ca.postExposure.Override(0.03f);
            ca.colorFilter.Override(Color.white);                         // neutral white

            var wb = profile.Add<WhiteBalance>();
            wb.active = true;
            wb.temperature.Override(0f);                                  // neutral; tunable from the scene

            var tone = profile.Add<Tonemapping>();
            tone.active = true;
            tone.mode.Override(TonemappingMode.Neutral);
        }

        /// <summary>
        /// SELECTIVE BAKE: rebuilds only the Belt visual + the PostFX (color tint) volume.
        /// Touches NOTHING ELSE in the world (bins/anchors/gate/dock/crates/background/ground/camera/lid)
        /// → manual scene tweaks are preserved. Used by the Editor 'Rebake Belt + Color' tool.
        /// </summary>
        public static void RebakeBeltAndPostFX(GameObject world)
        {
            if (world == null) return;

            var oldBelt = world.transform.Find("Belt");
            if (oldBelt != null) Object.DestroyImmediate(oldBelt.gameObject);
            BuildBeltVisual(world.transform, BuildUPath());

            var oldFx = world.transform.Find("PostFX");
            if (oldFx != null) Object.DestroyImmediate(oldFx.gameObject);
            BuildPostFX(world.transform);
        }

        static Material GroundMaterial()
        {
            var mat = NewLitMaterial(new Color(0.88f, 0.93f, 0.99f));   // slightly brighter ground
            var tex = MakeGridTexture();
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetTextureScale("_BaseMap", new Vector2(7f, 7f));   // larger, airier cells
            mat.mainTextureScale = new Vector2(7f, 7f);
            mat.SetFloat("_Smoothness", 0.18f);
            return mat;
        }

        static Texture2D MakeGridTexture()
        {
            const int s = 64;
            const int lineW = 2;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var baseC = (Color32)new Color(0.88f, 0.93f, 0.99f);
            var lineC = (Color32)new Color(0.70f, 0.79f, 0.90f);   // slightly more visible grid
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = (x < lineW || y < lineW) ? lineC : baseC;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Texture2D stripeTex;
        /// <summary>Yellow-black diagonal warning-stripe texture (procedural).</summary>
        static Texture2D MakeStripeTexture()
        {
            if (stripeTex != null) return stripeTex;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var yellow = (Color32)new Color(0.97f, 0.78f, 0.06f);
            var black = (Color32)new Color(0.11f, 0.11f, 0.12f);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = (((x + y) / 16) % 2 == 0) ? yellow : black;
            tex.SetPixels32(px);
            tex.Apply();
            stripeTex = tex;
            return tex;
        }

        static Texture2D crateTex;
        /// <summary>Wooden crate texture: plank seams + frame + diagonal brace (procedural).</summary>
        static Texture2D MakeCrateTexture()
        {
            if (crateTex != null) return crateTex;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var wood = (Color32)new Color(0.64f, 0.45f, 0.25f);
            var woodDark = (Color32)new Color(0.49f, 0.33f, 0.17f);
            var frame = (Color32)new Color(0.40f, 0.27f, 0.13f);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    Color32 c = wood;
                    if (x % 16 < 2) c = woodDark;                                   // plank seams
                    if (Mathf.Abs(x - y) < 3 || Mathf.Abs(x - (s - 1 - y)) < 3) c = woodDark; // diagonal brace
                    const int b = 5;
                    if (x < b || x >= s - b || y < b || y >= s - b) c = frame;      // outer frame
                    px[y * s + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            crateTex = tex;
            return tex;
        }

        /// <summary>
        /// Environment decor (idempotent): 4 corner wooden crates + a yellow-black warning stripe
        /// on top. Does nothing if a "Decor" root already exists → built once, including in reuse.
        /// </summary>
        static void EnsureDecor(GameObject world)
        {
            if (world.transform.Find("Decor") != null) return;

            var decor = new GameObject("Decor");
            decor.transform.SetParent(world.transform, false);

            // Corner wooden crates
            var crateMat = NewLitMaterial(new Color(0.64f, 0.45f, 0.25f));
            var ct = MakeCrateTexture();
            crateMat.SetTexture("_BaseMap", ct);
            crateMat.mainTexture = ct;
            crateMat.SetFloat("_Smoothness", 0.12f);

            var cratePos = new[]
            {
                new Vector3(-6.6f, 0.65f,  7.0f),
                new Vector3( 6.6f, 0.65f,  7.0f),
                new Vector3(-7.6f, 0.65f, -7.4f),
                new Vector3( 7.6f, 0.65f, -7.4f),
            };
            var crateYaw = new[] { 10f, -8f, -6f, 12f };
            for (int i = 0; i < cratePos.Length; i++)
            {
                var crate = ProcMesh.RoundedCube("Crate");
                crate.transform.SetParent(decor.transform, false);
                crate.transform.position = cratePos[i];
                crate.transform.localScale = Vector3.one * 1.3f;
                crate.transform.localRotation = Quaternion.Euler(0f, crateYaw[i], 0f);
                crate.GetComponent<Renderer>().sharedMaterial = crateMat;
                SpawnContactShadow(decor.transform, cratePos[i], 1.9f, 1.9f);
            }

            BuildWarningTape(decor.transform);
        }

        static void BuildWarningTape(Transform parent)
        {
            const float z = 8.0f;
            const float y = 0.95f;

            var stripeMat = NewLitMaterial(Color.white);
            var st = MakeStripeTexture();
            stripeMat.SetTexture("_BaseMap", st);
            stripeMat.mainTexture = st;
            stripeMat.SetTextureScale("_BaseMap", new Vector2(10f, 1f));
            stripeMat.mainTextureScale = new Vector2(10f, 1f);
            stripeMat.SetFloat("_Smoothness", 0.08f);

            var bar = ProcMesh.RoundedCube("WarningTape");
            bar.transform.SetParent(parent, false);
            bar.transform.position = new Vector3(0f, y, z);
            bar.transform.localScale = new Vector3(15f, 0.45f, 0.28f);
            bar.GetComponent<Renderer>().sharedMaterial = stripeMat;

            var poleMat = NewLitMaterial(new Color(0.55f, 0.57f, 0.60f));
            for (int s = -1; s <= 1; s += 2)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "TapePole";
                var pc = pole.GetComponent<Collider>();
                if (pc != null) Object.Destroy(pc);
                pole.transform.SetParent(parent, false);
                pole.transform.position = new Vector3(s * 7.2f, y * 0.5f, z);
                pole.transform.localScale = new Vector3(0.18f, y * 0.5f + 0.1f, 0.18f);
                pole.GetComponent<Renderer>().sharedMaterial = poleMat;
            }

            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "TapeKnob";
            var kc = knob.GetComponent<Collider>();
            if (kc != null) Object.Destroy(kc);
            knob.transform.SetParent(parent, false);
            knob.transform.position = new Vector3(-2.5f, y + 0.26f, z);
            knob.transform.localScale = Vector3.one * 0.3f;
            knob.GetComponent<Renderer>().sharedMaterial = poleMat;
        }

        public static Material NewLitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[GameBootstrap] 'Universal Render Pipeline/Lit' shader not found! " +
                               "Add it to Project Settings > Graphics > Always Included Shaders.");
                shader = Shader.Find("Standard");
            }
            var mat = new Material(shader) { color = color };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.5f);   // glossy "cartoon" finish
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }

    public class GameRunner : MonoBehaviour
    {
        void Update() => GameBootstrap.TickRebuild();
    }
}
