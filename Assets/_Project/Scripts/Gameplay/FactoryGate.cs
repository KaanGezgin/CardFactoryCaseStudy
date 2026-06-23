using System.Collections;
using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Yol (belt) giriş kapısı: X/20 sayacını tutar ve limit aşılınca kırmızı
    /// uyarı flaşı verir. Sayaç değerini HUD okur; flaş görseli kapı küpünde.
    /// </summary>
    public class FactoryGate : MonoBehaviour
    {
        public int Count { get; private set; }
        public int Max { get; private set; }
        public bool WarningActive { get; private set; }

        Renderer body;
        Color baseColor;
        GameConfig cfg;
        TextMesh label;

        public void Init(Renderer gateRenderer, Color gateColor, GameConfig config, TextMesh counterLabel)
        {
            body = gateRenderer;
            baseColor = gateColor;
            cfg = config;
            label = counterLabel;
        }

        public void SetCount(int count, int max)
        {
            Count = count;
            Max = max;
            if (label != null)
            {
                label.text = $"{count}/{max}";
                // Limite yaklaşınca sarı, doluyken kırmızı tonla uyar.
                label.color = count >= max ? new Color(1f, 0.35f, 0.35f)
                            : count >= max - 2 ? new Color(1f, 0.85f, 0.35f)
                            : Color.white;
            }
        }

        public void FlashWarning()
        {
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            WarningActive = true;
            float dur = cfg != null ? cfg.warningFlashDuration : 0.3f;
            var red = new Color(0.95f, 0.15f, 0.15f);
            if (label != null) label.color = new Color(1f, 0.3f, 0.3f);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (body != null)
                {
                    var c = Color.Lerp(red, baseColor, k);
                    body.sharedMaterial.SetColor("_BaseColor", c);
                    body.sharedMaterial.color = c;
                }
                yield return null;
            }
            if (body != null)
            {
                body.sharedMaterial.SetColor("_BaseColor", baseColor);
                body.sharedMaterial.color = baseColor;
            }
            if (label != null) SetCount(Count, Max);   // rengi sayaca göre geri al
            WarningActive = false;
        }
    }
}
