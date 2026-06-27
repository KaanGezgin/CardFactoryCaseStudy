using System.Collections.Generic;
using CardFactory.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardFactory.EditorTools
{
    /// <summary>
    /// Editor tools: builds the persistent world (camera/light/ground/path/gate/dock) into the
    /// SCENE as real objects, so it survives between Play sessions and is visible in the Inspector.
    /// Runtime still builds its own fresh world (for reliability); the baked world is stored in the
    /// saved scene.
    /// </summary>
    public static class CardFactoryWorldBaker
    {
        // When Play is pressed and there's no world in the scene, auto-build it (in edit mode,
        // before Play starts) → reused during Play, and it STAYS in the scene after exiting Play.
        // (Save with Ctrl+S afterwards to make it permanent.)
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
            // If a current-structure world (with the WorldPersistence marker) exists, leave it alone.
            if (existing != null && existing.GetComponent<WorldPersistence>() != null) return;

            var world = GameBootstrap.BakeWorld();
            EditorSceneManager.MarkSceneDirty(world.scene);
            Debug.Log("[CardFactory] World was missing/incomplete; auto-built. After exiting Play, " +
                      "SAVE with Ctrl+S to make it permanent.");
        }

        [MenuItem("Tools/Card Factory/Bake World Into Scene")]
        public static void Bake()
        {
            // The CORRECT way to commit code changes to the scene permanently (edit mode).
            // The world is rebuilt from scratch; the manual anchor layout (StackAnchor_*/BinAnchor_*)
            // is preserved so a re-bake doesn't break the arrangement. For a clean code-default
            // layout, run "Clear Baked World" first, then this.
            var layout = CaptureAnchorLayout();
            var world = GameBootstrap.BakeWorld();
            int restored = RestoreAnchorLayout(world, layout);
            EditorSceneManager.MarkSceneDirty(world.scene);
            Selection.activeGameObject = world;
            Debug.Log($"[CardFactory] Persistent world baked into the scene (anchors preserved: {restored}). " +
                      "SAVE the scene (Ctrl+S) so it persists between Play sessions.");
        }

        /// <summary>Captures the current world's anchor (card/bin layout) transforms by name.</summary>
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

        /// <summary>Re-applies the captured anchor layout to the newly built world.</summary>
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

        // Re-bakes only the belt visual + color tint (PostFX); does NOT touch the REST of the world
        // (bins/anchors/gate/dock/crates/background/ground) → manual tweaks are preserved.
        [MenuItem("Tools/Card Factory/Rebake Belt + Color (Keep Rest)")]
        public static void RebakeBeltColor()
        {
            var world = GameObject.Find("CardFactoryWorld");
            if (world == null)
            {
                Debug.LogWarning("[CardFactory] No CardFactoryWorld. Run 'Bake World Into Scene' first.");
                return;
            }
            GameBootstrap.RebakeBeltAndPostFX(world);
            EditorSceneManager.MarkSceneDirty(world.scene);
            Selection.activeGameObject = world;
            Debug.Log("[CardFactory] Belt + color tint re-baked (the rest of the world was PRESERVED). " +
                      "SAVE the scene (Ctrl+S).");
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
                Debug.Log("[CardFactory] Baked world cleared. Save the scene.");
            }
            else
            {
                Debug.Log("[CardFactory] No baked world to clear.");
            }
        }
    }
}
