using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [HelpURL(
        "https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1DialogueLinesPropertyDrawer.html")]
    [CustomPropertyDrawer(typeof(WitWeaverConversationData.DialogueLineInfo))]
    public class DialogueLinesPropertyDrawer : PropertyDrawer
    {
        // ──────────────────────────────────────────────
        // Constants & Static Caches
        // ──────────────────────────────────────────────
        private const float k_Spacing = 2f;
        private const float k_Pad = 4f;
        private const float k_MinPreviewHeight = 100f;
        private const float k_MaxPreviewHeight = 120f;
        private const float k_TooltipMaxWidth = 260f;

        // Foldout states per line
        private static readonly Dictionary<string, bool> CharacterRepresentationFoldouts = new();
        private static readonly Dictionary<string, bool> DialogueLineFoldouts = new();

        // Cached styles
        private static readonly GUIStyle s_HeaderStyle = new(EditorStyles.boldLabel)
        {
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft
        };

        private static readonly GUIStyle s_WrappedLabel = new(EditorStyles.label) { wordWrap = true };
        private static readonly GUIStyle s_HelpBox = new(EditorStyles.helpBox) { wordWrap = true, fontSize = 11 };
        private static readonly GUIStyle s_FoldoutBold = new(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

        // Header colors
        private static readonly Color s_HeaderColorPro = new(1f, 1f, 1f, 0.06f);
        private static readonly Color s_HeaderColorLight = new(0f, 0f, 0f, 0.08f);

        // Cached GUIContent
        private static readonly GUIContent GC_ConversationID = new("Conversation ID");
        private static readonly GUIContent GC_LineIndex = new("Line Index");
        private static readonly GUIContent GC_CharacterID = new("Character ID");
        private static readonly GUIContent GC_InputMethod = new("Input Method:");
        private static readonly GUIContent GC_DisplayDuration = new("Display Duration (seconds):");
        private static readonly GUIContent GC_PresentationMode = new("Presentation Mode:",
            "Controls whether this line displays text, plays audio, or both.");
        private static readonly GUIContent GC_BeforeActions = new("Actions Before Line:");
        private static readonly GUIContent GC_AfterActions = new("Actions After Line:");
        private static readonly GUIContent GC_Expression = new("Expression");
        
        private static readonly GUIContent GC_ContinuationHeader = new("Line Continuation");
        private static readonly GUIContent GC_ContinuationMode   = new("Continuation Mode:");
        private static readonly GUIContent GC_BranchKey         = new("Branch Key:");
        private static readonly GUIContent GC_PushReturnPoint   = new("Push Return Point:");
        private static readonly GUIContent GC_Choices            = new("Choices");
        private static readonly GUIContent GC_ChoiceTargetContainer = new("Target Container");
        private static readonly GUIContent GC_ChoiceTargetAlias     = new("Target Alias/Name");
        private static readonly GUIContent GC_AllowGoBack        = new("Allow Go Back",
            "When enabled, a '← Go Back' option is appended to the choice list at runtime. " +
            "If selected, the player is taken back to the previous dialogue line.");
        private static readonly GUIContent GC_TargetLine         = new("Target Line:",
            "Line in this conversation to jump to when this line completes.");


        // Preview header text cache
        private static readonly Dictionary<int, string> s_PreviewCache = new(128);
        private static double s_LastCachePurgeTime;
        private static readonly GUIContent s_HeaderContent = new();
        private static readonly Dictionary<string, bool> EditOverflowToggles = new();

        private static int GetIndexFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return -1;

            int end = path.LastIndexOf(']');
            if (end <= 0)
                return -1;

            int start = path.LastIndexOf('[', end);
            if (start < 0 || end - start < 2)
                return -1;

            int length = end - start - 1;
            int result = -1;
            if (int.TryParse(path.AsSpan(start + 1, length), out int idx))
                result = idx;

            return result;
        }

        /// <summary>
        /// Customizes the rendering of a dialogue line property's GUI in the Unity Editor. This method handles
        /// drawing the individual sections of the dialogue line, including the header, basic info, input methods,
        /// audio clips, localized dialogues, action lists, character representation, and line continuation settings.
        /// </summary>
        /// <param name="position">The position and dimensions where the property should be rendered in the Inspector.</param>
        /// <param name="property">The serialized property representing the dialogue line to be rendered and edited.</param>
        /// <param name="label">The label associated with the property in the Inspector.</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null || property.serializedObject == null)
                return;

            var evt = Event.current;
            var so = property.serializedObject;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect rect = new(position.x, position.y, position.width, lineHeight);

            // Header
            int lineIndex = GetIndexFromPath(property.propertyPath);
            string previewText = GetCachedPreviewText(property);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? s_HeaderColorPro : s_HeaderColorLight);
            s_HeaderContent.text = $"Dialogue Line {lineIndex}: {previewText}";
            EditorGUI.LabelField(rect, s_HeaderContent, s_HeaderStyle);
            rect.y += lineHeight + k_Spacing;

            if (evt.type == EventType.Layout)
                return;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            EditorGUI.indentLevel++;

            rect = DrawBasicInfo(rect, property);
            rect = DrawInputMethod(rect, property);
            rect = DrawPresentationMode(rect, property);
            rect = DrawLocalizedDialogues(rect, property);
            rect = DrawActionsList(rect, property);
            rect = DrawCharacterRepresentation(rect, property);
            rect = DrawLineContinuation(rect, property);
            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                if (so.targetObject is WitWeaverConversationData conversation)
                {
                    conversation.ValidateAndFixDialogueLines();
                    EditorUtility.SetDirty(conversation);
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Renders the line continuation settings for a dialogue line in the Unity Inspector.
        /// This includes configurable options such as continuation mode, target container,
        /// alias or name of the target, and whether to push a return point.
        /// </summary>
        /// <param name="rect">The current rendering area in the Inspector, defining the layout for this section.</param>
        /// <param name="property">The serialized property representing the line continuation settings,
        /// containing the relevant data to be displayed and modified.</param>
        /// <returns>The updated rendering area rectangle after the section has been drawn, accounting for
        /// the height of the rendered fields and any spacing.</returns>
        private static Rect DrawLineContinuation(Rect rect, SerializedProperty property)
        {
            var contProp = property.FindPropertyRelative("LineContinuationSettings");
            if (contProp == null) return rect;

            var modeProp = contProp.FindPropertyRelative("Mode");
            var containerProp = contProp.FindPropertyRelative("TargetContainer");
            var aliasProp = contProp.FindPropertyRelative("TargetAliasOrName");
            var pushReturnProp = contProp.FindPropertyRelative("PushReturnPoint");

            float line = EditorGUIUtility.singleLineHeight;

            EditorGUI.LabelField(rect, GC_ContinuationHeader, EditorStyles.boldLabel);
            rect.y += line + k_Spacing;

            var mode = (WitWeaverConversationData.LineContinuationMode)modeProp.enumValueIndex;
            mode = (WitWeaverConversationData.LineContinuationMode)EditorGUI.EnumPopup(rect, GC_ContinuationMode, mode);
            modeProp.enumValueIndex = (int)mode;
            rect.y += line + k_Spacing;

            if (mode == WitWeaverConversationData.LineContinuationMode.ContainerBranch)
            {
                if (containerProp != null)
                {
                    EditorGUI.PropertyField(rect, containerProp, new GUIContent("Target Container"));
                    rect.y += EditorGUI.GetPropertyHeight(containerProp, true) + k_Spacing;

                    if (IsPlaylistContainer(containerProp))
                    {
                        var warnRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight * 2f);
                        EditorGUI.HelpBox(warnRect,
                            "Playlist containers can't be branch targets — use a Selector container, " +
                            "or target its first conversation directly.", MessageType.Warning);
                        rect.y += warnRect.height + k_Spacing;
                    }
                }

                if (aliasProp != null)
                {
                    EditorGUI.PropertyField(rect, aliasProp, new GUIContent("Target Alias/Name"));
                    rect.y += EditorGUI.GetPropertyHeight(aliasProp, true) + k_Spacing;
                }

                if (pushReturnProp != null)
                {
                    EditorGUI.PropertyField(rect, pushReturnProp, GC_PushReturnPoint);
                    rect.y += EditorGUI.GetPropertyHeight(pushReturnProp, true) + k_Spacing;
                }
            }
            else if (mode == WitWeaverConversationData.LineContinuationMode.PlayerChoice)
            {
                var allowGoBackProp = contProp.FindPropertyRelative("AllowGoBack");
                if (allowGoBackProp != null)
                {
                    EditorGUI.PropertyField(rect, allowGoBackProp, GC_AllowGoBack);
                    rect.y += EditorGUI.GetPropertyHeight(allowGoBackProp, true) + k_Spacing;
                }

                var choicesProp = contProp.FindPropertyRelative("Choices");
                if (choicesProp != null)
                {
                    rect = DrawChoicesArray(rect, choicesProp);
                }
            }
            else if (mode == WitWeaverConversationData.LineContinuationMode.GoToLine)
            {
                var targetLineProp = contProp.FindPropertyRelative("TargetLineID");
                if (targetLineProp != null)
                {
                    EditorGUI.PropertyField(rect, targetLineProp, GC_TargetLine);
                    rect.y += EditorGUI.GetPropertyHeight(targetLineProp, true) + k_Spacing;
                }

                if (pushReturnProp != null)
                {
                    EditorGUI.PropertyField(rect, pushReturnProp, GC_PushReturnPoint);
                    rect.y += EditorGUI.GetPropertyHeight(pushReturnProp, true) + k_Spacing;
                }
            }

            return rect;
        }

        /// <summary>True when the property references a ConversationContainer in Playlist mode.</summary>
        private static bool IsPlaylistContainer(SerializedProperty containerProp) =>
            containerProp.objectReferenceValue is ConversationContainer container &&
            container.ContainerMode == ConversationContainerMode.Playlist;

        /// <summary>
        /// Manually draws the Choices array with per-element labels (e.g. "Choice 1: Hello") instead
        /// of the default "Element 0" labels Unity would show via a plain PropertyField on the list.
        /// </summary>
        private static Rect DrawChoicesArray(Rect rect, SerializedProperty choicesProp)
        {
            float line = EditorGUIUtility.singleLineHeight;

            choicesProp.isExpanded = EditorGUI.Foldout(rect, choicesProp.isExpanded, GC_Choices, true, EditorStyles.foldoutHeader);
            rect.y += line + k_Spacing;

            if (!choicesProp.isExpanded) return rect;

            EditorGUI.indentLevel++;

            int newSize = EditorGUI.IntField(rect, "Size", choicesProp.arraySize);
            if (newSize != choicesProp.arraySize && newSize >= 0)
                choicesProp.arraySize = newSize;
            rect.y += line + k_Spacing;

            var supported = WitWeaverChoiceLabelsDrawer.GetSupportedLanguages();

            for (int i = 0; i < choicesProp.arraySize; i++)
            {
                var element = choicesProp.GetArrayElementAtIndex(i);
                var label   = GetChoiceElementLabel(element, i);
                rect = DrawChoiceElement(rect, element, label, supported);
                rect.y += k_Spacing; // Matches the per-element spacing in GetChoicesArrayHeight
            }

            EditorGUI.indentLevel--;
            return rect;
        }

        /// <summary>
        /// Draws a single ChoiceOption. The Labels list is replaced by one fixed text row per
        /// language cached on the conversation asset; the remaining fields draw normally.
        /// Label rows (and the sync that backs them) are only produced for expanded choices, so a
        /// conversation with hundreds of collapsed lines pays no cost for them.
        /// </summary>
        private static Rect DrawChoiceElement(Rect rect, SerializedProperty element, GUIContent label,
            IList<string> supported)
        {
            float line = EditorGUIUtility.singleLineHeight;

            element.isExpanded = EditorGUI.Foldout(
                new Rect(rect.x, rect.y, rect.width, line), element.isExpanded, label, true);
            rect.y += line + k_Spacing;

            if (!element.isExpanded) return rect;

            EditorGUI.indentLevel++;

            rect = WitWeaverChoiceLabelsDrawer.Draw(rect, element.FindPropertyRelative("Labels"), supported);

            rect = DrawChoiceField(rect, element, "TargetContainer", GC_ChoiceTargetContainer);
            rect = DrawChoiceField(rect, element, "TargetAliasOrName", GC_ChoiceTargetAlias);
            rect = DrawChoiceField(rect, element, "PushReturnPoint", GC_PushReturnPoint);

            EditorGUI.indentLevel--;
            return rect;
        }

        private static Rect DrawChoiceField(Rect rect, SerializedProperty element, string relativeName,
            GUIContent label)
        {
            var prop = element.FindPropertyRelative(relativeName);
            if (prop == null) return rect;

            float h = EditorGUI.GetPropertyHeight(prop, true);
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, h), prop, label, true);
            rect.y += h + k_Spacing;
            return rect;
        }

        /// <summary>
        /// Height consumed by <see cref="DrawChoiceElement"/> so it matches exactly.
        /// </summary>
        private static float GetChoiceElementHeight(SerializedProperty element, IList<string> supported)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float h    = line + k_Spacing; // Foldout header

            if (!element.isExpanded) return h;

            h += WitWeaverChoiceLabelsDrawer.GetHeight(element.FindPropertyRelative("Labels"), supported);
            h += GetChoiceFieldHeight(element, "TargetContainer");
            h += GetChoiceFieldHeight(element, "TargetAliasOrName");
            h += GetChoiceFieldHeight(element, "PushReturnPoint");

            return h;
        }

        private static float GetChoiceFieldHeight(SerializedProperty element, string relativeName)
        {
            var prop = element.FindPropertyRelative(relativeName);
            return prop == null ? 0f : EditorGUI.GetPropertyHeight(prop, true) + k_Spacing;
        }

        /// <summary>
        /// Returns the height consumed by <see cref="DrawChoicesArray"/> so it matches exactly.
        /// </summary>
        private static float GetChoicesArrayHeight(SerializedProperty choicesProp)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float h    = line + k_Spacing; // foldout header

            if (!choicesProp.isExpanded) return h;

            h += line + k_Spacing; // Size field

            var supported = WitWeaverChoiceLabelsDrawer.GetSupportedLanguages();

            for (int i = 0; i < choicesProp.arraySize; i++)
            {
                var element = choicesProp.GetArrayElementAtIndex(i);
                h += GetChoiceElementHeight(element, supported) + k_Spacing;
            }

            return h;
        }

        /// <summary>
        /// Builds a GUIContent label for a single ChoiceOption element.
        /// Shows "Choice N: &lt;first non-empty label text&gt;" when text is available,
        /// otherwise falls back to "Choice N".
        /// </summary>
        private static GUIContent GetChoiceElementLabel(SerializedProperty element, int index)
        {
            var labelsProp = element.FindPropertyRelative("Labels");
            if (labelsProp != null)
            {
                for (int i = 0; i < labelsProp.arraySize; i++)
                {
                    var text = labelsProp.GetArrayElementAtIndex(i)
                                        .FindPropertyRelative("Text")?.stringValue;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (text.Length > 30) text = text.Substring(0, 27) + "...";
                    return new GUIContent($"Choice {index + 1}: {text}");
                }
            }
            return new GUIContent($"Choice {index + 1}");
        }

        /// <summary>
        /// Calculates the total height, in pixels, required to render the custom property drawer for a single dialogue line in the Unity Inspector.
        /// This includes the heights of all sections, such as basic information, input method, audio clip, localized dialogues,
        /// actions list, character representation, and line continuation areas.
        /// </summary>
        /// <param name="property">The serialized property representing a dialogue line, containing all relevant data used in rendering.</param>
        /// <param name="label">The label of the property as displayed in the Inspector.</param>
        /// <returns>The total height, in pixels, required to display the custom property drawer.</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float spacing = k_Spacing;
            float total = EditorGUIUtility.singleLineHeight + spacing; // Header

            total += GetBasicInfoHeight();
            total += GetInputMethodHeight(property);
            total += GetPresentationModeHeight(property);
            total += GetLocalizedDialoguesHeight(property);
            total += GetActionsListHeight(property);
            total += GetCharacterRepresentationSectionHeight(property);
            total += GetLineContinuationHeight(property);
            return total;
        }
        private float GetLineContinuationHeight(SerializedProperty lineProp)
        {
            var contProp = lineProp.FindPropertyRelative("LineContinuationSettings");
            if (contProp == null) return 0f;

            var modeProp = contProp.FindPropertyRelative("Mode");
            if (modeProp == null) return 0f;

            float h = 0f;

            h += EditorGUIUtility.singleLineHeight + k_Spacing; // header
            h += EditorGUIUtility.singleLineHeight + k_Spacing; // enum popup

            var mode = (WitWeaverConversationData.LineContinuationMode)modeProp.enumValueIndex;

            if (mode == WitWeaverConversationData.LineContinuationMode.ContainerBranch)
            {
                var containerProp  = contProp.FindPropertyRelative("TargetContainer");
                var aliasProp      = contProp.FindPropertyRelative("TargetAliasOrName");
                var pushReturnProp = contProp.FindPropertyRelative("PushReturnPoint");

                if (containerProp != null)
                {
                    h += EditorGUI.GetPropertyHeight(containerProp, true) + k_Spacing;

                    if (IsPlaylistContainer(containerProp))
                        h += EditorGUIUtility.singleLineHeight * 2f + k_Spacing;
                }

                if (aliasProp != null)
                    h += EditorGUI.GetPropertyHeight(aliasProp, true) + k_Spacing;

                if (pushReturnProp != null)
                    h += EditorGUI.GetPropertyHeight(pushReturnProp, true) + k_Spacing;
            }
            else if (mode == WitWeaverConversationData.LineContinuationMode.PlayerChoice)
            {
                var allowGoBackProp = contProp.FindPropertyRelative("AllowGoBack");
                if (allowGoBackProp != null)
                    h += EditorGUI.GetPropertyHeight(allowGoBackProp, true) + k_Spacing;

                var choicesProp = contProp.FindPropertyRelative("Choices");
                if (choicesProp != null)
                    h += GetChoicesArrayHeight(choicesProp);
            }
            else if (mode == WitWeaverConversationData.LineContinuationMode.GoToLine)
            {
                var targetLineProp  = contProp.FindPropertyRelative("TargetLineID");
                var pushReturnProp  = contProp.FindPropertyRelative("PushReturnPoint");

                if (targetLineProp != null)
                    h += EditorGUI.GetPropertyHeight(targetLineProp, true) + k_Spacing;

                if (pushReturnProp != null)
                    h += EditorGUI.GetPropertyHeight(pushReturnProp, true) + k_Spacing;
            }

            return h;
        }

        private float GetBasicInfoHeight() =>
            3 * (EditorGUIUtility.singleLineHeight + k_Spacing);

        private float GetInputMethodHeight(SerializedProperty property)
        {
            var methodProp = property.FindPropertyRelative("UserInputMethod");
            var method = (WitWeaverConversationData.DialogueLineProgressionMethod)methodProp.enumValueIndex;
            float h = EditorGUIUtility.singleLineHeight + k_Spacing;
            if (method == WitWeaverConversationData.DialogueLineProgressionMethod.Timed)
                h += EditorGUIUtility.singleLineHeight + k_Spacing;
            return h;
        }

        private float GetPresentationModeHeight(SerializedProperty property)
        {
            float h = EditorGUIUtility.singleLineHeight + k_Spacing;
            // Add helpbox for AudioOnly + UserInput warning
            var modeProp   = property.FindPropertyRelative("PresentationMode");
            var methodProp = property.FindPropertyRelative("UserInputMethod");
            if (modeProp != null && methodProp != null)
            {
                var mode   = (ConversationPresentationMode)modeProp.enumValueIndex;
                var method = (WitWeaverConversationData.DialogueLineProgressionMethod)methodProp.enumValueIndex;
                if (mode == ConversationPresentationMode.AudioOnly &&
                    method == WitWeaverConversationData.DialogueLineProgressionMethod.UserInput)
                    h += EditorStyles.helpBox.CalcHeight(
                             new GUIContent("UserInput progression on an AudioOnly line will stall the conversation. The runner will auto-coerce this to AudioComplete at runtime, but consider setting it explicitly."),
                             EditorGUIUtility.currentViewWidth - 40f) + k_Spacing;
            }
            return h;
        }

        /// <summary>
        /// Determines the total height, in pixels, required to render the localized dialogues section within the dialogue line UI.
        /// This includes the label, spacing, and a wrapped text preview of the localized dialogue for the current language,
        /// falling back to the first localized entry if a match is not found.
        /// </summary>
        /// <param name="property">The serialized property representing a dialogue line, which contains an array of localized dialogues.</param>
        /// <returns>The height, in pixels, needed to render the localized dialogues section, including text wrapping for the selected or fallback dialogue.</returns>
        private float GetLocalizedDialoguesHeight(SerializedProperty property)
        {
            var modeProp = property.FindPropertyRelative("PresentationMode");
            if (modeProp != null &&
                (ConversationPresentationMode)modeProp.enumValueIndex == ConversationPresentationMode.AudioOnly)
                return 0f;

            var localizedDialoguesProp = property.FindPropertyRelative("LocalizedDialogues");
            if (localizedDialoguesProp == null || !localizedDialoguesProp.isArray ||
                localizedDialoguesProp.arraySize == 0)
                return EditorGUIUtility.singleLineHeight + k_Spacing;

            float height = EditorGUIUtility.singleLineHeight + k_Spacing; // label

            string lang = WitWeaverLanguageManager.Instance?.CurrentLanguage ?? "EN";
            SerializedProperty match = null;

            for (int i = 0; i < localizedDialoguesProp.arraySize; i++)
            {
                var el = localizedDialoguesProp.GetArrayElementAtIndex(i);
                var langProp = el.FindPropertyRelative("Language");
                if (langProp != null && string.Equals(langProp.stringValue, lang, StringComparison.OrdinalIgnoreCase))
                {
                    match = el;
                    break;
                }
            }

            match ??= localizedDialoguesProp.arraySize > 0 ? localizedDialoguesProp.GetArrayElementAtIndex(0) : null;

            if (match != null)
            {
                var textProp = match.FindPropertyRelative("Text");
                var text = textProp?.stringValue ?? "";
                float textHeight = s_WrappedLabel.CalcHeight(new GUIContent(text),
                    Mathf.Max(10f, EditorGUIUtility.currentViewWidth - 80f));
                height += textHeight + k_Spacing;
            }
            else
            {
                height += EditorGUIUtility.singleLineHeight + k_Spacing;
            }

            return height;
        }

        /// <summary>
        /// Calculates the total height required to render the Actions List section within the dialogue line UI.
        /// This includes headers, foldouts for both actions before and after a dialogue line,
        /// and optionally expanded items with their associated buttons for modification.
        /// </summary>
        /// <param name="property">The serialized property representing the Actions List section,
        /// which contains arrays for actions before and after the dialogue line.</param>
        /// <returns>The total height, in pixels, needed to fully render the Actions List section.</returns>
        private float GetActionsListHeight(SerializedProperty property)
        {
            var beforeProp = property.FindPropertyRelative("ActionsBeforeDialogueLine");
            var afterProp = property.FindPropertyRelative("ActionsAfterDialogueLine");
            float h = 0f;
            float line = EditorGUIUtility.singleLineHeight + k_Spacing;

            // Header foldouts
            h += line;
            if (beforeProp.isExpanded)
            {
                for (int i = 0; i < beforeProp.arraySize; i++)
                    h += EditorGUI.GetPropertyHeight(beforeProp.GetArrayElementAtIndex(i), true) + k_Spacing;
                h += line; // buttons
            }

            h += line;
            if (afterProp.isExpanded)
            {
                for (int i = 0; i < afterProp.arraySize; i++)
                    h += EditorGUI.GetPropertyHeight(afterProp.GetArrayElementAtIndex(i), true) + k_Spacing;
                h += line; // buttons
            }

            return h;
        }

        /// <summary>
        /// Calculates the total height required to render the Character Representation section of the dialogue line property UI.
        /// Includes the height for the foldout element, separators, and dynamic elements based on the conversation data.
        /// </summary>
        /// <param name="property">The serialized property representing the dialogue line data, which contains information about character representations.</param>
        /// <returns>The total height, in pixels, needed to render the Character Representation section.</returns>
        private float GetCharacterRepresentationSectionHeight(SerializedProperty property)
        {
            float h = 0f;
            h += EditorGUIUtility.singleLineHeight + k_Spacing; // foldout

    string key =
        $"{property.serializedObject.targetObject.GetEntityId()}_{property.propertyPath}_CharacterRep";
    bool open = !CharacterRepresentationFoldouts.TryGetValue(key, out bool stored) || stored;
    if (open)
    {
        h += 1 + k_Spacing * 3; // separator

        var conversation = property.serializedObject.targetObject as WitWeaverConversationData;
        if (conversation == null)
        {
            h += EditorGUIUtility.singleLineHeight + k_Spacing;
        }
        else
        {
            var profiles = conversation.ConversationParticipantProfiles?.Where(p => p != null).ToList();
            if (profiles == null || profiles.Count == 0)
            {
                h += EditorGUIUtility.singleLineHeight * 2.5f + k_Spacing;
            }
            else
            {
                var listProp = property.FindPropertyRelative("CharacterRepresentations");
                if (listProp == null || !listProp.isArray)
                {
                    h += EditorGUIUtility.singleLineHeight + k_Spacing;
                }
                else
                {
                    int cap = Mathf.Max(1, GetMaxSlotsForEditor());
                    bool hasOverflow = listProp.arraySize > cap;

                    if (hasOverflow)
                    {
                        string msg =
                            $"UI can display {cap} character slot(s), but this line defines {listProp.arraySize}. " +
                            "Only the first slots supported by this UI will be shown at runtime.";

                        float width = Mathf.Max(10f, EditorGUIUtility.currentViewWidth - 80f);
                        float helpH = EditorStyles.helpBox.CalcHeight(new GUIContent(msg), width);

                        h += helpH + k_Spacing;
                        h += EditorGUIUtility.singleLineHeight + k_Spacing; // toggle
                    }

                    h += EditorGUIUtility.singleLineHeight + k_Spacing; // Visible Character Count field

                    int count = Mathf.Max(1, listProp.arraySize);
                    for (int i = 0; i < count; i++)
                    {
                        // Ensure the index is actually within the current array bounds before retrieving
                        if (i >= listProp.arraySize) break;
                        var repElement = listProp.GetArrayElementAtIndex(i);

                        if (i == 0)
                        {
                            h += GetSingleCharacterRepresentationHeight(repElement, property, false);
                        }
                        else
                        {
                            h += GetSingleCharacterRepresentationHeight(repElement, property, true);
                            h += EditorGUIUtility.singleLineHeight + k_Spacing; // remove button row
                        }

                        h += 5f; // padding
                    }
                }
            }
        }

        h += 5f; // padding
    }
    else
    {
        h += 2f;
    }

    return h;
}

            private float GetSingleCharacterRepresentationHeight(
        SerializedProperty repProp,
        SerializedProperty mainProperty,
        bool useRepresentationNameInsteadOfID)
        {
            if (repProp == null)
                return 0f;

            float h = 0f;
            float line = EditorGUIUtility.singleLineHeight + k_Spacing;

            // Section label: "Primary Character", "Secondary Character", etc
            h += line;

            var conversation = repProp.serializedObject.targetObject as WitWeaverConversationData;
            if (conversation == null)
                return h + line;

            var profiles = conversation.ConversationParticipantProfiles?.Where(p => p != null).ToList();
            if (profiles == null || profiles.Count == 0)
                return h + line;

            var selectedRepNameProp       = repProp.FindPropertyRelative("SelectedRepresentationName");
            var selectedExpressionGuidProp = repProp.FindPropertyRelative("SelectedExpressionId");

            if (useRepresentationNameInsteadOfID)
            {
                // Secondary / tertiary characters: character chosen inside this block
                var selectedCharacterIDProp = repProp.FindPropertyRelative("SelectedCharacterID");
                h += line; // profile popup

                string charId = selectedCharacterIDProp?.stringValue ?? string.Empty;
                var profile = !string.IsNullOrEmpty(charId)
                    ? profiles.FirstOrDefault(p => p.CharacterID == charId)
                    : null;

                if (profile != null)
                {
                    // Representation popup
                    h += line;

                    // Expression field
                    float emoH = selectedExpressionGuidProp != null
                        ? EditorGUI.GetPropertyHeight(selectedExpressionGuidProp, true)
                        : line;
                    h += emoH + k_Spacing;

                    // Representation-specific options
                    string emoId = selectedExpressionGuidProp?.stringValue ?? string.Empty;
                    var repType = profile.GetRepresentation(selectedRepNameProp?.stringValue ?? string.Empty);
                    if (repType != null)
                    {
                        h += GetRepresentationSpecificOptionsHeight(repType, emoId, repProp);
                    }
                }
            }
            else
            {
                // Primary character: character ID comes from the main line
                var characterIDProp = mainProperty.FindPropertyRelative("characterID");
                string charId = characterIDProp?.stringValue ?? string.Empty;
                var profile = profiles.FirstOrDefault(p => p.CharacterID == charId);

                if (profile == null)
                {
                    // Participant dropdown only
                    h += line;
                    return h;
                }

                // Representation popup
                h += line;

                // Expression field
                float emoH = selectedExpressionGuidProp != null
                    ? EditorGUI.GetPropertyHeight(selectedExpressionGuidProp, true)
                    : line;
                h += emoH + k_Spacing;

                // Representation-specific options
                string emoId = selectedExpressionGuidProp?.stringValue ?? string.Empty;
                var repType = profile.GetRepresentation(selectedRepNameProp?.stringValue ?? string.Empty);
                if (repType != null)
                {
                    h += GetRepresentationSpecificOptionsHeight(repType, emoId, repProp);
                }
            }

            return h;
        }


        /// <summary>
        /// Renders the basic information section for a dialogue line, including conversation ID, line index, and character ID.
        /// </summary>
        /// <param name="rect">The position and dimensions of the current UI area to be drawn.</param>
        /// <param name="property">The serialized property representing the dialogue line data.</param>
        /// <returns>The updated rectangle position and size after rendering the basic information section.</returns>
        private static Rect DrawBasicInfo(Rect rect, SerializedProperty property)
        {
            DrawLabelValue(ref rect, GC_ConversationID.text,
                property.FindPropertyRelative("ConversationID").stringValue);
            DrawLabelValue(ref rect, GC_LineIndex.text,
                property.FindPropertyRelative("ConversationLineIndex").intValue.ToString());
            DrawLabelValue(ref rect, GC_CharacterID.text, property.FindPropertyRelative("characterID").stringValue);
            return rect;
        }

        /// <summary>
        /// Renders the UI for specifying the input method for a dialogue line, including user input interaction or timed progression.
        /// </summary>
        /// <param name="rect">The position and dimensions of the control to be drawn.</param>
        /// <param name="property">The serialized property representing the dialogue line data.</param>
        /// <returns>The updated rectangle position and size after drawing the input method UI.</returns>
        private static Rect DrawInputMethod(Rect rect, SerializedProperty property)
        {
            var methodProp = property.FindPropertyRelative("UserInputMethod");
            var timedValueProp = property.FindPropertyRelative("TimeBeforeNextLine");

            var method = (WitWeaverConversationData.DialogueLineProgressionMethod)methodProp.enumValueIndex;
            method = (WitWeaverConversationData.DialogueLineProgressionMethod)EditorGUI.EnumPopup(rect, GC_InputMethod,
                method);
            methodProp.enumValueIndex = (int)method;
            rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;

            if (method == WitWeaverConversationData.DialogueLineProgressionMethod.Timed)
            {
                EditorGUI.DelayedFloatField(rect, timedValueProp, GC_DisplayDuration);
                rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
            }

            return rect;
        }

        private static Rect DrawPresentationMode(Rect rect, SerializedProperty property)
        {
            var modeProp = property.FindPropertyRelative("PresentationMode");
            if (modeProp == null) return rect;

            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                modeProp, GC_PresentationMode);
            rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;

            // HelpBox warning: AudioOnly + UserInput will stall the conversation
            var methodProp = property.FindPropertyRelative("UserInputMethod");
            if (methodProp != null)
            {
                var mode   = (ConversationPresentationMode)modeProp.enumValueIndex;
                var method = (WitWeaverConversationData.DialogueLineProgressionMethod)methodProp.enumValueIndex;
                if (mode == ConversationPresentationMode.AudioOnly &&
                    method == WitWeaverConversationData.DialogueLineProgressionMethod.UserInput)
                {
                    const string msg = "UserInput progression on an AudioOnly line will stall the conversation. " +
                                       "The runner will auto-coerce this to AudioComplete at runtime, but consider setting it explicitly.";
                    float boxH = EditorStyles.helpBox.CalcHeight(new GUIContent(msg), EditorGUIUtility.currentViewWidth - 40f) + k_Spacing;
                    EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, boxH), msg, MessageType.Warning);
                    rect.y += boxH + k_Spacing;
                }
            }

            return rect;
        }

        private static Rect DrawLocalizedDialogues(Rect rect, SerializedProperty property)
        {
            // Hide dialogue section entirely for AudioOnly lines
            var modeProp = property.FindPropertyRelative("PresentationMode");
            if (modeProp != null &&
                (ConversationPresentationMode)modeProp.enumValueIndex == ConversationPresentationMode.AudioOnly)
                return rect;

            var localizedDialoguesProp = property.FindPropertyRelative("LocalizedDialogues");
            if (localizedDialoguesProp == null || !localizedDialoguesProp.isArray)
            {
                EditorGUI.LabelField(rect, "Localized Dialogues not available.");
                rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
                return rect;
            }

            string lang = WitWeaverLanguageManager.Instance?.CurrentLanguage ?? "EN";
            SerializedProperty match = null;

            for (int i = 0; i < localizedDialoguesProp.arraySize; i++)
            {
                var el = localizedDialoguesProp.GetArrayElementAtIndex(i);
                var langProp = el.FindPropertyRelative("Language");
                if (langProp != null && string.Equals(langProp.stringValue, lang, StringComparison.OrdinalIgnoreCase))
                {
                    match = el;
                    break;
                }
            }

            match ??= localizedDialoguesProp.arraySize > 0 ? localizedDialoguesProp.GetArrayElementAtIndex(0) : null;

            if (match != null)
            {
                var textProp = match.FindPropertyRelative("Text");
                string text = textProp?.stringValue ?? "";
                EditorGUI.LabelField(rect, $"Localized Dialogue ({lang}):");
                rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;

                float textHeight = s_WrappedLabel.CalcHeight(new GUIContent(text), Mathf.Max(10f, EditorGUIUtility.currentViewWidth - 80f));
                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, textHeight), text, s_WrappedLabel);
                rect.y += textHeight + k_Spacing;
            }
            else
            {
                EditorGUI.LabelField(rect, $"No dialogue available for language: {lang}");
                rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
            }

            return rect;
        }

        private static Rect DrawActionsList(Rect rect, SerializedProperty property)
        {
            var beforeProp = property.FindPropertyRelative("ActionsBeforeDialogueLine");
            var afterProp = property.FindPropertyRelative("ActionsAfterDialogueLine");
            float lineHeight = EditorGUIUtility.singleLineHeight;

            // ---- Actions Before Line ----
            beforeProp.isExpanded = EditorGUI.Foldout(
                new Rect(rect.x, rect.y, rect.width, lineHeight),
                beforeProp.isExpanded,
                GC_BeforeActions,
                true
            );
            rect.y += lineHeight + k_Spacing;

            if (beforeProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < beforeProp.arraySize; i++)
                {
                    var el = beforeProp.GetArrayElementAtIndex(i);
                    float h = EditorGUI.GetPropertyHeight(el, true);
                    EditorGUI.PropertyField(
                        new Rect(rect.x, rect.y, rect.width, h),
                        el,
                        new GUIContent($"Element {i}"),
                        true
                    );
                    rect.y += h + k_Spacing;
                }

                Rect buttons = new(rect.x + 14, rect.y, rect.width - 14, lineHeight);
                if (GUI.Button(new Rect(buttons.x, buttons.y, 20, lineHeight), "+"))
                    beforeProp.InsertArrayElementAtIndex(beforeProp.arraySize);
                if (GUI.Button(new Rect(buttons.x + 25, buttons.y, 20, lineHeight), "-") && beforeProp.arraySize > 0)
                    beforeProp.DeleteArrayElementAtIndex(beforeProp.arraySize - 1);

                rect.y += lineHeight + k_Spacing;
                EditorGUI.indentLevel--;
            }

            // ---- Actions After Line ----
            afterProp.isExpanded = EditorGUI.Foldout(
                new Rect(rect.x, rect.y, rect.width, lineHeight),
                afterProp.isExpanded,
                GC_AfterActions,
                true
            );
            rect.y += lineHeight + k_Spacing;

            if (afterProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < afterProp.arraySize; i++)
                {
                    var el = afterProp.GetArrayElementAtIndex(i);
                    float h = EditorGUI.GetPropertyHeight(el, true);
                    EditorGUI.PropertyField(
                        new Rect(rect.x, rect.y, rect.width, h),
                        el,
                        new GUIContent($"Element {i}"),
                        true
                    );
                    rect.y += h + k_Spacing;
                }

                Rect buttons = new(rect.x + 14, rect.y, rect.width - 14, lineHeight);
                if (GUI.Button(new Rect(buttons.x, buttons.y, 20, lineHeight), "+"))
                {
                    afterProp.InsertArrayElementAtIndex(afterProp.arraySize);
                }                
                if (GUI.Button(new Rect(buttons.x + 25, buttons.y, 20, lineHeight), "-") && afterProp.arraySize > 0)
                {
                    afterProp.DeleteArrayElementAtIndex(afterProp.arraySize - 1);
                }
                rect.y += lineHeight + k_Spacing;
                EditorGUI.indentLevel--;
            }

            return rect;
        }


        private Rect DrawCharacterRepresentation(Rect rect, SerializedProperty property)
{
    string foldoutKey =
        $"{property.serializedObject.targetObject.GetEntityId()}_{property.propertyPath}_CharacterRep";
    if (!CharacterRepresentationFoldouts.ContainsKey(foldoutKey))
        CharacterRepresentationFoldouts[foldoutKey] = true;

    bool isExpanded = CharacterRepresentationFoldouts[foldoutKey];
    bool newExpanded = EditorGUI.Foldout(rect, isExpanded, "Character Representations", true, s_FoldoutBold);
    if (newExpanded != isExpanded)
        CharacterRepresentationFoldouts[foldoutKey] = newExpanded;

    rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;

    if (!newExpanded)
        return rect;

    rect.y += k_Spacing;
    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), new Color(0.5f, 0.5f, 0.5f, 1));
    rect.y += 1 + k_Spacing * 2;

    var conversation = property.serializedObject.targetObject as WitWeaverConversationData;
    if (conversation == null)
    {
        EditorGUI.indentLevel++;
        EditorGUI.LabelField(rect, "Error: Conversation data is missing.");
        rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
        EditorGUI.indentLevel--;
        return rect;
    }

    var validProfiles = conversation.ConversationParticipantProfiles.Where(p => p != null).ToList();
    if (validProfiles.Count == 0)
    {
        EditorGUI.indentLevel++;
        Rect helpRect = new(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight * 2.5f);
        EditorGUI.LabelField(helpRect,
            "No conversation participants are configured. Please add character profiles to the ConversationParticipantProfiles list.",
            s_HelpBox);
        rect.y += EditorGUIUtility.singleLineHeight * 2.5f + k_Spacing;
        EditorGUI.indentLevel--;
        return rect;
    }

    EditorGUI.indentLevel++;

    var speakerIdProp = property.FindPropertyRelative("characterID");
    var listProp = property.FindPropertyRelative("CharacterRepresentations");

    if (listProp == null || !listProp.isArray)
    {
        EditorGUI.LabelField(rect, "Error: CharacterRepresentations list is missing on DialogueLineInfo.");
        rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
        EditorGUI.indentLevel--;
        return rect;
    }

    if (listProp.arraySize < 1)
    {
        listProp.arraySize = 1;
        property.serializedObject.ApplyModifiedProperties();
    }

    bool shouldMigrate = true;
    if (listProp.arraySize > 0)
    {
        var e0 = listProp.GetArrayElementAtIndex(0);
        shouldMigrate = !HasAnyRepSelection(e0);
    }

    if (shouldMigrate)
    {
        var primaryLegacy = property.FindPropertyRelative("PrimaryCharacterRepresentation");
        var secondaryLegacy = property.FindPropertyRelative("SecondaryCharacterRepresentation");
        var tertiaryLegacy = property.FindPropertyRelative("TertiaryCharacterRepresentation");

        if (primaryLegacy != null)
            CopyRepData(primaryLegacy, listProp.GetArrayElementAtIndex(0));

        while (listProp.arraySize > 1)
            listProp.DeleteArrayElementAtIndex(listProp.arraySize - 1);

        if (secondaryLegacy != null && HasAnyRepSelection(secondaryLegacy))
        {
            int i = listProp.arraySize;
            listProp.arraySize++;
            CopyRepData(secondaryLegacy, listProp.GetArrayElementAtIndex(i));
        }

        if (tertiaryLegacy != null && HasAnyRepSelection(tertiaryLegacy))
        {
            int i = listProp.arraySize;
            listProp.arraySize++;
            CopyRepData(tertiaryLegacy, listProp.GetArrayElementAtIndex(i));
        }

        property.serializedObject.ApplyModifiedProperties();
    }

    int cap = Mathf.Max(1, GetMaxSlotsForEditor());
    bool hasOverflow = listProp.arraySize > cap;

    string overflowKey = $"{property.serializedObject.targetObject.GetEntityId()}_{property.propertyPath}_EditOverflow";
    if (!EditOverflowToggles.ContainsKey(overflowKey))
        EditOverflowToggles[overflowKey] = false;

    if (hasOverflow)
    {
        string msg =
            $"UI can display {cap} character slot(s), but this line defines {listProp.arraySize}. " +
            "Only the first slots supported by this UI will be shown at runtime.";

        float helpH = EditorStyles.helpBox.CalcHeight(new GUIContent(msg), rect.width);
        EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, helpH), msg, MessageType.Warning);
        rect.y += helpH + k_Spacing;

        EditOverflowToggles[overflowKey] = EditorGUI.ToggleLeft(
            new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
            "Edit overflow entries anyway",
            EditOverflowToggles[overflowKey]);

        rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
    }

    bool editOverflow = EditOverflowToggles[overflowKey];

    int newSize = Mathf.Max(1, EditorGUI.IntField(
        new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
        "Visible Character Count",
        listProp.arraySize));
    rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;

    if (newSize != listProp.arraySize)
    {
        listProp.arraySize = newSize;
        property.serializedObject.ApplyModifiedProperties();
    }

    for (int i = 0; i < listProp.arraySize; i++)
    {
        var repElement = listProp.GetArrayElementAtIndex(i);
        bool isOverflow = i >= cap;

        string label;
        if (i == 0)
        {
            label = "Speaker";
        }
        else
        {
            label = $"Visible Character {i + 1}";
        }
        if (isOverflow)
        {
            label += " (Overflow)";
        }

        using (new EditorGUI.DisabledScope(isOverflow && !editOverflow))
        {
            if (i == 0)
            {
                rect = DrawSingleCharacterRepresentation(
                    rect,
                    repElement,
                    label,
                    speakerIdProp,
                    k_Spacing,
                    useRepresentationNameInsteadOfID: false,
                    conversation);
            }
            else
            {
                rect = DrawSingleCharacterRepresentation(
                    rect,
                    repElement,
                    label,
                    repElement,
                    k_Spacing,
                    useRepresentationNameInsteadOfID: true,
                    conversation);
            }
        }

        if (i > 0)
        {
            var removeRect = new Rect(rect.x, rect.y, 120f, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(removeRect, "Remove"))
            {
                listProp.DeleteArrayElementAtIndex(i);
                property.serializedObject.ApplyModifiedProperties();
                break;
            }
            rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
        }

        rect.y += 5f;
    }

    EditorGUI.indentLevel--;
    rect.y += 5f;
    return rect;
}
        // Helper used by DrawCharacterRepresentation
        private static int GetMaxSlotsForEditor()
        {
            int v = WitWeaverEditorPresentationContext.MaxVisibleCharacterSlotsOverride ?? 3;
            return Mathf.Clamp(v, 1, 32);
        }
        
        private static bool HasAnyRepSelection(SerializedProperty repProp)
        {
            if (repProp == null) return false;

            var charId = repProp.FindPropertyRelative("SelectedCharacterID")?.stringValue;
            var repName = repProp.FindPropertyRelative("SelectedRepresentationName")?.stringValue;
            var repObj = repProp.FindPropertyRelative("SelectedRepresentation")?.objectReferenceValue;
            var exprId = repProp.FindPropertyRelative("SelectedExpressionId")?.stringValue;

            return !string.IsNullOrEmpty(charId)
                   || !string.IsNullOrEmpty(repName)
                   || repObj != null
                   || !string.IsNullOrEmpty(exprId);
        }

        private static void CopyRepData(SerializedProperty src, SerializedProperty dst)
        {
            if (src == null || dst == null) return;

            var srcCharId = src.FindPropertyRelative("SelectedCharacterID");
            var srcRepName = src.FindPropertyRelative("SelectedRepresentationName");
            var srcRepObj = src.FindPropertyRelative("SelectedRepresentation");
            var srcExprId = src.FindPropertyRelative("SelectedExpressionId");

            var dstCharId = dst.FindPropertyRelative("SelectedCharacterID");
            var dstRepName = dst.FindPropertyRelative("SelectedRepresentationName");
            var dstRepObj = dst.FindPropertyRelative("SelectedRepresentation");
            var dstExprId = dst.FindPropertyRelative("SelectedExpressionId");

            if (dstCharId != null && srcCharId != null) dstCharId.stringValue = srcCharId.stringValue;
            if (dstRepName != null && srcRepName != null) dstRepName.stringValue = srcRepName.stringValue;
            if (dstExprId != null && srcExprId != null) dstExprId.stringValue = srcExprId.stringValue;
            if (dstRepObj != null && srcRepObj != null) dstRepObj.objectReferenceValue = srcRepObj.objectReferenceValue;
        }
        
        private static readonly Dictionary<EntityId, GUIContent[]> _profilePopupCache = new();

        private static GUIContent[] GetCachedProfileNames(WitWeaverConversationData conversation, List<WitWeaverCharacterProfileBaseData> profiles)
        {
            EntityId id = conversation.GetEntityId();
            if (_profilePopupCache.TryGetValue(id, out var arr))
                return arr;

            var list = new List<GUIContent>(profiles.Count + 1) { new GUIContent("None") };
            foreach (var p in profiles)
            {
                if (p != null && !string.IsNullOrEmpty(p.CharacterName))
                    list.Add(new GUIContent(p.CharacterName));
            }

            arr = list.ToArray();
            _profilePopupCache[id] = arr;
            return arr;
        }

        /// <summary>
        /// Character representation drawing (with hover preview)
        /// </summary>
        /// <returns></returns>
        private Rect DrawSingleCharacterRepresentation(
            Rect rect,
            SerializedProperty representationProp,
            string label,
            SerializedProperty identifierProp,
            float spacing,
            bool useRepresentationNameInsteadOfID,
            WitWeaverConversationData conversation)
        {
            var so = representationProp.serializedObject;

            var selectedCharacterIDProp = representationProp.FindPropertyRelative("SelectedCharacterID");
            var selectedRepNameProp = representationProp.FindPropertyRelative("SelectedRepresentationName");
            var selectedRepProp = representationProp.FindPropertyRelative("SelectedRepresentation");
            var selectedExpressionGuidProp = representationProp.FindPropertyRelative("SelectedExpressionId");

            EditorGUI.LabelField(rect, $"{label}:",EditorStyles.boldLabel);
            rect.y += EditorGUIUtility.singleLineHeight + spacing;

            if (conversation == null)
            {
                EditorGUI.LabelField(rect, "Error: Conversation data is missing.");
                rect.y += EditorGUIUtility.singleLineHeight + spacing;
                return rect;
            }

            var validProfiles = conversation.ConversationParticipantProfiles.Where(p => p != null).ToList();
            if (validProfiles.Count == 0)
            {
                EditorGUI.LabelField(rect, "No participants available.");
                rect.y += EditorGUIUtility.singleLineHeight + spacing;
                return rect;
            }

            CharacterRepresentationBase selectedRepresentation = null;
            Rect expressionRect = Rect.zero;
            IEditorPreviewableRepresentation previewable = null;

            if (useRepresentationNameInsteadOfID)
            {
                // secondary / tertiary
                string currentCharacterID = selectedCharacterIDProp.stringValue;
                var currentProfile = !string.IsNullOrEmpty(currentCharacterID)
                    ? validProfiles.FirstOrDefault(p => p.CharacterID == currentCharacterID)
                    : null;

                var popupNames = GetCachedProfileNames(conversation, validProfiles);
                int currentIndex = 0;
                if (currentProfile != null)
                {
                    for (int i = 1; i < popupNames.Length; i++)
                        if (popupNames[i].text == currentProfile.CharacterName)
                        { currentIndex = i; break; }
                }

                int newIndex = EditorGUI.Popup(rect, new GUIContent($"{label} Profile:"), currentIndex, popupNames);
                rect.y += EditorGUIUtility.singleLineHeight + spacing;

                if (newIndex != currentIndex)
                {
                    if (newIndex == 0)
                    {
                        selectedCharacterIDProp.stringValue = "";
                        selectedRepNameProp.stringValue = "";
                        selectedExpressionGuidProp.stringValue = "";
                        if (selectedRepProp != null) selectedRepProp.objectReferenceValue = null;
                        so.ApplyModifiedProperties();
                        return rect;
                    }
                    else
                    {
                        var selProfile = validProfiles[newIndex - 1];
                        selectedCharacterIDProp.stringValue = selProfile.CharacterID;

                        var firstRep = selProfile.Representations?.FirstOrDefault(r => r != null);
                        selectedRepNameProp.stringValue = firstRep?.CharacterRepresentationName ?? "";
                        selectedExpressionGuidProp.stringValue = "";
                        if (selectedRepProp != null)
                            selectedRepProp.objectReferenceValue = firstRep?.CharacterRepresentationType;
                        so.ApplyModifiedProperties();
                        currentProfile = selProfile;
                    }
                }

                if (currentProfile == null)
                    return rect;

                var repNames = currentProfile.Representations
                    .Where(r => r != null && !string.IsNullOrEmpty(r.CharacterRepresentationName))
                    .Select(r => r.CharacterRepresentationName)
                    .ToList();

                if (repNames.Count > 0)
                {
                    string repName = selectedRepNameProp.stringValue;
                    int repIdx = Mathf.Max(0, repNames.IndexOf(repName));
                    repIdx = EditorGUI.Popup(rect, "Representation:", repIdx, repNames.ToArray());
                    string newRepName = repNames[repIdx];
                    rect.y += EditorGUIUtility.singleLineHeight + spacing;

                    if (newRepName != repName)
                    {
                        selectedRepNameProp.stringValue = newRepName;
                        selectedExpressionGuidProp.stringValue = "";
                        if (selectedRepProp != null)
                            selectedRepProp.objectReferenceValue = currentProfile.GetRepresentation(newRepName);
                        so.ApplyModifiedProperties();
                    }

                    selectedRepresentation = currentProfile.GetRepresentation(newRepName);

                    if (selectedExpressionGuidProp != null)
                    {
                        float emoH = EditorGUI.GetPropertyHeight(selectedExpressionGuidProp, true);
                        expressionRect = new Rect(rect.x, rect.y, rect.width, emoH);
                        EditorGUI.PropertyField(expressionRect,
                            selectedExpressionGuidProp, GC_Expression, true);
                        rect.y += emoH + spacing;
                    }

                    if (selectedRepProp?.objectReferenceValue is IEditorPreviewableRepresentation prA)
                        previewable = prA;
                    else if (selectedRepresentation is IEditorPreviewableRepresentation prB)
                        previewable = prB;
                }
            }
            else
            {
                // primary
                string characterID = identifierProp.stringValue;
                var profile = validProfiles.FirstOrDefault(p => p.CharacterID == characterID);
                if (profile == null)
                {
                    var names = validProfiles.Where(p => !string.IsNullOrEmpty(p.CharacterName))
                        .Select(p => p.CharacterName).ToArray();
                    int idx = EditorGUI.Popup(rect, $"{label} Participant:", 0, names);
                    rect.y += EditorGUIUtility.singleLineHeight + spacing;
                    if (names.Length > 0)
                    {
                        var chosen = validProfiles[idx];
                        identifierProp.stringValue = chosen.CharacterID;
                        so.ApplyModifiedProperties();
                    }
                    return rect;
                }

                var repNames = profile.Representations
                    .Where(r => r != null && !string.IsNullOrEmpty(r.CharacterRepresentationName))
                    .Select(r => r.CharacterRepresentationName)
                    .ToList();

                if (repNames.Count > 0)
                {
                    string repName = selectedRepNameProp.stringValue;
                    int repIdx = Mathf.Max(0, repNames.IndexOf(repName));
                    repIdx = EditorGUI.Popup(rect, "Representation:", repIdx, repNames.ToArray());
                    string newRepName = repNames[repIdx];
                    rect.y += EditorGUIUtility.singleLineHeight + spacing;

                    if (newRepName != repName)
                    {
                        selectedRepNameProp.stringValue = newRepName;
                        if (selectedRepProp != null)
                            selectedRepProp.objectReferenceValue = profile.GetRepresentation(newRepName);
                        selectedExpressionGuidProp.stringValue = "";
                        so.ApplyModifiedProperties();
                    }

                    selectedRepresentation = profile.GetRepresentation(newRepName);

                    if (selectedExpressionGuidProp != null)
                    {
                        float emoH = EditorGUI.GetPropertyHeight(selectedExpressionGuidProp, true);
                        expressionRect = new Rect(rect.x, rect.y, rect.width, emoH);
                        EditorGUI.PropertyField(expressionRect,
                            selectedExpressionGuidProp, GC_Expression, true);
                        rect.y += emoH + spacing;
                    }

                    if (selectedRepProp?.objectReferenceValue is IEditorPreviewableRepresentation prA)
                        previewable = prA;
                    else if (selectedRepresentation is IEditorPreviewableRepresentation prB)
                        previewable = prB;
                }
            }

            if (selectedRepresentation != null && selectedExpressionGuidProp != null)
            {
                rect = DrawRepresentationSpecificOptions(rect, representationProp, selectedRepresentation,
                    selectedExpressionGuidProp.stringValue, spacing);
            }

            // Hover preview tooltip (non-interactable)
            if (previewable != null && expressionRect != Rect.zero)
            {
                var evt = Event.current;
                if (expressionRect.Contains(evt.mousePosition))
                {
                    DrawHoverPreviewTooltip(expressionRect, previewable, selectedExpressionGuidProp?.stringValue ?? "");
                }
            }

            return rect;
        }

        /// <summary>
        /// Representation-specific draw options
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="representationProp"></param>
        /// <param name="representation"></param>
        /// <param name="expressionGuid"></param>
        /// <param name="spacing"></param>
        /// <returns></returns>
        private Rect DrawRepresentationSpecificOptions(
            Rect rect,
            SerializedProperty representationProp,
            CharacterRepresentationBase representation,
            string expressionGuid,
            float spacing)
        {
#if UNITY_EDITOR
            if (representation is IDialogueLineEditorCustomizable customizable)
            {
                var displayOptionsProp = representationProp.FindPropertyRelative("LineSpecificDisplayOptions");
                if (displayOptionsProp != null)
                {
                    rect = customizable.DrawDialogueLineOptions(rect, expressionGuid, displayOptionsProp, spacing);
                }
            }
#endif
            return rect;
        }

        /// <summary>
        /// Calculates the height required for displaying specific options
        /// of a character representation based on its type and associated expression.
        /// </summary>
        /// <param name="representation">
        /// The character representation object associated with the dialogue line.
        /// </param>
        /// <param name="expressionGuid">
        /// The unique identifier of the expression associated with the representation.
        /// </param>
        /// <param name="representationProp">
        /// The serialized property containing the data for the character representation.
        /// </param>
        /// <returns>
        /// The height necessary to display the specific options for the representation;
        /// returns 0 if no additional height is required or the representation is not customizable.
        /// </returns>
        private float GetRepresentationSpecificOptionsHeight(
            CharacterRepresentationBase representation,
            string expressionGuid,
            SerializedProperty representationProp)
        {
#if UNITY_EDITOR
            if (representation is IDialogueLineEditorCustomizable customizable)
            {
                var displayOptionsProp = representationProp.FindPropertyRelative("LineSpecificDisplayOptions");
                if (displayOptionsProp != null)
                {
                    return customizable.GetDialogueLineOptionsHeight(expressionGuid, displayOptionsProp);
                }
            }
#endif
            return 0f;
        }

        /// <summary>
        /// Calculates and returns the clamped height for the preview block of the specified object.
        /// </summary>
        /// <param name="previewable">The object implementing <see cref="IEditorPreviewableRepresentation"/> to retrieve the preview height from.</param>
        /// <returns>The clamped preview block height, constrained between a minimum and maximum value, or 0 if the input is null or has no valid height.</returns>
        private static float GetPreviewBlockHeight(IEditorPreviewableRepresentation previewable)
        {
            if (previewable == null) return 0f;
            float desired = previewable.GetPreviewHeight();
            if (desired <= 0f) return 0f;
            return Mathf.Clamp(desired, k_MinPreviewHeight, k_MaxPreviewHeight);
        }

        private static readonly Color s_TooltipBgPro   = new Color(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Color s_TooltipBgLight = new Color(0.90f, 0.90f, 0.90f, 1f);
        private static readonly Color s_TooltipInnerBgPro   = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color s_TooltipInnerBgLight = new Color(0.95f, 0.95f, 0.95f, 1f);

        /// <summary>
        /// Displays a hover tooltip with a preview of the given representation.
        /// </summary>
        /// <param name="anchorRect">The rectangular area used to position the tooltip.</param>
        /// <param name="previewable">The object containing the content to be previewed in the tooltip.</param>
        /// <param name="expressionGuid">The unique identifier for the specific expression to be displayed in the preview.</param>
        private static void DrawHoverPreviewTooltip(
            Rect anchorRect,
            IEditorPreviewableRepresentation previewable,
            string expressionGuid)
        {
            var evt = Event.current;
            if (evt.type != EventType.Repaint)
                return;

            float previewHeight = GetPreviewBlockHeight(previewable);
            if (previewHeight <= 0f)
                return;

            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            float width = Mathf.Min(k_TooltipMaxWidth, inspectorWidth - 16f);
            float height = previewHeight + k_Pad * 2f;

            // Anchor above mouse if possible, otherwise fall back to above control
            Vector2 anchorPos = evt.mousePosition;
            if (!anchorRect.Contains(anchorPos))
            {
                anchorPos = new Vector2(
                    anchorRect.xMin + anchorRect.width * 0.5f,
                    anchorRect.yMin);
            }

            const float verticalOffset = 20f;

            float x = anchorPos.x - width * 0.5f;
            float y = anchorPos.y - height - verticalOffset;

            if (x < 4f) x = 4f;
            if (x + width > inspectorWidth - 4f)
                x = inspectorWidth - width - 4f;

            if (y < 4f)
                y = anchorPos.y + anchorRect.height + verticalOffset;

            Rect outer = new Rect(x, y, width, height);
            Rect inner = new Rect(
                outer.x + k_Pad,
                outer.y + k_Pad,
                outer.width - k_Pad * 2f,
                outer.height - k_Pad * 2f);

            int id = GUIUtility.GetControlID("WitWeaverEmotionHover".GetHashCode(), FocusType.Passive);

            // Opaque outer background
            Color outerBg = EditorGUIUtility.isProSkin ? s_TooltipBgPro : s_TooltipBgLight;
            EditorGUI.DrawRect(outer, outerBg);

            EditorStyles.helpBox.Draw(outer, GUIContent.none, id);

            // Opaque inner background so transparent parts of the sprite see this, not the inspector
            Color innerBg = EditorGUIUtility.isProSkin ? s_TooltipInnerBgPro : s_TooltipInnerBgLight;
            EditorGUI.DrawRect(inner, innerBg);

            object expressionMapping = null;
            if (!string.IsNullOrEmpty(expressionGuid) && previewable is CharacterRepresentationBase repBase)
                expressionMapping = repBase.GetExpressionMappingByGuid(expressionGuid);

            // Draw preview inside the inner rect
            previewable.DrawInlineEditorPreview(expressionMapping, inner);
        }

        /// <summary>
        /// Retrieves a cached preview text representation of a dialogue line.
        /// </summary>
        /// <param name="property">The serialized property representing a dialogue line.</param>
        /// <returns>A string containing the cached preview text.</returns>
        private string GetCachedPreviewText(SerializedProperty property)
        {
            int id = HashCacheKey(property);
            double now = EditorApplication.timeSinceStartup;

            if (s_PreviewCache.TryGetValue(id, out string cached))
                return cached;

            string preview = GetPreviewText(property);
            s_PreviewCache[id] = preview;

            if (now - s_LastCachePurgeTime > 5.0)
            {
                if (s_PreviewCache.Count > 256)
                    s_PreviewCache.Clear();
                s_LastCachePurgeTime = now;
            }
            return preview;
        }

        /// <summary>
        /// Retrieves a preview text from a serialized property representing dialogue line information.
        /// Filters the preview based on the current language setting or provides a fallback text.
        /// </summary>
        /// <param name="property">The serialized property containing localized dialogue data.</param>
        /// <returns>
        /// A string representation of the dialogue preview text. Returns a truncated version if
        /// the text exceeds the maximum length of 60 characters or a fallback string if no suitable text is found.
        /// </returns>
        private string GetPreviewText(SerializedProperty property)
        {
            var localizedDialoguesProp = property.FindPropertyRelative("LocalizedDialogues");
            if (localizedDialoguesProp == null || !localizedDialoguesProp.isArray || localizedDialoguesProp.arraySize == 0)
                return "(No dialogue text)";

            string lang = WitWeaverLanguageManager.Instance?.CurrentLanguage ?? "EN";
            for (int i = 0; i < localizedDialoguesProp.arraySize; i++)
            {
                var el = localizedDialoguesProp.GetArrayElementAtIndex(i);
                var langProp = el.FindPropertyRelative("Language");
                var textProp = el.FindPropertyRelative("Text");
                if (langProp != null && textProp != null &&
                    string.Equals(langProp.stringValue, lang, StringComparison.OrdinalIgnoreCase))
                {
                    string text = textProp.stringValue ?? "";
                    return text.Length > 60 ? text[..60] + "..." : text;
                }
            }

            var firstText = localizedDialoguesProp.GetArrayElementAtIndex(0)?.FindPropertyRelative("Text");
            string fallback = firstText?.stringValue ?? "";
            return fallback.Length > 60 ? fallback[..60] + "..." : fallback;
        }

        /// <summary>
        /// Generates a unique hash key for caching purposes based on the specified serialized property.
        /// </summary>
        /// <param name="property">The serialized property from which the hash key is derived.</param>
        /// <returns>An integer representing the hash cache key for the given property.</returns>
        private static int HashCacheKey(SerializedProperty property)
        {
            unchecked
            {
                int id = property.serializedObject.targetObject.GetEntityId().GetHashCode();
                int pathHash = property.propertyPath?.GetHashCode() ?? 0;
                return (id * 397) ^ pathHash;
            }
        }

        /// <summary>
        /// Renders a label and its corresponding value within the given rectangular area, adjusting the layout for subsequent elements.
        /// </summary>
        /// <param name="rect">The rectangular area in which to draw the label and value. Updated to account for the next element's position after drawing.</param>
        /// <param name="label">The text for the label to be displayed.</param>
        /// <param name="value">The text representing the value to be displayed next to the label.</param>
        private static void DrawLabelValue(ref Rect rect, string label, string value)
        {
            EditorGUI.LabelField(rect, label, value);
            rect.y += EditorGUIUtility.singleLineHeight + k_Spacing;
        }
    }
}