using System.Collections.Generic;
using CardFactory.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardFactory.EditorTools
{
    /// <summary>
    /// Editör araçları: kalıcı ortamı (kamera/ışık/zemin/yol/kapı/dock) SAHNEYE
    /// gerçek obje olarak kurar; böylece Play oturumları arasında kalır ve
    /// Inspector'dan görülebilir. Runtime yine kendi taze dünyasını kurar
    /// (güvenilirlik), baked dünya kayıtlı sahnede saklanır.
    /// </summary>
    public static class CardFactoryWorldBaker
    {
        // Play'e basıldığında dünya sahnede yoksa otomatik kurar (edit mode'da, Play
        // başlamadan önce) → Play boyunca yeniden kullanılır, Play'den çıkınca sahnede
        // KALIR. (Kalıcı olması için sonra Ctrl+S ile kaydet.)
        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            var existing = GameObject.Find("CardFactoryWorld");
            // Güncel yapıda (WorldPersistence marker'lı) bir dünya varsa dokunma.
            if (existing != null && existing.GetComponent<WorldPersistence>() != null) return;

            var world = GameBootstrap.BakeWorld();
            EditorSceneManager.MarkSceneDirty(world.scene);
            Debug.Log("[CardFactory] Dünya yok/eksikti; otomatik kuruldu. Play'den çıkınca " +
                      "Ctrl+S ile KAYDET ki kalıcı olsun.");
        }

        [MenuItem("Tools/Card Factory/Bake World Into Scene")]
        public static void Bake()
        {
            // Kod değişikliğini sahneye kalıcı işlemenin DOĞRU yolu (edit mode).
            // Dünya sıfırdan kurulur; manuel anchor yerleşimi (StackAnchor_*/BinAnchor_*)
            // korunur ki re-bake düzeni bozmasın. Sıfırdan kod-default düzen istiyorsan
            // önce "Clear Baked World" çalıştır, sonra bunu.
            var layout = CaptureAnchorLayout();
            var world = GameBootstrap.BakeWorld();
            int restored = RestoreAnchorLayout(world, layout);
            EditorSceneManager.MarkSceneDirty(world.scene);
            Selection.activeGameObject = world;
            Debug.Log($"[CardFactory] Kalıcı ortam sahneye kuruldu (anchor korundu: {restored}). " +
                      "Sahneyi KAYDET (Ctrl+S) ki Play oturumları arasında kalsın.");
        }

        /// <summary>Mevcut dünyadaki anchor (kart/kutu yerleşim) transformlarını ada göre yakalar.</summary>
        static Dictionary<string, (Vector3 pos, Quaternion rot)> CaptureAnchorLayout()
        {
            var map = new Dictionary<string, (Vector3, Quaternion)>();
            var existing = GameObject.Find("CardFactoryWorld");
            if (existing == null) return map;
            foreach (var t in existing.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("StackAnchor_") || t.name.StartsWith("BinAnchor_"))
                    map[t.name] = (t.localPosition, t.localRotation);
            }
            return map;
        }

        /// <summary>Yakalanan anchor yerleşimini yeni kurulan dünyaya geri uygular.</summary>
        static int RestoreAnchorLayout(GameObject world, Dictionary<string, (Vector3 pos, Quaternion rot)> map)
        {
            if (map == null || map.Count == 0) return 0;
            int n = 0;
            foreach (var t in world.GetComponentsInChildren<Transform>(true))
            {
                if (map.TryGetValue(t.name, out var v))
                {
                    t.localPosition = v.pos;
                    t.localRotation = v.rot;
                    n++;
                }
            }
            return n;
        }

        // Yalnızca Belt görselini + renk tonunu (PostFX) yeniden bake eder; dünyanın
        // GERİSİNE (bins/anchor/kapı/dock/sandık/arka plan/zemin) DOKUNMAZ → manuel ayarlar korunur.
        [MenuItem("Tools/Card Factory/Rebake Belt + Color (Keep Rest)")]
        public static void RebakeBeltColor()
        {
            var world = GameObject.Find("CardFactoryWorld");
            if (world == null)
            {
                Debug.LogWarning("[CardFactory] CardFactoryWorld yok. Önce 'Bake World Into Scene'.");
                return;
            }
            GameBootstrap.RebakeBeltAndPostFX(world);
            EditorSceneManager.MarkSceneDirty(world.scene);
            Selection.activeGameObject = world;
            Debug.Log("[CardFactory] Bant + renk tonu yeniden bake edildi (dünyanın gerisi KORUNDU). " +
                      "Sahneyi KAYDET (Ctrl+S).");
        }

        [MenuItem("Tools/Card Factory/Clear Baked World")]
        public static void Clear()
        {
            bool any = false;
            foreach (var n in new[] { "CardFactory", "CardFactoryWorld", "CardFactoryLevel" })
            {
                var go = GameObject.Find(n);
                if (go != null) { Object.DestroyImmediate(go); any = true; }
            }
            if (any)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[CardFactory] Baked dünya temizlendi. Sahneyi kaydet.");
            }
            else
            {
                Debug.Log("[CardFactory] Temizlenecek baked dünya yok.");
            }
        }
    }
}
