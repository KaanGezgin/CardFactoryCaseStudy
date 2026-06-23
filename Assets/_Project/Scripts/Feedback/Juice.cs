using System;
using System.Collections;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Bağımsız küçük tween/juice yardımcısı (DOTween YOK). Coroutine'leri kalıcı
    /// bir runner üzerinde çalıştırır; hedef yok olursa güvenle durur.
    /// </summary>
    public static class Juice
    {
        static JuiceRunner runner;

        static JuiceRunner Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("JuiceRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    runner = go.AddComponent<JuiceRunner>();
                }
                return runner;
            }
        }

        /// <summary>0'dan baz ölçeğe overshoot ile büyür (spawn pop).</summary>
        public static void PopIn(Transform t, Vector3 baseScale, float dur = 0.18f, float overshoot = 1.18f)
        {
            if (t == null) return;
            Runner.StartCoroutine(PopInRoutine(t, baseScale, dur, overshoot));
        }

        /// <summary>Baz ölçekten kısa bir punch (vurgu) yapıp geri döner.</summary>
        public static void PunchScale(Transform t, Vector3 baseScale, float amount = 0.18f, float dur = 0.16f)
        {
            if (t == null) return;
            Runner.StartCoroutine(PunchRoutine(t, baseScale, amount, dur));
        }

        /// <summary>Ölçeği 0'a küçültüp yok eder/onDone çağırır.</summary>
        public static void ShrinkOut(Transform t, float dur = 0.2f, Action onDone = null)
        {
            if (t == null) { onDone?.Invoke(); return; }
            Runner.StartCoroutine(ShrinkRoutine(t, dur, onDone));
        }

        /// <summary>Verilen konumda kısa ömürlü renkli partikül patlaması.</summary>
        public static void Burst(Vector3 pos, Color color, int count = 12)
        {
            Runner.StartCoroutine(BurstRoutine(pos, color, count));
        }

        public static void CameraPunch(float strength = 0.2f)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.Punch(strength);
        }

        // --- Routines ---

        static IEnumerator PopInRoutine(Transform t, Vector3 baseScale, float dur, float overshoot)
        {
            float time = 0f;
            while (time < dur)
            {
                if (t == null) yield break;
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / dur);
                // 0 -> overshoot -> 1
                float s = k < 0.7f
                    ? Mathf.Lerp(0f, overshoot, k / 0.7f)
                    : Mathf.Lerp(overshoot, 1f, (k - 0.7f) / 0.3f);
                t.localScale = baseScale * s;
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        static IEnumerator PunchRoutine(Transform t, Vector3 baseScale, float amount, float dur)
        {
            float time = 0f;
            while (time < dur)
            {
                if (t == null) yield break;
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / dur);
                float s = 1f + Mathf.Sin(k * Mathf.PI) * amount;
                t.localScale = baseScale * s;
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        static IEnumerator ShrinkRoutine(Transform t, float dur, Action onDone)
        {
            Vector3 start = t != null ? t.localScale : Vector3.one;
            float time = 0f;
            while (time < dur)
            {
                if (t == null) { onDone?.Invoke(); yield break; }
                time += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / dur));
                t.localScale = Vector3.Lerp(start, Vector3.zero, k);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.zero;
            onDone?.Invoke();
        }

        static IEnumerator BurstRoutine(Vector3 pos, Color color, int count)
        {
            var pieces = new Transform[count];
            var dirs = new Vector3[count];
            var mat = Core.GameBootstrap.NewLitMaterial(color);

            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.12f;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                pieces[i] = go.transform;
                var d = UnityEngine.Random.insideUnitSphere;
                d.y = Mathf.Abs(d.y) + 0.4f;
                dirs[i] = d.normalized;
            }

            float dur = 0.45f;
            float time = 0f;
            while (time < dur)
            {
                time += Time.deltaTime;
                float k = time / dur;
                for (int i = 0; i < count; i++)
                {
                    if (pieces[i] == null) continue;
                    pieces[i].position += dirs[i] * (4f * Time.deltaTime);
                    pieces[i].localScale = Vector3.one * Mathf.Lerp(0.12f, 0f, k);
                }
                yield return null;
            }
            for (int i = 0; i < count; i++)
                if (pieces[i] != null) UnityEngine.Object.Destroy(pieces[i].gameObject);
        }
    }

    public class JuiceRunner : MonoBehaviour { }
}
