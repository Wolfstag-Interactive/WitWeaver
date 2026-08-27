using System.Collections;
using System.Threading.Tasks;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreAddressablesUtil.html")]
    public static class ConvoCoreAddressablesUtil
    {
#if CONVOCORE_ADDRESSABLES
        public static async Task UpdateCatalogsIfNeededAsync()
        {
            await UnityEngine.AddressableAssets.Addressables.InitializeAsync().Task;
            var need = await UnityEngine.AddressableAssets.Addressables.CheckForCatalogUpdates().Task;
            if (need != null && need.Count > 0)
                await UnityEngine.AddressableAssets.Addressables.UpdateCatalogs(need).Task;
        }

        public static IEnumerator UpdateCatalogsIfNeededCoroutine()
        {
            var init = UnityEngine.AddressableAssets.Addressables.InitializeAsync();
            yield return init;

            var check = UnityEngine.AddressableAssets.Addressables.CheckForCatalogUpdates();
            yield return check;

            var list = check.Result;
            if (list != null && list.Count > 0)
            {
                var upd = UnityEngine.AddressableAssets.Addressables.UpdateCatalogs(list);
                yield return upd;
            }
        }
#else
        public static Task UpdateCatalogsIfNeededAsync() => Task.CompletedTask;
        public static IEnumerator UpdateCatalogsIfNeededCoroutine() { yield break; }
#endif
    }
}