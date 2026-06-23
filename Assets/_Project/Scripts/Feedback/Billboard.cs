using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// 3B etiketleri (TextMesh / quad) kameraya dönük tutar; böylece objeye gömülü
    /// dursalar da hep okunaklı kalırlar. Kamera neredeyse sabit olsa da punch
    /// animasyonlarında hizayı korur.
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
