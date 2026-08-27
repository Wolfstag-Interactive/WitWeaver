#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WolfstagInteractive.WitWeaver.Editor
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/witweaver/api/classWolfstagInteractive_1_1WitWeaver_1_1Editor_1_1ExpressionIdSelectorDrawer.html")]
[CustomPropertyDrawer(typeof(ExpressionIDSelectorAttribute))]
    public class ExpressionIdSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (ExpressionIDSelectorAttribute)attribute;

            // Find the representation property relative to this field
            var repPropPath = property.propertyPath.Replace(property.name, attr.RepresentationPropertyName);
            var repProp = property.serializedObject.FindProperty(repPropPath);

            var rep = repProp?.objectReferenceValue as CharacterRepresentationBase;
            if (rep == null)
            {
                EditorGUI.HelpBox(position, "Assign a Representation to select an Expression.", MessageType.Info);
                return;
            }

            string[] names;
            string[] ids;

            if (rep is IExpressionCatalogProvider catalogProvider)
            {
                var catalog = catalogProvider.GetExpressionCatalog();
                names = catalog.Select(c => c.name).ToArray();
                ids   = catalog.Select(c => c.id).ToArray();
            }
            else if (rep is PrefabCharacterRepresentationData prefabRep)
            {
                var catalog = prefabRep.GetExpressionCatalog();
                names = catalog.Select(c => c.name).ToArray();
                ids   = catalog.Select(c => c.id).ToArray();
            }
            else if (rep is SpriteCharacterRepresentationData spriteRep)
            {
                var catalog = spriteRep.GetExpressionCatalog();
                names = catalog.Select(c => c.name).ToArray();
                ids   = catalog.Select(c => c.id).ToArray();
            }
            else
            {
                EditorGUI.HelpBox(position, "Representation does not expose a GUID catalog.", MessageType.Warning);
                return;
            }

            if (ids.Length == 0)
            {
                EditorGUI.Popup(position, label.text, -1, new[] { "(No Expressions)" });
                return;
            }

            var currentId = property.stringValue;
            var idx = Mathf.Max(0, System.Array.IndexOf(ids, currentId));
            var newIdx = EditorGUI.Popup(position, label.text, idx, names);

            if (newIdx != idx && newIdx >= 0 && newIdx < ids.Length)
            {
                property.stringValue = ids[newIdx];
            }
        }
    }
}
#endif