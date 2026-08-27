using UnityEngine;
[RequireComponent(typeof(WolfstagInteractive.ConvoCore.ConvoCore))]
public class AutoStartSampleConversation : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<WolfstagInteractive.ConvoCore.ConvoCore>().PlayConversation();
    }
}