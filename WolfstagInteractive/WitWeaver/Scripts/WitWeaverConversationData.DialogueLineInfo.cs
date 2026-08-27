using System;
using System.Collections.Generic;
using UnityEngine;
namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Controls when a character's prefab is resolved and placed during a conversation.
    /// </summary>
    public enum WitWeaverSpawnTiming
    {
        /// <summary>Resolve and place the character as soon as the conversation begins.</summary>
        OnConversationBegin,
        /// <summary>Resolve and place the character only when they first appear in a dialogue line.</summary>
        OnFirstAppearance
    }

    /// <summary>
    /// Associates a conversation participant (by CharacterID) with a default configuration entry
    /// name on their <see cref="PrefabCharacterRepresentationData"/> asset.
    ///
    /// Resolution chain for an entry name: per-line <see cref="WitWeaverConversationData.CharacterRepresentationData.SelectedConfigurationEntryName"/>
    /// → this participant default → representation asset's default entry.
    /// </summary>
    [Serializable]
    public class ParticipantConfigurationSlot
    {
        [Tooltip("CharacterID of the participant. Must match the profile's CharacterID.")]
        public string CharacterID;

        [Tooltip("Default configuration entry name to use for this participant when no per-line entry is specified.")]
        public string DefaultConfigurationEntryName;

        [Tooltip("When to resolve and place this character's prefab representation.")]
        public WitWeaverSpawnTiming SpawnTiming = WitWeaverSpawnTiming.OnConversationBegin;
    }

