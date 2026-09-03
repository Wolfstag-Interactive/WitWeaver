// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    /// <summary>
    /// One-time (idempotent) migration for the stable representation-ID system.
    ///
    /// Phase A stamps a <c>RepresentationID</c> onto every <see cref="RepresentationPair"/> in every
    /// character profile in the project and saves the profiles, so the IDs are durable before
    /// anything references them. Phase B then resolves each dialogue line's legacy
    /// <c>SelectedRepresentationName</c> to its profile pair's stable ID and writes
    /// <c>SelectedRepresentationID</c>, reporting every reference it cannot resolve.
    ///
    /// Safe to run repeatedly: already-stamped profiles and already-migrated lines are skipped.
    /// </summary>
    public static class WitWeaverRepresentationIdMigration
    {
        [MenuItem("Tools/Wolfstag Interactive/WitWeaver/Migrate Representation IDs", false, 300)]
        public static void Run()
        {
            int profilesScanned = 0, profilesStamped = 0;
            int conversationsScanned = 0, conversationsChanged = 0;
            int referencesMigrated = 0;
            var unresolved = new List<string>();

            // ---- Phase A: stamp profile pairs and save BEFORE any line reference is resolved. ----
            foreach (var guid in AssetDatabase.FindAssets("t:WitWeaverCharacterProfileBaseData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<WitWeaverCharacterProfileBaseData>(path);
                if (profile == null || profile.Representations == null) continue;

                profilesScanned++;
                var used = new HashSet<string>();
                bool changed = false;
                foreach (var pair in profile.Representations)
                {
                    if (pair == null) continue;
                    string before = pair.RepresentationID;
                    pair.EnsureValidId(used);
                    changed |= before != pair.RepresentationID;
                }

                if (changed)
                {
                    profilesStamped++;
                    EditorUtility.SetDirty(profile);
                }
            }

            AssetDatabase.SaveAssets();

            // ---- Phase B: migrate line references name -> ID. ----
            foreach (var guid in AssetDatabase.FindAssets("t:WitWeaverConversationData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var conversation = AssetDatabase.LoadAssetAtPath<WitWeaverConversationData>(path);
                if (conversation == null || conversation.DialogueLines == null) continue;

                conversationsScanned++;
                bool changed = false;

                for (int lineIndex = 0; lineIndex < conversation.DialogueLines.Count; lineIndex++)
                {
                    var line = conversation.DialogueLines[lineIndex];
                    if (line?.CharacterRepresentations == null) continue;

                    for (int r = 0; r < line.CharacterRepresentations.Count; r++)
                    {
                        var data = line.CharacterRepresentations[r];
                        if (!string.IsNullOrEmpty(data.SelectedRepresentationID)) continue; // already migrated
                        if (string.IsNullOrEmpty(data.SelectedRepresentationName)) continue; // empty = "default"

                        string characterId = !string.IsNullOrEmpty(data.SelectedCharacterID)
                            ? data.SelectedCharacterID
                            : line.characterID;

                        var profile = conversation.ResolveCharacterProfile(
                            conversation.ConversationParticipantProfiles, characterId);
                        if (profile == null)
                        {
                            unresolved.Add(
                                $"'{path}' line {lineIndex} [{r}]: no profile for CharacterID '{characterId}' " +
                                $"(representation '{data.SelectedRepresentationName}').");
                            continue;
                        }

                        if (profile.TryGetRepresentationIdByName(data.SelectedRepresentationName, out var repId))
                        {
                            data.SelectedRepresentationID = repId;
                            if (data.SelectedRepresentation == null &&
                                profile.TryGetRepresentation(repId, out var representation))
                            {
                                data.SelectedRepresentation = representation;
                            }

                            line.CharacterRepresentations[r] = data; // struct copy-back
                            referencesMigrated++;
                            changed = true;
                        }
                        else
                        {
                            unresolved.Add(
                                $"'{path}' line {lineIndex} [{r}]: representation '{data.SelectedRepresentationName}' " +
                                $"not found in profile '{profile.CharacterName}'. Fix via the line's Representation dropdown.");
                        }
                    }
                }

                if (changed)
                {
                    conversationsChanged++;
                    EditorUtility.SetDirty(conversation);
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[WitWeaver] Representation ID migration: {profilesScanned} profiles scanned ({profilesStamped} stamped), " +
                $"{conversationsScanned} conversations scanned ({conversationsChanged} changed), " +
                $"{referencesMigrated} line references migrated, {unresolved.Count} unresolvable.");

            foreach (var entry in unresolved)
                Debug.LogWarning($"[WitWeaver] Unresolvable representation reference: {entry}");

            if (unresolved.Count > 0)
            {
                EditorUtility.DisplayDialog("WitWeaver Representation ID Migration",
                    $"Migration finished with {unresolved.Count} unresolvable reference(s).\n\n" +
                    "See the Console for the full list; fix each via the line's Representation dropdown " +
                    "and run the migration again.",
                    "OK");
            }
        }
    }
}
