using System.Collections.Generic;
using UnityEngine;
using M = IntroMath;

// The 3D part of the intro: pot, orbs, floor, glow, light shafts, sparks and the camera move.
// Plain class (not a MonoBehaviour) — IntroSequence creates and drives it.
public class IntroPotRig
{
    class Orb { public Transform tr, halo, outer; public Renderer core, haloR, outerR; public Material haloM, outerM; public Transform[] trail; public Material[] trailM; public Color col; public float a0, y0; public bool vis; }
    class Beam { public Transform holder, mesh; public Material mat; public float w, sp, ph; }
    class Spark { public Transform tr; public Material mat; public float a, r, off, drift, h, size; }
    class Glow { public Material m; public string prop; public Color baseCol; }

    readonly Camera cam;
    readonly Transform root, pivot, backdrop, mouthGlow, pool, beamsRoot;
    readonly Material mouthM, poolM, floorM, shellM;
    readonly Light inner;
    readonly List<Orb> orbs = new List<Orb>();
    readonly List<Beam> beams = new List<Beam>();
    readonly List<Spark> sparks = new List<Spark>();
    readonly List<Glow> glows = new List<Glow>();
    readonly Transform[] ripples = new Transform[6];
    readonly Material[] rippleM = new Material[6];
    readonly List<Transform> billboards = new List<Transform>();
    readonly Color gold = M.Hex("#F2C14E"), amber = M.Hex("#FFB040");

    public IntroPotRig(Camera cam, GameObject potPrefab, float potScale)
    {
        this.cam = cam;
        root = new GameObject("IntroPotRig").transform;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = M.Hex("#0C0604");
        cam.nearClipPlane = 0.01f; cam.farClipPlane = 30f;

        var glowTex = IntroTex.Glow();

        // Backdrop gradient that follows the camera
        var bm = Mat("Intro/Transparent", IntroTex.Backdrop(), Color.white); bm.renderQueue = 1000;
        backdrop = Quad("Backdrop", bm, cam.transform, 1f);

        // Lights
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = M.Hex("#FFD7A8") * 0.25f;
        RenderSettings.ambientEquatorColor = M.Hex("#7A4428") * 0.22f;
        RenderSettings.ambientGroundColor = M.Hex("#2A1810") * 0.25f;
        cam.allowMSAA = true;
        // Unity reflects its default grey-blue sky on shiny surfaces — that is the whitish edge. Turn it way down.
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0.12f;
        DynamicGI.UpdateEnvironment();
        MakeLight("Key", LightType.Directional, M.Hex("#FFD2A0"), 0.9f, new Vector3(1f, 1.6f, 1.2f));
        MakeLight("Rim", LightType.Directional, M.Hex("#FFB070"), 0.6f, new Vector3(-1.2f, 0.8f, -1f));
        inner = MakeLight("PotInner", LightType.Point, gold, 0f, Vector3.zero);
        inner.transform.localPosition = new Vector3(0, 0.33f, 0); inner.range = 1.2f;

        // Floor, contact shadow, glow pool, ripples
        floorM = Mat("Intro/Transparent", IntroTex.Floor(), Color.white);
        Flat(Quad("Floor", floorM, root, 2.4f), -0.002f);
        Flat(Quad("Shadow", Mat("Intro/Transparent", glowTex, new Color(0, 0, 0, 0.6f)), root, 0.7f), 0.001f);
        poolM = Mat("Intro/Additive", glowTex, M.A(amber, 0));
        pool = Quad("GlowPool", poolM, root, 1.5f); Flat(pool, 0.0015f);
        var ringTex = IntroTex.Ring();
        for (int i = 0; i < 6; i++) { rippleM[i] = Mat("Intro/Additive", ringTex, M.A(amber, 0)); ripples[i] = Quad("Ripple" + i, rippleM[i], root, 1f); Flat(ripples[i], 0.002f); }

        // Pot
        pivot = new GameObject("PotPivot").transform; pivot.SetParent(root, false);
        if (potPrefab != null)
        {
            var pot = Object.Instantiate(potPrefab, pivot, false);
            pot.transform.localPosition = Vector3.zero;
            pot.transform.localScale = pot.transform.localScale * potScale;
            CollectGlow(pot);
            shellM = Mat("Intro/Fresnel", null, amber);
            foreach (var mf in pot.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name.ToLower().Contains("interior") || mf.sharedMesh == null) continue;
                var s = new GameObject("GlowShell");
                s.transform.SetParent(mf.transform, false);
                s.transform.localScale = Vector3.one * 1.012f;
                s.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = s.AddComponent<MeshRenderer>(); r.sharedMaterial = shellM; NoShadows(r);
            }
        }
        else Debug.LogError("[Intro] Assign your pot to 'Pot Prefab' on IntroSequence.");

