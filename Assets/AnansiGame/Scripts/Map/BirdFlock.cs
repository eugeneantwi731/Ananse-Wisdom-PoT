using System.Collections.Generic;
using UnityEngine;

// Small 3D birds that fly ACROSS the village: a pair or a little V comes in from one side of the map,
// crosses over, and shrinks away to nothing as it gets far from the map. Then, after a pause, another group.
// Built in code (no model needed). Added by ScanFlow after the village has grown in; hidden when the map is lost.
public class BirdFlock : MonoBehaviour
{
    public Color birdColor = new Color32(61, 36, 22, 255);
    [Tooltip("Bird wingspan compared with the map's width.")]
    public float birdSize = .022f;
    [Tooltip("Map widths per second.")]
    public float speed = .22f;
    [Tooltip("Seconds between groups (random between the two).")]
    public Vector2 pause = new Vector2(10f, 22f);
    [Tooltip("Birds are full size inside this distance from the map centre (map widths) and gone by Vanish Distance.")]
    public float fullSizeDistance = .6f, vanishDistance = 1.1f;

    class Bird { public Transform root, wingL, wingR; public float flap, bob, glideSeed; public bool busy; }
    class Pass { public List<Bird> birds = new List<Bird>(); public List<Vector3> offsets = new List<Vector3>(); public Vector3 start, dir; public float t0, height, curve, spd; }
    readonly List<Bird> pool = new List<Bird>();
    readonly List<Pass> passes = new List<Pass>();
    float map = 1f, nextPass; bool visible;
    static Mesh bodyMesh, wingMesh;

    public void Show(float mapSize, int count)
    {
        map = mapSize;
        if (pool.Count != count) Build(count);
        visible = true; nextPass = Time.time + Random.Range(3f, 6f);
    }
    public void Hide()
    {
        visible = false; passes.Clear();
        foreach (var b in pool) { b.busy = false; if (b.root) b.root.gameObject.SetActive(false); }
    }

    void Build(int count)
    {
        foreach (var b in pool) if (b.root) Destroy(b.root.gameObject);
        pool.Clear(); passes.Clear();
        var mat = MakeMat();
        for (int i = 0; i < count; i++)
        {
            var root = new GameObject("Bird" + i).transform; root.SetParent(transform, false);
            AddPart("Body", root, BodyMesh(), mat);
            var wl = new GameObject("WingL").transform; wl.SetParent(root, false); AddPart("Mesh", wl, WingMesh(), mat);
            var wr = new GameObject("WingR").transform; wr.SetParent(root, false); AddPart("Mesh", wr, WingMesh(), mat); wr.localScale = new Vector3(-1, 1, 1);
            pool.Add(new Bird { root = root, wingL = wl, wingR = wr, flap = Random.Range(9f, 12f), bob = Random.Range(0f, 6f), glideSeed = Random.Range(0f, 100f) });
            root.gameObject.SetActive(false);
        }
    }

    void StartPass()
    {
        var free = pool.FindAll(b => !b.busy);
        if (free.Count == 0) return;
        int n = Mathf.Min(free.Count, Random.Range(1, 3)); // 1-2 birds
        var p = new Pass();
        float a = Random.Range(0f, Mathf.PI * 2f), across = Random.Range(-.25f, .25f);
        var from = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        var side = new Vector3(-from.z, 0, from.x);
        p.start = from * vanishDistance * 1.05f + side * across;          // just outside the vanish ring
        p.dir = (-from + side * Random.Range(-.25f, .25f)).normalized;   // cross over the village
        p.height = Random.Range(.22f, .38f); p.curve = Random.Range(-.25f, .25f); p.spd = speed * Random.Range(.85f, 1.15f); p.t0 = Time.time;
        for (int i = 0; i < n; i++)
        {
            var b = free[i]; b.busy = true; b.root.gameObject.SetActive(true); p.birds.Add(b);
            // little V: leader in front, the others behind and to the sides
            float row = (i + 1) / 2, sgn = i % 2 == 1 ? -1 : 1;
            p.offsets.Add(new Vector3(sgn * row * .06f, Random.Range(-.01f, .01f), -row * .07f));
        }
        passes.Add(p);
    }

