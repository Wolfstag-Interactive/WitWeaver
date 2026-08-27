using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    [HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SpriteCharacterRepresentationData.html")]
[CreateAssetMenu(fileName = "SpriteRepresentation", menuName = "ConvoCore/Character/Representation/Sprite Character Representation")]
    //This class dictates how a sprite should be represented by telling ConvoCore how to get and apply expressions and expression actions 
    public class SpriteCharacterRepresentationData : CharacterRepresentationBase, IExpressionCatalogProvider
#if UNITY_EDITOR
        , IDialogueLineEditorCustomizable
#endif
    {
        public List<SpriteExpressionMapping> ExpressionMappings = new();

        public IReadOnlyList<(string id, string name)> GetExpressionCatalog() =>
            ExpressionMappings.Select(m => (ExpressionId: m.ExpressionID, m.DisplayName)).ToList();

        private bool TryResolveById(string id, out SpriteExpressionMapping mapping)
        {
            mapping = ExpressionMappings.FirstOrDefault(m => m.ExpressionID == id);
            return mapping != null;
        }
        public override void ApplyExpression(string expressionId, ConvoCore runtime, ConvoCoreConversationData conversation, int lineIndex,
            IConvoCoreCharacterDisplay display)
        {
            if (!TryResolveById(expressionId, out var mapping))
            {
                Debug.LogWarning($"[SpriteCharacterRepresentationData] Expression '{expressionId}' not found on '{name}'.");
                return;
            }

            var actions = mapping.ExpressionActions;
            if (actions == null || actions.Count == 0)
                return;

            var ctx = new ExpressionActionContext
            {
                Runtime      = runtime,
                Conversation = conversation,
                LineIndex    = lineIndex,
                Representation = this,
                Display      = display, 
                ExpressionId = mapping.ExpressionID
            };

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action != null)
                    action.ExecuteAction(ctx);
            }
            
        }

        public override object GetExpressionMappingByGuid(string expressionGuid)
        {
            if (string.IsNullOrEmpty(expressionGuid))
                return null;
            
            return ExpressionMappings.FirstOrDefault(m => m.ExpressionID == expressionGuid);
        }

        public override object ProcessExpression(string expressionId)
        {
            if (string.IsNullOrEmpty(expressionId))
            {
                return ExpressionMappings.Count > 0 ? ExpressionMappings[0] : null;
            }

            if (TryResolveById(expressionId, out var byGuid))
                return byGuid;

            Debug.LogWarning($"Sprite expression '{expressionId}' not found; using first mapping as fallback.");
            return ExpressionMappings.Count > 0 ? ExpressionMappings[0] : null;
        }

