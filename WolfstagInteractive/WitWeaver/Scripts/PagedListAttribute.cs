using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1PagedListAttribute.html")]
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class PagedListAttribute : PropertyAttribute
    {
        public readonly int DefaultItemsPerPage;

        public PagedListAttribute(int defaultItemsPerPage = 25)
        {
            DefaultItemsPerPage = Mathf.Max(1, defaultItemsPerPage);
        }
    }
}