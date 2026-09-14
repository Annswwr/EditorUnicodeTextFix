using UnityEditor;

namespace EditorUnicodeTextFix.Editor
{
    internal static class EditorUnicodeTextFixMenu
    {
        private const string MenuPath = "Tools/EditorUnicodeTextFix/Enabled";
        private const string CompatibilityRefreshMenuPath = "Tools/EditorUnicodeTextFix/Compatibility Refresh";
        private const string DebugMessagesMenuPath = "Tools/EditorUnicodeTextFix/Debug Messages";

        private const string EnabledKey = "EditorUnicodeTextFix.Enabled";
        private const string CompatibilityRefreshKey = "EditorUnicodeTextFix.CompatibilityRefresh";
        private const string DebugMessagesKey = "EditorUnicodeTextFix.DebugMessages";

        internal static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, true);
        internal static bool IsCompatibilityRefreshEnabled => EditorPrefs.GetBool(CompatibilityRefreshKey, false);
        internal static bool IsDebugMessagesEnabled => EditorPrefs.GetBool(DebugMessagesKey, false);
        
        [MenuItem(MenuPath, false, 1)]
        private static void ToggleEnabled()
        {
            var enabled = !IsEnabled;
            EditorPrefs.SetBool(EnabledKey, enabled);

            if (enabled)
            {
                EditorComplexTextLayout.Enable();
            }
            else
            {
                EditorFontFallbackInjector.Disable();
                EditorComplexTextLayout.Disable();
            }

            EditorUtility.RequestScriptReload();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }

        [MenuItem(CompatibilityRefreshMenuPath, false, 2)]
        private static void ToggleCompatibilityRefresh() => EditorPrefs.SetBool(CompatibilityRefreshKey, !IsCompatibilityRefreshEnabled);

        [MenuItem(CompatibilityRefreshMenuPath, true)]
        private static bool ToggleCompatibilityRefreshValidate()
        {
            Menu.SetChecked(CompatibilityRefreshMenuPath, IsCompatibilityRefreshEnabled);
            return true;
        }

        [MenuItem(DebugMessagesMenuPath, false, 3)]
        private static void ToggleDebugMessages() => EditorPrefs.SetBool(DebugMessagesKey, !IsDebugMessagesEnabled);

        [MenuItem(DebugMessagesMenuPath, true)]
        private static bool ToggleDebugMessagesValidate()
        {
            Menu.SetChecked(DebugMessagesMenuPath, IsDebugMessagesEnabled);
            return true;
        }
    }
}
