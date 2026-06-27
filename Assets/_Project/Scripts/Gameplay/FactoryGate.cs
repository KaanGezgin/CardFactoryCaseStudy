using System.Collections;
using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Belt entry gate: holds the X/20 counter and flashes red when the limit is exceeded.
    /// The HUD reads the counter value; the flash visual is on the gate cube.
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

        Transform progressFill;            // fill bar under the counter (grows from the left edge)
        Renderer progressFillRend;
        const float ProgressWidth = 0.88f; // full width, anchor-local

        public void Init(Renderer gateRenderer, Color gateColor, GameConfig config, TextMesh counterLabel,
                         Transform progressBarFill = null)
        {
            body = gateRenderer;
            baseColor = gateColor;
            cfg = config;
            label = counterLabel;
            BindProgress(progressBarFill);
        }

        void BindProgress(Transform fill)
        {
            progressFill = fill;
            progressFillRend = fill != null ? fill.GetComponent<Renderer>() : null;
            UpdateProgress(0f);
        }

        void UpdateProgress(float ratio)
        {
            if (progressFill == null) return;
            ratio = Mathf.Clamp01(ratio);
            float w = Mathf.Max(0.0008f, ratio * ProgressWidth);
            var s = progressFill.localScale;
            progressFill.localScale = new Vector3(w, s.y, s.z);
            var p = progressFill.localPosition;
            progressFill.localPosition = new Vector3(-ProgressWidth * 0.5f + w * 0.5f, p.y, p.z);

            if (progressFillRend != null)
            {
                var c = ratio >= 0.9f ? new Color(0.95f, 0.3f, 0.3f)
                      : ratio >= 0.7f ? new Color(1f, 0.78f, 0.3f)
                      : new Color(0.32f, 0.82f, 0.45f);
                progressFillRend.sharedMaterial.SetColor("_BaseColor", c);
                progressFillRend.sharedMaterial.color = c;
            }
        }

        /// <summary>
        /// Rebinds the baked gate's references (Init is not called). The counter label is
        /// found under the scene's "GateCounter".
        /// </summary>
        public void Rebind(GameConfig config)
        {
            cfg = config;
            body = GetComponent<Renderer>();
            if (body != null) baseColor = body.sharedMaterial.color;
            var parent = transform.parent;
            var anchor = parent != null ? parent.Find("GateCounter") : null;
            if (anchor != null)
            {
                label = anchor.GetComponentInChildren<TextMesh>();
                // Remove the old Billboard → rotation stays as set in the Inspector (no drift on Play).
                var bb = anchor.GetComponent<CardFactory.Feedback.Billboard>();
                if (bb != null) Destroy(bb);
            }
            // Rebind the baked progress bar fill (if present).
            var prog = parent != null ? parent.Find("GateProgress") : null;
            BindProgress(prog != null ? prog.Find("GateProgressFill") : null);
        }

        public void SetCount(int count, int max)
        {
            Count = count;
            Max = max;
            if (label != null)
            {
                label.text = $"{count}/{max}";
                // Yellow as it nears the limit, red when full.
                label.color = count >= max ? new Color(1f, 0.35f, 0.35f)
                            : count >= max - 2 ? new Color(1f, 0.85f, 0.35f)
                            : Color.white;
            }
            UpdateProgress(max > 0 ? (float)count / max : 0f);
        }

        /// <summary>Resets the visual when a new level starts (the gate is a persistent object).</summary>
        public void ResetVisual()
        {
            StopAllCoroutines();
            WarningActive = false;
            if (body != null)
            {
                body.sharedMaterial.SetColor("_BaseColor", baseColor);
                body.sharedMaterial.color = baseColor;
            }
            UpdateProgress(0f);
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
            if (label != null) SetCount(Count, Max);   // restore color based on the count
            WarningActive = false;
        }
    }
}