public partial class WitWeaverConversationData
    {
        [Serializable]
        public struct CharacterRepresentationData
        {
            [Tooltip("The ID of the selected character profile (for secondary/tertiary characters).")]
            public string SelectedCharacterID;
            public string SelectedRepresentationName;
            public CharacterRepresentationBase SelectedRepresentation;

            // This drawer shows DisplayName but stores GUID from the representation asset.
            [ExpressionIDSelector(nameof(SelectedRepresentation))]
            public string SelectedExpressionId;

            [Tooltip("Name of the configuration entry to use for this line. " +
                     "Overrides the participant default and the representation asset default. " +
                     "Leave empty to use the participant default, or the asset's default entry if no participant default is set.")]
            public string SelectedConfigurationEntryName;

            [Header("Per-Line Display Overrides")]
            [Tooltip("Display options specific to this dialogue line. These override the default expression settings.")]
            public DialogueLineDisplayOptions LineSpecificDisplayOptions;
        }
    }

    public partial class WitWeaverConversationData
    {
        [Serializable]
        public enum DialogueLineProgressionMethod
        {
            UserInput,
            Timed,

            /// <summary>
            /// Advance automatically when the voice clip finishes playing.
            /// If no clip is resolved, advances immediately.
            /// </summary>
            AudioComplete
        }
    }

    public partial class WitWeaverConversationData
    {
        [Serializable]
        public struct LocalizedDialogue
        {
            public string Language;

            [TextArea(2, 6)]
            public string Text; // null or empty = audio-only for this locale

            public AudioClip Clip; // Built-in Unity audio path. Middleware users leave this null.
        }
    }
    public partial class WitWeaverConversationData
    {
        [Serializable]
        public enum LineContinuationMode
        {
            Continue = 0,
            EndConversation = 1,
            ContainerBranch = 2,
            PlayerChoice = 3,

            /// <summary>
            /// Jump to another line within the same conversation, identified by its stable
            /// <see cref="DialogueLineInfo.LineID"/> in <see cref="LineContinuation.TargetLineID"/>.
            /// Enables nonlinear conversation flow (loops, skips, hubs).
            /// </summary>
            GoToLine = 4
        }

        /// <summary>
        /// Represents a single selectable option in a <see cref="LineContinuationMode.PlayerChoice"/> line.
        /// Holds the localized label text and the branch target. Target resolution priority:
        /// a non-empty <see cref="TargetLineID"/> jumps to that line within the current conversation;
        /// otherwise a non-null <see cref="TargetContainer"/> branches to another conversation;
        /// otherwise the conversation ends with a warning.
        /// </summary>
        [Serializable]
        public struct ChoiceOption
        {
            [Tooltip("Localized display text for this choice option.")]
            public List<LocalizedDialogue> Labels;
            public ConversationContainer TargetContainer;
            [ContainerAliasSelector(nameof(TargetContainer))]
            public string TargetAliasOrName;
            public bool PushReturnPoint;

            [Tooltip("LineID of a line in this conversation to jump to when selected. " +
                     "When non-empty this takes priority over Target Container.")]
            [LineIDSelector]
            public string TargetLineID;
        }

        [Serializable]
        public struct LineContinuation
        {
            public LineContinuationMode Mode;
            public ConversationContainer TargetContainer;
            [ContainerAliasSelector(nameof(TargetContainer))]
            public string TargetAliasOrName;
            public bool PushReturnPoint;
            /// <summary>Only populated when Mode == PlayerChoice.</summary>
            public List<ChoiceOption> Choices;
            /// <summary>
            /// When true and Mode == PlayerChoice, a "Go Back" option is appended to the
            /// presented choices at runtime. If selected, the runner revisits the previous
            /// dialogue line, allowing the player to re-read it before committing to a choice.
            /// </summary>
            public bool AllowGoBack;

            [Tooltip("LineID of the line in this conversation to jump to when Mode == GoToLine.")]
            [LineIDSelector]
            public string TargetLineID;
        }

    }
    public partial class WitWeaverConversationData
    {
        [Serializable]
        public class DialogueLineInfo
        {
            public string ConversationID;            // Key or ConversationID in the YAML
            public string LineID; // Stable Line ID
            public int ConversationLineIndex; // Line index within the conversation
            public string characterID; //ID of the character speaking the line
            [Tooltip("Ordered list of visible character representations for this line. Index 0 is the speaker.")]
            public List<CharacterRepresentationData> CharacterRepresentations = new();

            public List<LocalizedDialogue> LocalizedDialogues; // Localized dialogues per language

            // Audio is now either in LocalizedDialogue.Clip (Unity path)
            // or resolved via WitWeaverAudioManifest (middleware path).

            [Tooltip("Controls whether this line displays text, plays audio, or both. Inherits from the conversation default on creation.")]
            public ConversationPresentationMode PresentationMode = ConversationPresentationMode.AudioAndText;

            public List<BaseDialogueLineAction> ActionsBeforeDialogueLine; // Actions before the dialogue line
            public List<BaseDialogueLineAction> ActionsAfterDialogueLine; // Actions after the dialogue line

            public DialogueLineProgressionMethod
                UserInputMethod; // Whether to wait for user input before continuing to the next line

            public float TimeBeforeNextLine; // Time in seconds to wait before continuing to the next line

            public LineContinuation LineContinuationSettings;

            // Add a constructor to ensure initialization
            public DialogueLineInfo(string conversationID)
            {
                ConversationID = conversationID;
                ConversationLineIndex = 0;
                LineID = null;
                characterID = "";
                LocalizedDialogues = new List<LocalizedDialogue>();
                PresentationMode = ConversationPresentationMode.AudioAndText;
                ActionsBeforeDialogueLine = new List<BaseDialogueLineAction>();
                ActionsAfterDialogueLine = new List<BaseDialogueLineAction>();
                UserInputMethod = DialogueLineProgressionMethod.UserInput;
                TimeBeforeNextLine = 0f;
                LineContinuationSettings = new LineContinuation
                {
                    Mode = LineContinuationMode.Continue,
                    TargetAliasOrName = null,
                    TargetContainer = null,
                    PushReturnPoint = false,
                    Choices = null
                };
            }
            public void EnsureCharacterRepresentationListInitialized()
            {
                CharacterRepresentations ??= new List<CharacterRepresentationData>();
                if (CharacterRepresentations.Count == 0)
                {
                    CharacterRepresentations.Add(new CharacterRepresentationData());
                }
            }
            private static bool HasAnySelection(CharacterRepresentationData data)
            {
                return !string.IsNullOrEmpty(data.SelectedCharacterID)
                       || !string.IsNullOrEmpty(data.SelectedRepresentationName)
                       || data.SelectedRepresentation != null
                       || !string.IsNullOrEmpty(data.SelectedExpressionId);
            }
        }
    }
}
