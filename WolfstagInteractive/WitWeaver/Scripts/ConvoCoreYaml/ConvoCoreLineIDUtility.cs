using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreLineID.html")]
    public static class ConvoCoreLineID
    {
        public static string NewLineID()
        {
            var hex = Guid.NewGuid().ToString("N"); // 32 hex chars
            return "L_" + hex.Substring(0, 12);
        }
    }
    public static class ConvoCoreLineIDUtility
    {
        // Returns true if it mutated any cfg.LineID
        public static bool EnsureLineIds(
            Dictionary<string, List<DialogueYamlConfig>> dict,
            out string error)
        {
            error = null;
            if (dict == null) return false;

            bool changed = false;

            foreach (var kv in dict)
            {
                string conversationKey = kv.Key;
                var list = kv.Value;
                if (list == null) continue;

                var seen = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 0; i < list.Count; i++)
                {
                    var cfg = list[i];
                    if (cfg == null) continue;

                    if (string.IsNullOrWhiteSpace(cfg.LineID))
                    {
                        cfg.LineID = ConvoCoreLineID.NewLineID();
                        changed = true;
                    }

                    if (!seen.Add(cfg.LineID))
                    {
                        error = $"Duplicate LineID '{cfg.LineID}' in conversation '{conversationKey}'.";
                        return changed;
                    }
                }
            }
            return changed;
        }
    }
}