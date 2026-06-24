using CardFactory.Core;
using CardFactory.InputSys;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Reklam/öğretme için "buraya dokun" göstergesi: hedef desteyi tıklatan,
    /// aşağı-yukarı zıplayan beyaz bir parmak + zemine yayılan halka. İlk gerçek
    /// dokunuşta kendini yok eder.
    /// </summary>
    public class HandPointer : MonoBehaviour
    {
        Transform finger;
        Transform ripple;
        Vector3 fingerBase;
        InputController input;
        float t;

        public void Init(Vector3 target, InputController inputController)
        {
            transform.position = target;
            input = inputController;
            if (input != null) input.OnTap += Hide;

            var white = GameBootstrap.NewLitMaterial(new Color(1f, 1f, 1f));

            // Parmak (zıplayan beyaz küre)
            var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            f.name = "PointerFinger";
            DestroyCollider(f);
            f.transform.SetParent(transform, false);
            fingerBase = new Vector3(0f, 1.5f, -0.1f);
            f.transform.localPosition = fingerBase;
            f.transform.localScale = Vector3.one * 0.42f;
            f.GetComponent<Renderer>().sharedMaterial = white;
            finger = f.transform;

            // Halka (deste üstünde nabız atan ince disk)
            var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            r.name = "PointerRipple";
            DestroyCollider(r);
            r.transform.SetParent(transform, false);
            r.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            r.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            r.GetComponent<Renderer>().sharedMaterial = white;
            ripple = r.transform;
        }

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        void Update()
        {
            t += Time.deltaTime;
            float phase = Mathf.Sin(t * 3.2f) * 0.5f + 0.5f;   // 0..1
            // Parmak aşağı dalıp çıkar (dokunma)
            if (finger != null)
                finger.localPosition = fingerBase - Vector3.up * (phase * 0.55f);
            // Halka nabzı
            if (ripple != null)
            {
                float s = 0.35f + phase * 0.75f;
                ripple.localScale = new Vector3(s, 0.02f, s);
            }
        }

        void Hide()
        {
            if (input != null) input.OnTap -= Hide;
            Destroy(gameObject);
        }
    }
}
