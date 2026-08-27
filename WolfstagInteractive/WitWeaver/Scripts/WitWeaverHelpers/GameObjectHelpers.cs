using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WolfstagInteractive.ConvoCore
{
[UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GameObjectHelper.html")]
    public static class GameObjectHelper
    {
        private static Transform searchRoot;
        
        /// <summary>
        /// Sets the root transform for child searches. If null, searches entire scene.
        /// </summary>
        public static void SetSearchRoot(Transform root)
        {
            searchRoot = root;
        }

        /// <summary>
        /// Finds a GameObject by name in children of the search root
        /// </summary>
        public static GameObject FindInChildren(string name)
        {
            if (searchRoot != null)
            {
                Transform found = searchRoot.Find(name);
                if (found != null) return found.gameObject;
                
                // Deep search in all children
                return FindInChildrenRecursive(searchRoot, name);
            }
            
            // Search in all root objects if no search root is set
            foreach (GameObject rootObj in GetRootGameObjects())
            {
                GameObject found = FindInChildrenRecursive(rootObj.transform, name);
                if (found != null) return found;
            }
            
            return null;
        }

        /// <summary>
        /// Finds a GameObject by tag in children of the search root
        /// </summary>
        public static GameObject FindByTagInChildren(string tag)
        {
            if (searchRoot != null)
            {
                return FindByTagInChildrenRecursive(searchRoot, tag);
            }
            
            // Search in all root objects if no search root is set
            foreach (GameObject rootObj in GetRootGameObjects())
            {
                GameObject found = FindByTagInChildrenRecursive(rootObj.transform, tag);
                if (found != null) return found;
            }
            
            return null;
        }

        /// <summary>
        /// Gets all GameObjects by name in the scene
        /// </summary>
        public static GameObject[] FindAllByName(string name)
        {
            return GetAllGameObjects().Where(go => go.name == name).ToArray();
        }

        /// <summary>
        /// Gets all GameObjects by tag in the scene
        /// </summary>
        public static GameObject[] FindAllByTag(string tag)
        {
            return GameObject.FindGameObjectsWithTag(tag);
        }

        private static GameObject FindInChildrenRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child.gameObject;
                    
                GameObject found = FindInChildrenRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindByTagInChildrenRecursive(Transform parent, string tag)
        {
            foreach (Transform child in parent)
            {
                if (child.CompareTag(tag))
                    return child.gameObject;
                    
                GameObject found = FindByTagInChildrenRecursive(child, tag);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject[] GetRootGameObjects()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        }

        private static GameObject[] GetAllGameObjects()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.scene.isLoaded)
                .ToArray();
        }
    }
}