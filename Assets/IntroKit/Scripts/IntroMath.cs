using UnityEngine;

// Easing + helpers shared by the intro. Mirrors the web prototype one-to-one.
public static class IntroMath
{
    public static float Cl(float x) { return Mathf.Clamp01(x); }
    public static float OutCubic(float x) { x = Cl(x); return 1f - Mathf.Pow(1f - x, 3f); }
    public static float InOutCubic(float x) { x = Cl(x); return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f; }
    public static float InOutSine(float x) { x = Cl(x); return -(Mathf.Cos(Mathf.PI * x) - 1f) / 2f; }
    public static float OutBack(float x)
    {
        x = Cl(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
    public static float Bump(float x, float a, float b) { return (x <= a || x >= b) ? 0f : Mathf.Sin((x - a) / (b - a) * Mathf.PI); }
    public static float BumpLen(float t, float at, float len) { return Bump(t, at, at + len); }

    public static float Enter(float t, float from, float to, float start, float dur) { return Mathf.LerpUnclamped(from, to, OutCubic((t - start) / dur)); }
    public static float Pop(float t, float from, float to, float start, float dur) { return Mathf.LerpUnclamped(from, to, OutBack((t - start) / dur)); }
    public static float Draw(float t, float from, float to, float start, float dur) { return Mathf.LerpUnclamped(from, to, InOutSine((t - start) / dur)); }

    public static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }
    public static Color A(Color c, float a) { c.a = a; return c; }
}

// Procedural textures (no image files needed for these).
public static class IntroTex
{
    delegate Color Px(float u, float v, int x, int y);

    static Texture2D Make(int w, int h, Px f, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = wrap; t.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h, x, y);
        t.SetPixels(px); t.Apply(false);
        return t;
    }

    static float Stops(float x, float[] pos, float[] val)
    {
        if (x <= pos[0]) return val[0];
        for (int i = 1; i < pos.Length; i++)
            if (x <= pos[i]) return Mathf.Lerp(val[i - 1], val[i], (x - pos[i - 1]) / (pos[i] - pos[i - 1]));
        return val[val.Length - 1];
    }

