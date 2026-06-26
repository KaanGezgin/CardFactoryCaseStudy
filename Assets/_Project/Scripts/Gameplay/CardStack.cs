using System.Collections.Generic;
using CardFactory.Core;
using CardFactory.Data;
using CardFactory.Feedback;
using UnityEngine;

namespace CardFactory.Gameplay
{
    /// <summary>
    /// Karışık renkli kart destesi (alttan-üste). Veride tüm kartlar tutulur ama
    /// görselde yalnızca en üstteki MaxVisible kadar kart yelpaze gibi gösterilir.
    /// Tıkta üstteki aynı-renk grubu yola gönderilir.
    /// </summary>
    public class CardStack : MonoBehaviour
    {
        readonly List<CardColor> cards = new();          // index 0 = en alt (tüm veri)
        readonly List<GameObject> visuals = new();       // sadece üst pencere
        readonly Dictionary<CardColor, Material> mats = new();

        Conveyor conveyor;
        BoxCollider tapCollider;

        const int MaxVisible = 12;    // elde 16'ya kadar olsa da en fazla 12 kart görünür
        const float CardW = 0.85f;
        const float CardThick = 0.1f;     // daha tok kart (yandan kalınlık görünür)
        const float CardLen = 0.95f;
        const float TiltX = -20f;     // neredeyse yere yatık (kuş bakışı)
        const float StepY = 0.032f;   // 20 kart sığsın diye sıkı
        const float StepZ = 0.11f;    // geriye doğru dizilir
        const float BaseY = 0.14f;

        public bool IsEmpty => cards.Count == 0;
        public CardColor? TopColor => cards.Count == 0 ? (CardColor?)null : cards[cards.Count - 1];

        public void Init(List<CardColor> colors, Vector3 basePos, Conveyor conv)
        {
            conveyor = conv;
            transform.position = basePos;
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
            // Oyun bittiyse (fail/win) artık kart gönderme — fail anında oyun anında dursun.
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            CardColor top = cards[cards.Count - 1];
            int groupSize = 0;
            for (int i = cards.Count - 1; i >= 0 && cards[i] == top; i--)
                groupSize++;

            var group = new List<CardColor>(groupSize);
            for (int i = 0; i < groupSize; i++) group.Add(top);

            // Kartlar bu konumdan (tepe kart) banda süzülür.
            Vector3 origin = visuals.Count > 0
                ? visuals[visuals.Count - 1].transform.position
                : transform.position;

            if (!conveyor.TrySend(group, origin))
            {
                // Bant dolu → gönderilemez: güçlü geri bildirim + ekran kızarması.
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

            // Açığa çıkan yeni üst kart tatmin edici şekilde "pop" yapar.
            if (visuals.Count > 0)
            {
                var topT = visuals[visuals.Count - 1].transform;
                Juice.PopIn(topT, topT.localScale, 0.16f);
            }
        }
    }
}
