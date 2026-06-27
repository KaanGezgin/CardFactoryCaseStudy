using System.Collections;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Gives the camera a small "punch" (shake). Juice effects call into this.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        Vector3 basePos;
        Coroutine punching;

        void Awake()
        {
            Instance = this;
            basePos = transform.localPosition;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Punch(float strength = 0.25f, float duration = 0.18f)
        {
            if (punching != null) StopCoroutine(punching);
            punching = StartCoroutine(PunchRoutine(strength, duration));
        }

        IEnumerator PunchRoutine(float strength, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                Vector3 off = Random.insideUnitSphere * (strength * k);
                off.z = 0f;
                transform.localPosition = basePos + off;
                yield return null;
            }
            transform.localPosition = basePos;
            punching = null;
        }
    }
}
