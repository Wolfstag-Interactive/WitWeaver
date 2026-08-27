using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverAudioManifestEditor.html")]
    [CustomEditor(typeof(WitWeaverAudioManifest))]
    public class WitWeaverAudioManifestEditor : UnityEditor.Editor
    {
        // ── Pagination state ──────────────────────────────────────────────────
        private const string k_PageKey    = "WitWeaverAudioManifest_Page_";
        private const string k_SizeKey    = "WitWeaverAudioManifest_Size_";
        private const int    k_DefaultPageSize = 10;

        private static readonly Dictionary<string, bool> s_LineFoldouts = new();

        // ─────────────────────────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manifest    = (WitWeaverAudioManifest)target;
            var entriesProp = serializedObject.FindProperty("Entries");
            var backend     = manifest.Backend;

            // ── Backend selector ──────────────────────────────────────────────
            EditorGUILayout.LabelField("Backend", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Backend"),
                new GUIContent("Audio Backend", "Selects how audio is driven at runtime. Controls which fields are shown per entry."));

            // Warn when non-Unity backend has no provider on the runner
            if (backend != AudioBackend.UnityAudioSource)
            {
                EditorGUILayout.HelpBox(
                    $"Backend is '{backend}'. Assign a MonoBehaviour implementing IWitWeaverAudioProvider to the WitWeaver runner's Audio Provider field.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(4f);

            // ── Unassigned count warning ──────────────────────────────────────
            int missingCount = 0;
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var ep = entriesProp.GetArrayElementAtIndex(i);
                switch (backend)
                {
                    case AudioBackend.UnityAudioSource:
                    {
                        var clipProp = ep.FindPropertyRelative("Clip");
                        var refProp  = ep.FindPropertyRelative("Reference");
                        bool hasClip = clipProp != null && clipProp.objectReferenceValue != null;
                        bool hasRef  = refProp  != null && refProp.objectReferenceValue  != null;
                        if (!hasClip && !hasRef) missingCount++;
                        break;
                    }
                    case AudioBackend.FMOD:
                    case AudioBackend.Wwise:
                    {
                        var keyProp = ep.FindPropertyRelative("EventKey");
                        if (keyProp != null && string.IsNullOrEmpty(keyProp.stringValue)) missingCount++;
                        break;
                    }
                    case AudioBackend.Custom:
                    {
                        var refProp = ep.FindPropertyRelative("Reference");
                        if (refProp != null && refProp.objectReferenceValue == null) missingCount++;
                        break;
                    }
                }
            }
            if (missingCount > 0)
            {
                string slotLabel = backend switch
                {
                    AudioBackend.FMOD    => "FMOD event path",
                    AudioBackend.Wwise   => "Wwise event name",
                    AudioBackend.Custom  => "audio reference",
                    _                   => "audio clip"
                };
                EditorGUILayout.HelpBox($"{missingCount} locale slot(s) are missing {(slotLabel.StartsWith("a") || slotLabel.StartsWith("F") || slotLabel.StartsWith("W") ? "a" : "an")} {slotLabel}.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("Mode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SourceConversation"));

            EditorGUILayout.Space(4f);

            if (manifest.Mode == AudioManifestMode.ConversationDriven)
                DrawConversationDrivenMode(manifest, entriesProp);
            else
                DrawStandaloneMode(manifest, entriesProp);

            serializedObject.ApplyModifiedProperties();
        }

        // ── ConversationDriven mode ───────────────────────────────────────────

        private void DrawConversationDrivenMode(WitWeaverAudioManifest manifest, SerializedProperty entriesProp)
        {
            if (GUILayout.Button("Sync Rows From Conversation"))
            {
                SyncFromConversation(manifest);
                serializedObject.Update();
                entriesProp = serializedObject.FindProperty("Entries");
            }

            EditorGUILayout.Space(4f);

            if (entriesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No entries. Assign a Source Conversation and click Sync.", MessageType.Info);
                return;
            }

            DrawLineGroups(manifest, entriesProp, showLineText: true);
        }

        // ── Standalone mode ───────────────────────────────────────────────────

        private void DrawStandaloneMode(WitWeaverAudioManifest manifest, SerializedProperty entriesProp)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Line"))
            {
                AddStandaloneLine(manifest);
                serializedObject.Update();
                entriesProp = serializedObject.FindProperty("Entries");
            }

            GUI.enabled = manifest.SourceConversation == null && entriesProp.arraySize > 0;
            if (GUILayout.Button("Generate Conversation Asset"))
                GenerateConversationAsset(manifest);
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);

            if (entriesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No entries. Click 'Add Line' to create rows.", MessageType.Info);
                return;
            }

            DrawLineGroups(manifest, entriesProp, showLineText: false);
        }

        // ── Line-grouped paged list ───────────────────────────────────────────
        // Entries in the flat list are grouped by LineID. Each group is one "line" in the
        // paginated display. Within a group the user sees all locale → reference slots.

        private void DrawLineGroups(WitWeaverAudioManifest manifest, SerializedProperty entriesProp, bool showLineText)
        {
            // Build ordered groups: preserve insertion order of first-seen LineID
            var orderedLineIds = new List<string>();
            var groups         = new Dictionary<string, List<int>>(); // LineID -> flat entry indices

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var ep     = entriesProp.GetArrayElementAtIndex(i);
                string lid = ep.FindPropertyRelative("LineID")?.stringValue ?? "";
                if (!groups.ContainsKey(lid))
                {
                    groups[lid] = new List<int>();
                    orderedLineIds.Add(lid);
                }
                groups[lid].Add(i);
            }

            int totalLines = orderedLineIds.Count;

            // ── Pagination toolbar ────────────────────────────────────────────
            string instanceKey = k_PageKey + target.GetEntityId();
            string sizeKey     = k_SizeKey + target.GetEntityId();
            int pageSize    = Mathf.Max(1, EditorPrefs.GetInt(sizeKey, k_DefaultPageSize));
            int totalPages  = Mathf.Max(1, Mathf.CeilToInt(totalLines / (float)pageSize));
            int currentPage = Mathf.Clamp(SessionState.GetInt(instanceKey, 0), 0, totalPages - 1);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(25)))
                currentPage = Mathf.Max(currentPage - 1, 0);
            GUILayout.Label($"{currentPage + 1}/{totalPages}", GUILayout.Width(55));
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(25)))
                currentPage = Mathf.Min(currentPage + 1, totalPages - 1);

            int rangeStart = currentPage * pageSize;
            int rangeEnd   = Mathf.Min(rangeStart + pageSize, totalLines);
            GUILayout.Label($"  Lines {rangeStart + 1}–{rangeEnd} of {totalLines}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Per page", EditorStyles.miniLabel, GUILayout.Width(55));
            int newSize = EditorGUILayout.DelayedIntField(pageSize, GUILayout.Width(40));
            if (newSize > 0 && newSize != pageSize)
            {
                pageSize = newSize;
                EditorPrefs.SetInt(sizeKey, pageSize);
                totalPages  = Mathf.Max(1, Mathf.CeilToInt(totalLines / (float)pageSize));
                currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);
            }
            EditorGUILayout.EndHorizontal();

            SessionState.SetInt(instanceKey, currentPage);

            GUILayout.Space(3f);

            // ── Visible line groups ───────────────────────────────────────────
            bool needsRebuild = false;

            for (int g = rangeStart; g < rangeEnd; g++)
            {
                if (needsRebuild) break; // list mutated — stop and let next repaint rebuild

                string lineId    = orderedLineIds[g];
                var    flatIdxes = groups[lineId];

                // Foldout key
                string foldKey = target.GetEntityId() + "_" + lineId;
                if (!s_LineFoldouts.ContainsKey(foldKey))
                    s_LineFoldouts[foldKey] = false;

                // Build header label
                string charId      = entriesProp.GetArrayElementAtIndex(flatIdxes[0]).FindPropertyRelative("CharacterID")?.stringValue ?? "";
                string textPreview = showLineText ? GetLinePreview(manifest.SourceConversation, lineId) : "";
                string header      = string.IsNullOrEmpty(textPreview)
                    ? $"{lineId}  [{charId}]"
                    : $"{lineId}  [{charId}]  \"{TruncatePreview(textPreview, 45)}\"";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // ── Group header row ──────────────────────────────────────────
                EditorGUILayout.BeginHorizontal();
                s_LineFoldouts[foldKey] = EditorGUILayout.Foldout(s_LineFoldouts[foldKey], header, true, EditorStyles.foldoutHeader);

                if (GUILayout.Button("Remove Line", EditorStyles.miniButton, GUILayout.Width(88)))
                {
                    // Delete all entries for this line in reverse order to preserve indices
                    Undo.RecordObject(target, "Remove Audio Manifest Line");
                    for (int k = flatIdxes.Count - 1; k >= 0; k--)
                        entriesProp.DeleteArrayElementAtIndex(flatIdxes[k]);
                    serializedObject.ApplyModifiedProperties();
                    needsRebuild = true;
                }

                EditorGUILayout.EndHorizontal();

                // ── Locale slots ──────────────────────────────────────────────
                if (!needsRebuild && s_LineFoldouts[foldKey])
                {
                    EditorGUI.indentLevel++;

                    for (int k = 0; k < flatIdxes.Count; k++)
                    {
                        if (needsRebuild) break;

                        int flatIdx  = flatIdxes[k];
                        var ep       = entriesProp.GetArrayElementAtIndex(flatIdx);
                        var langProp = ep.FindPropertyRelative("Language");
                        var clipProp = ep.FindPropertyRelative("Clip");
                        var keyProp  = ep.FindPropertyRelative("EventKey");
                        var refProp  = ep.FindPropertyRelative("Reference");

                        EditorGUILayout.BeginHorizontal();

                        // Language slot
                        if (manifest.Mode == AudioManifestMode.Standalone)
                        {
                            EditorGUILayout.PropertyField(langProp, GUIContent.none, GUILayout.Width(60));
                        }
                        else
                        {
                            string langDisplay = string.IsNullOrEmpty(langProp?.stringValue) ? "(any)" : langProp.stringValue;
                            EditorGUILayout.LabelField(langDisplay, GUILayout.Width(60));
                        }

                        // Audio slot — varies by backend
                        switch (manifest.Backend)
                        {
                            case AudioBackend.UnityAudioSource:
                                // Prominent AudioClip drag-drop
                                EditorGUILayout.PropertyField(clipProp, GUIContent.none);
                                break;

                            case AudioBackend.FMOD:
                                // User types the full FMOD event path
                                EditorGUILayout.PropertyField(keyProp,
                                    new GUIContent("Event Path", "FMOD event path, e.g. \"event:/VO/CharA/Line001\""));
                                break;

                            case AudioBackend.Wwise:
                                // User types the Wwise event name
                                EditorGUILayout.PropertyField(keyProp,
                                    new GUIContent("Event Name", "Wwise event name, e.g. \"VO_CharA_Intro_01\""));
                                break;

                            case AudioBackend.Custom:
                                // All three slots visible — custom provider decides what to use
                                EditorGUILayout.PropertyField(clipProp,
                                    new GUIContent("Clip", "AudioClip (optional, custom provider may use this)."),
                                    GUILayout.MaxWidth(110));
                                EditorGUILayout.PropertyField(keyProp,
                                    new GUIContent("Key", "Arbitrary event key string for custom providers."),
                                    GUILayout.MaxWidth(110));
                                EditorGUILayout.PropertyField(refProp,
                                    new GUIContent("Ref", "WitWeaverAudioReference ScriptableObject for custom middleware."));
                                break;
                        }

                        // Remove locale button (only in Standalone mode)
                        if (manifest.Mode == AudioManifestMode.Standalone)
                        {
                            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                            {
                                Undo.RecordObject(target, "Remove Audio Locale Entry");
                                entriesProp.DeleteArrayElementAtIndex(flatIdx);
                                serializedObject.ApplyModifiedProperties();
                                needsRebuild = true;
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    // Add locale button (Standalone only)
                    if (!needsRebuild && manifest.Mode == AudioManifestMode.Standalone)
                    {
                        EditorGUILayout.Space(2f);
                        if (GUILayout.Button("+ Add Locale", EditorStyles.miniButton))
                        {
                            // Insert a new entry after the last entry for this line
                            int insertAt = flatIdxes[flatIdxes.Count - 1] + 1;
                            entriesProp.InsertArrayElementAtIndex(insertAt);
                            var newEntry = entriesProp.GetArrayElementAtIndex(insertAt);
                            newEntry.FindPropertyRelative("LineID").stringValue             = lineId;
                            newEntry.FindPropertyRelative("CharacterID").stringValue        = charId;
                            newEntry.FindPropertyRelative("Language").stringValue           = "";
                            newEntry.FindPropertyRelative("Clip").objectReferenceValue      = null;
                            newEntry.FindPropertyRelative("EventKey").stringValue           = "";
                            newEntry.FindPropertyRelative("Reference").objectReferenceValue = null;
                            serializedObject.ApplyModifiedProperties();
                            needsRebuild = true;
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(2f);
            }
        }

        // ── Bulk operations ───────────────────────────────────────────────────

        private static void SyncFromConversation(WitWeaverAudioManifest manifest)
        {
            if (manifest.SourceConversation == null)
            {
                EditorUtility.DisplayDialog("Sync", "Assign a Source Conversation first.", "OK");
                return;
            }

            var conversation = manifest.SourceConversation;
            if (conversation.DialogueLines == null || conversation.DialogueLines.Count == 0)
            {
                Debug.LogWarning("[WitWeaverAudioManifest] Source conversation has no dialogue lines.");
                return;
            }

            // Build lookup of existing entries: (LineID, Language) -> existing entry (preserves Clip + Reference)
            var existing = new Dictionary<(string, string), WitWeaverAudioManifest.AudioEntry>();
            if (manifest.Entries != null)
                foreach (var e in manifest.Entries)
                    existing[(e.LineID ?? "", e.Language ?? "")] = e;

            var updated = new List<WitWeaverAudioManifest.AudioEntry>();

            foreach (var line in conversation.DialogueLines)
            {
                if (line == null) continue;

                bool addedAny = false;
                if (line.LocalizedDialogues != null && line.LocalizedDialogues.Count > 0)
                {
                    foreach (var ld in line.LocalizedDialogues)
                    {
                        var key = (line.LineID ?? "", ld.Language ?? "");
                        var entry = new WitWeaverAudioManifest.AudioEntry
                        {
                            LineID      = line.LineID,
                            CharacterID = line.characterID,
                            Language    = ld.Language ?? ""
                        };
                        if (existing.TryGetValue(key, out var prev))
                        {
                            entry.Clip      = prev.Clip;
                            entry.EventKey  = prev.EventKey;
                            entry.Reference = prev.Reference;
                        }
                        updated.Add(entry);
                        addedAny = true;
                    }
                }

                if (!addedAny)
                {
                    var key = (line.LineID ?? "", "");
                    var entry = new WitWeaverAudioManifest.AudioEntry
                    {
                        LineID      = line.LineID,
                        CharacterID = line.characterID,
                        Language    = ""
                    };
                    if (existing.TryGetValue(key, out var prev))
                    {
                        entry.Clip      = prev.Clip;
                        entry.EventKey  = prev.EventKey;
                        entry.Reference = prev.Reference;
                    }
                    updated.Add(entry);
                }
            }

            Undo.RecordObject(manifest, "Sync Audio Manifest Rows");
            manifest.Entries = updated;
            EditorUtility.SetDirty(manifest);
            Debug.Log($"[WitWeaverAudioManifest] Synced {updated.Count} entries for {conversation.DialogueLines.Count} lines from '{conversation.name}'.");
        }

        private static void AddStandaloneLine(WitWeaverAudioManifest manifest)
        {
            Undo.RecordObject(manifest, "Add Audio Manifest Line");
            manifest.Entries ??= new List<WitWeaverAudioManifest.AudioEntry>();
            manifest.Entries.Add(new WitWeaverAudioManifest.AudioEntry
            {
                LineID      = WitWeaverLineID.NewLineID(),
                CharacterID = "",
                Language    = "",
                Reference   = null
            });
            EditorUtility.SetDirty(manifest);
        }

        private static void GenerateConversationAsset(WitWeaverAudioManifest manifest)
        {
            string manifestPath = AssetDatabase.GetAssetPath(manifest);
            string folder       = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') ?? "Assets";
            string assetName    = Path.GetFileNameWithoutExtension(manifestPath) + "_Conversation";
            string assetPath    = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");

            var convo = CreateInstance<WitWeaverConversationData>();
            convo.ConversationKey             = assetName;
            convo.ConversationTitle           = assetName;
            convo.DefaultPresentationMode     = ConversationPresentationMode.AudioOnly;
            convo.DialogueLines               = new List<WitWeaverConversationData.DialogueLineInfo>();

            // One DialogueLineInfo per unique LineID
            var seen = new HashSet<string>();
            if (manifest.Entries != null)
            {
                foreach (var entry in manifest.Entries)
                {
                    if (string.IsNullOrEmpty(entry.LineID) || !seen.Add(entry.LineID)) continue;
                    var lineInfo = new WitWeaverConversationData.DialogueLineInfo(convo.ConversationKey)
                    {
                        LineID                = entry.LineID,
                        characterID           = entry.CharacterID,
                        PresentationMode      = ConversationPresentationMode.AudioOnly,
                        UserInputMethod       = WitWeaverConversationData.DialogueLineProgressionMethod.AudioComplete,
                        ConversationLineIndex = convo.DialogueLines.Count
                    };
                    convo.DialogueLines.Add(lineInfo);
                }
            }

            AssetDatabase.CreateAsset(convo, assetPath);

            Undo.RecordObject(manifest, "Generate Conversation Asset");
            manifest.SourceConversation = convo;
            EditorUtility.SetDirty(manifest);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(convo);
            Debug.Log($"[WitWeaverAudioManifest] Generated conversation asset at '{assetPath}'.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GetLinePreview(WitWeaverConversationData conversation, string lineID)
        {
            if (conversation?.DialogueLines == null) return null;
            foreach (var line in conversation.DialogueLines)
            {
                if (line.LineID != lineID) continue;
                if (line.LocalizedDialogues == null || line.LocalizedDialogues.Count == 0) return null;
                return line.LocalizedDialogues[0].Text;
            }
            return null;
        }

        private static string TruncatePreview(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace('\n', ' ').Replace('\r', ' ');
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "…";
        }
    }
}
