using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WolfstagInteractive.ConvoCore;

namespace WolfstagInteractive.ConvoCore.Editor
{
    internal sealed class ConvoCoreBulkImportWindow : EditorWindow
    {
        // ---- Window state ----

        private enum WindowState { Configuration, Preview, Results }
        private WindowState _state;

        // ---- Configuration ----

        private string _inputFolderPath = string.Empty;
        private string _outputFolderPath = string.Empty;
        private bool _outputFolderPathManuallySet;
        private bool _recursive = true;
        private AssetNamingMode _namingMode = AssetNamingMode.YamlFileAndKey;

        // ---- Preview ----

        private List<BulkImportManifestEntry> _manifest;
        private Vector2 _previewScroll;
        private BulkImportManifestEntry _selectedDetailEntry;
        private Vector2 _detailPanelScroll;
        private float _detailPanelHeight = 60f;
        private bool _draggingDetailHandle;

        // ---- Results ----

        private List<BulkImportResult> _results;
        private Vector2 _resultsScroll;

        // ---- Cached styles ----

        private GUIStyle _richTextStyle;
        private GUIStyle _linkStyle;
        private GUIStyle _headerStyle;
        private bool _stylesInitialized;

        // ---- EditorPrefs keys ----

        private const string PrefInputKey = "ConvoCore_BulkImport_InputFolder";
        private const string PrefOutputKey = "ConvoCore_BulkImport_OutputFolder";

        // ---- Menu item ----

        [MenuItem("Tools/Wolfstag Interactive/ConvoCore/Bulk Import", priority = 200)]
        public static void ShowWindow()
        {
            GetWindow<ConvoCoreBulkImportWindow>("ConvoCore Bulk Import");
        }

        // ---- Unity callbacks ----

        private void OnEnable()
        {
            minSize = new Vector2(600, 400);
            LoadEditorPrefs();
        }

        private void OnDisable()
        {
            SaveEditorPrefs();
        }

        private void OnGUI()
        {
            EnsureStyles();

            // Redirect before any controls are drawn so Layout and Repaint see the same control tree.
            if (_state == WindowState.Preview && _manifest == null)
                _state = WindowState.Configuration;
            if (_state == WindowState.Results && _results == null)
                _state = WindowState.Configuration;

            switch (_state)
            {
                case WindowState.Configuration: DrawConfiguration(); break;
                case WindowState.Preview:       DrawPreview();       break;
                case WindowState.Results:       DrawResults();       break;
            }
        }

        // ===== CONFIGURATION STATE =====

        private void DrawConfiguration()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Bulk YAML Import", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawFolderField(
                label: "Input YAML Folder",
                folderPath: ref _inputFolderPath,
                onChanged: () =>
                {
                    if (!_outputFolderPathManuallySet)
                        _outputFolderPath = GetDefaultOutputPath(_inputFolderPath);
                    SaveEditorPrefs();
                });

            if (!string.IsNullOrEmpty(_inputFolderPath))
                EditorGUILayout.LabelField(_inputFolderPath, EditorStyles.miniLabel);

            // Outside-Assets error
            if (!string.IsNullOrEmpty(_inputFolderPath) && !_inputFolderPath.StartsWith("Assets", StringComparison.Ordinal))
                EditorGUILayout.HelpBox("Input folder must be inside the Assets directory.", MessageType.Error);

            EditorGUILayout.Space(4);

            DrawFolderField(
                label: "Output Folder for New Assets",
                folderPath: ref _outputFolderPath,
                onChanged: () =>
                {
                    _outputFolderPathManuallySet = true;
                    SaveEditorPrefs();
                });

            EditorGUILayout.LabelField("This folder will be created if it does not exist.", EditorStyles.miniLabel);

