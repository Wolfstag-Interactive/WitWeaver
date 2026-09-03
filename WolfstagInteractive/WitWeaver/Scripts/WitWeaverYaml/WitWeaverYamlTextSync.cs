// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Collections.Generic;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// Editor-only helper that keeps the serialized DialogueLines' localized text in sync with
    /// parsed YAML. Text-only by design: it never adds, removes, or reorders lines and never
    /// touches actions, representations, or continuation settings, so it is safe to run
    /// automatically. Structural changes are reported via <see cref="Analyze"/> and left to the
    /// explicit Import From YAML For Key workflow.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverYamlTextSync.html")]
    internal static class WitWeaverYamlTextSync
    {
        /// <summary>
        /// Parses the embedded ConversationYaml on the asset. Returns false when there is no
        /// embedded text or it fails to parse.
        /// </summary>
        internal static bool TryParseEmbedded(WitWeaverConversationData data,
            out Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            dict = null;
            var text = data != null && data.ConversationYaml != null ? data.ConversationYaml.text : null;
            if (string.IsNullOrEmpty(text)) return false;
            return WitWeaverYamlParser.TryParse(text, out dict, out IReadOnlyList<WitWeaverYamlDiagnostic> _) &&
                   dict != null;
        }

        /// <summary>
        /// Merges YAML text into the serialized DialogueLines, matching lines the same way the
        /// runtime does (LineID first, legacy index fallback). Existing per-language AudioClip
        /// references are preserved; only Text changes and languages are added/removed to mirror
        /// the YAML. Returns true when any serialized value changed.
        /// </summary>
        internal static bool MergeLocalizedText(WitWeaverConversationData data,
            Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            if (data?.DialogueLines == null || dict == null) return false;

            bool changed = false;
            foreach (var line in data.DialogueLines)
            {
                if (line == null) continue;

                var config = FindConfig(line, dict);
                if (config?.LocalizedDialogue == null) continue;

                var merged = new List<WitWeaverConversationData.LocalizedDialogue>(config.LocalizedDialogue.Count);
                foreach (var kvp in config.LocalizedDialogue)
                {
                    var entry = new WitWeaverConversationData.LocalizedDialogue
                    {
                        Language = kvp.Key,
                        Text = kvp.Value
                    };

                    // Preserve any authored clip for this language
                    if (line.LocalizedDialogues != null)
                    {
                        foreach (var existing in line.LocalizedDialogues)
                        {
                            if (string.Equals(existing.Language, kvp.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                entry.Clip = existing.Clip;
                                break;
                            }
                        }
                    }

                    merged.Add(entry);
                }

                if (!ListsEqual(line.LocalizedDialogues, merged))
                {
                    line.LocalizedDialogues = merged;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Compares the serialized lines against parsed YAML without mutating anything.
        /// staleTextLines counts matched lines whose localized text differs; yamlOnlyLines counts
        /// YAML entries no serialized line matches (added in YAML); assetOnlyLines counts
        /// serialized lines no YAML entry matches (removed from YAML).
        /// </summary>
        internal static void Analyze(WitWeaverConversationData data,
            Dictionary<string, List<DialogueYamlConfig>> dict,
            out int staleTextLines, out int yamlOnlyLines, out int assetOnlyLines)
        {
            staleTextLines = 0;
            yamlOnlyLines = 0;
            assetOnlyLines = 0;
            if (data?.DialogueLines == null || dict == null) return;

            var matchedConfigs = new HashSet<DialogueYamlConfig>();
            foreach (var line in data.DialogueLines)
            {
                if (line == null) continue;

                var config = FindConfig(line, dict);
                if (config == null)
                {
                    assetOnlyLines++;
                    continue;
                }

                matchedConfigs.Add(config);
                if (config.LocalizedDialogue == null) continue;

                var fresh = new List<WitWeaverConversationData.LocalizedDialogue>(config.LocalizedDialogue.Count);
                foreach (var kvp in config.LocalizedDialogue)
                    fresh.Add(new WitWeaverConversationData.LocalizedDialogue { Language = kvp.Key, Text = kvp.Value });

                if (!TextEqual(line.LocalizedDialogues, fresh))
                    staleTextLines++;
            }

            foreach (var section in dict.Values)
            {
                if (section == null) continue;
                foreach (var config in section)
                {
                    if (config != null && !matchedConfigs.Contains(config))
                        yamlOnlyLines++;
                }
            }
        }

        // Mirrors the runtime matching in WitWeaverConversationData.InitializeDialogueData:
        // section by ConversationID, then LineID; the index fallback applies ONLY to legacy
        // lines that have no LineID at all. A line whose LineID is absent from the YAML was
        // deleted there and must not fall back to the index (that would hand it the next
        // line's text); it is drift, handled by the structural import.
        static DialogueYamlConfig FindConfig(WitWeaverConversationData.DialogueLineInfo line,
            Dictionary<string, List<DialogueYamlConfig>> dict)
        {
            if (string.IsNullOrEmpty(line.ConversationID) ||
                !dict.TryGetValue(line.ConversationID, out var configList) || configList == null)
                return null;

            if (!string.IsNullOrEmpty(line.LineID))
            {
                foreach (var cfg in configList)
                {
                    if (cfg != null && cfg.LineID == line.LineID)
                        return cfg;
                }

                return null;
            }

            return line.ConversationLineIndex >= 0 && line.ConversationLineIndex < configList.Count
                ? configList[line.ConversationLineIndex]
                : null;
        }

        // Full equality including preserved clips (used to decide whether a merge dirtied the asset)
        static bool ListsEqual(List<WitWeaverConversationData.LocalizedDialogue> a,
            List<WitWeaverConversationData.LocalizedDialogue> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i].Language, b[i].Language, StringComparison.Ordinal) ||
                    !string.Equals(a[i].Text, b[i].Text, StringComparison.Ordinal) ||
                    a[i].Clip != b[i].Clip)
                    return false;
            }
            return true;
        }

        // Language/Text-only equality (clips are not authored in YAML, so they never make a line stale)
        static bool TextEqual(List<WitWeaverConversationData.LocalizedDialogue> a,
            List<WitWeaverConversationData.LocalizedDialogue> b)
        {
            if (a == null || b == null) return (a?.Count ?? 0) == (b?.Count ?? 0);
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i].Language, b[i].Language, StringComparison.Ordinal) ||
                    !string.Equals(a[i].Text, b[i].Text, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }
}
#endif
