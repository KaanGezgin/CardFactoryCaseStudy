using System;
using System.Collections;
using CardFactory.Data;
using UnityEngine;

namespace CardFactory.Gameplay
{
    public enum CardState { Source, Belt, Bin, Dock }

    /// <summary>
    /// Tek bir kart: renk + görsel + basit hareket. Belt üzerindeyken pozisyonu
    /// Conveyor sürer (BeltDist). Kutuya/dock'a giderken kendi MoveTo coroutine'i.
    /// Juice (squash/pop) Faz C'de eklenecek.
    /// </summary>
    public class Card : MonoBehaviour
    {
        public CardColor Color { get; private set; }
        public CardState State = CardState.Source;

        /// <summary>true iken kart stack'ten banda süzülüyor; Conveyor onu henüz hareket ettirmez.</summary>
        public bool Entering;

        /// <summary>Konveyör boyunca başlangıçtan uzaklık (birim). Conveyor yönetir.</summary>
        public float BeltDist;

        Renderer rend;

        public void Setup(CardColor color, Material mat)
        {
            Color = color;
            rend = GetComponent<Renderer>();
            rend.sharedMaterial = mat;
        }

        public void MoveTo(Vector3 target, float duration, Action onDone = null)
        {
            StopAllCoroutines();
            StartCoroutine(MoveRoutine(target, duration, onDone));
        }

        /// <summary>Yay çizerek hareket (bant sonundan dock'a uçup konma gibi). height = tepe yüksekliği.</summary>
        public void MoveArc(Vector3 target, float height, float duration, Action onDone = null)
        {
            StopAllCoroutines();
            StartCoroutine(ArcRoutine(target, height, duration, onDone));
        }

        IEnumerator ArcRoutine(Vector3 target, float height, float duration, Action onDone)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                Vector3 p = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, k));
                p.y += height * 4f * k * (1f - k);   // parabolik tepe
                transform.position = p;
                yield return null;
            }
            transform.position = target;
            onDone?.Invoke();
        }

        IEnumerator MoveRoutine(Vector3 target, float duration, Action onDone)
        {
            Vector3 start = transform.position;
            if (duration <= 0f)
            {
                transform.position = target;
            }
            else
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                    transform.position = Vector3.Lerp(start, target, k);
                    yield return null;
                }
                transform.position = target;
            }
            onDone?.Invoke();
        }
    }
}
