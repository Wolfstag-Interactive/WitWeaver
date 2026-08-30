using UnityEngine;
[RequireComponent(typeof(WolfstagInteractive.WitWeaver.WitWeaver))]
public class AutoStartSampleConversation : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<WolfstagInteractive.WitWeaver.WitWeaver>().PlayConversation();
    }
}