// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    public interface IWitWeaverCharacterDisplay
    {
        /// <summary> Inject the representation asset (expression catalog, single source of truth). </summary>
        void BindRepresentation(CharacterRepresentationBase representationAsset);

        /// <summary> Apply expression by GUID. </summary>
        void ApplyExpression(string expressionId);

        /// <summary> Apply per-line display overrides (scale/flip/position/etc.). </summary>
        void ApplyDisplayOptions(DialogueLineDisplayOptions options);
    }
}