using CardFactory.Core;
using CardFactory.UI;
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
            // Tam (HUD dahil) bir dünya varsa dokunma (Inspector düzenlemeleri korunur).
            if (existing != null && existing.GetComponentInChildren<HudController>(true) != null) return;

            var world = GameBootstrap.BakeWorld();
            EditorSceneManager.MarkSceneDirty(world.scene);
            Debug.Log("[CardFactory] Dünya yok/eksikti; otomatik kuruldu. Play'den çıkınca " +
                      "Ctrl+S ile KAYDET ki kalıcı olsun.");
        }

        [MenuItem("Tools/Card Factory/Bake World Into Scene")]
        public static void Bake()
        {
            var world = GameBootstrap.BakeWorld();
            EditorSceneManager.MarkSceneDirty(world.scene);
            Selection.activeGameObject = world;
            Debug.Log("[CardFactory] Kalıcı ortam sahneye kuruldu. Sahneyi KAYDET (Ctrl+S) " +
                      "ki Play oturumları arasında kalsın.");
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
