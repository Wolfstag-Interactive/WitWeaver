// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1DialogueLineDisplayOptions.html")]
    [System.Serializable]
    public class DialogueLineDisplayOptions : ISerializationCallbackReceiver
    {
        [Tooltip("Flip the display of the portrait sprite horizontally.")]
        public bool FlipPortraitX = false;

        [Tooltip("Flip the display of the portrait sprite vertically.")]
        public bool FlipPortraitY = false;

        [Tooltip("Flip the display of the full-body sprite horizontally.")]
        public bool FlipFullBodyX = false;

        [Tooltip("Flip the display of the full-body sprite vertically.")]
        public bool FlipFullBodyY = false;

        [Tooltip("The name of the display slot this character should occupy, as configured on the WitWeaverUIFoundation.")]
        public string DisplaySlot;

        [Tooltip("Additional scale applied to the portrait sprite.")]
        public Vector3 PortraitScale = Vector3.one;

        [Tooltip("Additional scale applied to the full-body sprite.")]
        public Vector3 FullBodyScale = Vector3.one;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (PortraitScale == Vector3.zero) PortraitScale = Vector3.one;
            if (FullBodyScale == Vector3.zero) FullBodyScale = Vector3.one;
        }
    }
}