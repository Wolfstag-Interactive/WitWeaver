using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Discovers all IConvoCoreHistoryRenderer implementations.
    /// Usually used only in the editor to auto-generate profiles.
    /// </summary>
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConvoCoreHistoryRendererRegistry.html")]
    public static class ConvoCoreHistoryRendererRegistry
    {
        private static readonly List<Type> _rendererTypes = new();

        public static void DiscoverRenderers()
        {
            _rendererTypes.Clear();
            var iface = typeof(IConvoCoreHistoryRenderer);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }

                foreach (var t in types)
                {
                    if (iface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        _rendererTypes.Add(t);
                }
            }
        }

        public static IReadOnlyList<Type> GetRendererTypes()
        {
            if (_rendererTypes.Count == 0)
                DiscoverRenderers();
            return _rendererTypes;
        }

        public static string[] GetRendererNames()
        {
            return GetRendererTypes()
                .Select(t => ((IConvoCoreHistoryRenderer)Activator.CreateInstance(t)).RendererName)
                .ToArray();
        }

        public static IConvoCoreHistoryRenderer CreateInstance(string name)
        {
            foreach (var t in GetRendererTypes())
            {
                var inst = (IConvoCoreHistoryRenderer)Activator.CreateInstance(t);
                if (string.Equals(inst.RendererName, name, StringComparison.OrdinalIgnoreCase))
                    return inst;
            }
            return null;
        }
    }
}