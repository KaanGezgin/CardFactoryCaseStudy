using CardFactory.Core;
using CardFactory.InputSys;
using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// CUSTOM CURSOR in the scene: a procedural stylized hand. The system cursor is hidden and
    /// the hand follows the mouse position every frame (projected onto the ground plane). It
    /// never disappears — it stays in the scene and moves like a cursor. No external assets.
    /// </summary>
    public class HandPointer : MonoBehaviour
    {
        Transform handRoot;
        Transform fingerTip;
        Vector3 fingerTipBase;
        Vector3 fingerOffset;          // world offset of the fingertip from the root (tip = cursor point)
        Vector3 handBase;
        Material ghostMat;
        Camera cam;
        float clickT;                  // left-click press animation (1→0)
        bool autoDriven;               // when AdDirector drives, follow autoTarget instead of the mouse
        Vector3 autoTarget;

        const float PlaneY = 1.4f;     // plane the hand glides on (above the card top → won't clip into it)
        const float HandScale = 2f;    // root object scale (x,y,z)
        // Hand orientation (no Inspector; tuned here).
        static readonly Vector3 RootEuler = new Vector3(-63.8f, -46.4f, 0f);  // root object rotation
        static readonly Vector3 HandEuler = new Vector3(92f, -12f, 6f);      // inner angle of the hand model

        public void Init(Vector3 target, InputController inputController)
        {
            transform.position = target;
            transform.rotation = Quaternion.Euler(RootEuler);   // FollowCursor drives position only
            transform.localScale = Vector3.one * HandScale;
            cam = Camera.main;
            Cursor.visible = false;     // system cursor hidden; the hand replaces it

            ghostMat = BuildGhostMaterial();
            BuildHand();

            // World offset between fingertip and root (incl. rot+scale) — fixed, computed once.
            if (fingerTip != null) fingerOffset = fingerTip.position - transform.position;
        }

        void OnDestroy()
        {
            Cursor.visible = true;
        }

        /// <summary>For AdDirector: the hand targets this world point (with its fingertip).</summary>
        public void SetAutoTarget(Vector3 worldPoint)
        {
            autoDriven = true;
            autoTarget = worldPoint;
        }

        /// <summary>For AdDirector: trigger the "select" press programmatically.</summary>
        public void Press() => clickT = 1f;

        /// <summary>Projects the mouse (or AdDirector target) to the ground plane and smooths the hand position.</summary>
        void FollowCursor()
        {
            Vector3 point;
            if (autoDriven)
            {
                // Slight idle drift → stays lively, as if "thinking".
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

            // Keep the fingertip on the target point → pull the root back by the offset.
            Vector3 desiredRoot = point - fingerOffset;
            transform.position = Vector3.Lerp(transform.position, desiredRoot,
                1f - Mathf.Exp(-22f * Time.deltaTime));
        }

        static Material BuildGhostMaterial()
        {
            // Skin/peach tone for a cartoon-hand feel (like the reference hand).
            var c = new UnityEngine.Color(0.98f, 0.80f, 0.62f);
            var mat = GameBootstrap.NewLitMaterial(c);
            mat.SetFloat("_Smoothness", 0.3f);   // matte skin
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

            // Finger: 3 segments (user Inspector tuning). Seg2 = between Seg1 and Seg3.
            var seg1 = ProcMesh.RoundedCube("FingerSeg1");
            SetupPart(seg1, handRoot, new Vector3(-0.075f, 0.028f, 0.369f),
                new Vector3(0.13f, 0.13f, 0.24f), Quaternion.Euler(6.087f, 0f, 0f));

            var seg2 = ProcMesh.RoundedCube("FingerSeg2");
            SetupPart(seg2, handRoot, new Vector3(-0.075f, 0.011f, 0.564f),
                new Vector3(0.11f, 0.11f, 0.22f), Quaternion.Euler(10.242f, 0f, 0f));

            var seg3 = ProcMesh.RoundedCube("FingerSeg3");
            SetupPart(seg3, handRoot, new Vector3(-0.075f, -0.032f, 0.722f),
                new Vector3(0.105f, 0.105f, 0.22f), Quaternion.Euler(24f, 0f, 0f));

            fingerTip = seg3.transform;                 // tip segment for the click press
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

            // No constant sway; only a short "select" press on left click.
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
