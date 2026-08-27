#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Editor-only utility to add [HelpURL] to ConvoCore scripts:
/// 1) On new script creation (automatic)
/// 2) Retroactively via menu: ConvoCore → Scan & Add HelpURLs
/// 
/// - Handles class/struct/interface/enum with any modifiers
/// - Inserts above existing attributes if present
/// - Uses fully-qualified UnityEngine.HelpURL to avoid needing usings
/// - Produces a clear breakdown of what happened
/// </summary>
public class ConvoCoreHelpURLInjector : AssetModificationProcessor
{
    private const string DocsBaseUrl = "https://docs.wolfstaginteractive.com/convocore/api";
    private const string RootFolder  = "Packages/com.wolfstaginteractive.convocore/";
    private static readonly Regex HelpUrlAttrRegex =
        new Regex(@"\[\s*(?:UnityEngine\.)?HelpURL\s*\(", RegexOptions.Multiline);

    // Matches the FIRST top-level type declaration in a file (with optional attributes).
    // Captures:
    //   kind  -> class|struct|interface|enum
    //   name  -> type name
    // We match any order/combination of modifiers (public/internal/abstract/partial/sealed/static/etc.)
    private static readonly Regex TopLevelTypeRegex = new Regex(
        @"^[ \t]*(?<attrs>(?:\[[^\]]*\]\s*)*)" +           // optional attributes
        @"[ \t]*(?<mods>(?:public|internal|protected|private|static|abstract|sealed|partial)\s+)*" +
        @"(?<kind>class|struct|interface|enum)\s+(?<name>\w+)",
        RegexOptions.Multiline
    );

    // ---------- AUTO ON NEW FILES ----------
    public static void OnWillCreateAsset(string path)
    {
        if (!path.EndsWith(".cs.meta")) return;
        var scriptPath = path.Substring(0, path.Length - 5);
        TryInjectHelpUrl(scriptPath, out _);
    }

   // ---------------- RETROACTIVE SCAN ----------------
[MenuItem("ConvoCoreDevHelpers/Scan & Add HelpURLs")]
public static void ScanAllConvoCoreScripts()
{
    // Detect the package folder dynamically
    string pkgPath = Path.GetFullPath("Packages/com.wolfstaginteractive.convocore");
    if (!Directory.Exists(pkgPath))
    {
        Debug.LogError("[ConvoCoreHelpURLInjector] Could not find package folder: " + pkgPath);
        return;
    }

    var csFiles = Directory.GetFiles(pkgPath, "*.cs", SearchOption.AllDirectories);

    int added = 0, already = 0, skipped = 0;
    foreach (var path in csFiles)
    {
        if (!File.Exists(path)) continue;

        string norm = path.Replace('\\', '/').ToLowerInvariant();
        if (!norm.Contains("convocore")) continue;

        string text = File.ReadAllText(path);
        if (HelpUrlAttrRegex.IsMatch(text))
        {
            already++;
            continue;
        }

        if (TryInjectHelpUrl(path, out var reason))
        {
            added++;
            Debug.Log($"[ConvoCoreHelpURLInjector] Injected HelpURL → {Path.GetFileName(path)}");
        }
        else
        {
            skipped++;
        }
    }

    Debug.Log($"[ConvoCoreHelpURLInjector] Scan complete:\n" +
              $"  Added: {added}\n  Already: {already}\n  Skipped: {skipped}");
}
[MenuItem("ConvoCoreDevHelpers/Update Existing HelpURLs")]
public static void UpdateExistingHelpUrls()
{
    // Locate your package folder dynamically
    string pkgPath = Path.GetFullPath("Packages/com.wolfstaginteractive.convocore");
    if (!Directory.Exists(pkgPath))
    {
        Debug.LogError("[ConvoCoreHelpURLInjector] Package folder not found: " + pkgPath);
        return;
    }

    var csFiles = Directory.GetFiles(pkgPath, "*.cs", SearchOption.AllDirectories);

    int updated = 0;
    int skipped = 0;
    int thirdParty = 0;

    foreach (var path in csFiles)
    {
        if (!File.Exists(path)) continue;

        string norm = path.Replace('\\', '/').ToLowerInvariant();

        // 🧹 Skip ThirdParty files
        if (norm.Contains("/convocore/thirdparty/"))
        {
            thirdParty++;
            continue;
        }

        string text = File.ReadAllText(path);

        // 🧩 Only process scripts that already have a HelpURL
        if (!HelpUrlAttrRegex.IsMatch(text))
        {
            skipped++;
            continue;
        }

        // Find the top-level type
        var m = TopLevelTypeRegex.Match(text);
        if (!m.Success)
        {
            skipped++;
            continue;
        }

        string kind = m.Groups["kind"].Value.ToLowerInvariant();
        string typeName = m.Groups["name"].Value;
        string ns = ExtractNamespace(text) ?? "WolfstagInteractive.ConvoCore";

        // 🚫 HelpURL not valid on enums/interfaces/delegates
        if (kind is "enum" or "interface" or "delegate")
        {
            skipped++;
            continue;
        }

        string newUrl = BuildDoxygenUrl(ns, kind, typeName);

        // Replace existing HelpURL value only if it differs
        string updatedText = Regex.Replace(
            text,
            @"(\[.*HelpURL\s*\(\s*"")[^""]*(\""\s*\)\s*\])",
            $"$1{newUrl}$2",
            RegexOptions.Multiline
        );

        if (updatedText != text)
        {
            try
            {
                File.WriteAllText(path, updatedText);
                updated++;
                Debug.Log($"[ConvoCoreHelpURLInjector] Updated HelpURL → {Path.GetFileName(path)}");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[ConvoCoreHelpURLInjector] Failed to write {path}: {ex.Message}");
            }
        }
        else
        {
            skipped++;
        }
    }

    AssetDatabase.Refresh();

    Debug.Log(
        $"[ConvoCoreHelpURLInjector] Update Existing HelpURLs complete:\n" +
        $"  Updated: {updated}\n" +
        $"  Skipped (no change or invalid): {skipped}\n" +
        $"  Ignored ThirdParty: {thirdParty}"
    );
}


