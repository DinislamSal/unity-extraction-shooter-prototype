using OfflineExtraction.Core;
using UnityEngine;

namespace OfflineExtraction.Raid
{
    internal static class RaidBodyFigure
    {
        private static Texture2D body, head, chest, abdomen, rightArm, leftArm, rightLeg, leftLeg;

        public static void Initialize()
        {
            if (body != null) return;
            Texture2D source = Resources.Load<Texture2D>("UI/body_health_dynamic") ?? Resources.Load<Texture2D>("UI/body_health");
            if (source == null) return;
            body = ExtractBody(source);
            // Контуры заданы по самой анатомической модели. Голова не включает
            // шею, а грудь не захватывает руки — соседние повреждения не сливаются.
            head = Mask(new[] { P(126,14),P(133,7),P(157,7),P(164,14),P(166,38),P(159,55),P(145,61),P(131,55),P(124,38) });
            chest = Mask(new[] { P(111,86),P(127,79),P(145,84),P(163,79),P(179,86),P(190,101),P(187,136),P(174,160),P(145,174),P(116,160),P(103,136),P(100,101) });
            abdomen = Mask(new[] { P(112,165),P(178,165),P(184,198),P(176,230),P(160,248),P(145,254),P(130,248),P(114,230),P(106,198) });
            rightArm = Mask(new[] { P(101,78),P(112,91),P(108,118),P(104,145),P(98,174),P(87,203),P(75,231),P(63,247),P(51,252),P(38,244),P(31,231),P(35,216),P(50,202),P(61,174),P(69,153),P(70,118),P(76,91),P(76,78) });
            leftArm = Mask(new[] { P(189,78),P(178,91),P(182,118),P(186,145),P(192,174),P(203,203),P(215,231),P(227,247),P(239,252),P(252,244),P(259,231),P(255,216),P(240,202),P(229,174),P(221,153),P(220,118),P(214,91),P(214,78) });
            rightLeg = Mask(new[] { P(106,205),P(124,211),P(145,218),P(143,260),P(138,300),P(127,330),P(124,363),P(119,401),P(116,438),P(105,449),P(91,444),P(94,410),P(98,380),P(92,350),P(96,315),P(98,275),P(96,238) });
            leftLeg = Mask(new[] { P(184,205),P(166,211),P(145,218),P(147,260),P(152,300),P(163,330),P(166,363),P(171,401),P(174,438),P(185,449),P(199,444),P(196,410),P(192,380),P(198,350),P(194,315),P(192,275),P(194,238) });
        }

        public static void Draw(Rect rect, PlayerVitals v)
        {
            Initialize();
            if (body == null || v == null) return;
            GUI.DrawTexture(rect, body, ScaleMode.ScaleToFit, true);
            Tint(rect, head, v.head, 35); Tint(rect, chest, v.chest, 85); Tint(rect, abdomen, v.abdomen, 70);
            Tint(rect, rightArm, v.rightArm, 60); Tint(rect, leftArm, v.leftArm, 60); Tint(rect, rightLeg, v.rightLeg, 65); Tint(rect, leftLeg, v.leftLeg, 65);
        }

        private static void Tint(Rect rect, Texture2D mask, int value, int maximum)
        {
            float ratio = maximum <= 0 ? 0f : Mathf.Clamp01(value / (float)maximum);
            if (ratio >= .8f || mask == null) return;
            Color old = GUI.color;
            GUI.color = value <= 0 ? new Color(0f,0f,0f,.92f) : ratio > .3f ? new Color(1f,.72f,.05f,.58f) : new Color(1f,.08f,.04f,.68f);
            GUI.DrawTexture(rect, mask, ScaleMode.ScaleToFit, true); GUI.color = old;
        }

        private static Vector2 P(float x, float y) => new(x, y);

        private static Texture2D Mask(Vector2[] polygon)
        {
            // Маска создаётся в точном разрешении базовой картинки. Поэтому при
            // любом размере UI она масштабируется пиксель-в-пиксель с силуэтом.
            int width = body.width, height = body.height;
            const float referenceWidth = 290f, referenceHeight = 455f;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color32[] pixels = new Color32[width * height];
            float fittedWidth = referenceHeight * width / (float)height;
            float horizontalMargin = (referenceWidth - fittedWidth) * .5f;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                // В эталонной сетке 290x455 исходная узкая фигура расположена
                // по центру с боковыми полями. Учитываем их до проверки контура.
                float polygonX = horizontalMargin + (x + .5f) / width * fittedWidth;
                float polygonY = (height - y - .5f) / height * referenceHeight;
                bool inside = false;
                for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                {
                    Vector2 a = polygon[i], b = polygon[j];
                    if ((a.y > polygonY) != (b.y > polygonY) &&
                        polygonX < (b.x - a.x) * (polygonY - a.y) / (b.y - a.y) + a.x) inside = !inside;
                }
                // Берём исходную прозрачность, а не грубую геометрическую заливку:
                // цвет физически не может выйти за внешний контур тела.
                byte alpha = inside ? (byte)Mathf.RoundToInt(body.GetPixel(x, y).a * 255f) : (byte)0;
                pixels[y * width + x] = new Color32(255,255,255,alpha);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        private static Texture2D ExtractBody(Texture2D source)
        {
            Color32[] input = source.GetPixels32(), output = new Color32[input.Length];
            Texture2D result = new(source.width, source.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int i = 0; i < input.Length; i++)
            {
                Color32 p = input[i]; float min = Mathf.Min(p.r, Mathf.Min(p.g, p.b)) / 255f, max = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) / 255f;
                output[i] = new Color32(p.r,p.g,p.b,min > .88f && max - min < .09f ? (byte)0 : (byte)255);
            }
            result.SetPixels32(output); result.Apply(false, false); return result;
        }
    }
}
