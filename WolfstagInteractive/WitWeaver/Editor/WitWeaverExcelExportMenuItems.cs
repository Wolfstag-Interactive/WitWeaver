#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.ConvoCore.Editor
{
    /// <summary>
    /// Provides menu items for exporting ConvoCore YAML dialogue files to .xlsx spreadsheets.
    ///
    /// Two entry points:
    /// <list type="bullet">
    ///   <item>Right-click a .yml asset in the Project window → ConvoCore / Export to Excel…</item>
    ///   <item>Tools → ConvoCore → Export YAML to Excel… (file picker)</item>
    /// </list>
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1ConvoCoreExcelExportMenuItems.html")]
    public static class ConvoCoreExcelExportMenuItems
    {

        [MenuItem("Assets/ConvoCore/Export to Excel\u2026", false, 1200)]
        private static void ExportSelectedYaml()
        {
            var assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            var absolutePath = Path.GetFullPath(assetPath);

            var defaultName = Path.GetFileNameWithoutExtension(assetPath) + ".xlsx";
            var saveDir = Path.GetDirectoryName(absolutePath);

            var outputPath = EditorUtility.SaveFilePanel("Export YAML to Excel", saveDir, defaultName, "xlsx");
            if (string.IsNullOrEmpty(outputPath)) return;

            RunExport(absolutePath, outputPath);
        }

        [MenuItem("Assets/ConvoCore/Export to Excel\u2026", true)]
        private static bool ExportSelectedYamlValidate()
        {
            if (Selection.activeObject == null) return false;
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path) &&
                   (path.EndsWith(".yml", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".yaml", System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Opens a file picker to select a ConvoCore YAML file and an output location,
        /// then exports the YAML file to an Excel (.xlsx) spreadsheet.
        /// </summary>
        [MenuItem("Tools/Wolfstag Interactive/ConvoCore/Export YAML to Excel\u2026", false, 200)]
        private static void ExportYamlFromToolsMenu()
        {
            var yamlPath = EditorUtility.OpenFilePanel("Select ConvoCore YAML file", Application.dataPath, "yml,yaml");
            if (string.IsNullOrEmpty(yamlPath)) return;

            var defaultName = Path.GetFileNameWithoutExtension(yamlPath) + ".xlsx";
            var saveDir = Path.GetDirectoryName(yamlPath);

            var outputPath = EditorUtility.SaveFilePanel("Save Excel File", saveDir, defaultName, "xlsx");
            if (string.IsNullOrEmpty(outputPath)) return;

            RunExport(yamlPath, outputPath);
        }

        /// <summary>
        /// Converts a YAML file at the specified path into an Excel file and saves it to the given output path.
        /// </summary>
        /// <param name="yamlPath">The full file path to the input YAML file to be exported.</param>
        /// <param name="outputPath">The destination file path where the converted Excel file will be saved.</param>
        private static void RunExport(string yamlPath, string outputPath)
        {
            var error = ConvoCoreYamlToExcelExporter.Export(yamlPath, outputPath);

            if (error != null)
            {
                EditorUtility.DisplayDialog("Export Failed", $"Could not export YAML to Excel:\n\n{error}", "OK");
                return;
            }

            // Refresh the asset database if the output is inside the project.
            var projectRoot = Path.GetFullPath(Application.dataPath + "/..");
            if (outputPath.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
                AssetDatabase.Refresh();

            Debug.Log($"[ConvoCore] Exported YAML to Excel: {outputPath}");
            EditorUtility.RevealInFinder(outputPath);
        }
    }
}
#endif