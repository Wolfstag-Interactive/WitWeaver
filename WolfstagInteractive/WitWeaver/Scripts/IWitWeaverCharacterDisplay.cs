using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    public interface IConvoCoreCharacterDisplay
    {
        /// <summary> Inject the representation asset (expression catalog, single source of truth). </summary>
        void BindRepresentation(CharacterRepresentationBase representationAsset);

        /// <summary> Apply expression by GUID. </summary>
        void ApplyExpression(string expressionId);

        /// <summary> Apply per-line display overrides (scale/flip/position/etc.). </summary>
        void ApplyDisplayOptions(DialogueLineDisplayOptions options);
    }
}