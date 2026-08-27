using System.IO;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1WitWeaverActionCreatorWindow.html")]
    public class WitWeaverActionCreatorWindow : EditorWindow
    {
        private string actionName = "NewAction";

        [MenuItem("Tools/Wolfstag Interactive/WitWeaver/Create New Dialogue Action")]
        public static void ShowWindow()
        {
            GetWindow<WitWeaverActionCreatorWindow>("New Dialogue Action");
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
            string scriptFolder = "Assets/Scripts/WitWeaverCustomActions";
            string scriptPath = $"{scriptFolder}/{name}.cs";
            string assetFolder = "Assets/WitWeaverCustomActions";

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

            EditorPrefs.SetString("WitWeaver_PendingActionName", name);
            EditorPrefs.SetString("WitWeaver_PendingAssetPath", assetFolder);

            AssetDatabase.Refresh();
            Debug.Log($"Created script for {name}. Waiting for Unity to compile before asset is created.");
        }
        string GetTemplate()
        {
            return
                
@"using UnityEngine;
using System.Collections;
using WolfstagInteractive.WitWeaver;

[CreateAssetMenu(menuName = ""WitWeaver/Actions/#NAME#"")] [System.Serializable]
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