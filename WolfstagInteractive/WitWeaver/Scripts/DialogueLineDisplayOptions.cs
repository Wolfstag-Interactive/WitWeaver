using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1DialogueLineDisplayOptions.html")]
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

        [Tooltip("The name of the display slot this character should occupy, as configured on the ConvoCoreUIFoundation.")]
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