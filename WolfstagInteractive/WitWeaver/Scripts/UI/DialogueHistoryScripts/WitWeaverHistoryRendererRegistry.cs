// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfstagInteractive.WitWeaver
{
    /// <summary>
    /// Discovers all IWitWeaverHistoryRenderer implementations.
    /// Usually used only in the editor to auto-generate profiles.
    /// </summary>
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1WitWeaverHistoryRendererRegistry.html")]
    public static class WitWeaverHistoryRendererRegistry
    {
        private static readonly List<Type> _rendererTypes = new();

        public static void DiscoverRenderers()
        {
            _rendererTypes.Clear();
            var iface = typeof(IWitWeaverHistoryRenderer);

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
                .Select(t => ((IWitWeaverHistoryRenderer)Activator.CreateInstance(t)).RendererName)
                .ToArray();
        }

        public static IWitWeaverHistoryRenderer CreateInstance(string name)
        {
            foreach (var t in GetRendererTypes())
            {
                var inst = (IWitWeaverHistoryRenderer)Activator.CreateInstance(t);
                if (string.Equals(inst.RendererName, name, StringComparison.OrdinalIgnoreCase))
                    return inst;
            }
            return null;
        }
    }
}