using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CardFactory.EditorTools
{
    /// <summary>
    /// SSAO (Screen Space Ambient Occlusion) Renderer Feature'ını URP renderer
    /// asset'lerine PROGRAMATİK ekler (elle .asset düzenlemeden, "her şey koddan/araçtan").
    /// SSAO bir Volume override değil, bir Renderer Feature olduğundan koddan runtime'da
    /// kurulamaz; bu yüzden tek seferlik editör aracı. Idempotent: zaten varsa atlar.
    /// Manuel alternatif: URP Renderer asset → Add Renderer Feature → Screen Space Ambient Occlusion.
    /// </summary>
    public static class SsaoSetup
    {
        [MenuItem("Tools/Card Factory/Add SSAO (Depth AO)")]
        public static void AddSsao()
        {
            var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[CardFactory] UniversalRendererData bulunamadı. Manuel ekle: " +
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
                Debug.LogWarning($"[CardFactory] SSAO: {added} eklendi, {already} zaten vardı, {failed} BAŞARISIZ. " +
                                 "Başarısız olan(lar) için manuel: Renderer → Add Renderer Feature → SSAO.");
            else
                Debug.Log($"[CardFactory] SSAO: {added} renderer'a eklendi, {already} zaten vardı. " +
                          "Kamera post-processing zaten açık (ApplyPolish). Play'e basıp derinliği kontrol et.");
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

            // Ayarları SerializedObject ile (alan adları sürüme göre değişebilir → null-güvenli).
            var ssaoSo = new SerializedObject(ssao);
            SetFloat(ssaoSo, "m_Settings.Intensity", 0.7f);
            SetFloat(ssaoSo, "m_Settings.Radius", 0.32f);
            SetFloat(ssaoSo, "m_Settings.DirectLightingStrength", 0.25f);
            ssaoSo.ApplyModifiedProperties();

            // Sub-asset olarak ekle; local file id alabilmek için kaydet.
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
