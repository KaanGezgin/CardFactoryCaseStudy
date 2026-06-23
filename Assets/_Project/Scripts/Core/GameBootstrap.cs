using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Core
{
    /// <summary>
    /// Boş sahnede Play'e basınca tüm oyunu KODDAN kurar. Hiç sahne/prefab
    /// dosyası elle düzenlenmez. Faz A: kamera + ışık + zemin + GameManager.
    /// (Konveyör/kaynak/kutu/dock/UI sonraki fazlarda buraya eklenecek.)
    /// </summary>
    public static class GameBootstrap
    {
        // Portrait 2.5D referans framing. Oyun alanı ~origin etrafında kurulur.
        const float GroundSize = 14f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var config = GameConfig.Default;

            // --- Kök obje ---
            var root = new GameObject("CardFactory");

            // --- Kamera ---
            BuildCamera(config, root.transform);

            // --- Işık ---
            BuildLight(root.transform);

            // --- Zemin ---
            BuildGround(root.transform);

            // --- GameManager ---
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.transform.SetParent(root.transform, false);
            gm.Init(config, levelIndex: 0);

            Debug.Log("[GameBootstrap] Faz A sahnesi koddan kuruldu (kamera+ışık+zemin).");
        }

        static void BuildCamera(GameConfig config, Transform parent)
        {
            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(parent, false);
            camGo.tag = "MainCamera";

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.backgroundColor;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            // Hafif eğik 2.5D portrait açı: yukarıdan/önden bakış, origin'e doğru.
            camGo.transform.position = new Vector3(0f, 11f, -10f);
            camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            camGo.AddComponent<AudioListener>();
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

            // Hafif ortam ışığı (URP gölgeleri çok sert olmasın).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.37f, 0.42f);
        }

        static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);

            var mat = NewLitMaterial(new Color(0.20f, 0.22f, 0.28f));
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// URP/Lit materyali oluşturur. Tüm görsel objeler bunu kullanır.
        /// </summary>
        public static Material NewLitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[GameBootstrap] 'Universal Render Pipeline/Lit' shader'ı " +
                               "bulunamadı! URP/Lit'i Project Settings > Graphics > " +
                               "Always Included Shaders listesine eklemen gerekiyor.");
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader) { color = color };
            mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
