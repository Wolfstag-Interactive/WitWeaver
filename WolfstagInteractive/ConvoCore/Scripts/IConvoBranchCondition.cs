using System;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Contract for gating a branch or choice on runtime state (e.g. variables, quest flags).
    /// Reserved extension point for conditional branching: continuations and choices do not
    /// evaluate conditions yet, but implementations written against this interface will plug
    /// into that system when it ships.
    /// </summary>
    public interface IConvoBranchCondition
    {
        /// <summary>Returns true when the branch or choice guarded by this condition is available.</summary>
        bool Evaluate(ConvoCore runner);
    }

    /// <summary>
    /// Serializable base for <see cref="IConvoBranchCondition"/> implementations so conditions
    /// can be authored as [SerializeReference] instances once continuation data exposes them.
    /// </summary>
    [Serializable]
    public abstract class BaseConvoBranchCondition : IConvoBranchCondition
    {
        public abstract bool Evaluate(ConvoCore runner);
    }
}
