using System.Collections;
using System.Collections.Generic;
using CardFactory.Core;
using CardFactory.Data;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// A mixed-color card stack (bottom-to-top). All cards are kept in data, but only the
    /// top MaxVisible cards are shown as a fanned-out visual. Tapping sends the top
    /// same-color group onto the belt.
    /// </summary>
    public class CardStack : MonoBehaviour
    {
        readonly List<CardColor> cards = new();          // index 0 = bottom (all data)
        readonly List<GameObject> visuals = new();       // top window only
        readonly Dictionary<CardColor, Material> mats = new();

        Conveyor conveyor;
        BoxCollider tapCollider;

        Vector3 homeLocalPos;         // stack home position (relative to the anchor)
        float advanceZ;              // accumulated advance toward the belt
        Coroutine slideCo;
        const float AdvanceStep = 0.085f;   // forward step per sent card

        const int MaxVisible = 12;    // even with up to 16 in hand, at most 12 cards are shown
        const float CardW = 0.85f;
        const float CardThick = 0.07f;   // thin card (like the reference)
        const float CardLen = 0.95f;
        const float TiltX = -18f;     // nearly flat (top-down view)
        const float StepY = 0.1f;     // gap between cards → count is readable
        const float StepZ = 0.11f;    // fan opens backward → the color behind is visible
        const float BaseY = 0.14f;

        public bool IsEmpty => cards.Count == 0;
        public CardColor? TopColor => cards.Count == 0 ? (CardColor?)null : cards[cards.Count - 1];

        public void Init(List<CardColor> colors, Vector3 basePos, Conveyor conv)
        {
            conveyor = conv;
            transform.position = basePos;
            homeLocalPos = transform.localPosition;
            tapCollider = gameObject.AddComponent<BoxCollider>();

            cards.AddRange(colors);
            RefreshVisuals();
        }

        Material MatFor(CardColor color)
        {
            if (!mats.TryGetValue(color, out var m))
            {
                m = GameBootstrap.NewLitMaterial(CardPalette.Get(color));
                mats[color] = m;
            }
            return m;
        }

        void RefreshVisuals()
        {
            foreach (var v in visuals)
                if (v != null) Destroy(v);
            visuals.Clear();

            int count = cards.Count;
            int show = Mathf.Min(MaxVisible, count);
            int start = count - show;

            for (int v = 0; v < show; v++)
            {
                var color = cards[start + v];
                var go = Feedback.ProcMesh.RoundedCube("DeckCard_" + color);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, BaseY + v * StepY, v * StepZ);
                go.transform.localRotation = Quaternion.Euler(TiltX, 0f, 0f);
                go.transform.localScale = new Vector3(CardW, CardThick, CardLen);
                go.GetComponent<Renderer>().sharedMaterial = MatFor(color);
                visuals.Add(go);
            }

            UpdateCollider(show);
        }

        // When a group is sent, the whole stack steps toward the belt (the queue moves forward).
        void AdvanceForward(int group)
        {
            advanceZ += group * AdvanceStep;
            if (slideCo != null) StopCoroutine(slideCo);
            if (isActiveAndEnabled) slideCo = StartCoroutine(SlideRoutine());
        }

        IEnumerator SlideRoutine()
        {
            Vector3 target = homeLocalPos + new Vector3(0f, 0f, advanceZ);
            while ((transform.localPosition - target).sqrMagnitude > 1e-5f)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, 6f * Time.deltaTime);
                target = homeLocalPos + new Vector3(0f, 0f, advanceZ);   // keep target updated for back-to-back sends
                yield return null;
            }
            transform.localPosition = target;
            slideCo = null;
        }

        void UpdateCollider(int show)
        {
            if (show == 0)
            {
                tapCollider.enabled = false;
                return;
            }
            float yTop = BaseY + (show - 1) * StepY + 0.6f;
            float zBack = (show - 1) * StepZ + 0.6f;
            tapCollider.enabled = true;
            tapCollider.center = new Vector3(0f, yTop * 0.5f, zBack * 0.5f - 0.3f);
            tapCollider.size = new Vector3(CardW + 0.2f, yTop, zBack + 0.6f);
        }

        public void OnTapped()
        {
            if (IsEmpty) return;
            // Once the game is over (fail/win) don't send cards — gameplay must stop instantly on fail.
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            CardColor top = cards[cards.Count - 1];
            int groupSize = 0;
            for (int i = cards.Count - 1; i >= 0 && cards[i] == top; i--)
                groupSize++;

            var group = new List<CardColor>(groupSize);
            for (int i = 0; i < groupSize; i++) group.Add(top);

            // Cards glide onto the belt from this position (the top card).
            Vector3 origin = visuals.Count > 0
                ? visuals[visuals.Count - 1].transform.position
                : transform.position;

            if (!conveyor.TrySend(group, origin))
            {
                // Belt full → can't send: strong feedback + red screen flash.
                Sfx.Play("warn");
                Juice.CameraPunch(0.32f);
                Sfx.Haptic();
                var hud = Object.FindFirstObjectByType<CardFactory.UI.HudController>();
                if (hud != null) hud.FlashScreen(new Color(1f, 0.16f, 0.13f, 0.34f), 0.3f);
                return;
            }

            Sfx.Play("click");
            Sfx.Haptic();
            Juice.PunchScale(transform, Vector3.one, 0.10f);

            cards.RemoveRange(cards.Count - groupSize, groupSize);
            RefreshVisuals();
            AdvanceForward(groupSize);   // remaining stack advances toward the belt

            // The newly revealed top card does a satisfying "pop".
            if (visuals.Count > 0)
            {
                var topT = visuals[visuals.Count - 1].transform;
                Juice.PopIn(topT, topT.localScale, 0.16f);
            }
        }
    }
}