#if UNITY_EDITOR
        private static IReadOnlyList<ConvoCoreUIFoundation.DisplaySlotDefinition> GetFoundationSlots()
        {
            var runner = Object.FindAnyObjectByType<ConvoCore>();
            var foundation = runner != null ? runner.ConversationUI : null;
            return foundation != null ? foundation.DisplaySlots : null;
        }

        public Rect DrawDialogueLineOptions(Rect rect, string expressionID, SerializedProperty displayOptionsProperty,
            float spacing)
        {
            var slotProp = displayOptionsProperty?.FindPropertyRelative("DisplaySlot");
            if (slotProp == null) return rect;

            var fieldRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            var slots = GetFoundationSlots();

            if (slots != null && slots.Count > 0)
            {
                var names = new string[slots.Count];
                for (int i = 0; i < slots.Count; i++)
                    names[i] = slots[i]?.SlotName ?? $"Slot {i}";

                string currentName = slotProp.stringValue;
                int currentIndex = System.Array.IndexOf(names, currentName);
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUI.Popup(fieldRect, "Display Slot", currentIndex, names);
                if (newIndex != currentIndex || string.IsNullOrEmpty(slotProp.stringValue))
                    slotProp.stringValue = names[newIndex];
            }
            else
            {
                EditorGUI.PropertyField(fieldRect, slotProp, new GUIContent("Display Slot"));
            }

            rect.y = fieldRect.yMax + spacing;
            return rect;
        }

        public float GetDialogueLineOptionsHeight(string expressionID, SerializedProperty displayOptionsProperty)
        {
            return displayOptionsProperty?.FindPropertyRelative("DisplaySlot") != null
                ? EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing
                : 0f;
        }

        public override float GetPreviewHeight() => 84f;

        public override void DrawInlineEditorPreview(object mappingData, Rect position)
        {
            var mapping = (mappingData as SpriteExpressionMapping) ??
                          (ExpressionMappings.Count > 0 ? ExpressionMappings[0] : null);
            if (mapping == null)
            {
                EditorGUI.LabelField(position, "No sprite mapping to preview.");
                return;
            }

            Texture2D portraitTex = mapping.PortraitSprite != null ? mapping.PortraitSprite.texture : null;
            Texture2D fullBodyTex = mapping.FullBodySprite != null ? mapping.FullBodySprite.texture : null;

            // FIX: explicit null checks—can't treat Texture2D as bool
            int count = (portraitTex != null ? 1 : 0) + (fullBodyTex != null ? 1 : 0);
            if (count == 0)
            {
                EditorGUI.LabelField(position, "(No sprites)");
                return;
            }

            const float pad = 4f;
            var inner = new Rect(position.x + pad, position.y + pad,
                position.width - pad * 2f, position.height - pad * 2f);

            if (count == 1)
            {
                var tex = portraitTex != null ? portraitTex : fullBodyTex;
                if (tex)
                {
                    GUI.DrawTexture(FitRectPreserveAspect(inner, tex.width, tex.height), tex, ScaleMode.ScaleToFit, true);
                }
            }
            else
            {
                float slotW = (inner.width - pad) * 0.5f;
                var left = new Rect(inner.x, inner.y, slotW, inner.height);
                var right = new Rect(inner.x + slotW + pad, inner.y, slotW, inner.height);

                if (portraitTex != null)
                    GUI.DrawTexture(FitRectPreserveAspect(left, portraitTex.width, portraitTex.height), portraitTex,
                        ScaleMode.ScaleToFit, true);
                if (fullBodyTex != null)
                    GUI.DrawTexture(FitRectPreserveAspect(right, fullBodyTex.width, fullBodyTex.height), fullBodyTex,
                        ScaleMode.ScaleToFit, true);
            }
        }

        private static Rect FitRectPreserveAspect(Rect container, float texW, float texH)
        {
            if (texW <= 0f || texH <= 0f) return container;

            float ar = texW / texH;
            float targetW = container.width;
            float targetH = targetW / ar;

            if (targetH > container.height)
            {
                targetH = container.height;
                targetW = targetH * ar;
            }

            float x = container.x + (container.width - targetW) * 0.5f;
            float y = container.y + (container.height - targetH) * 0.5f;
            return new Rect(x, y, targetW, targetH);
        }

        private void OnValidate()
        {
            var used = new HashSet<string>();
            foreach (var m in ExpressionMappings)
            {
                if (m == null) continue;
                m.EnsureValidId(used);
                m.EnsureValidBasics();
            }
        }

#endif
    }

    [System.Serializable]
    // This class defines what a sprite expression is
    public sealed class SpriteExpressionMapping
    {
        [SerializeField, Tooltip("Stable unique ID (GUID). Non-editable.")]
        private string expressionID = System.Guid.NewGuid().ToString("N");

        public string ExpressionID => expressionID;

        [Tooltip("Human-readable name shown in dropdowns and inspector list headers.")]
        public string DisplayName = "Neutral";

        [Tooltip("Portrait sprite for the expression.")]
        public Sprite PortraitSprite;

        [Tooltip("Full body sprite for the expression.")]
        public Sprite FullBodySprite;

        [Header("Default Display Options")]
        public DialogueLineDisplayOptions DisplayOptions = new DialogueLineDisplayOptions();
        [Tooltip("Actions that run when this expression is applied on this representation")]
        public List<BaseExpressionAction> ExpressionActions = new();
        public void EnsureValidId(HashSet<string> used)
        {
            if (string.IsNullOrWhiteSpace(expressionID) || !used.Add(expressionID))
                expressionID = System.Guid.NewGuid().ToString("N");
        }

        public void EnsureValidBasics()
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
                DisplayName = "Unnamed";
        }
    }
}