// ---------------- CORE INJECTION ----------------
private static bool TryInjectHelpUrl(string scriptPath, out FailReason failReason)
{
    failReason = FailReason.None;

    if (!File.Exists(scriptPath))
        return false;

    string norm = scriptPath.Replace('\\', '/').ToLowerInvariant();

    // 🧹 Skip any vendor code
    if (norm.Contains("/convocore/thirdparty/"))
        return false;

    string text = File.ReadAllText(scriptPath);

    // 🧩 Skip if already has a HelpURL
    if (HelpUrlAttrRegex.IsMatch(text))
        return false;

    // Remove preprocessor lines to make regex detection more reliable
    string cleaned = Regex.Replace(
        text,
        @"^\s*#(if|endif|region|endregion).*?$",
        string.Empty,
        RegexOptions.Multiline
    );

    // Find the first top-level type declaration
    var m = TopLevelTypeRegex.Match(cleaned);
    if (!m.Success)
    {
        failReason = FailReason.NoTopLevelType;
        return false;
    }

    string kind = m.Groups["kind"].Value.ToLowerInvariant();
    string typeName = m.Groups["name"].Value;

    // 🚫 Only apply to valid declaration types (no enums, interfaces, delegates)
    if (kind is "enum" or "interface" or "delegate")
        return false;

    string ns = ExtractNamespace(text) ?? "WolfstagInteractive.ConvoCore";
    string url = BuildDoxygenUrl(ns, kind, typeName);

    // Find the actual declaration line in the original file (including modifiers)
    var declMatch = Regex.Match(
        text,
        $@"^[ \t]*(?:public|internal|protected|private|static|abstract|sealed|partial|\s)*\s*{kind}\s+{typeName}",
        RegexOptions.Multiline
    );

    if (!declMatch.Success)
    {
        Debug.LogWarning($"[ConvoCoreHelpURLInjector] Could not find declaration line for {typeName}");
        failReason = FailReason.NoTopLevelType;
        return false;
    }

    int insertIndex = declMatch.Index;

    // Move insertion above existing attributes if present
    var attrBlock = Regex.Matches(
        text[..insertIndex],
        @"\[[^\]]*\]\s*$",
        RegexOptions.Multiline
    );
    if (attrBlock.Count > 0)
        insertIndex = attrBlock[^1].Index;

    insertIndex = Mathf.Clamp(insertIndex, 0, text.Length);

    string helpAttr = $"[HelpURL(\"{url}\")]\n";

    try
    {
        string modified = text.Insert(insertIndex, helpAttr);

        // Ensure 'using UnityEngine;' is present at the top of the file
        if (!Regex.IsMatch(modified, @"^\s*using\s+UnityEngine\s*;", RegexOptions.Multiline))
        {
            // Insert after any leading #if / preprocessor line, or at the very start
            var ppMatch = Regex.Match(modified, @"^[ \t]*#(if|region)\b.*$", RegexOptions.Multiline);
            int usingInsert = ppMatch.Success ? ppMatch.Index + ppMatch.Length + 1 : 0;
            modified = modified.Insert(usingInsert, "using UnityEngine;\n");
        }

        File.WriteAllText(scriptPath, modified);
        return true;
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[ConvoCoreHelpURLInjector] Failed to update {scriptPath}: {ex.Message}");
        failReason = FailReason.WriteFailed;
        return false;
    }
}

    private enum FailReason { None, NoTopLevelType, WriteFailed }


    private static string ExtractNamespace(string fileText)
    {
        var nsm = Regex.Match(fileText, @"namespace\s+([\w\.]+)");
        return nsm.Success ? nsm.Groups[1].Value : null;
    }

    private static string BuildDoxygenUrl(string ns, string kind, string typeName)
    {
        // Kind prefix (matches Doxygen naming)
        string prefix = kind switch
        {
            "interface" => "interface",
            "struct"    => "struct",
            "enum"      => "enum",
            _           => "class"
        };

        // Split namespace into tokens, preserving case
        string[] parts = ns.Split('.');

        // Join namespaces with _1_1 separators (Doxygen style)
        string joinedNamespaces = string.Join("_1_1", parts);

        // Construct final filename
        string fileName = $"{prefix}{joinedNamespaces}_1_1{typeName}.html";

        // For safety, collapse any accidental double separators
        fileName = fileName.Replace("__", "_");

        // Compose final URL using the DocsBaseUrl
        return $"{DocsBaseUrl}/{fileName}";
    }



}
#endif