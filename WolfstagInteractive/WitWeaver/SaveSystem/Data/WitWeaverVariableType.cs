// Copyright (c) 2025-2026 Wolfstag Interactive LLC. All rights reserved.
// WitWeaver™ dialogue middleware for Unity. See LICENSE.md for terms.
// Unauthorized redistribution of source or compiled assemblies is prohibited.

namespace WolfstagInteractive.WitWeaver.SaveSystem
{
    public enum WitWeaverVariableType
    {
        String,
        Int,
        Float,
        Bool,
        CollectionInt,     // sub-entries are string -> int
        CollectionString   // sub-entries are string -> string
    }
}