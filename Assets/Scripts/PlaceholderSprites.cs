using UnityEngine;

/// <summary>
/// Procedurally generates placeholder art at runtime so this slice needs no
/// imported textures. Every sprite is plain white; visual states are applied
/// by tinting SpriteRenderer.color, not by baking color into the texture.
/// Results are cached per call arguments — rebuilt only when the requested
/// size actually changes (e.g. a different tileWidth), not on every call.
/// </summary>
public static class PlaceholderSprites
{
    private static Sprite _diamondTile;
    private static float _diamondTileWidth;

    private static Sprite _unitToken;
    private static float _unitTokenWidth;

    /// <summary>A 2:1 diamond tile sprite, sized so it is exactly `tileWidth` world units wide.</summary>
    public static Sprite DiamondTile(float tileWidth = 1f, int pixelWidth = 128)
    {
        if (_diamondTile != null && _diamondTileWidth == tileWidth) return _diamondTile;

        int w = pixelWidth;
        int h = pixelWidth / 2;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color fill = Color.white;
        Color rim = new Color(0.55f, 0.55f, 0.55f, 1f);
        Color clear = new Color(0, 0, 0, 0);

        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                // Normalize to [-1, 1] on both axes, diamond = |u| + |v| <= 1.
                float u = (px + 0.5f) / w * 2f - 1f;
                float v = (py + 0.5f) / h * 2f - 1f;
                float d = Mathf.Abs(u) + Mathf.Abs(v);

                Color c;
                if (d > 1f) c = clear;
                else if (d > 0.92f) c = rim;
                else c = fill;

                tex.SetPixel(px, py, c);
            }
        }
        tex.Apply();

        // pixelsPerUnit chosen so the sprite renders exactly tileWidth world units wide.
        float pixelsPerUnit = w / tileWidth;
        _diamondTile = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        _diamondTile.name = "PlaceholderDiamondTile";
        _diamondTileWidth = tileWidth;
        return _diamondTile;
    }

    /// <summary>A simple circular token to represent a unit, sized to `tileWidth` world units and pivoted near its base.</summary>
    public static Sprite UnitToken(float tileWidth = 1f, int pixels = 64)
    {
        if (_unitToken != null && _unitTokenWidth == tileWidth) return _unitToken;

        int size = pixels;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color fill = Color.white;
        Color rim = new Color(0.25f, 0.25f, 0.25f, 1f);
        Color clear = new Color(0, 0, 0, 0);
        Vector2 center = new Vector2(size * 0.5f, size * 0.48f);
        float radius = size * 0.42f;

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float dist = Vector2.Distance(new Vector2(px + 0.5f, py + 0.5f), center);
                Color c;
                if (dist > radius) c = clear;
                else if (dist > radius - 2.5f) c = rim;
                else c = fill;
                tex.SetPixel(px, py, c);
            }
        }
        tex.Apply();

        // Pivot slightly below center so the token appears to sit "on" the tile
        // rather than floating at the tile's exact center.
        // pixelsPerUnit chosen so the sprite renders exactly tileWidth world units wide,
        // matching DiamondTile so both scale together when tileWidth changes.
        float pixelsPerUnit = size / tileWidth;
        _unitToken = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.42f), pixelsPerUnit);
        _unitToken.name = "PlaceholderUnitToken";
        _unitTokenWidth = tileWidth;
        return _unitToken;
    }
}
