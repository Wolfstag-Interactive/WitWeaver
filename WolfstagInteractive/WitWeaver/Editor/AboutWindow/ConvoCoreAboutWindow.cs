#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreAboutWindow.html")]
    public class ConvoCoreAboutWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Wolfstag Interactive/ConvoCore/About";

        private const string LogoSearchFilter = "t:Texture2D ConvoCoreLogo";
        private const string PackageJsonSearchFilter = "package t:TextAsset";

        private Texture2D _logo;
        private string _version;
        private Vector2 _scroll;
        private static readonly Vector2 MinWindowSize = new Vector2(500f, 550f);

        [MenuItem(MenuPath, priority = 2000)]
        public static void Open()
        {
            var w = GetWindow<ConvoCoreAboutWindow>(true, "About ConvoCore", true);
            w.minSize = MinWindowSize;
            w.ShowUtility();
        }

        private void OnEnable()
        {
            minSize = MinWindowSize;
            _logo = FindLogo();
            _version = ResolveVersion();
        }
        private void EnforceMinSize()
        {
            if (position.width < minSize.x || position.height < minSize.y)
            {
                position = new Rect(
                    position.x,
                    position.y,
                    Mathf.Max(position.width, minSize.x),
                    Mathf.Max(position.height, minSize.y)
                );
            }
        }
        private void OnGUI()
        {
            EnforceMinSize();
            if (_logo == null) _logo = FindLogo();
            if (string.IsNullOrEmpty(_version)) _version = ResolveVersion();

            using (new EditorGUILayout.VerticalScope(new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(18, 18, 18, 18)
            }))
            {
                DrawHeader();

                EditorGUILayout.Space(10);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                DrawSection("Version", () =>
                {
                    DrawCopyRow("ConvoCore Version:", _version);
                    DrawCopyRow("Unity Version:", Application.unityVersion);
                });

                EditorGUILayout.Space(8);

                DrawSection("Credits", () =>
                {
                    EditorGUILayout.LabelField(ConvoCoreVersion.Credits, EditorStyles.wordWrappedLabel);
                });

                EditorGUILayout.Space(8);

                DrawSection("Links", () =>
                {
                    DrawLinkRow("Docs:", ConvoCoreVersion.DocsUrl);
                    DrawLinkRow("Website:", ConvoCoreVersion.WebsiteUrl);
                    EditorGUILayout.Space(6);
                    DrawLinkRow("Support Discord:", ConvoCoreVersion.SupportDiscordUrl);
                    DrawLinkRow("Support Email:", ConvoCoreVersion.SupportEmail);
                });
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(100))) Close();
            }
        }

        private void DrawHeader()
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };

            var subStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };

            if (_logo)
            {
                float pad = 16f;

                float availableWidth = Mathf.Max(1f, position.width - (pad * 2f));

                float aspect = (float)_logo.width / Mathf.Max(1, _logo.height);

                float desiredHeight = availableWidth / Mathf.Max(0.01f, aspect);

                float minH = 80f;
                float maxH = 220f;

                float h = Mathf.Clamp(desiredHeight, minH, maxH);

                Rect rect = GUILayoutUtility.GetRect(availableWidth, h, GUILayout.ExpandWidth(true));

                rect.x += pad;
                rect.width -= pad * 2f;

                GUI.DrawTexture(rect, _logo, ScaleMode.ScaleToFit, true);
                EditorGUILayout.Space(8);
            }

            EditorGUILayout.LabelField(ConvoCoreVersion.AssetName, titleStyle);
            EditorGUILayout.LabelField(ConvoCoreVersion.PublisherName, subStyle);
        }
        private static void DrawSection(string title, Action content)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                content?.Invoke();
            }
        }
        private static void DrawCopyRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(value ?? "", GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Copy", GUILayout.Width(60)))
                    EditorGUIUtility.systemCopyBuffer = value ?? "";
            }
        }
        private static void DrawLinkRow(string label, string url)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(100));

                if (string.IsNullOrWhiteSpace(url))
                {
                    EditorGUILayout.LabelField("(not set)");
                    return;
                }

                if (GUILayout.Button(url, EditorStyles.linkLabel))
                    Application.OpenURL(url);

                if (GUILayout.Button("Copy", GUILayout.Width(60)))
                    EditorGUIUtility.systemCopyBuffer = url;
            }
        }
        private static Texture2D FindLogo()
        {
            var guids = AssetDatabase.FindAssets(LogoSearchFilter);
            if (guids == null || guids.Length == 0) return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static string ResolveVersion()
        {
            var fromPackageJson = TryGetVersionFromPackageJson();
            if (!string.IsNullOrEmpty(fromPackageJson)) return fromPackageJson;

            return ConvoCoreVersion.VersionFallback;
        }
        private static string TryGetVersionFromPackageJson()
        {
            try
            {
                var guids = AssetDatabase.FindAssets(PackageJsonSearchFilter);
                if (guids == null || guids.Length == 0) return null;

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)) continue;

                    var text = File.ReadAllText(path);
                    var version = ExtractJsonString(text, "version");
                    var name = ExtractJsonString(text, "name");

                    if (string.IsNullOrWhiteSpace(version)) continue;

                    if (!string.IsNullOrWhiteSpace(name) &&
                        name.IndexOf("convocore", StringComparison.OrdinalIgnoreCase) >= 0)
                        return version;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            var pattern = "\""+ Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]+)\"";
            var match = Regex.Match(json, pattern);
            if (!match.Success) return null;
            return match.Groups[1].Value;
        }
    }
    
    public static class ConvoCoreVersion
    {
        public const string AssetName = "ConvoCore";
        public const string VersionFallback = "0.0.0";

        public const string PublisherName = "Wolfstag Interactive";
        public const string WebsiteUrl = "www.wolfstaginteractive.com";
        public const string DocsUrl = "www.docs.wolfstaginteractive.com";
        public const string SupportEmail = "support@wolfstaginteractive.com";
        public const string SupportDiscordUrl = "https://discord.gg/XZ5mhr7SwN";

        public const string Credits =
            "Created by Wolfstag Interactive.\n" +
            "Uses YamlDotNet (MIT License).\n" +
            "Sample Art assets: Indonesian Gentleman\n";
    }
    
}
#endif