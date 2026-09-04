// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;
[RequireComponent(typeof(WolfstagInteractive.WitWeaver.WitWeaver))]
public class AutoStartSampleConversation : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<WolfstagInteractive.WitWeaver.WitWeaver>().PlayConversation();
    }
}