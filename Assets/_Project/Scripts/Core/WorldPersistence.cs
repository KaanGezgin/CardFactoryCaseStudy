using UnityEngine;

namespace CardFactory.Core
{
    /// <summary>
    /// Kalıcı dünya kökünde (CardFactoryWorld) durur. Açıkken Play'de dünya silinip
    /// koddan yeniden kurulur; KAPALIYKEN (varsayılan) mevcut sahne objeleri aynen
    /// korunur ve runtime yalnızca kart + ortadaki kutuları anchor'ların altına kurar.
    /// </summary>
    public class WorldPersistence : MonoBehaviour
    {
        [Tooltip("Açık: Play'de dünyayı koddan baştan kur (kod değişince işaretle). " +
                 "Kapalı: sahnedeki objeleri koru (Inspector ayarların korunur).")]
        public bool rebuildOnPlay = false;
    }
}
