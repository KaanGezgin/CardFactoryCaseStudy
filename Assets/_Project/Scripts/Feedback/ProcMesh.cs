using UnityEngine;

namespace CardFactory.Feedback
{
    /// <summary>
    /// Prosedürel yuvarlatılmış küp mesh'i (harici asset YOK). Tüm kutu nesneleri
    /// (kart, slot, kasa, kapı, dock parçaları) tek paylaşılan BİRİM mesh'i kullanır
    /// ve non-uniform localScale ile boyutlanır — yani CreatePrimitive(Cube)'un
    /// yumuşak köşeli, parlak "cartoon" muadili. Köşe yarıçapı birim küpün yarısının
    /// oranı olduğu için aşırı uzun parçalarda kullanılmaz (bant düz kalır).
    /// </summary>
    public static class ProcMesh
    {
        static Mesh unitCube;

        public static Mesh UnitRoundedCube
        {
            get
            {
                if (unitCube == null) unitCube = BuildRoundedCube(Vector3.one, 0.2f, 16);
                return unitCube;
            }
        }

        /// <summary>CreatePrimitive(Cube) yerine: birim yuvarlak-küp mesh'li, collider'sız GO.</summary>
        public static GameObject RoundedCube(string name = "Box")
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = UnitRoundedCube;
            go.AddComponent<MeshRenderer>();
            return go;
        }

        public static Mesh BuildRoundedCube(Vector3 size, float radius, int seg)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)));
            Vector3 e = new Vector3(h.x - radius, h.y - radius, h.z - radius);

            int per = (seg + 1) * (seg + 1);
            const int faces = 6;
            var verts = new Vector3[per * faces];
            var norms = new Vector3[per * faces];
            var uvs = new Vector2[per * faces];
            var tris = new int[seg * seg * 6 * faces];

            int vi = 0, ti = 0;
            // n, u, v seçimi cross(u, v) == n olacak şekilde (winding sonradan düzeltiliyor).
            AddFace(Vector3.right,   Vector3.up,      Vector3.forward, h, e, radius, seg, verts, norms, uvs, tris, ref vi, ref ti);
            AddFace(Vector3.left,    Vector3.forward, Vector3.up,      h, e, radius, seg, verts, norms, uvs, tris, ref vi, ref ti);
            AddFace(Vector3.up,      Vector3.forward, Vector3.right,   h, e, radius, seg, verts, norms, uvs, tris, ref vi, ref ti);
            AddFace(Vector3.down,    Vector3.right,   Vector3.forward, h, e, radius, seg, verts, norms, uvs, tris, ref vi, ref ti);
            AddFace(Vector3.forward, Vector3.right,   Vector3.up,      h, e, radius, seg, verts, norms, uvs, tris, ref vi, ref ti);
            AddFace(Vector3.back,    Vector3.up,      Vector3.right,   h, e, radius, seg, verts, norms, uvs, tris, ref vi, ref ti);

            // Winding'i, sakladığımız (dışa dönük) normallere göre garantiye al.
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 p0 = verts[tris[t]], p1 = verts[tris[t + 1]], p2 = verts[tris[t + 2]];
                Vector3 gn = Vector3.Cross(p1 - p0, p2 - p0);
                Vector3 vn = norms[tris[t]] + norms[tris[t + 1]] + norms[tris[t + 2]];
                if (Vector3.Dot(gn, vn) < 0f)
                {
                    (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
                }
            }

            var mesh = new Mesh { name = "RoundedCube" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFace(Vector3 n, Vector3 u, Vector3 v, Vector3 h, Vector3 e, float r, int seg,
            Vector3[] verts, Vector3[] norms, Vector2[] uvs, int[] tris, ref int vi, ref int ti)
        {
            int baseV = vi;
            float hu = Mathf.Abs(u.x * h.x + u.y * h.y + u.z * h.z);
            float hv = Mathf.Abs(v.x * h.x + v.y * h.y + v.z * h.z);
            float hn = Mathf.Abs(n.x * h.x + n.y * h.y + n.z * h.z);

            for (int j = 0; j <= seg; j++)
            for (int i = 0; i <= seg; i++)
            {
                float fu = Mathf.Lerp(-hu, hu, i / (float)seg);
                float fv = Mathf.Lerp(-hv, hv, j / (float)seg);
                Vector3 p = n * hn + u * fu + v * fv;
                Vector3 c = new Vector3(
                    Mathf.Clamp(p.x, -e.x, e.x),
                    Mathf.Clamp(p.y, -e.y, e.y),
                    Mathf.Clamp(p.z, -e.z, e.z));
                Vector3 dir = p - c;
                float mag = dir.magnitude;
                Vector3 nm;
                if (mag > 1e-5f) { nm = dir / mag; p = c + nm * r; }
                else nm = n;

                verts[vi] = p;
                norms[vi] = nm;
                uvs[vi] = new Vector2(i / (float)seg, j / (float)seg);
                vi++;
            }

            int row = seg + 1;
            for (int j = 0; j < seg; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = baseV + j * row + i;
                int b = a + 1;
                int c = a + row;
                int d = c + 1;
                tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                tris[ti++] = a; tris[ti++] = d; tris[ti++] = b;
            }
        }
    }
}
