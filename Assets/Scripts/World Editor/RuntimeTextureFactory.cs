using UnityEngine;

public static class RuntimeTextureFactory
{
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap"); // URP/HDRP
    static readonly int MainTex = Shader.PropertyToID("_MainTex"); // Built-in

    // --- TEXTURES ---

    // Subtle Perlin noise texture (good for dirt/grass base)
    public static Texture2D MakeNoise(int size = 512, float scale = 0.05f, float contrast = 0.8f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        tex.name = "NoiseTex";
        for (int y=0; y<size; y++)
        for (int x=0; x<size; x++)
        {
            float n = Mathf.PerlinNoise(x * scale, y * scale);
            n = Mathf.Lerp(0.5f, n, contrast); // pull toward mid for subtlety
            tex.SetPixel(x, y, new Color(n, n, n, 1f));
        }
        FinalizeTex(tex);
        return tex;
    }

    // Checker/grid (great for Center Park); tilesX/Y = how many squares across the image
    public static Texture2D MakeChecker(int size = 512, int tilesX = 16, int tilesY = 16, Color a = default, Color b = default)
    {
        if (a == default) a = new Color(0.92f, 0.92f, 0.92f);
        if (b == default) b = new Color(0.82f, 0.82f, 0.82f);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        tex.name = "CheckerTex";
        int pxX = size / tilesX;
        int pxY = size / tilesY;

        for (int y=0; y<size; y++)
        for (int x=0; x<size; x++)
        {
            int cx = x / pxX;
            int cy = y / pxY;
            bool odd = ((cx + cy) & 1) == 1;
            tex.SetPixel(x, y, odd ? a : b);
        }
        FinalizeTex(tex);
        return tex;
    }

    static void FinalizeTex(Texture2D tex)
    {
        tex.wrapMode   = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.Apply();
    }

    // --- APPLY/TILING ---

    // tilesInMeters = how big one texture "tile" should feel in world units (meters)
    public static void ApplyTiled(Material mat, Texture2D tex, Vector2 planeSizeMeters, float tilesInMeters = 2f)
    {
        Vector2 scale = new Vector2(
            Mathf.Max(1f, planeSizeMeters.x / tilesInMeters),
            Mathf.Max(1f, planeSizeMeters.y / tilesInMeters)
        );

        // Assign texture + scale for URP/HDRP or Built-in
        if (mat && tex)
        {
            if (mat.HasProperty(BaseMap))
            {
                mat.SetTexture(BaseMap, tex);
                mat.SetTextureScale(BaseMap, scale);
            }
            else if (mat.HasProperty(MainTex))
            {
                mat.SetTexture(MainTex, tex);
                mat.mainTextureScale = scale;
            }
        }
    }
}
