using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CardFactory.EditorTools
{
    /// <summary>
    /// Adds the SSAO (Screen Space Ambient Occlusion) Renderer Feature to the URP renderer
    /// assets PROGRAMMATICALLY (without hand-editing the .asset, "everything from code/tools").
    /// SSAO is a Renderer Feature, not a Volume override, so it can't be set up from code at
    /// runtime; hence this one-off editor tool. Idempotent: skips if already present.
    /// Manual alternative: URP Renderer asset → Add Renderer Feature → Screen Space Ambient Occlusion.
    /// </summary>
    public static class SsaoSetup
    {
        [MenuItem("Tools/Card Factory/Add SSAO (Depth AO)")]
        public static void AddSsao()
        {
            var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[CardFactory] No UniversalRendererData found. Add manually: " +
                                 "URP Renderer asset → Add Renderer Feature → Screen Space Ambient Occlusion.");
                return;
            }

            int added = 0, already = 0, failed = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;

                if (HasSsao(data)) { already++; continue; }
                if (TryAddSsao(data)) added++; else failed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failed > 0)
                Debug.LogWarning($"[CardFactory] SSAO: {added} added, {already} already present, {failed} FAILED. " +
                                 "For the failed one(s) add manually: Renderer → Add Renderer Feature → SSAO.");
            else
                Debug.Log($"[CardFactory] SSAO: added to {added} renderer(s), {already} already present. " +
                          "Camera post-processing is already on (ApplyPolish). Press Play and check the depth.");
        }

        static bool HasSsao(UniversalRendererData data)
        {
            var so = new SerializedObject(data);
            var feats = so.FindProperty("m_RendererFeatures");
            if (feats == null) return false;
            for (int i = 0; i < feats.arraySize; i++)
            {
                var obj = feats.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj is ScreenSpaceAmbientOcclusion) return true;
            }
            return false;
        }

        static bool TryAddSsao(UniversalRendererData data)
        {
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";

            // Settings via SerializedObject (field names vary by version → null-safe).
            var ssaoSo = new SerializedObject(ssao);
            SetFloat(ssaoSo, "m_Settings.Intensity", 0.7f);
            SetFloat(ssaoSo, "m_Settings.Radius", 0.32f);
            SetFloat(ssaoSo, "m_Settings.DirectLightingStrength", 0.25f);
            ssaoSo.ApplyModifiedProperties();

            // Add as a sub-asset; save so it gets a local file id.
            AssetDatabase.AddObjectToAsset(ssao, data);
            AssetDatabase.SaveAssets();
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId))
            {
                Object.DestroyImmediate(ssao, true);
                return false;
            }

            var so = new SerializedObject(data);
            var feats = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            if (feats == null || map == null)
            {
                Object.DestroyImmediate(ssao, true);
                return false;
            }

            feats.arraySize++;
            feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = ssao;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            return true;
        }

        static void SetFloat(SerializedObject so, string prop, float v)
        {
            var p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = v;
        }
    }
}
