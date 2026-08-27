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