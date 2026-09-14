using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using TextElement = UnityEngine.UIElements.TextElement;

namespace EditorUnicodeTextFix.Editor
{
    /// <summary>
    /// Fixes text shaping for various complex scripts / languages
    /// Ensures Unity's Advanced text generator is used instead of Standard
    /// </summary>
    [InitializeOnLoad]
    public static class EditorComplexTextLayout
    {
        static EditorComplexTextLayout()
        {
            if (EditorUnicodeTextFixMenu.IsEnabled)
                Enable();
        }

        private static readonly HashSet<VisualElement> AttachedRoots = new();
        private static double nextRefreshTime;

        internal static void Enable()
        {
            EditorApplication.update += OnUpdate;
            AttachToEditorWindows();
        }

        internal static void Disable()
        {
            EditorApplication.update -= OnUpdate;

            foreach (var root in AttachedRoots.Where(root => root != null))
            {
                root.UnregisterCallback<AttachToPanelEvent>(OnElementAttached);
                root.UnregisterCallback<DetachFromPanelEvent>(OnRootDetached);
                if (root.panel != null)
                    root.Query<TextElement>().ForEach(RevertShapingOnInputField);
            }

            AttachedRoots.Clear();
        }
        
        private static void OnUpdate()
        {
            if (!EditorUnicodeTextFixMenu.IsEnabled)
                return;
                
            AttachToEditorWindows();

            // Refresh at a fixed interval (fixes issues with certain custom inspectors)
            if (EditorUnicodeTextFixMenu.IsCompatibilityRefreshEnabled)
            {
                if (EditorApplication.timeSinceStartup < nextRefreshTime)
                    return;
                
                // Fixed interval instead of running every tick
                nextRefreshTime = EditorApplication.timeSinceStartup + 0.5f;
                RefreshAttachedWindows();
            }
        }

        private static void AttachToEditorWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var root = window.rootVisualElement;
                if (root?.panel == null || !AttachedRoots.Add(root))
                    continue;

                root.RegisterCallback<AttachToPanelEvent>(OnElementAttached);
                root.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
                root.Query<TextElement>().ForEach(FixShapingOnInputField);
            }
        }

        private static void RefreshAttachedWindows()
        {
            foreach (var root in AttachedRoots.Where(root => root?.panel != null))
                root.Query<TextElement>().ForEach(FixShapingOnInputField);
        }

        private static void OnRootDetached(DetachFromPanelEvent @event)
        {
            if (@event.currentTarget is VisualElement root)
                AttachedRoots.Remove(root);
        }

        private static void OnElementAttached(AttachToPanelEvent @event)
        {
            if (@event.target is TextElement textElement)
                FixShapingOnInputField(textElement);
        }

        private static void FixShapingOnInputField(TextElement textElement)
        {
            if (!IsInnerInputField(textElement))
                return;

            var inline = textElement.style.unityTextGenerator;
            if (inline is { keyword: StyleKeyword.Undefined, value: TextGeneratorType.Advanced })
                return;

            textElement.style.unityTextGenerator = TextGeneratorType.Advanced;
            textElement.MarkDirtyRepaint();
        }

        private static void RevertShapingOnInputField(TextElement textElement)
        {
            if (!IsInnerInputField(textElement))
                return;

            textElement.style.unityTextGenerator = TextGeneratorType.Standard;
            textElement.MarkDirtyRepaint();
        }

        private static bool IsInnerInputField(TextElement textElement)
            => textElement.GetClasses().Any(cls => cls.StartsWith("unity-text-element--inner-input-field-component", StringComparison.Ordinal));
    }
}
