using CardFactory.Core;
using CardFactory.Gameplay;
using UnityEngine;

namespace CardFactory.InputSys
{
    /// <summary>
    /// Turns taps/clicks into a ray via legacy Input and forwards them to CardStack.
    /// (The Input System package is NOT used.)
    /// </summary>
    public class InputController : MonoBehaviour
    {
        Camera cam;
        GameManager gm;

        /// <summary>Fired when a stack is tapped (the ghost pointer listens to this).</summary>
        public event System.Action OnTap;

        public void Init(Camera camera, GameManager gameManager)
        {
            cam = camera;
            gm = gameManager;
        }

        void Update()
        {
            if (cam == null || gm == null || gm.State != GameState.Playing)
                return;

            bool pressed = false;
            Vector3 screenPos = Vector3.zero;

            if (Input.GetMouseButtonDown(0))
            {
                pressed = true;
                screenPos = Input.mousePosition;
            }
            else if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    pressed = true;
                    screenPos = t.position;
                }
            }

            if (!pressed) return;

            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 200f))
            {
                var stack = hit.collider.GetComponentInParent<CardStack>();
                if (stack != null)
                {
                    stack.OnTapped();
                    OnTap?.Invoke();
                }
            }
        }
    }
}