        mouthM = Mat("Intro/Additive", glowTex, M.A(gold, 0));
        mouthGlow = Quad("MouthGlow", mouthM, root, 0.3f); mouthGlow.localPosition = new Vector3(0, 0.39f, 0); billboards.Add(mouthGlow);

        // Light shafts
        beamsRoot = new GameObject("Beams").transform; beamsRoot.SetParent(root, false); beamsRoot.localPosition = new Vector3(0, 0.38f, 0);
        long seed = 7;
        System.Func<float> rnd = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647f; };
        AddBeam(0.13f, 0.19f, 0.34f, 0, 0, 0.45f, 0.9f, 0);
        for (int i = 0; i < 7; i++)
            AddBeam(0.025f + rnd() * 0.02f, 0.06f + rnd() * 0.05f, 0.26f + rnd() * 0.18f, 0.12f + rnd() * 0.28f, i / 7f * Mathf.PI * 2 + rnd() * 0.5f, 0.5f + rnd() * 0.35f, 0.6f + rnd() * 1.2f, rnd() * 6);

        // Sparks
        for (int i = 0; i < 40; i++)
        {
            var m = Mat("Intro/Additive", glowTex, M.A(M.Hex("#FFC860"), 0));
            var t = Quad("Spark" + i, m, root, 0.03f); billboards.Add(t);
            sparks.Add(new Spark { tr = t, mat = m, a = rnd() * Mathf.PI * 2, r = 0.03f + rnd() * 0.14f, off = rnd() * 2.4f, drift = (rnd() - .5f) * 0.25f, h = 0.25f + rnd() * 0.35f, size = 0.014f + rnd() * 0.02f });
        }

        // Orbs (the pin colours)
                for (int i = 0; i < IntroSequence.OrbColors.Length; i++)
        {
            var col = M.Hex(IntroSequence.OrbColors[i]);
            var o = new Orb { col = col, a0 = i * Mathf.PI / 3 + 0.4f, y0 = 0.08f + (i % 3) * 0.12f };
            o.tr = new GameObject("Orb" + i).transform; o.tr.SetParent(root, false);
            o.outerM = Mat("Intro/Additive", glowTex, M.A(col, 0.5f)); o.outer = Quad("Outer", o.outerM, o.tr, 0.3f); o.outerR = o.outer.GetComponent<Renderer>(); billboards.Add(o.outer);
            o.haloM = Mat("Intro/Additive", glowTex, M.A(col, 0.9f)); o.halo = Quad("Halo", o.haloM, o.tr, 0.12f); o.haloR = o.halo.GetComponent<Renderer>(); billboards.Add(o.halo);
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(o.tr, false); core.transform.localScale = Vector3.one * 0.044f;
            o.core = core.GetComponent<Renderer>(); o.core.sharedMaterial = Mat("Intro/Transparent", null, Color.Lerp(col, Color.white, 0.15f)); NoShadows(o.core);
            o.trail = new Transform[10]; o.trailM = new Material[10];
            for (int k = 0; k < 10; k++)
            {
                o.trailM[k] = Mat("Intro/Additive", glowTex, M.A(col, 0));
                o.trail[k] = Quad("Trail" + k, o.trailM[k], root, 0.09f * (1 - k / 10f)); billboards.Add(o.trail[k]);
            }
            SetVis(o, false);
            orbs.Add(o);
        }
    }

    // ---------- per frame ----------

    public void Render(float t, bool land, float stageH, float uiScale, float symbolAngle)
    {
        const float ORB_START = IntroSequence.ORB_START, ORB_GAP = IntroSequence.ORB_GAP, ORB_FLIGHT = IntroSequence.ORB_FLIGHT, LAST_IN = IntroSequence.LAST_IN;
        int entered = 0; float flash = 0; Color mix = gold;
        float rise = LAST_IN + 0.5f;

        for (int i = 0; i < orbs.Count; i++)
        {
            var o = orbs[i];
            Vector3 p;
            float f = Mathf.Max(0, OrbAt(o, i, t, rise, out p));
            bool vis = f > 0;
            SetVis(o, vis);
            if (vis)
            {
                o.tr.localPosition = p;
                o.tr.localScale = Vector3.one * f;
                float pulse = 1 + Mathf.Sin(t * 14 + i) * 0.08f;
                o.halo.localScale = Vector3.one * 0.12f * pulse; o.outer.localScale = Vector3.one * 0.3f * pulse;
            }
            bool orbiting = t > rise + i * 0.18f;
            for (int k = 0; k < 10; k++)
            {
                Vector3 tp;
                float ft = OrbAt(o, i, t - (k + 1) * (orbiting ? 0.07f : 0.035f), rise, out tp);
                bool on = ft > 0 && f > 0;
                o.trail[k].gameObject.SetActive(on);
                if (!on) continue;
                o.trail[k].localPosition = tp;
                o.trailM[k].color = M.A(o.col, (orbiting ? 0.7f : 0.9f) * (1 - k / 10f) * Mathf.Min(ft, f));
            }

            float u = (t - (ORB_START + i * ORB_GAP)) / ORB_FLIGHT;
            if (u >= 1)
            {
                entered++;
                mix = entered == 1 ? o.col : Color.Lerp(mix, o.col, 0.6f);
                flash = Mathf.Max(flash, 1 - M.Cl((u - 1) / 0.5f));
            }
        }
        if (entered == orbs.Count)
        {
            float cyc = (t * 1.2f) % orbs.Count; int i0 = Mathf.FloorToInt(cyc);
            mix = Color.Lerp(orbs[i0].col, orbs[(i0 + 1) % orbs.Count].col, cyc - i0);
        }
        float charge = entered / (float)orbs.Count, breathe = 1 + Mathf.Sin(t * 3) * 0.06f * charge;
        inner.color = mix; inner.intensity = (charge * 0.9f + flash * 0.8f) * 1.5f;
        mouthM.color = M.A(mix, entered > 0 ? 1 : 0);
        mouthGlow.localScale = Vector3.one * (0.1f + charge * 0.2f + flash * 0.14f) * breathe;

        // Pot: keeps turning (slowing) while orbs enter, hops on each one, then settles facing the camera
        float stop = LAST_IN + 0.7f;
        float yaw = M.OutCubic(t / stop) * (1440f + symbolAngle);
        float hop = 0, tilt = 0;
        for (int i = 0; i < orbs.Count; i++)
        {
            float age = t - (ORB_START + i * ORB_GAP + ORB_FLIGHT);
            hop += M.Bump(age, 0, 0.4f);
            tilt += age > 0 ? Mathf.Exp(-5 * age) * Mathf.Sin(age * 16) * (i % 2 == 1 ? 1 : -1) : 0;
        }
        float lift = t < stop ? Mathf.Sin(Mathf.PI * M.Cl(t / 1.2f)) * 0.02f : 0;
        pivot.localPosition = new Vector3(0, lift + hop * 0.018f, 0);
        pivot.localRotation = Quaternion.Euler(0, yaw, tilt * 0.03f * Mathf.Rad2Deg);
        pivot.localScale = new Vector3(1 + hop * 0.03f, 1 - hop * 0.04f, 1 + hop * 0.03f);

        float firstIn = ORB_START + ORB_FLIGHT;
        float potGlow = t < firstIn - 0.3f ? 0 : t < stop ? 0.45f + charge * 0.6f + flash * 0.9f + Mathf.Sin(t * 6) * 0.08f : 0.9f;
        foreach (var gl in glows) gl.m.SetColor(gl.prop, gl.baseCol * potGlow);
        Color glowCol = Color.Lerp(mix, amber, 0.55f);
        if (shellM != null) { shellM.SetFloat("_Strength", potGlow * 0.85f + flash * 0.5f); shellM.color = glowCol; }

        poolM.color = M.A(glowCol, Mathf.Min(1, potGlow * 0.75f + flash * 0.5f));
        pool.localScale = Vector3.one * 1.5f * (0.8f + potGlow * 0.45f + flash * 0.2f + Mathf.Sin(t * 2.4f) * 0.03f * potGlow);
        for (int i = 0; i < 6; i++)
        {
            float age = t - (ORB_START + i * ORB_GAP + ORB_FLIGHT);
            bool on = age > 0 && age < 1.4f;
            ripples[i].gameObject.SetActive(on);
            if (!on) continue;
            float q = age / 1.4f;
            ripples[i].localScale = Vector3.one * (0.35f + M.OutCubic(q) * 1.5f);
            rippleM[i].color = M.A(Color.Lerp(orbs[i].col, amber, 0.6f), (1 - q) * 0.45f);
        }

        float burst = M.Cl((t - LAST_IN) / 0.6f);
        Color rayCol = Color.Lerp(M.Hex("#FFC45A"), mix, 0.25f);
        for (int i = 0; i < beams.Count; i++)
        {
            var b = beams[i];
            float flicker = i == 0 ? 1 : 0.55f + 0.45f * Mathf.Sin(t * b.sp + b.ph) * Mathf.Sin(t * b.sp * 0.37f + b.ph * 2);
            float op = burst * b.w * Mathf.Max(0, flicker) + flash * 0.35f * b.w;
            b.mat.color = M.A(Color.Lerp(M.Hex("#FFB347"), rayCol, 0.3f), op);
            var s = b.mesh.localScale; s.y = 0.7f + burst * 0.3f + (i > 0 ? Mathf.Sin(t * b.sp * 0.5f + b.ph) * 0.08f : 0); b.mesh.localScale = s;
        }
        beamsRoot.localRotation = Quaternion.Euler(0, t * 0.05f * Mathf.Rad2Deg, 0);

        foreach (var p in sparks)
        {
            bool on = t > LAST_IN;
            p.tr.gameObject.SetActive(on);
            if (!on) continue;
            float q = ((t - LAST_IN + p.off) % 2.4f) / 2.4f;
            p.tr.localPosition = new Vector3(Mathf.Cos(p.a) * (p.r + q * 0.08f) + p.drift * q, 0.4f + q * p.h, Mathf.Sin(p.a) * (p.r + q * 0.08f));
            p.tr.localScale = Vector3.one * p.size * (1 - q * 0.6f);
            p.mat.color = M.A(p.mat.color, Mathf.Sin(q * Mathf.PI) * burst);
        }

        float fl = 0.3f + potGlow * 0.3f + flash * 0.1f;
        floorM.color = new Color(fl, fl, fl, 1);

        PlaceCamera(t, land, stageH, uiScale);
        foreach (var b in billboards) b.rotation = cam.transform.rotation;
    }

    void PlaceCamera(float t, bool land, float stageH, float uiScale)
    {
        float k = M.InOutCubic((t - IntroSequence.CAM_MOVE) / 1.3f);
        float ang = -0.3f + 0.55f * M.InOutCubic(t / (IntroSequence.CAM_MOVE + 1.3f));
        Vector3 target;
        if (land)
        {
            float rad = 1.5f + k * 0.35f;
            cam.transform.position = new Vector3(Mathf.Sin(ang) * rad, 0.42f + k * 0.04f, Mathf.Cos(ang) * rad);
            target = new Vector3(0, 0.22f, 0);
        }
        else
        {
            float rad = 1.25f + k * 0.4f;
            cam.transform.position = new Vector3(Mathf.Sin(ang) * rad, 0.42f + k * 0.16f, Mathf.Cos(ang) * rad);
            target = new Vector3(0, 0.24f + k * 0.08f, 0);
        }
        cam.transform.LookAt(target);

        // Keep the pot the same size relative to the design, whatever the phone's shape
        float stageFov = land ? 30f : 52f;
        float stagePx = stageH * uiScale;
        float fov = 2f * Mathf.Atan(Mathf.Tan(stageFov * 0.5f * Mathf.Deg2Rad) * Screen.height / Mathf.Max(1f, stagePx)) * Mathf.Rad2Deg;
        cam.ResetProjectionMatrix();
        cam.fieldOfView = fov;
        if (land)
        {
            // Slide the pot to the left third (same as the prototype's view offset)
            var p = cam.projectionMatrix;
            p[0, 2] = 2f * (k * 420f * uiScale) / Screen.width;
            cam.projectionMatrix = p;
        }

        float d = 15f, h = 2f * d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        backdrop.localPosition = new Vector3(0, 0, d);
        backdrop.localRotation = Quaternion.identity;
        backdrop.localScale = new Vector3(h * cam.aspect * 1.6f, h * 1.1f, 1);
    }

    float OrbAt(Orb o, int i, float t, float rise, out Vector3 p)
    {
        float s = IntroSequence.ORB_START + i * IntroSequence.ORB_GAP, u = (t - s) / IntroSequence.ORB_FLIGHT;
        p = Vector3.zero;
        if (u <= 0) return 0;
        if (u <= 1) { p = OrbPath(o, M.InOutCubic(u)); return Mathf.Min(1, u * 5); }
        float r0 = rise + i * 0.18f;
        if (t < r0)
        {
            if (u < 1.15f) { p = new Vector3(0, 0.37f - (u - 1) * 0.9f, 0); return 1 - (u - 1) / 0.15f; }
            return 0;
        }
        float k = M.OutCubic((t - r0) / 1.1f);
        float ang = i * Mathf.PI / 3 + (t - r0) * 0.7f + 0.8f;
        float oy = 0.43f + i * 0.012f + Mathf.Sin(t * 1.6f + i) * 0.01f;
        p = new Vector3(Mathf.Cos(ang) * 0.24f * k, 0.37f + (oy - 0.37f) * k + Mathf.Sin(k * Mathf.PI) * 0.06f, Mathf.Sin(ang) * 0.24f * k);
        return Mathf.Min(1, (t - r0) * 4);
    }

    static Vector3 OrbPath(Orb o, float u)
    {
        float r = 0.8f * Mathf.Pow(1 - u, 1.3f), a = o.a0 + u * Mathf.PI * 1.35f;
        return new Vector3(Mathf.Cos(a) * r, o.y0 + (0.37f - o.y0) * u + Mathf.Sin(u * Mathf.PI) * 0.2f, Mathf.Sin(a) * r);
    }

    // ---------- helpers ----------

    void CollectGlow(GameObject pot)
    {
        foreach (var r in pot.GetComponentsInChildren<Renderer>())
            foreach (var m in r.materials)
            {
                string prop = m.HasProperty("_EmissionColor") ? "_EmissionColor" : m.HasProperty("emissiveFactor") ? "emissiveFactor" : null;
                if (prop == null) continue;
                Color c = m.GetColor(prop);
                if (c.maxColorComponent < 0.01f) continue;   // this material has no emission (e.g. the inside of the pot)
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                glows.Add(new Glow { m = m, prop = prop, baseCol = c });
                m.SetColor(prop, Color.black);
            }
        if (glows.Count == 0) Debug.Log("[Intro] The pot has no emission map, so only the rim glow will show. That's OK.");
    }

    void AddBeam(float rb, float rt, float h, float tilt, float yaw, float w, float sp, float ph)
    {
        var holder = new GameObject("BeamHolder").transform; holder.SetParent(beamsRoot, false);
        holder.localRotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
        var go = new GameObject("Beam");
        go.transform.SetParent(holder, false);
        go.transform.localRotation = Quaternion.Euler(0, 0, tilt * Mathf.Rad2Deg);
        go.transform.localPosition = new Vector3(Mathf.Sin(tilt) * 0.02f, 0, 0);
        go.AddComponent<MeshFilter>().sharedMesh = Cone(rt, rb, h, 32);
        var mat = Mat("Intro/Beam", null, M.A(M.Hex("#FFC45A"), 0));
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; NoShadows(r);
        beams.Add(new Beam { holder = holder, mesh = go.transform, mat = mat, w = w, sp = sp, ph = ph });
    }

    static Mesh Cone(float rt, float rb, float h, int seg)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2, c = Mathf.Cos(a), s = Mathf.Sin(a);
            v.Add(new Vector3(c * rb, 0, s * rb)); v.Add(new Vector3(c * rt, h, s * rt));
            var nn = new Vector3(c, (rb - rt) / h, s).normalized; n.Add(nn); n.Add(nn);
            uv.Add(new Vector2(i / (float)seg, 0)); uv.Add(new Vector2(i / (float)seg, 1));
        }
        for (int i = 0; i < seg; i++) { int a = i * 2; tri.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
        var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return m;
    }

    Light MakeLight(string name, LightType type, Color c, float intensity, Vector3 from)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(root, false);
        l.type = type; l.color = c; l.intensity = intensity; l.shadows = LightShadows.None;
        if (type == LightType.Directional) l.transform.rotation = Quaternion.LookRotation(-from.normalized);
        return l;
    }

    static Material Mat(string shader, Texture tex, Color c)
    {
        var sh = Shader.Find(shader);
        if (sh == null) { Debug.LogError("[Intro] Shader not found: " + shader + ". Keep the Shaders folder inside Resources/Intro."); sh = Shader.Find("Sprites/Default"); }
        var m = new Material(sh);
        if (tex != null) m.mainTexture = tex;
        m.color = c;
        return m;
    }

    static Transform Quad(string name, Material m, Transform parent, float size)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(g.GetComponent<Collider>());
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localScale = Vector3.one * size;
        var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; NoShadows(r);
        return g.transform;
    }

    static void Flat(Transform t, float y) { t.localRotation = Quaternion.Euler(90, 0, 0); t.localPosition = new Vector3(0, y, 0); }

    static void NoShadows(Renderer r) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }

    static void SetVis(Orb o, bool v)
    {
        o.vis = v;
        o.core.enabled = v; o.haloR.enabled = v; o.outerR.enabled = v;
    }
}
