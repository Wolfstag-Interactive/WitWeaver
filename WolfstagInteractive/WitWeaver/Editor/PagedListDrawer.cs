
using UnityEditor;
using UnityEngine;

/*#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif*/

namespace WolfstagInteractive.ConvoCore.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1Editor_1_1GenericPagedListDrawer.html")]
[CustomPropertyDrawer(typeof(PagedListAttribute))]
    public sealed class GenericPagedListDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PagedListUtility.DrawPagedList(property, ((PagedListAttribute)attribute).DefaultItemsPerPage);
        }
    }
    [CustomPropertyDrawer(typeof(PagedListAttribute))]
    public sealed class PagedListDrawer : PropertyDrawer
    {
        private const string PREF_KEY_PREFIX = "PagedListDrawer_";
        private const string PAGE_SIZE_KEY_SUFFIX = "_PageSize";

        private int GetSavedPage(string key)
        {
            return SessionState.GetInt(key, 0);
        }
       
        private void SavePage(string key, int page)
        {
            SessionState.SetInt(key, page);
        }

        private int GetSavedPageSize(string key, int defaultSize)
        {
            return EditorPrefs.GetInt(key + PAGE_SIZE_KEY_SUFFIX, defaultSize);
        }
/*#if ODIN_INSPECTOR
    // Odin variant: auto-bridges to Odin’s paging system
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class PagedListOdinDrawer : ListDrawerSettingsAttribute
    {
        public PagedListOdinDrawer(int itemsPerPage = 25)
        {
            Paged = true;
            NumberOfItemsPerPage = Mathf.Max(1, itemsPerPage);
        }
    }
#else*/
        private void SavePageSize(string key, int size)
        {
            EditorPrefs.SetInt(key + PAGE_SIZE_KEY_SUFFIX, size);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!property.isArray)
            {
                EditorGUI.LabelField(position, "PagedList only works with arrays/lists");
                return;
            }

            var attr = (PagedListAttribute)attribute;
            string uniqueKey = PREF_KEY_PREFIX + property.serializedObject.targetObject.GetEntityId() + "_" + property.propertyPath;

            int pageSize = Mathf.Max(1, GetSavedPageSize(uniqueKey, attr.DefaultItemsPerPage));
            int total = property.arraySize;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(total / (float)pageSize));

            int currentPage = Mathf.Clamp(GetSavedPage(uniqueKey), 0, totalPages - 1);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{label.text} ({total})", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Page size field
            int newPageSize = EditorGUILayout.IntField("Per Page", pageSize, GUILayout.Width(120));
            if (newPageSize != pageSize && newPageSize > 0)
            {
                pageSize = newPageSize;
                SavePageSize(uniqueKey, pageSize);
                totalPages = Mathf.Max(1, Mathf.CeilToInt(total / (float)pageSize));
                currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);
            }

            // Page controls
            if (GUILayout.Button("<", GUILayout.Width(25))) currentPage = Mathf.Max(currentPage - 1, 0);
            GUILayout.Label($"{currentPage + 1}/{totalPages}", GUILayout.Width(60));
            if (GUILayout.Button(">", GUILayout.Width(25))) currentPage = Mathf.Min(currentPage + 1, totalPages - 1);
            EditorGUILayout.EndHorizontal();

            SavePage(uniqueKey, currentPage);

            EditorGUI.indentLevel++;
            int start = currentPage * pageSize;
            int end = Mathf.Min(start + pageSize, total);
            for (int i = start; i < end; i++)
            {
                var el = property.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(el, GUIContent.none, true);
            }
            EditorGUI.indentLevel--;
        }
    }
}
//#endif