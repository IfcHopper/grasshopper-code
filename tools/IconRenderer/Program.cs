using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using Svg;

// Renders SVG icons to PNGs (24x24 by default) next to the source file and applies the Grasshopper drop shadow
// (+1px right, +1px down, 2px blur, black ~30%), see docs/ICONS.md.
// Usage: IconRenderer [--no-shadow] [--preview] [--size N] <file.svg | directory>...
const int DefaultSize = 24;
const float ShadowOpacity = 75f / 255f;

var shadow = !args.Contains("--no-shadow");
var preview = args.Contains("--preview");
var sizeIndex = Array.IndexOf(args, "--size");
var size = sizeIndex >= 0 && sizeIndex + 1 < args.Length ? int.Parse(args[sizeIndex + 1]) : DefaultSize;
var inputs = args.Where((a, i) => !a.StartsWith("--") && (sizeIndex < 0 || i != sizeIndex + 1))
    .SelectMany(a => Directory.Exists(a) ? Directory.GetFiles(a, "*.svg") : new[] { a })
    .ToList();
if (inputs.Count == 0)
{
    Console.Error.WriteLine("Usage: IconRenderer [--no-shadow] [--preview] [--size N] <file.svg | directory>...");
    return 1;
}

foreach (var svgPath in inputs)
{
    using var icon = SvgDocument.Open(svgPath).Draw(size, size);
    using var result = shadow ? AddDropShadow(icon) : new Bitmap(icon);
    var pngPath = Path.ChangeExtension(svgPath, ".png");
    result.Save(pngPath, ImageFormat.Png);
    Console.WriteLine($"{pngPath} {Bounds(icon)}");
    if (preview) Console.WriteLine(Preview(result));
}
return 0;

static Bitmap AddDropShadow(Bitmap icon)
{
    int size = icon.Width;
    // Shadow alpha = icon alpha shifted by (1,1), blurred with a 5-tap Gaussian (sigma 1).
    var kernel = new[] { 0.054f, 0.244f, 0.403f, 0.244f, 0.054f };
    var alpha = new float[size, size];
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            if (x >= 1 && y >= 1) alpha[x, y] = icon.GetPixel(x - 1, y - 1).A / 255f;

    var blurred = Convolve(Convolve(alpha, kernel, true), kernel, false);
    var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var src = icon.GetPixel(x, y);
            float sa = src.A / 255f, da = blurred[x, y] * ShadowOpacity;
            float oa = sa + da * (1 - sa);
            if (oa <= 0) { result.SetPixel(x, y, Color.Transparent); continue; }
            // Source over a black shadow.
            int Channel(int c) => (int)Math.Round(c * sa / oa);
            result.SetPixel(x, y, Color.FromArgb((int)Math.Round(oa * 255), Channel(src.R), Channel(src.G), Channel(src.B)));
        }
    return result;
}

static float[,] Convolve(float[,] input, float[] kernel, bool horizontal)
{
    int size = input.GetLength(0);
    var output = new float[size, size];
    int r = kernel.Length / 2;
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float sum = 0;
            for (int k = -r; k <= r; k++)
            {
                int sx = horizontal ? x + k : x, sy = horizontal ? y : y + k;
                if (sx >= 0 && sx < size && sy >= 0 && sy < size) sum += input[sx, sy] * kernel[k + r];
            }
            output[x, y] = sum;
        }
    return output;
}

static string Bounds(Bitmap icon)
{
    int size = icon.Width;
    var points = new List<Point>();
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            if (icon.GetPixel(x, y).A > 0) points.Add(new Point(x, y));
    if (points.Count == 0) return "empty";
    return $"artwork x {points.Min(p => p.X)}-{points.Max(p => p.X)}, y {points.Min(p => p.Y)}-{points.Max(p => p.Y)}";
}

// Text-only preview: transparent ' ', otherwise a character by luminance (dark '@' to light '.').
static string Preview(Bitmap bitmap)
{
    int size = bitmap.Width;
    const string ramp = "@%#*+=-:.";
    var sb = new StringBuilder();
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            var c = bitmap.GetPixel(x, y);
            if (c.A < 40) { sb.Append("  "); continue; }
            var lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            var ch = ramp[Math.Min(ramp.Length - 1, (int)(lum * ramp.Length))];
            sb.Append(ch).Append(ch);
        }
        sb.AppendLine();
    }
    return sb.ToString();
}
