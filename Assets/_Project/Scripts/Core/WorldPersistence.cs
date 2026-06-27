using UnityEngine;

namespace CardFactory.Core
{
    /// <summary>
    /// Lives on the persistent world root (CardFactoryWorld). When enabled, the world is
    /// destroyed and rebuilt from code on Play; when disabled (default) the existing scene
    /// objects are kept as-is and runtime only builds the cards + center bins under the anchors.
    /// </summary>
    public class WorldPersistence : MonoBehaviour
    {
        [Tooltip("On: rebuild the world from code on Play (tick this when code changes). " +
                 "Off: keep the scene objects (your Inspector tweaks are preserved).")]
        public bool rebuildOnPlay = false;
    }
}
