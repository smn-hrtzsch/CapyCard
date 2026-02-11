using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CSharpMath.Atom;
using CSharpMath.SkiaSharp;
using SkiaSharp;

namespace CapyCard.Services
{
    /// <summary>
    /// Renders strict LaTeX formulas to bitmaps.
    /// Uses supersampling for sharper output in UI text flow.
    /// </summary>
    public static class FormulaRenderingService
    {
        private const int MaxCacheEntries = 256;
        private const float InlineFontSize = 15f;
        private const float BlockFontSize = 18f;
        private const float InlineRenderScale = 2.0f;
        private const float BlockRenderScale = 2.0f;
        private const float InlineCanvasWidth = 4096f;
        private const float BlockCanvasWidth = 2200f;

        private static readonly object CacheLock = new();
        private static readonly Dictionary<CacheKey, FormulaRenderResult> Cache = new();
        private static readonly LinkedList<CacheKey> CacheOrder = new();
        private static readonly Dictionary<CacheKey, LinkedListNode<CacheKey>> CacheNodes = new();

        public static FormulaRenderResult? RenderFormula(string latex, bool isInline, Color textColor)
        {
            if (string.IsNullOrWhiteSpace(latex))
            {
                return null;
            }

            var normalizedLatex = latex.Trim();
            var scale = isInline ? InlineRenderScale : BlockRenderScale;
            var key = new CacheKey(normalizedLatex, isInline, GetColorValue(textColor), scale);

            lock (CacheLock)
            {
                if (Cache.TryGetValue(key, out var cached))
                {
                    TouchEntry(key);
                    return cached;
                }
            }

            var rendered = RenderCore(normalizedLatex, isInline, textColor, scale);
            if (rendered == null)
            {
                return null;
            }

            lock (CacheLock)
            {
                if (Cache.TryGetValue(key, out var alreadyCached))
                {
                    TouchEntry(key);
                    return alreadyCached;
                }

                Cache[key] = rendered;
                CacheNodes[key] = CacheOrder.AddFirst(key);
                EvictOldEntriesIfNeeded();
            }

            return rendered;
        }

        private static FormulaRenderResult? RenderCore(string latex, bool isInline, Color textColor, float renderScale)
        {
            try
            {
                var painter = new MathPainter
                {
                    LaTeX = latex,
                    FontSize = (isInline ? InlineFontSize : BlockFontSize) * renderScale,
                    LineStyle = isInline ? LineStyle.Text : LineStyle.Display,
                    TextColor = new SKColor(textColor.R, textColor.G, textColor.B, textColor.A),
                    DisplayErrorInline = true,
                    AntiAlias = true
                };

                using var stream = painter.DrawAsStream(
                    (isInline ? InlineCanvasWidth : BlockCanvasWidth) * renderScale,
                    CSharpMath.Rendering.FrontEnd.TextAlignment.TopLeft,
                    SKEncodedImageFormat.Png,
                    100);

                if (stream == null)
                {
                    return null;
                }

                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                var rawPng = memoryStream.ToArray();
                var croppedPng = CropTransparentBorders(rawPng, isInline);

                using var bitmapStream = new MemoryStream(croppedPng);
                var bitmap = new Bitmap(bitmapStream);

                var width = Math.Max(1d, bitmap.PixelSize.Width / renderScale);
                var height = Math.Max(1d, bitmap.PixelSize.Height / renderScale);

                return new FormulaRenderResult(bitmap, width, height);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] CropTransparentBorders(byte[] pngBytes, bool isInline)
        {
            using var sourceBitmap = SKBitmap.Decode(pngBytes);
            if (sourceBitmap == null)
            {
                return pngBytes;
            }

            var minX = sourceBitmap.Width;
            var minY = sourceBitmap.Height;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < sourceBitmap.Height; y++)
            {
                for (var x = 0; x < sourceBitmap.Width; x++)
                {
                    if (sourceBitmap.GetPixel(x, y).Alpha == 0)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return pngBytes;
            }

            var padding = isInline ? 5 : 6;
            minX = Math.Max(0, minX - padding);
            minY = Math.Max(0, minY - padding);
            maxX = Math.Min(sourceBitmap.Width - 1, maxX + padding);
            maxY = Math.Min(sourceBitmap.Height - 1, maxY + padding);

            var cropRect = new SKRectI(minX, minY, maxX + 1, maxY + 1);
            var targetWidth = cropRect.Width;
            var targetHeight = cropRect.Height;

            if (targetWidth <= 0 || targetHeight <= 0)
            {
                return pngBytes;
            }

            using var croppedBitmap = new SKBitmap(targetWidth, targetHeight, sourceBitmap.ColorType, sourceBitmap.AlphaType);
            using (var canvas = new SKCanvas(croppedBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(sourceBitmap, cropRect, new SKRect(0, 0, targetWidth, targetHeight));
                canvas.Flush();
            }

            using var image = SKImage.FromBitmap(croppedBitmap);
            using var encodedData = image.Encode(SKEncodedImageFormat.Png, 100);
            return encodedData?.ToArray() ?? pngBytes;
        }

        private static void TouchEntry(CacheKey key)
        {
            if (!CacheNodes.TryGetValue(key, out var node))
            {
                return;
            }

            CacheOrder.Remove(node);
            CacheNodes[key] = CacheOrder.AddFirst(key);
        }

        private static void EvictOldEntriesIfNeeded()
        {
            while (Cache.Count > MaxCacheEntries)
            {
                var oldest = CacheOrder.Last;
                if (oldest == null)
                {
                    break;
                }

                CacheOrder.RemoveLast();
                CacheNodes.Remove(oldest.Value);
                Cache.Remove(oldest.Value);
            }
        }

        private static uint GetColorValue(Color color)
        {
            return ((uint)color.A << 24) |
                   ((uint)color.R << 16) |
                   ((uint)color.G << 8) |
                   color.B;
        }

        public sealed record FormulaRenderResult(Bitmap Bitmap, double Width, double Height);

        private readonly record struct CacheKey(string Latex, bool IsInline, uint ColorValue, float RenderScale);
    }
}
