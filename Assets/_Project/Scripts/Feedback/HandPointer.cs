using CardFactory.Core;
using CardFactory.InputSys;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Reklam/öğretme için "buraya dokun" göstergesi: prosedürel stilize el +
    /// zeminde genişleyen altın halka. İlk gerçek dokunuşta kendini yok eder.
    /// Harici asset yok — ProcMesh + prosedürel sprite.
    /// </summary>
    public class HandPointer : MonoBehaviour
    {
        Transform handRoot;
        Transform fingerTip;
        SpriteRenderer rippleA;
        SpriteRenderer rippleB;
        Vector3 handBase;
        Material ghostMat;
        InputController input;
        float t;

        public void Init(Vector3 target, InputController inputController)
        {
            transform.position = target;
            input = inputController;
            if (input != null) input.OnTap += Hide;

            ghostMat = BuildGhostMaterial();
            BuildHand();
            BuildRipples();
        }

        static Material BuildGhostMaterial()
        {
            var c = new UnityEngine.Color(1f, 0.97f, 0.93f);
            var mat = GameBootstrap.NewLitMaterial(c);
            mat.SetFloat("_Smoothness", 0.72f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 0.18f);
            return mat;
        }

        void BuildHand()
        {
            handRoot = new GameObject("HandRoot").transform;
            handRoot.SetParent(transform, false);
            handBase = new Vector3(0.15f, 1.55f, -0.35f);
            handRoot.localPosition = handBase;
            handRoot.localRotation = Quaternion.Euler(48f, -18f, 8f);

            // Avuç — yassı yuvarlak kutu.
            var palm = ProcMesh.RoundedCube("Palm");
            SetupPart(palm, handRoot, new Vector3(0f, 0f, 0.06f),
                new Vector3(0.46f, 0.14f, 0.52f), Quaternion.identity);

            // Başparmak tümseği.
            var thumb = ProcMesh.RoundedCube("Thumb");
            SetupPart(thumb, handRoot, new Vector3(-0.28f, 0.02f, 0.12f),
                new Vector3(0.14f, 0.12f, 0.22f), Quaternion.Euler(0f, 25f, -28f));

            // İşaret parmağı — 3 segment (hyper-casual cartoon el).
            var seg1 = ProcMesh.RoundedCube("FingerSeg1");
            SetupPart(seg1, handRoot, new Vector3(0.06f, -0.02f, -0.22f),
                new Vector3(0.13f, 0.13f, 0.24f), Quaternion.Euler(12f, 0f, 0f));

            var seg2 = ProcMesh.RoundedCube("FingerSeg2");
            SetupPart(seg2, handRoot, new Vector3(0.06f, -0.06f, -0.44f),
                new Vector3(0.11f, 0.11f, 0.22f), Quaternion.Euler(18f, 0f, 0f));

            var tip = ProcMesh.RoundedCube("FingerTip");
            SetupPart(tip, handRoot, new Vector3(0.06f, -0.10f, -0.62f),
                new Vector3(0.10f, 0.10f, 0.18f), Quaternion.Euler(22f, 0f, 0f));
            fingerTip = tip.transform;

            // Diğer parmak tümseği (arka planda siluet).
            var knuckles = ProcMesh.RoundedCube("Knuckles");
            SetupPart(knuckles, handRoot, new Vector3(0.02f, 0.04f, -0.08f),
                new Vector3(0.38f, 0.10f, 0.18f), Quaternion.Euler(-8f, 0f, 0f));
        }

        void SetupPart(GameObject go, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            DestroyCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = ghostMat;
        }

        void BuildRipples()
        {
            var sprite = GameBootstrap.MakeGlowSprite();
            rippleA = MakeRipple("RippleA", sprite, 0.82f);
            rippleB = MakeRipple("RippleB", sprite, 0.82f);
        }

        SpriteRenderer MakeRipple(string name, Sprite sprite, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0.05f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new UnityEngine.Color(1f, 0.92f, 0.45f, 0f);
            return sr;
        }

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        void Update()
        {
            t += Time.deltaTime;
            float phase = Mathf.Sin(t * 3.4f) * 0.5f + 0.5f;          // 0..1 dokunma döngüsü
            float tap = Mathf.SmoothStep(0f, 1f, phase);               // yumuşak iniş

            if (handRoot != null)
            {
                // El aşağı iner + hafif eğim değişir (dokunma hissi).
                handRoot.localPosition = handBase - Vector3.up * (tap * 0.62f);
                handRoot.localRotation = Quaternion.Euler(48f + tap * 6f, -18f, 8f - tap * 4f);
            }

            if (fingerTip != null)
            {
                float squash = 1f - tap * 0.12f;
                fingerTip.localScale = new Vector3(0.10f * (1f + tap * 0.08f), 0.10f * squash, 0.18f * squash);
            }

            // İki halka faz kaymalı genişler — referans reklamlardaki tap pulse.
            AnimateRipple(rippleA, t, 0f);
            AnimateRipple(rippleB, t, 0.55f);
        }

        static void AnimateRipple(SpriteRenderer sr, float time, float offset)
        {
            if (sr == null) return;
            float cycle = (time * 1.6f + offset) % 1f;
            float scale = Mathf.Lerp(0.35f, 1.35f, cycle);
            float alpha = Mathf.Lerp(0.55f, 0f, cycle);
            sr.transform.localScale = Vector3.one * scale;
            sr.color = new UnityEngine.Color(1f, 0.92f, 0.45f, alpha);
        }

        void Hide()
        {
            if (input != null) input.OnTap -= Hide;
            Destroy(gameObject);
        }
    }
}
