using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreHistoryRendererProfile.html")]
[CreateAssetMenu(fileName = "HistoryRendererProfile", menuName = "ConvoCore/UI/History Renderer Profile", order = 50)]
    public class ConvoCoreHistoryRendererProfile : ScriptableObject
    {
        [SerializeField] private string rendererName = "Rich";

        public string RendererName => rendererName;

        public void UpdateFromDiscovered(string newRendererName)
        {
            rendererName = newRendererName;
            name = $"{newRendererName}RendererProfile";
        }
    }
}