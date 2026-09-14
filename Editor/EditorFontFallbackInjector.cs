using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using TextElement = UnityEngine.UIElements.TextElement;

namespace EditorUnicodeTextFix.Editor
{
    /// <summary>
    /// Adds compatible system fonts found using <see cref="EditorFontFallbackDiscovery"/> as an Editor fallback.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorFontFallbackInjector
    {
        static EditorFontFallbackInjector()
        {
            if (EditorUnicodeTextFixMenu.IsEnabled)
                Enable();
        }

        private static string GlobalFallbackAssetPath => $"UIPackageResources/FontAssets/DynamicOSFontAssets/GlobalFallback/GlobalFallback - {PlatformSuffix}.asset";
        private static string PlatformSuffix => Application.platform switch
        {
            RuntimePlatform.WindowsEditor => "Win",
            RuntimePlatform.OSXEditor => "OSX",
            RuntimePlatform.LinuxEditor => "Linux",
            _ => "Win",
        };
        private const string IsInitializedKey = "EditorUnicodeTextFix.IsInitialized";
        private const string FallbackFontsCacheKey = "EditorUnicodeTextFix.FallbackFonts";

        private const int MaxRetries = 10;
        private static int attempts;
        private static readonly List<FontAsset> InjectedAssets = new();
        
        private static void Enable()
        {
            attempts = 0;
            EditorApplication.delayCall += Initialize;
        }

        internal static void Disable()
        {
            var settingsRemoved = 0;
            if (DefaultTextSettings.Value?.GetValue(null) is TextSettings { fallbackFontAssets: not null } settings)
            {
                var fallbacks = settings.fallbackFontAssets;
                settingsRemoved = fallbacks.RemoveAll(f => f == null || InjectedAssets.Contains(f));
                settings.fallbackFontAssets = fallbacks;
            }

            var globalRemoved = 0;
            if (EditorGUIUtility.Load(GlobalFallbackAssetPath) is FontAsset { fallbackFontAssetTable: not null } global)
            {
                var table = global.fallbackFontAssetTable;
                globalRemoved = table.RemoveAll(f => f == null || InjectedAssets.Contains(f));
                global.fallbackFontAssetTable = table;
            }
            
            Log($"Removed {settingsRemoved} from TextSettings.fallbackFontAssets, {globalRemoved} from GlobalFallback.fallbackFontAssetTable.");

            InjectedAssets.Clear();
            ClearTextHandleCache();
            MarkElementsDirty();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void Initialize()
        {
            if (!EditorUnicodeTextFixMenu.IsEnabled) return;

            attempts++;
            try
            {
                var isInitialized = SessionState.GetBool(IsInitializedKey, false);
                if (!isInitialized)
                {
                    var result = EditorFontFallbackDiscovery.Scan();
                    Log($"Initialization scanned {result.installedFontCount} installed fonts, {result.loadedFontCount} loaded, Editor font covers {result.editorCoveredBlockCount} block(s), found {result.fonts.Length} fallback candidate(s).");

                    if (result.fonts.Length == 0)
                    {
                        Debug.LogWarning($"{nameof(EditorFontFallbackInjector)}: Attempt {attempts} found 0 candidates - retrying.");
                        Retry();
                        return;
                    }

                    SessionState.SetString(FallbackFontsCacheKey, string.Join(";", result.fonts));
                    SessionState.SetBool(IsInitializedKey, true);
                    EditorApplication.delayCall += Initialize;
                    return;
                }

                // Reinitialize font engine (fixes issues upon startup)
                FontEngine.DestroyFontEngine();
                FontEngine.InitializeFontEngine();

                var cached = SessionState.GetString(FallbackFontsCacheKey, "");
                var families = cached.Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (families.Length == 0)
                {
                    Debug.LogWarning($"{nameof(EditorFontFallbackInjector)}: Attempt {attempts} found a cached value with 0 families (\"{cached}\", length {cached.Length}) - clearing it and retrying.");
                    SessionState.EraseString(FallbackFontsCacheKey);
                    SessionState.EraseBool(IsInitializedKey);
                    Retry();
                    return;
                }

                var succeeded = families.Count(AddFallbackFont);
                Log($"Attempt {attempts} - {families.Length} candidate(s), added {succeeded}.");
                if (succeeded < families.Length)
                {
                    Retry();
                    return;
                }

                ClearTextHandleCache();
                MarkElementsDirty();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                Log($"Fallback fonts active [{string.Join(", ", families)}].");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(EditorFontFallbackInjector)}: Attempt {attempts} threw ({e}). Retrying.");
                Retry();
            }
        }

        private static void Log(string message)
        {
            if (EditorUnicodeTextFixMenu.IsDebugMessagesEnabled)
                Debug.Log($"{nameof(EditorFontFallbackInjector)}: {message}");
        }

        private static void ClearTextHandleCache()
        {
            try
            {
                EmptyTextHandleCache.Value?.Invoke(null, null);
            }
            catch
            {
                // ignored
            }
        }

        private static void MarkElementsDirty()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var root = window.rootVisualElement;
                if (root?.panel == null) continue;
                root.Query<TextElement>().ForEach(te => te.MarkDirtyText());
            }
        }

        private static void Retry()
        {
            if (attempts < MaxRetries)
                EditorApplication.delayCall += Initialize;
            else
                Debug.LogWarning($"{nameof(EditorFontFallbackInjector)}: Giving up after {attempts} initialization attempts.");
        }

        private static bool AddFallbackFont(string font)
        {
            var asset = FontAsset.CreateFontAsset(font, "Regular");
            if (asset == null) return false;
            SetHideFlags(asset);

            var added = false;

            if (DefaultTextSettings.Value?.GetValue(null) is TextSettings settings)
            {
                var fallbacks = settings.fallbackFontAssets ?? new List<FontAsset>();
                fallbacks.RemoveAll(f => f == null || f.name == asset.name);
                fallbacks.Add(asset);
                settings.fallbackFontAssets = fallbacks;
                added = true;
            }

            if (EditorGUIUtility.Load(GlobalFallbackAssetPath) is FontAsset global)
            {
                var table = global.fallbackFontAssetTable ?? new List<FontAsset>();
                table.RemoveAll(f => f == null || f.name == asset.name);
                table.Add(asset);
                global.fallbackFontAssetTable = table;
                added = true;
            }

            if (!added)
            {
                Debug.LogWarning($"{nameof(EditorFontFallbackInjector)}: Could not reach Editor text settings.");
                return false;
            }

            InjectedAssets.Add(asset);
            return true;
        }

        private static void SetHideFlags(FontAsset asset)
        {
            asset.hideFlags = HideFlags.HideAndDontSave;
        
            foreach (var atlas in asset.atlasTextures)
            {
                if (atlas != null)
                    atlas.hideFlags = HideFlags.HideAndDontSave;
            }
        
            if (asset.material != null)
                asset.material.hideFlags = HideFlags.HideAndDontSave;
        }

        private static readonly Lazy<PropertyInfo> DefaultTextSettings = new(() => Type.GetType("UnityEditor.EditorTextSettings, UnityEditor.CoreModule")
            ?.GetProperty("defaultTextSettings", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

        private static readonly Lazy<MethodInfo> EmptyTextHandleCache = new(() => Type.GetType("UnityEngine.IMGUITextHandle, UnityEngine.IMGUIModule")
            ?.GetMethod("EmptyCache", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
    }
}
