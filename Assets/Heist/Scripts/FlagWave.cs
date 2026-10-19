using UnityEngine;

// A cloth flag flying from a pole: a two-sided grid mesh whose free end ripples
// in the wind (travelling sine waves that grow away from the hoist, plus a
// little droop and flutter). Origin = top corner at the pole, flag along +X.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlagWave : MonoBehaviour
{
    public float width = 2.7f, height = 1.42f;
    public int nx = 28, ny = 14;
    public float amplitude = 0.2f, speed = 5f, waveLength = 1.6f;

    Mesh _mesh;
    Vector3[] _base, _v;
    float _seed;

    void Awake() { Build(); _seed = Random.value * 10f; }

    public void Build()
    {
        int row = nx + 1, side = row * (ny + 1);
        _base = new Vector3[side * 2];
        var uv = new Vector2[side * 2];
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                int k = j * row + i;
                _base[k] = _base[k + side] = new Vector3(width * i / nx, -height * j / ny, 0f);
                uv[k] = uv[k + side] = new Vector2((float)i / nx, 1f - (float)j / ny);
            }
        var tris = new int[nx * ny * 12];
        int t = 0;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                // front (faces -Z) and back (faces +Z) use separate vertices so each side gets its own normals
                tris[t++] = a; tris[t++] = b; tris[t++] = c; tris[t++] = b; tris[t++] = d; tris[t++] = c;
                tris[t++] = a + side; tris[t++] = c + side; tris[t++] = b + side; tris[t++] = b + side; tris[t++] = c + side; tris[t++] = d + side;
            }
        _v = (Vector3[])_base.Clone();
        _mesh = new Mesh { name = "Flag" };
        _mesh.MarkDynamic();
        _mesh.vertices = _v; _mesh.uv = uv; _mesh.triangles = tris;
        _mesh.RecalculateNormals(); _mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        Ripple(0f);
    }

    void Update()
    {
        if (_mesh == null) return;
        Ripple(Time.time + _seed);
    }

    void Ripple(float time)
    {
        float k = Mathf.PI * 2f / waveLength;
        for (int n = 0; n < _base.Length; n++)
        {
            Vector3 p = _base[n];
            float u = p.x / width;                     // 0 at the pole, 1 at the fly end
            float a = amplitude * (0.15f + u);
            float z = Mathf.Sin(p.x * k - time * speed) * a
                    + Mathf.Sin(p.x * k * 2.3f - time * speed * 1.7f + p.y * 2f) * a * 0.3f;
            float y = p.y - u * u * 0.08f * (1.2f + Mathf.Sin(time * 0.7f));   // the fly end sags a little
            float x = p.x * (1f - 0.04f * u);          // ripples pull the cloth in
            _v[n] = new Vector3(x, y, z);
        }
        _mesh.vertices = _v;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }
}
