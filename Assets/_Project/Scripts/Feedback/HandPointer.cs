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
        Vector3 fingerTipBase;
        Vector3 fingerOffset;          // parmak ucunun kökten dünya-ofseti (uç = cursor noktası)
        Vector3 handBase;
        Material ghostMat;
        Camera cam;
        float clickT;                  // sol tık basış animasyonu (1→0)
        bool autoDriven;               // AdDirector sürüyorsa mouse yerine autoTarget'ı takip et
        Vector3 autoTarget;

        const float PlaneY = 1.4f;     // elin süzüldüğü düzlem (kart üst yüzeyinin üstünde → içine girmesin)
        const float HandScale = 2f;    // kök obje ölçeği (x,y,z)
        // El yönü (Inspector yok; buradan ayarlanır).
        static readonly Vector3 RootEuler = new Vector3(-63.8f, -46.4f, 0f);  // kök obje rotasyonu
        static readonly Vector3 HandEuler = new Vector3(92f, -12f, 6f);      // el modelinin iç açısı

        public void Init(Vector3 target, InputController inputController)
        {
            transform.position = target;
            transform.rotation = Quaternion.Euler(RootEuler);   // FollowCursor sadece konumu sürer
            transform.localScale = Vector3.one * HandScale;
            cam = Camera.main;
            Cursor.visible = false;     // sistem imleci gizli; el onu takip eder

            ghostMat = BuildGhostMaterial();
            BuildHand();

            // Parmak ucu ile kök arasındaki dünya-ofset (rot+scale dahil) — sabit, bir kez.
            if (fingerTip != null) fingerOffset = fingerTip.position - transform.position;
        }

        void OnDestroy()
        {
            Cursor.visible = true;
        }

        /// <summary>AdDirector için: el bu dünya noktasını (parmak ucuyla) hedefler.</summary>
        public void SetAutoTarget(Vector3 worldPoint)
        {
            autoDriven = true;
            autoTarget = worldPoint;
        }

        /// <summary>AdDirector için: tık "seçme" basışını programatik tetikle.</summary>
        public void Press() => clickT = 1f;

        /// <summary>Mouse'u (veya AdDirector hedefini) zemin düzlemine alıp el konumunu yumuşatır.</summary>
        void FollowCursor()
        {
            Vector3 point;
            if (autoDriven)
            {
                // Boşta hafif sürüklenme → "düşünüyormuş" gibi canlı dursun.
                float tt = Time.time;
                point = autoTarget + new Vector3(Mathf.Sin(tt * 1.3f) * 0.18f, 0f, Mathf.Cos(tt * 1.0f) * 0.12f);
            }
            else
            {
                if (cam == null) cam = Camera.main;
                if (cam == null) return;
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                var plane = new Plane(Vector3.up, new Vector3(0f, PlaneY, 0f));
                if (!plane.Raycast(ray, out float enter)) return;
                point = ray.GetPoint(enter);
            }

            // Parmak UCU hedef noktada dursun → kökü ofset kadar geri al.
            Vector3 desiredRoot = point - fingerOffset;
            transform.position = Vector3.Lerp(transform.position, desiredRoot,
                1f - Mathf.Exp(-22f * Time.deltaTime));
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

            // Parmak: 3 segment (kullanıcı Inspector ayarları). Seg2 = Seg1↔Seg3 arası.
            var seg1 = ProcMesh.RoundedCube("FingerSeg1");
            SetupPart(seg1, handRoot, new Vector3(-0.075f, 0.028f, 0.369f),
                new Vector3(0.13f, 0.13f, 0.24f), Quaternion.Euler(6.087f, 0f, 0f));

            var seg2 = ProcMesh.RoundedCube("FingerSeg2");
            SetupPart(seg2, handRoot, new Vector3(-0.075f, 0.011f, 0.564f),
                new Vector3(0.11f, 0.11f, 0.22f), Quaternion.Euler(10.242f, 0f, 0f));

            var seg3 = ProcMesh.RoundedCube("FingerSeg3");
            SetupPart(seg3, handRoot, new Vector3(-0.075f, -0.032f, 0.722f),
                new Vector3(0.105f, 0.105f, 0.22f), Quaternion.Euler(24f, 0f, 0f));

            fingerTip = seg3.transform;                 // tık basışı için uç segment
            fingerTipBase = seg3.transform.localScale;

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
                fingerTip.localScale = Vector3.Scale(fingerTipBase,
                    new Vector3(1f + press * 0.08f, squash, squash));
            }
        }
    }
}
