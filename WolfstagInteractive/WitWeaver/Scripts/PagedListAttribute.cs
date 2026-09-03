// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

using UnityEngine;

namespace WolfstagInteractive.WitWeaver
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1PagedListAttribute.html")]
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