using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreDialogueLocalizationHandler.html")]
    public class ConvoCoreDialogueLocalizationHandler
    {
        private readonly ConvoCoreLanguageManager _convoCoreLanguageManager;

        /// <summary>
        /// Constructor requires a LanguageManager instance for dependency injection.
        /// </summary>
        public ConvoCoreDialogueLocalizationHandler(ConvoCoreLanguageManager convoCoreLanguageManager)
        {
            _convoCoreLanguageManager = convoCoreLanguageManager ?? throw new ArgumentNullException(nameof(convoCoreLanguageManager));
        }

        /// <summary>
        /// Gets localized text and audio clip for a dialogue line, with case-insensitive and base-locale fallback.
        /// Tries: exact -> base (fr-CA -> fr) -> "en" -> base("en") -> first available.
        /// A line succeeds if it resolves either text or a clip (or both).
        /// </summary>
        public LocalizedDialogueResult GetLocalizedDialogue(ConvoCoreConversationData.DialogueLineInfo lineInfo)
        {
            if (lineInfo?.LocalizedDialogues == null || lineInfo.LocalizedDialogues.Count == 0)
            {
                return new LocalizedDialogueResult
                {
                    Success = false,
                    Text = "[Error: Missing Translations]",
                    ErrorMessage = $"LocalizedDialogues is null or empty for line {lineInfo?.ConversationLineIndex} in '{lineInfo?.ConversationID}'"
                };
            }

            // Build case-insensitive maps for text and clips
            var textMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var clipMap = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            foreach (var ld in lineInfo.LocalizedDialogues)
            {
                if (string.IsNullOrEmpty(ld.Language)) continue;
                if (ld.Text != null)
                    textMap[ld.Language] = ld.Text; // last-in wins if duplicates
                if (ld.Clip != null)
                    clipMap[ld.Language] = ld.Clip;
            }

            string requested = _convoCoreLanguageManager?.CurrentLanguage ?? "en";
            string fallback = "en";

            // Run the 5-step fallback chain and return the first hit
            string[] candidates = BuildCandidates(requested, fallback);
            foreach (var lang in candidates)
            {
                bool hasText = textMap.TryGetValue(lang, out var text);
                bool hasClip = clipMap.TryGetValue(lang, out var clip);

                if (!hasText && !hasClip) continue;

                bool isFallback = !string.Equals(lang, requested, StringComparison.OrdinalIgnoreCase);
                return new LocalizedDialogueResult
                {
                    Success = true,
                    Text = hasText ? text : null,
                    ResolvedClip = hasClip ? clip : null,
                    UsedLanguage = lang,
                    IsFallback = isFallback,
                    ErrorMessage = isFallback ? $"Missing '{requested}', using '{lang}'." : null
                };
            }

            // Last resort: first entry that has anything
            foreach (var ld in lineInfo.LocalizedDialogues)
            {
                bool hasText = !string.IsNullOrEmpty(ld.Text);
                bool hasClip = ld.Clip != null;
                if (!hasText && !hasClip) continue;

                return new LocalizedDialogueResult
                {
                    Success = true,
                    Text = hasText ? ld.Text : null,
                    ResolvedClip = hasClip ? ld.Clip : null,
                    UsedLanguage = ld.Language,
                    IsFallback = true,
                    ErrorMessage = $"Missing '{requested}', using '{ld.Language}'."
                };
            }

            // Shouldn't get here, but be safe
            return new LocalizedDialogueResult
            {
                Success = false,
                Text = "[Missing Translation]",
                ErrorMessage = $"No translations available for line {lineInfo.ConversationLineIndex} in '{lineInfo.ConversationID}'"
            };
        }

        private static string[] BuildCandidates(string requested, string fallback)
        {
            var baseReq = BaseLang(requested);
            var baseFb  = BaseLang(fallback);

            // Deduplicated ordered list
            var list = new List<string>(5) { requested };
            if (!baseReq.Equals(requested, StringComparison.OrdinalIgnoreCase)) list.Add(baseReq);
            if (!fallback.Equals(requested, StringComparison.OrdinalIgnoreCase)  &&
                !fallback.Equals(baseReq,   StringComparison.OrdinalIgnoreCase))  list.Add(fallback);
            if (!baseFb.Equals(fallback,   StringComparison.OrdinalIgnoreCase)   &&
                !baseFb.Equals(baseReq,    StringComparison.OrdinalIgnoreCase)   &&
                !baseFb.Equals(requested,  StringComparison.OrdinalIgnoreCase))  list.Add(baseFb);
            return list.ToArray();
        }

        private static string BaseLang(string code)
        {
            if (string.IsNullOrEmpty(code)) return code;
            var i = code.IndexOfAny(new[] { '-', '_' });
            return i > 0 ? code.Substring(0, i) : code;
        }
    }

    public class LocalizedDialogueResult
    {
        public bool Success { get; set; }
        public string Text { get; set; }
        public AudioClip ResolvedClip { get; set; }
        public bool IsAudioOnly => string.IsNullOrEmpty(Text) && ResolvedClip != null;
        public string UsedLanguage { get; set; }
        public bool IsFallback { get; set; }
        public string ErrorMessage { get; set; }
    }
}
