using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverHistoryRendererProfile.html")]
[CreateAssetMenu(fileName = "HistoryRendererProfile", menuName = "WitWeaver/UI/History Renderer Profile", order = 50)]
    public class WitWeaverHistoryRendererProfile : ScriptableObject
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