            // Same-folder soft warning
            if (!string.IsNullOrEmpty(_inputFolderPath) &&
                !string.IsNullOrEmpty(_outputFolderPath) &&
                _inputFolderPath.TrimEnd('/') == _outputFolderPath.TrimEnd('/'))
            {
                EditorGUILayout.HelpBox(
                    "Input and output folders are the same. YAML files and conversation assets will coexist in this folder.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            _recursive = EditorGUILayout.Toggle("Recursive", _recursive);
            _namingMode = (AssetNamingMode)EditorGUILayout.EnumPopup("Asset Naming", _namingMode);

            EditorGUILayout.Space(10);

            bool inputValid = !string.IsNullOrEmpty(_inputFolderPath) &&
                              _inputFolderPath.StartsWith("Assets", StringComparison.Ordinal);
            using (new EditorGUI.DisabledScope(!inputValid))
            {
                if (GUILayout.Button("Scan"))
                {
                    _manifest = BulkImportProcessor.BuildManifest(_inputFolderPath, _recursive);
                    _selectedDetailEntry = null;
                    _state = WindowState.Preview;
                }
            }
        }

        private void DrawFolderField(string label, ref string folderPath, Action onChanged)
        {
            EditorGUILayout.BeginHorizontal();

            var folderObj = string.IsNullOrEmpty(folderPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);

            var newObj = (DefaultAsset)EditorGUILayout.ObjectField(label, folderObj, typeof(DefaultAsset), false);
            if (newObj != folderObj)
            {
                if (newObj != null)
                {
                    var p = AssetDatabase.GetAssetPath(newObj);
                    if (AssetDatabase.IsValidFolder(p))
                    {
                        folderPath = p;
                        onChanged?.Invoke();
                    }
                }
                else
                {
                    folderPath = string.Empty;
                    onChanged?.Invoke();
                }
            }

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string startPath = Application.dataPath;
                if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
                {
                    startPath = Path.GetFullPath(
                        Path.Combine(Application.dataPath, "..", folderPath));
                }

                var selected = EditorUtility.OpenFolderPanel($"Select {label}", startPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                    {
                        folderPath = "Assets" + selected.Substring(Application.dataPath.Length).Replace('\\', '/');
                        onChanged?.Invoke();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Folder",
                            "Please select a folder inside the project's Assets directory.", "OK");
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ===== PREVIEW STATE =====

        // Column widths shared between header and data rows.
        private const float ColCheck  = 20f;
        private const float ColIcon   = 20f;
        private const float ColSource = 180f;
        private const float ColLines  = 50f;
        private const float ColDetail = 90f;

        private void DrawPreview()
        {
            // ---- fixed top ----

            EditorGUILayout.Space(6);

            int countNew = 0, countUpdate = 0, countConflict = 0, countError = 0;
            var yamlFiles = new HashSet<string>();
            foreach (var e in _manifest)
            {
                yamlFiles.Add(e.YamlAssetPath);
                switch (e.Status)
                {
                    case BulkImportEntryStatus.New:      countNew++;      break;
                    case BulkImportEntryStatus.Update:   countUpdate++;   break;
                    case BulkImportEntryStatus.Conflict: countConflict++; break;
                    case BulkImportEntryStatus.Error:    countError++;    break;
                }
            }

            EditorGUILayout.LabelField(
                $"Found {_manifest.Count} conversations in {yamlFiles.Count} YAML files. " +
                $"{countNew} new, {countUpdate} updates, {countConflict} conflicts, {countError} errors.",
                EditorStyles.wordWrappedLabel);

            var keysPerFile = new Dictionary<string, int>();
            foreach (var e in _manifest)
            {
                keysPerFile.TryGetValue(e.YamlAssetPath, out int c);
                keysPerFile[e.YamlAssetPath] = c + 1;
            }
            bool anyMultiKey = false;
            foreach (var kv in keysPerFile) if (kv.Value > 1) { anyMultiKey = true; break; }
            if (anyMultiKey)
                EditorGUILayout.HelpBox(
                    "Some YAML files contain multiple conversation keys. Each key will produce a separate Conversation asset.",
                    MessageType.Info);

            if (_manifest.Count == 0)
            {
                EditorGUILayout.HelpBox("No YAML files found in the selected folder.", MessageType.Warning);
            }
            else
            {
                // Column header row (fixed, outside the scroll view)
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Space(ColCheck + ColIcon);
                GUILayout.Label("Conversation Key", _headerStyle, GUILayout.ExpandWidth(true));
                GUILayout.Label("Source YAML",      _headerStyle, GUILayout.Width(ColSource));
                GUILayout.Label("Lines",            _headerStyle, GUILayout.Width(ColLines));
                GUILayout.Label("Detail",           _headerStyle, GUILayout.Width(ColDetail));
                EditorGUILayout.EndHorizontal();

                bool detailVisible = _selectedDetailEntry != null &&
                                     !string.IsNullOrEmpty(_selectedDetailEntry.StatusDetail);
                float detailReserved = detailVisible ? _detailPanelHeight + 5f : 0f;
                float previewScrollH = Mathf.Max(position.height - 160f - detailReserved, 80f);
                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(previewScrollH));
                DrawPreviewRows();
                EditorGUILayout.EndScrollView();
            }

            // ---- fixed bottom ----

            if (_selectedDetailEntry != null && !string.IsNullOrEmpty(_selectedDetailEntry.StatusDetail))
            {
                DrawDetailPanelHandle();
                _detailPanelScroll = EditorGUILayout.BeginScrollView(
                    _detailPanelScroll, GUILayout.Height(_detailPanelHeight));
                EditorGUILayout.HelpBox(_selectedDetailEntry.StatusDetail, MessageType.Warning);
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select All", GUILayout.Width(90)))
                SetAllSelectableSelected(true);
            if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
                SetAllSelectableSelected(false);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Back", GUILayout.Width(70)))
                _state = WindowState.Configuration;

            int selectedCount = CountSelected();
            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                if (GUILayout.Button($"Import Selected ({selectedCount})", GUILayout.Width(160)))
                    RunImport();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawPreviewRows()
        {
            foreach (var entry in _manifest)
            {
                bool selectable = entry.Status == BulkImportEntryStatus.New ||
                                  entry.Status == BulkImportEntryStatus.Update;

                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledScope(!selectable))
                    if (EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(ColCheck)) != entry.Selected && selectable)
                        entry.Selected = !entry.Selected;

                GUILayout.Label(GetStatusIcon(entry.Status), _richTextStyle, GUILayout.Width(ColIcon));
                GUILayout.Label(entry.ConversationKey, GUILayout.ExpandWidth(true));
                GUILayout.Label(Path.GetFileName(entry.YamlAssetPath), GUILayout.Width(ColSource));

                string lineLabel = (entry.Status == BulkImportEntryStatus.Error ||
                                    entry.Status == BulkImportEntryStatus.Conflict)
                    ? "-" : entry.LineCount.ToString();
                GUILayout.Label(lineLabel, _numberStyle, GUILayout.Width(ColLines));

                if (!selectable)
                {
                    if (GUILayout.Button(entry.Status.ToString(), EditorStyles.miniButton, GUILayout.Width(ColDetail)))
                        _selectedDetailEntry = _selectedDetailEntry == entry ? null : entry;
                }
                else
                {
                    GUILayout.Label(string.Empty, GUILayout.Width(ColDetail));
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void RunImport()
        {
            int total = CountSelected();
            int current = 0;

            try
            {
                _results = BulkImportProcessor.Execute(
                    _manifest,
                    _outputFolderPath,
                    _namingMode,
                    onProgress: (i, count, key) =>
                    {
                        current = i;
                        EditorUtility.DisplayProgressBar(
                            "ConvoCore Bulk Import",
                            $"Importing conversation {i + 1} of {count}: {key}",
                            count > 0 ? (float)i / count : 1f);
                    });
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _state = WindowState.Results;
        }

        // ===== RESULTS STATE =====

        private void DrawResults()
        {
            EditorGUILayout.Space(6);

            int created = 0, updated = 0, failed = 0;
            foreach (var r in _results)
            {
                switch (r.Outcome)
                {
                    case BulkImportOutcome.Created: created++; break;
                    case BulkImportOutcome.Updated: updated++; break;
                    case BulkImportOutcome.Failed:  failed++;  break;
                }
            }

            EditorGUILayout.LabelField(
                $"Import complete. {created} created, {updated} updated, {failed} failed.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);

            const float rColIcon = 20f;
            const float rColPath = 220f;
            const float rColErr  = 200f;
            // Conversation Key fills remaining space.

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(rColIcon);
            GUILayout.Label("Conversation Key", _headerStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("Asset Path",       _headerStyle, GUILayout.Width(rColPath));
            GUILayout.Label("Error",            _headerStyle, GUILayout.Width(rColErr));
            EditorGUILayout.EndHorizontal();

            float resultsScrollH = Mathf.Max(position.height - 120f, 80f);
            _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll, GUILayout.Height(resultsScrollH));

            foreach (var result in _results)
            {
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(GetOutcomeIcon(result.Outcome), _richTextStyle, GUILayout.Width(rColIcon));
                GUILayout.Label(result.ConversationKey, GUILayout.ExpandWidth(true));

                if (!string.IsNullOrEmpty(result.OutputAssetPath))
                {
                    if (GUILayout.Button(Path.GetFileName(result.OutputAssetPath), _linkStyle, GUILayout.Width(rColPath)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.OutputAssetPath);
                        if (obj != null) EditorGUIUtility.PingObject(obj);
                    }
                }
                else
                {
                    GUILayout.Label("-", GUILayout.Width(rColPath));
                }

                GUILayout.Label(result.ErrorMessage ?? string.Empty, GUILayout.Width(rColErr));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Show in Project", GUILayout.Width(120)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_outputFolderPath);
                if (folder != null) EditorGUIUtility.PingObject(folder);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("New Import", GUILayout.Width(100)))
            {
                _manifest = null;
                _results = null;
                _selectedDetailEntry = null;
                _state = WindowState.Configuration;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ===== DETAIL PANEL RESIZE HANDLE =====

        private void DrawDetailPanelHandle()
        {
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none, GUILayout.Height(5f), GUILayout.ExpandWidth(true));

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.toolbar.Draw(rect, GUIContent.none, false, false, false, false);
                float cx = rect.center.x;
                float cy = rect.center.y;
                var dotColor = EditorGUIUtility.isProSkin
                    ? new Color(0.55f, 0.55f, 0.55f, 1f)
                    : new Color(0.35f, 0.35f, 0.35f, 1f);
                for (int i = -1; i <= 1; i++)
                    EditorGUI.DrawRect(new Rect(cx + i * 6 - 1f, cy - 1f, 2f, 2f), dotColor);
            }

            switch (Event.current.type)
            {
                case EventType.MouseDown when rect.Contains(Event.current.mousePosition):
                    _draggingDetailHandle = true;
                    Event.current.Use();
                    break;
                case EventType.MouseUp:
                    _draggingDetailHandle = false;
                    break;
                case EventType.MouseDrag when _draggingDetailHandle:
                    _detailPanelHeight = Mathf.Clamp(_detailPanelHeight - Event.current.delta.y, 40f, 300f);
                    Event.current.Use();
                    Repaint();
                    break;
            }
        }

        // ===== HELPERS =====

        private static string GetDefaultOutputPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath)) return string.Empty;
            var parent = Path.GetDirectoryName(inputPath)?.Replace('\\', '/');
            return string.IsNullOrEmpty(parent) ? "Assets/Conversations" : $"{parent}/Conversations";
        }

        private int CountSelected()
        {
            if (_manifest == null) return 0;
            int n = 0;
            foreach (var e in _manifest)
                if (e.Selected && (e.Status == BulkImportEntryStatus.New || e.Status == BulkImportEntryStatus.Update))
                    n++;
            return n;
        }

        private void SetAllSelectableSelected(bool selected)
        {
            if (_manifest == null) return;
            foreach (var e in _manifest)
                if (e.Status == BulkImportEntryStatus.New || e.Status == BulkImportEntryStatus.Update)
                    e.Selected = selected;
        }

        private static string GetStatusIcon(BulkImportEntryStatus status)
        {
            return status switch
            {
                BulkImportEntryStatus.New      => "<color=#44CC44>●</color>",
                BulkImportEntryStatus.Update   => "<color=#4488FF>↑</color>",
                BulkImportEntryStatus.Conflict => "<color=#FFAA00>⚠</color>",
                BulkImportEntryStatus.Error    => "<color=#FF4444>✕</color>",
                _                              => "-"
            };
        }

        private static string GetOutcomeIcon(BulkImportOutcome outcome)
        {
            return outcome switch
            {
                BulkImportOutcome.Created => "<color=#44CC44>✓</color>",
                BulkImportOutcome.Updated => "<color=#4488FF>✓</color>",
                BulkImportOutcome.Failed  => "<color=#FF4444>✕</color>",
                _                         => "-"
            };
        }

        private GUIStyle _numberStyle;

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _richTextStyle = new GUIStyle(EditorStyles.label) { richText = true };
            _linkStyle = new GUIStyle(EditorStyles.linkLabel) { wordWrap = false };
            _headerStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            _numberStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
        }

        private void LoadEditorPrefs()
        {
            var storedInput = EditorPrefs.GetString(PrefInputKey, string.Empty);
            if (!string.IsNullOrEmpty(storedInput) && AssetDatabase.IsValidFolder(storedInput))
                _inputFolderPath = storedInput;

            var storedOutput = EditorPrefs.GetString(PrefOutputKey, string.Empty);
            if (!string.IsNullOrEmpty(storedOutput))
            {
                _outputFolderPath = storedOutput;
                _outputFolderPathManuallySet = true;
            }
            else if (!string.IsNullOrEmpty(_inputFolderPath))
            {
                _outputFolderPath = GetDefaultOutputPath(_inputFolderPath);
            }
        }

        private void SaveEditorPrefs()
        {
            EditorPrefs.SetString(PrefInputKey, _inputFolderPath ?? string.Empty);
            EditorPrefs.SetString(PrefOutputKey, _outputFolderPath ?? string.Empty);
        }
    }
}
