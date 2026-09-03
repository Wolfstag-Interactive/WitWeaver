// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
using System.Collections;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverDialogueLineActionPlayAudioClip.html")]
[CreateAssetMenu(menuName = "WitWeaver/Actions/PlayAudioClip")] [System.Serializable]
    public class WitWeaverDialogueLineActionPlayAudioClip : BaseDialogueLineAction
    {
        public AudioClip AudioClip;
        public Vector3 Position;
        [Range(0,1)]
        public float Volume = 1f;

        public override IEnumerator ExecuteLineAction()
        {
            AudioSource.PlayClipAtPoint(AudioClip, Position,Volume);
            yield return new WaitForSeconds(AudioClip.length);
        }

    }
}