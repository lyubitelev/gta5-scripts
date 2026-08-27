using System;
using System.Drawing;
using System.Linq;
using GTA.UI;

namespace gta.Core
{
    internal static class MenuPanelRenderer
    {
        private const float Padding = 12f;
        private const float LineHeight = 25f;
        private const float MaxWidth = 860f;
        private const float MinWidth = 310f;

        private static readonly Color BackgroundColor = Color.FromArgb(175, 0, 0, 0);
        private static readonly Color TextColor = Color.FromArgb(245, 255, 255, 255);

        public static void Draw(string text, PointF position, float scale)
        {
            var lines = SplitLines(text);
            var panel = new ContainerElement(position, Measure(lines, scale), BackgroundColor, false);

            for (var i = 0; i < lines.Length; i++)
            {
                panel.Items.Add(new TextElement(
                    lines[i],
                    new PointF(Padding, Padding + LineHeight * i),
                    scale,
                    TextColor,
                    GTA.UI.Font.ChaletLondon,
                    Alignment.Left,
                    true,
                    false));
            }

            panel.Draw();
        }

        private static SizeF Measure(string[] lines, float scale)
        {
            var maxLength = lines.Length == 0 ? 0 : lines.Max(line => line.Length);
            var width = Math.Min(MaxWidth, Math.Max(MinWidth, maxLength * scale * 18f + Padding * 2f));
            var height = Math.Max(54f, lines.Length * LineHeight + Padding * 2f);

            return new SizeF(width, height);
        }

        private static string[] SplitLines(string text)
        {
            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
    }
}