    static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float l2 = dx * dx + dy * dy;
        float k = l2 > 0 ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / l2) : 0;
        float cx = ax + dx * k - px, cy = ay + dy * k - py;
        return Mathf.Sqrt(cx * cx + cy * cy);
    }

    public static Texture2D Glow()
    {
        float[] p = { 0f, 0.2f, 0.5f, 1f }, a = { 1f, 0.8f, 0.18f, 0f };
        return Make(128, 128, (u, v, x, y) =>
        {
            float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f;
            return new Color(1, 1, 1, Stops(d, p, a));
        });
    }

    public static Texture2D Ring()
    {
        float[] p = { 0f, 0.55f, 1f }, a = { 0f, 0.8f, 0f };
        return Make(256, 256, (u, v, x, y) =>
        {
            float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 256f;
            float k = (d - 60f) / 68f;
            return new Color(1, 1, 1, k < 0 ? 0 : Stops(k, p, a));
        });
    }

    public static Texture2D Circle()
    {
        return Make(256, 256, (u, v, x, y) =>
        {
            float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f));
            return new Color(1, 1, 1, Mathf.Clamp01((0.5f - d) * 256f));
        });
    }

    // 64x32 horizontal capsule; slice border 16 left/right.
    public static Texture2D Capsule()
    {
        return Make(64, 32, (u, v, x, y) =>
        {
            float d = SegDist(x + .5f, y + .5f, 16, 16, 48, 16);
            return new Color(1, 1, 1, Mathf.Clamp01(16f - d + 0.5f));
        });
    }

    public static Texture2D Diamond()
    {
        return Make(64, 64, (u, v, x, y) =>
        {
            float d = Mathf.Abs(u - .5f) + Mathf.Abs(v - .5f);
            return new Color(1, 1, 1, Mathf.Clamp01((0.5f - d) * 64f));
        });
    }

    // 272x136 pill outline, 5px stroke; slice border 68.
    public static Texture2D PillOutline()
    {
        return Make(272, 136, (u, v, x, y) =>
        {
            float d = SegDist(x + .5f, y + .5f, 68, 68, 204, 68);
            return new Color(1, 1, 1, Mathf.Clamp01(2.5f - Mathf.Abs(d - 65.5f) + 0.5f));
        });
    }

    // 256x128 soft glow around a pill; slice border 64.
    public static Texture2D PillGlow()
    {
        return Make(256, 128, (u, v, x, y) =>
        {
            float d = SegDist(x + .5f, y + .5f, 64, 64, 192, 64);
            float a = d < 32 ? 1f : Mathf.Exp(-Mathf.Pow((d - 32f) / 14f, 2f));
            return new Color(1, 1, 1, a);
        });
    }

    // Across-the-width profile for trails.
    public static Texture2D SoftLine()
    {
        return Make(4, 64, (u, v, x, y) => new Color(1, 1, 1, Mathf.Exp(-Mathf.Pow((v - .5f) / .22f, 2f))));
    }

    // Soft-edged vertical line: smooth edges so a thin, swaying thread never flickers
    public static Texture2D Thread()
    {
        var t = Make(16, 4, (u, v, x, y) => new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(u - .5f) * 2f, 3f))));
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    public static Texture2D Shimmer()
    {
        return Make(64, 4, (u, v, x, y) => new Color(1, 1, 1, 1f - Mathf.Abs(2f * u - 1f)));
    }

    public static Texture2D VerticalFade()
    {
        return Make(4, 32, (u, v, x, y) => new Color(0, 0, 0, (1f - v) * 0.4f));
    }

    public static Texture2D Backdrop()
    {
        Color c0 = IntroMath.Hex("#4A2C19"), c1 = IntroMath.Hex("#231208"), c2 = IntroMath.Hex("#0C0604");
        return Make(256, 256, (u, v, x, y) =>
        {
            float d = Mathf.Clamp01(Mathf.Sqrt(Mathf.Pow((u - .5f) / .8f, 2) + Mathf.Pow((v - .52f) / .6f, 2)));
            Color c = d < .55f ? Color.Lerp(c0, c1, d / .55f) : Color.Lerp(c1, c2, (d - .55f) / .45f);
            float e = Mathf.Sqrt(Mathf.Pow((u - .5f) / 1.2f, 2) + Mathf.Pow((v - .5f) / .9f, 2));
            float vig = e < .55f ? 0 : Mathf.Clamp01((e - .55f) / .45f) * .55f;
            c = Color.Lerp(c, Color.black, vig); c.a = 1;
            return c;
        });
    }

    // Warm lit floor with a faint cracked-tile pattern (same recipe as the prototype).
    public static Texture2D Floor()
    {
        const int S = 1024;
        var col = new Color[S * S];
        float[] gp = { 0f, 0.3f, 1f };
        Color g0 = new Color(240 / 255f, 150 / 255f, 60 / 255f, .85f), g1 = new Color(160 / 255f, 76 / 255f, 26 / 255f, .5f), g2 = new Color(60 / 255f, 24 / 255f, 8 / 255f, 0f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float r = Mathf.Sqrt((x + .5f - 512) * (x + .5f - 512) + (y + .5f - 512) * (y + .5f - 512)) / 512f;
                col[y * S + x] = r < gp[1] ? Color.Lerp(g0, g1, r / gp[1]) : Color.Lerp(g1, g2, Mathf.Clamp01((r - gp[1]) / (1 - gp[1])));
            }

        long seed = 7;
        System.Func<float> rnd = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647f; };
        const int N = 32;
        var P = new Vector2[N + 1, N + 1];
        for (int y = 0; y <= N; y++)
            for (int x = 0; x <= N; x++)
                P[y, x] = new Vector2(x * 32 + (rnd() - .5f) * 20, y * 32 + (rnd() - .5f) * 20);

        Color lc = new Color(45 / 255f, 18 / 255f, 5 / 255f, 1);
        System.Action<Vector2, Vector2> line = (a, b) =>
        {
            int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - 3)), x1 = Mathf.Min(S - 1, (int)(Mathf.Max(a.x, b.x) + 3));
            int y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - 3)), y1 = Mathf.Min(S - 1, (int)(Mathf.Max(a.y, b.y) + 3));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float cov = Mathf.Clamp01(1.6f - SegDist(x + .5f, y + .5f, a.x, a.y, b.x, b.y) + .5f) * 0.45f;
                    if (cov <= 0) continue;
                    Color d = col[y * S + x];
                    col[y * S + x] = new Color(lc.r * cov + d.r * (1 - cov), lc.g * cov + d.g * (1 - cov), lc.b * cov + d.b * (1 - cov), cov + d.a * (1 - cov));
                }
        };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                line(P[y, x], P[y, x + 1]); line(P[y, x + 1], P[y + 1, x + 1]);
                line(P[y + 1, x + 1], P[y + 1, x]); line(P[y + 1, x], P[y, x]);
                if (rnd() > .55f) line(P[y, x], P[y + 1, x + 1]);
            }

        float[] mp = { 0f, 0.45f, 1f }, mv = { 1f, 0.45f, 0f };
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float r = Mathf.Sqrt((x + .5f - 512) * (x + .5f - 512) + (y + .5f - 512) * (y + .5f - 512)) / 512f;
                col[y * S + x].a *= Stops(r, mp, mv);
            }

        var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear;
        t.SetPixels(col); t.Apply(true);
        return t;
    }

    public static Sprite ToSprite(Texture2D t, Vector4 border = default(Vector4))
    {
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    public static Sprite Load(string path)
    {
        var t = Resources.Load<Texture2D>(path);
        if (t == null) { Debug.LogError("[Intro] Missing image: Resources/" + path); return null; }
        return ToSprite(t);
    }
}
