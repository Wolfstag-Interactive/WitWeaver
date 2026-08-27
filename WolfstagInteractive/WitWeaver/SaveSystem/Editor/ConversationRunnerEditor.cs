#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using WolfstagInteractive.ConvoCore.SaveSystem;
using WolfstagInteractive.ConvoCore;
using YamlDotNet.Serialization;

namespace WolfstagInteractive.ConvoCore.SaveSystem.Editor
{
    [CustomEditor(typeof(ConvoCoreConversationSaveManager))]
    public class ConvoCoreConversationSaveManagerEditor : UnityEditor.Editor
    {
        // ── Serialized properties ─────────────────────────────────────────────

        private SerializedProperty _directConversationProp;
        private SerializedProperty _conversationContainerProp;
        private SerializedProperty _activeConversationIndexProp;
        private SerializedProperty _saveManagerProp;
        private SerializedProperty _variableStoreProp;
        private SerializedProperty _defaultStartModeProp;
        private SerializedProperty _restoreBehaviorProp;
        private SerializedProperty _autoCommitOnStartProp;
        private SerializedProperty _autoCommitOnEndProp;
        private SerializedProperty _autoCommitOnLineCompleteProp;
        private SerializedProperty _autoCommitOnChoiceMadeProp;
        private SerializedProperty _autoRestoreOnAwakeProp;
        private SerializedProperty _autoRestoreOnStartProp;

        // ── Repaint timer ─────────────────────────────────────────────────────

        private double _lastRepaintTime;
        private const double k_RepaintInterval = 0.1;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _directConversationProp        = serializedObject.FindProperty("DirectConversation");
            _conversationContainerProp     = serializedObject.FindProperty("ConversationContainer");
            _activeConversationIndexProp   = serializedObject.FindProperty("ActiveConversationIndex");
            _saveManagerProp               = serializedObject.FindProperty("SaveManager");
            _variableStoreProp             = serializedObject.FindProperty("VariableStore");
            _defaultStartModeProp          = serializedObject.FindProperty("_defaultStartMode");
            _restoreBehaviorProp           = serializedObject.FindProperty("_restoreBehavior");
            _autoCommitOnStartProp         = serializedObject.FindProperty("_autoCommitOnStart");
            _autoCommitOnEndProp           = serializedObject.FindProperty("_autoCommitOnEnd");
            _autoCommitOnLineCompleteProp  = serializedObject.FindProperty("_autoCommitOnLineComplete");
            _autoCommitOnChoiceMadeProp    = serializedObject.FindProperty("_autoCommitOnChoiceMade");
            _autoRestoreOnAwakeProp        = serializedObject.FindProperty("_autoRestoreOnAwake");
            _autoRestoreOnStartProp        = serializedObject.FindProperty("_autoRestoreOnStart");

            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup - _lastRepaintTime >= k_RepaintInterval)
            {
                _lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (ConvoCoreConversationSaveManager)target;
            bool hasDirect    = _directConversationProp.objectReferenceValue != null;
            bool hasContainer = _conversationContainerProp.objectReferenceValue != null;

            // === Conversation ===
            EditorGUILayout.LabelField("Conversation", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_directConversationProp,
                new GUIContent("Direct Conversation",
                    "A single ConvoCoreConversationData. Takes priority over Conversation Container."));

            EditorGUILayout.PropertyField(_conversationContainerProp,
                new GUIContent("Conversation Container",
                    "A ConversationContainer. Used when Direct Conversation is not assigned."));

            // Index field only relevant when container is set without a direct override
            if (hasContainer && !hasDirect)
            {
                EditorGUILayout.PropertyField(_activeConversationIndexProp,
                    new GUIContent("Active Conversation Index",
                        "Which entry in the container's Conversations list to track."));
            }

            // Identity panel (title + GUID)
            DrawIdentityPanel(hasDirect, hasContainer);

            EditorGUILayout.Space(6f);

            // === References ===
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_saveManagerProp,
                new GUIContent("Save Manager", "The ConvoCoreSaveManager ScriptableObject used to persist snapshots."));
            EditorGUILayout.PropertyField(_variableStoreProp,
                new GUIContent("Variable Store", "The ConvoVariableStore holding conversation-scoped variables."));

            // Validation warnings
            if (!hasDirect && !hasContainer)
                EditorGUILayout.HelpBox(
                    "No conversation assigned. Add a Direct Conversation or Conversation Container.",
                    MessageType.Warning);