    void Update()
    {
        if (!visible) return;
        float t = Time.time;
        if (t >= nextPass && passes.Count == 0) { StartPass(); nextPass = t + Random.Range(pause.x, pause.y); }
        float s = map * birdSize;
        for (int pi = passes.Count - 1; pi >= 0; pi--)
        {
            var p = passes[pi]; float e = t - p.t0, dist = e * p.spd, total = vanishDistance * 2.2f;
            // gentle curve: the heading turns a little over the crossing
            var dir = Quaternion.Euler(0, p.curve * 60f * (dist / total - .5f), 0) * p.dir;
            var lead = p.start + p.dir * dist + Vector3.Cross(Vector3.up, p.dir) * p.curve * .5f * Mathf.Sin(dist / total * Mathf.PI);
            bool done = dist > total;
            for (int i = 0; i < p.birds.Count; i++)
            {
                var b = p.birds[i];
                if (done) { b.busy = false; b.root.gameObject.SetActive(false); continue; }
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                var local = lead + rot * p.offsets[i];
                local.y = p.height + p.offsets[i].y + .015f * Mathf.Sin(t * 1.4f + b.bob);
                float r = new Vector2(local.x, local.z).magnitude;
                float size = 1f - Mathf.Clamp01((r - fullSizeDistance) / Mathf.Max(.01f, vanishDistance - fullSizeDistance)); // shrink away far from the map
                size = size * size * (3f - 2f * size);
                b.root.localPosition = local * map;
                b.root.localRotation = rot * Quaternion.Euler(0, 0, -p.curve * 30f);
                b.root.localScale = Vector3.one * s * size;
                bool glide = Mathf.PerlinNoise(b.glideSeed, t * .35f) > .62f;
                float ang = glide ? 8f : Mathf.Sin(t * b.flap + b.bob) * 38f + 6f;
                b.wingL.localRotation = Quaternion.Euler(0, 0, ang);
                b.wingR.localRotation = Quaternion.Euler(0, 0, -ang);
            }
            if (done) passes.RemoveAt(pi);
        }
    }

    void AddPart(string n, Transform parent, Mesh m, Material mat)
    {
        var g = new GameObject(n); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }
    Material MakeMat()
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"); // flat silhouettes
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", birdColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", birdColor);
        m.color = birdColor;
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        return m;
    }

    // Body: a small diamond pointing forward (+Z). Wing: a swept triangle out to +X. Unit size (wingspan ~1).
    static Mesh BodyMesh()
    {
        if (bodyMesh) return bodyMesh;
        bodyMesh = new Mesh { name = "BirdBody" };
        bodyMesh.vertices = new[] { new Vector3(0, 0, .32f), new Vector3(0, 0, -.28f), new Vector3(.07f, 0, 0), new Vector3(-.07f, 0, 0), new Vector3(0, .06f, .02f), new Vector3(0, -.05f, 0), new Vector3(.09f, 0, -.34f), new Vector3(-.09f, 0, -.34f) };
        bodyMesh.triangles = new[] { 0, 4, 2, 0, 3, 4, 0, 2, 5, 0, 5, 3, 1, 2, 4, 1, 4, 3, 1, 5, 2, 1, 3, 5, 1, 6, 7, 1, 7, 6 };
        bodyMesh.RecalculateNormals(); bodyMesh.RecalculateBounds();
        return bodyMesh;
    }
    static Mesh WingMesh()
    {
        if (wingMesh) return wingMesh;
        wingMesh = new Mesh { name = "BirdWing" };
        wingMesh.vertices = new[] { new Vector3(.03f, 0, .12f), new Vector3(.03f, 0, -.08f), new Vector3(.5f, 0, -.12f), new Vector3(.28f, 0, .05f) };
        wingMesh.triangles = new[] { 0, 3, 1, 1, 3, 2, 0, 1, 3, 1, 2, 3 };
        wingMesh.RecalculateNormals(); wingMesh.RecalculateBounds();
        return wingMesh;
    }
}
