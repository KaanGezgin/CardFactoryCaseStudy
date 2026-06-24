using CardFactory.Core;
using CardFactory.InputSys;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Sahnedeki ÖZEL İMLEÇ: prosedürel stilize el + zeminde halka. Sistem imleci gizlenir,
    /// el her kare mouse konumunu (zemin düzlemine projeksiyon) takip eder. Yok OLMAZ —
    /// sahnede kalıp cursor gibi hareket eder. Harici asset yok.
    /// </summary>
    public class HandPointer : MonoBehaviour
    {
        Transform handRoot;
        Transform fingerTip;
        Vector3 handBase;
        Material ghostMat;
        Camera cam;
        float clickT;                  // sol tık basış animasyonu (1→0)

        const float PlaneY = 0.1f;     // elin süzüldüğü zemin düzlemi
        // El yönü: parmak AŞAĞI baksın (Inspector yok; buradan ayarlanır).
        static readonly Vector3 HandEuler = new Vector3(92f, -12f, 6f);

        public void Init(Vector3 target, InputController inputController)
        {
            transform.position = target;
            cam = Camera.main;
            Cursor.visible = false;     // sistem imleci gizli; el onu takip eder

            ghostMat = BuildGhostMaterial();
            BuildHand();
        }

        void OnDestroy()
        {
            Cursor.visible = true;
        }

        /// <summary>Mouse'u zemin düzlemine ışınlayıp el konumunu (yumuşak) oraya taşır.</summary>
        void FollowCursor()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, PlaneY, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 p = ray.GetPoint(enter);
                transform.position = Vector3.Lerp(transform.position, p,
                    1f - Mathf.Exp(-22f * Time.deltaTime));
            }
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
            handRoot.localRotation = Quaternion.Euler(HandEuler);

            var palm = ProcMesh.RoundedCube("Palm");
            SetupPart(palm, handRoot, new Vector3(0f, 0f, 0.06f),
                new Vector3(0.46f, 0.14f, 0.52f), Quaternion.identity);

            var thumb = ProcMesh.RoundedCube("Thumb");
            SetupPart(thumb, handRoot, new Vector3(-0.28f, 0.02f, 0.12f),
                new Vector3(0.14f, 0.12f, 0.22f), Quaternion.Euler(0f, 25f, -28f));

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

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        void Update()
        {
            FollowCursor();

            // Sürekli sallanma yok; yalnızca sol tıkta kısa "seçme" basışı.
            if (Input.GetMouseButtonDown(0)) clickT = 1f;
            clickT = Mathf.MoveTowards(clickT, 0f, Time.deltaTime / 0.18f);
            float press = Mathf.SmoothStep(0f, 1f, clickT);

            if (handRoot != null)
                handRoot.localPosition = handBase - Vector3.up * (press * 0.35f);

            if (fingerTip != null)
            {
                float squash = 1f - press * 0.12f;
                fingerTip.localScale = new Vector3(0.10f * (1f + press * 0.08f), 0.10f * squash, 0.18f * squash);
            }
        }
    }
}
