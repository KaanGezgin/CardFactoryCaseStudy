using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Keeps 3D labels (TextMesh / quad) facing the camera so they stay readable even when
    /// embedded in an object. The camera is nearly static, but this preserves alignment
    /// during punch animations.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        public Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
