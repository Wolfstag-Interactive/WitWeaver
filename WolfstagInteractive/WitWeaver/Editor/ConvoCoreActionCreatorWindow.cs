using System.IO;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreActionCreatorWindow.html")]
    public class ConvoCoreActionCreatorWindow : EditorWindow
    {
        private string actionName = "NewAction";

        [MenuItem("Tools/Wolfstag Interactive/ConvoCore/Create New Dialogue Action")]
        public static void ShowWindow()
        {
            GetWindow<ConvoCoreActionCreatorWindow>("New Dialogue Action");
        }

        void OnGUI()
        {
            GUILayout.Label("Create a new Dialogue Action", EditorStyles.boldLabel);
            actionName = EditorGUILayout.TextField("Action Name", actionName);

            if (GUILayout.Button("Create Action"))
            {
                CreateActionScript(actionName);
            }
        }

        void CreateActionScript(string name)
        {
            string scriptFolder = "Assets/Scripts/ConvoCoreCustomActions";
            string scriptPath = $"{scriptFolder}/{name}.cs";
            string assetFolder = "Assets/ConvoCoreCustomActions";

            // Create folders if they don't exist
            if (!Directory.Exists(scriptFolder))
                Directory.CreateDirectory(scriptFolder);

            if (!Directory.Exists(assetFolder))
                Directory.CreateDirectory(assetFolder);

            if (File.Exists(scriptPath))
            {
                Debug.LogError("A script with that name already exists!");
                return;
            }

            string template = GetTemplate().Replace("#NAME#", name);
            File.WriteAllText(scriptPath, template);

            EditorPrefs.SetString("ConvoCore_PendingActionName", name);
            EditorPrefs.SetString("ConvoCore_PendingAssetPath", assetFolder);

            AssetDatabase.Refresh();
            Debug.Log($"Created script for {name}. Waiting for Unity to compile before asset is created.");
        }
        string GetTemplate()
        {
            return
                
@"using UnityEngine;
using System.Collections;
using WolfstagInteractive.ConvoCore;

[CreateAssetMenu(menuName = ""ConvoCore/Actions/#NAME#"")] [System.Serializable]
public class #NAME# : BaseAction
{

        public override IEnumerator DoAction()
        {
            //add action logic here
            yield return null; 
            //alternatively you can use yield return new WaitForSecondsRealtime(amount); to wait for a certain amount of time before or after continuing
        }
        
}";
        }
    }
}