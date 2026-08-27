using System;
using UnityEngine;
using System.Collections;
using System.Linq;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1ConversationContainerRuntime.html")]
    public static class ConversationContainerRuntime
{
    public static IEnumerator Play(
        ConversationContainer c,
        IConvoCoreRunner runner,
        string startAliasOrName = null,
        bool? loopOverride = null,
        Func<ConversationContainer.Entry> hubSelector = null)
    {
        return PlayLinear(c, runner, startAliasOrName, loopOverride);
    }

    public static IEnumerator PlayLinear(
        ConversationContainer c, IConvoCoreRunner runner,
        string startAliasOrName = null, bool? loopOverride = null)
    {
        if (c == null || c.Conversations == null || c.Conversations.Count == 0)
        {
            Debug.LogWarning("Conversation container is null, or container contains no conversation entries, exiting.");
            yield break;
        }
        if (c.Conversations != null)
        {
            int beforeCount = c.Conversations.Count;
            c.Conversations.RemoveAll(e => e == null || e.ConversationData == null);


            if (beforeCount != c.Conversations.Count)
            {
                Debug.LogWarning($"[ConvoCore] Removed {beforeCount - c.Conversations.Count} null entries from container '{c.name}'." +
                                 $"This is done to prevent errors during runtime. Ensure that the container does not contain null entries during edit time.");
            }
        }

        if (c.Conversations == null || c.Conversations.Count == 0)
            yield break;

        bool loop = loopOverride ?? c.Loop;
        int start = ResolveIndex(c, string.IsNullOrEmpty(startAliasOrName) ? c.DefaultStart : startAliasOrName);

        int played = 0;
        while (played < c.Conversations.Count)
        {
            int i = (start + played) % c.Conversations.Count;
            var e = c.Conversations[i];
            played++;

            if (e.Enabled && e.ConversationData != null)
                yield return PlayOne(e, runner);

            if (!loop && i == c.Conversations.Count - 1) yield break;
        }
    }

    private static IEnumerator PlayOne(ConversationContainer.Entry e, IConvoCoreRunner runner)
    {
        bool done = false;
        void OnEnd() => done = true;

        try
        {
            runner.CompletedConversation += OnEnd;
            runner.PlayConversation(e.ConversationData);
            while (!done) yield return null;
        }
        finally
        {
            runner.CompletedConversation -= OnEnd;
        }

        if (e.DelayAfterEndSeconds > 0f)
            yield return new WaitForSeconds(e.DelayAfterEndSeconds);
    }

    public static int ResolveIndex(ConversationContainer c, string aliasOrName)
    {
        if (!string.IsNullOrEmpty(aliasOrName))
        {
            int idx = c.Conversations.FindIndex(e =>
                (!string.IsNullOrEmpty(e.Alias) &&
                 e.Alias.Equals(aliasOrName, StringComparison.OrdinalIgnoreCase)) ||
                (e.ConversationData && e.ConversationData.name.Equals(aliasOrName, StringComparison.OrdinalIgnoreCase)));
            if (idx >= 0) return idx;
        }
        return 0;
    }

   
    
}

}