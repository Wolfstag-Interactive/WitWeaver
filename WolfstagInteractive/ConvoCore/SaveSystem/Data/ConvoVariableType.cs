namespace WolfstagInteractive.ConvoCore.SaveSystem
{
    public enum ConvoVariableType
    {
        String,
        Int,
        Float,
        Bool,
        CollectionInt,     // sub-entries are string -> int
        CollectionString   // sub-entries are string -> string
    }
}