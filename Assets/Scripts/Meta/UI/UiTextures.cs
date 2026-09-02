using UnityEngine;

namespace BattleRunner.Meta.UI
{
    /// <summary>
    /// Sprites generated in code, so the UI can stop being flat rectangles without
    /// importing a single image.
    ///
    /// The shape lives in the ALPHA channel and the RGB carries only a vertical bevel
    /// gradient. That matters: screens tint these images to say what a widget means — a
    /// taken talent is gold, a locked one is dark — and a sprite with colour baked into
    /// its RGB would multiply against that tint and turn every state muddy. White-ish RGB
    /// means the tint a screen sets is the colour the player sees.
    ///
    /// Frames are a separate sprite drawn over the fill for the same reason: the frame is
    /// always bronze regardless of what the fill underneath is saying.
    /// </summary>
    public static class UiTextures
    {
        private const int Size = 48;
        private const int Slice = 15;      // 9-slice border, comfortably outside the radius
        private const float Radius = 11f;
        private const float FrameWidth = 3f;

        private static Sprite _fill;
        private static Sprite _frame;
        private static Sprite _backdrop;
        private static Sprite _glow;

        /// <summary>Rounded panel body. Tint it; the sprite only carries shape and bevel.</summary>
        public static Sprite Fill => _fill != null ? _fill : _fill = BuildFill();

        /// <summary>Bronze border with corner notches. Draw over a Fill, leave it white.</summary>
        public static Sprite Frame => _frame != null ? _frame : _frame = BuildFrame();

        /// <summary>Full-screen vertical gradient — a flat backdrop reads as a blank app.</summary>
        public static Sprite Backdrop => _backdrop != null ? _backdrop : _backdrop = BuildBackdrop();

        /// <summary>Soft radial falloff, for putting light behind a thing.</summary>
        public static Sprite Glow => _glow != null ? _glow : _glow = BuildGlow();

        // Signed distance to a rounded rectangle: negative inside, positive outside.
        private static float RoundedRectDistance(float x, float y, float halfW, float halfH, float radius)
        {
            float dx = Mathf.Abs(x) - (halfW - radius);
            float dy = Mathf.Abs(y) - (halfH - radius);
            float outsideX = Mathf.Max(dx, 0f);
            float outsideY = Mathf.Max(dy, 0f);
            float outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }

        private static Sprite Sliced(Texture2D texture, float border)
        {
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        private static Sprite BuildFill()
        {
            Texture2D texture = NewTexture(Size, Size);
            float half = Size * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = RoundedRectDistance(x + 0.5f - half, y + 0.5f - half,
                        half, half, Radius);
                    // One pixel of falloff, so a scaled-up corner is not a staircase.
                    float alpha = Mathf.Clamp01(0.5f - distance);

                    // Lit from above: the top of a panel is brighter than its bottom, which
                    // is the whole of what makes a flat rectangle look like a raised surface.
                    float bevel = Mathf.Lerp(0.74f, 1.06f, y / (float)(Size - 1));
                    float value = Mathf.Clamp01(bevel);
                    texture.SetPixel(x, y, new Color(value, value, value, alpha));
                }
            }

            return Sliced(texture, Slice);
        }

        private static Sprite BuildFrame()
        {
            Texture2D texture = NewTexture(Size, Size);
            float half = Size * 0.5f;
            var bronze = new Color(0.62f, 0.47f, 0.21f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float distance = RoundedRectDistance(px, py, half, half, Radius);

                    // A ring hugging the inside of the rounded edge.
                    float outer = Mathf.Clamp01(0.5f - distance);
                    float inner = Mathf.Clamp01(0.5f - (distance + FrameWidth));
                    float alpha = Mathf.Clamp01(outer - inner);

                    // Corner notches: a small diamond tucked into each corner, inside the
                    // 9-slice corner region so it never stretches with the widget.
                    float cornerX = Mathf.Abs(px) - (half - Slice * 0.62f);
                    float cornerY = Mathf.Abs(py) - (half - Slice * 0.62f);
                    float diamond = Mathf.Abs(cornerX) + Mathf.Abs(cornerY) - 3.2f;
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(0.5f - diamond) * 0.9f);

                    texture.SetPixel(x, y, new Color(bronze.r, bronze.g, bronze.b, alpha));
                }
            }

            return Sliced(texture, Slice);
        }

        private static Sprite BuildBackdrop()
        {
            const int height = 96;
            Texture2D texture = NewTexture(2, height);

            var top = new Color(0.055f, 0.048f, 0.080f);
            var bottom = new Color(0.105f, 0.070f, 0.085f);   // a warm ember cast, like the sky

            for (int y = 0; y < height; y++)
            {
                // Eased so the warm half stays low in the frame instead of climbing the middle.
                float t = y / (float)(height - 1);
                Color row = Color.Lerp(bottom, top, t * t * (3f - 2f * t));
                texture.SetPixel(0, y, row);
                texture.SetPixel(1, y, row);
            }

            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildGlow()
        {
            const int size = 64;
            Texture2D texture = NewTexture(size, size);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, radial * radial));
                }
            }

            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
