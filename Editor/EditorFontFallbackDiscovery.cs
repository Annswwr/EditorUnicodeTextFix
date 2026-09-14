using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace EditorUnicodeTextFix.Editor
{
    /// <summary>
    /// Identifies the best system fonts to use for each missing Unicode block that the Unity Editor can't render.
    /// </summary>
    public static class EditorFontFallbackDiscovery
    {
        public readonly struct Result
        {
            public readonly string[] fonts;
            public readonly int installedFontCount;
            public readonly int loadedFontCount;
            public readonly int editorCoveredBlockCount;

            public Result(string[] fonts, int installedFontCount, int loadedFontCount, int editorCoveredBlockCount)
            {
                this.fonts = fonts;
                this.installedFontCount = installedFontCount;
                this.loadedFontCount = loadedFontCount;
                this.editorCoveredBlockCount = editorCoveredBlockCount;
            }
        }
        
        // Mainly covers the Basic Multilingual Plane
        private static readonly (int start, int endExclusive) ScanRange = (0x0080, 0x10000);

        // Ignore icons / dingbats / other incompatible fonts (appears to crash Unity if used as a fallback)
        private static readonly (int start, int endExclusive)[] ExcludedRanges =
        {
            (0xE000, 0xF900),   // Surrogates / Private Use Area
            (0xFFF0, 0x10000),  // Specials
        };

        private static readonly int[] BlockSampleOffsets = Enumerable.Range(0, 16).Select(i => i * 8).ToArray();
        
        public static Result Scan()
        {
            FontEngine.InitializeFontEngine();

            var installedFonts = Font.GetOSInstalledFontNames();
            var coveredByEditor = EditorCoveredBlocks();
            var bestPerBlock = new Dictionary<int, (int hits, int score, int size, string font)>();
            var loadedCount = 0;

            foreach (var font in installedFonts)
            {
                try
                {
                    if (FontEngine.LoadFontFace(font, "Regular") != FontEngineError.Success)
                        continue;

                    // Skips color font faces (crashes Unity if used as fallback)
                    if (IsColorFontFace())
                        continue;
                    
                    loadedCount++;

                    var blockHits = ScanLoadedFace();
                    var score = DetermineScore(font);
                    var size = blockHits.Count;
                    foreach (var (block, hits) in blockHits)
                    {
                        if (coveredByEditor.ContainsKey(block))
                            continue;
                        
                        if (!bestPerBlock.TryGetValue(block, out var current)
                            || hits > current.hits
                            || (hits == current.hits
                            && (score > current.score || (score == current.score && size < current.size))))
                        {
                            bestPerBlock[block] = (hits, score, size, font);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{nameof(EditorFontFallbackDiscovery)}: Failed to check '{font}': {e.Message}");
                }
                finally
                {
                    FontEngine.UnloadFontFace();
                }
            }

            var fonts = bestPerBlock.Values.Select(v => v.font).Distinct().ToArray();
            return new Result(fonts, installedFonts.Length, loadedCount, coveredByEditor.Count);
        }

        private static Dictionary<int, int> EditorCoveredBlocks()
        {
            Font font;
        
            try
            {
                font = EditorStyles.label?.font;
            }
            catch
            {
                // EditorStyles not ready yet (try again)
                return new Dictionary<int, int>();
            }

            if (font == null || FontEngine.LoadFontFace(font) != FontEngineError.Success)
                return new Dictionary<int, int>();

            try
            {
                return ScanLoadedFace();
            }
            finally
            {
                FontEngine.UnloadFontFace();
            }
        }

        private static Dictionary<int, int> ScanLoadedFace()
        {
            var hits = new Dictionary<int, int>();
            for (var start = ScanRange.start; start < ScanRange.endExclusive; start += 128)
            {
                if (IsExcludedBlock(start))
                    continue;

                var count = 0;
                foreach (var offset in BlockSampleOffsets)
                {
                    if (FontEngine.TryGetGlyphIndex((uint)(start + offset), out var glyph) && glyph != 0)
                        count++;
                }

                if (count > 0)
                    hits[start >> 7] = count;
            }

            return hits;
        }

        private static bool IsExcludedBlock(int blockStart)
        {
            foreach (var (start, endExclusive) in ExcludedRanges)
            {
                if (blockStart >= start && blockStart < endExclusive)
                    return true;
            }

            return false;
        }

        private static int DetermineScore(string font)
        {
            var score = 0;

            if (font.StartsWith("Noto Sans", StringComparison.OrdinalIgnoreCase))
                score += 3;
            else if (font.StartsWith("Noto", StringComparison.OrdinalIgnoreCase))
                score += 2;
            if (font.EndsWith(" UI", StringComparison.OrdinalIgnoreCase))
                score += 2;
            if (font.EndsWith(" Text", StringComparison.OrdinalIgnoreCase))
                score += 2;
            if (font.StartsWith("Segoe UI", StringComparison.OrdinalIgnoreCase))
                score += 1;

            return score;
        }

        private static readonly Lazy<MethodInfo> IsColorFontFaceMethod = new(() =>
            typeof(FontEngine).GetMethod("IsColorFontFace", BindingFlags.Static | BindingFlags.NonPublic));

        private static bool IsColorFontFace()
        {
            try
            {
                if (IsColorFontFaceMethod.Value?.Invoke(null, null) is bool isColor)
                    return isColor;
            }
            catch
            {
                // ignored
            }

            return false;
        }
    }
}