            if (_saveManagerProp.objectReferenceValue == null)
                EditorGUILayout.HelpBox(
                    "Save Manager is not assigned. Save and restore will be skipped at runtime.",
                    MessageType.Warning);

            EditorGUILayout.Space(6f);

            // === Start Behavior ===
            EditorGUILayout.LabelField("Start Behavior", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_defaultStartModeProp,
                new GUIContent("Default Start Mode",
                    "What to do when a saved snapshot is found:\n" +
                    "  Fresh – ignore the snapshot, always start from the beginning.\n" +
                    "  Resume – restore active line and visited-line history.\n" +
                    "  Restart – restore variables but start from the first line."));

            EditorGUILayout.PropertyField(_restoreBehaviorProp,
                new GUIContent("Restore Behavior",
                    "Used when TryAutoRestore is triggered.\n" +
                    "  ResumeFromActiveLine – apply the snapshot and resume.\n" +
                    "  RestartFromBeginning – restore variables, restart from line 0.\n" +
                    "  AskViaEvent – fire OnRestoreDecisionRequired and let the caller decide."));

            EditorGUILayout.Space(6f);

            // === Auto-Commit ===
            EditorGUILayout.LabelField("Auto-Commit", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoCommitOnStartProp,       new GUIContent("On Conversation Start"));
            EditorGUILayout.PropertyField(_autoCommitOnEndProp,         new GUIContent("On Conversation End"));
            EditorGUILayout.PropertyField(_autoCommitOnLineCompleteProp, new GUIContent("On Line Complete"));
            EditorGUILayout.PropertyField(_autoCommitOnChoiceMadeProp,  new GUIContent("On Choice Made"));

            EditorGUILayout.Space(6f);

            // === Auto-Restore ===
            EditorGUILayout.LabelField("Auto-Restore", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoRestoreOnAwakeProp, new GUIContent("On Awake"));
            EditorGUILayout.PropertyField(_autoRestoreOnStartProp, new GUIContent("On Start"));

            // === Play Mode state ===
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8f);
                DrawPlayModeSection(manager);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Identity panel ────────────────────────────────────────────────────

        private void DrawIdentityPanel(bool hasDirect, bool hasContainer)
        {
            ConvoCoreConversationData data = null;

            if (hasDirect)
            {
                data = (ConvoCoreConversationData)_directConversationProp.objectReferenceValue;
            }
            else if (hasContainer)
            {
                var container = (ConversationContainer)_conversationContainerProp.objectReferenceValue;
                int idx = _activeConversationIndexProp.intValue;
                if (container?.Conversations != null && idx >= 0 && idx < container.Conversations.Count)
                    data = container.Conversations[idx]?.ConversationData;
            }

            if (data == null) return;

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Identity", EditorStyles.miniBoldLabel);

                string title = string.IsNullOrEmpty(data.ConversationTitle) ? data.name : data.ConversationTitle;
                EditorGUILayout.LabelField("Title", title);

                string guid = data.ConversationGuid;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("GUID");
                    EditorGUILayout.SelectableLabel(
                        guid ?? "(none)",
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }
        }

        // ── Play-mode section ─────────────────────────────────────────────────

        private void DrawPlayModeSection(ConvoCoreConversationSaveManager manager)
        {
            EditorGUILayout.LabelField("Play Mode State", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Active Line ID",         manager.ActiveLineId ?? "(none)");
                EditorGUILayout.Toggle("Is Complete",               manager.IsComplete);
                EditorGUILayout.IntField("Visited Lines",           manager.VisitedLinesCount);
                EditorGUILayout.Toggle("Is Dirty",                  manager.IsDirty);
                EditorGUILayout.TextField("Last Committed",
                    manager.LastCommitTime != default
                        ? manager.LastCommitTime.ToString("HH:mm:ss")
                        : "(never)");
                EditorGUILayout.IntField("Conversation Variables",  manager.ConversationVariablesCount);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Snapshot"))
                {
                    var snapshot = manager.GetConversationSnapshot();
                    if (snapshot != null)
                    {
                        var serializer = new SerializerBuilder().Build();
                        Debug.Log($"[ConvoCoreConversationSaveManager] Snapshot:\n{serializer.Serialize(snapshot)}");
                    }
                    else
                    {
                        Debug.Log("[ConvoCoreConversationSaveManager] No active conversation — snapshot unavailable.");
                    }
                }

                if (GUILayout.Button("Force Commit"))
                    manager.CommitSnapshot();

                if (GUILayout.Button("Force Restore"))
                    manager.TryAutoRestore();
            }
        }
    }
}
#endif
