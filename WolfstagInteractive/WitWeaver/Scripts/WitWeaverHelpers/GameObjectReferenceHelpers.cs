using UnityEngine;
using System;

namespace WolfstagInteractive.ConvoCore
{
    [UnityEngine.HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1GameObjectReference.html")]
[Serializable]
    public class GameObjectReference
    {
        [SerializeField] private GameObject directReference;
        [SerializeField] private string objectName;
        [SerializeField] private string tagName;
        [SerializeField] private FindMethod findMethod = FindMethod.DirectReference;
        
        public enum FindMethod
        {
            DirectReference,
            ByName,
            ByTag,
            ByNameInChildren,
            ByTagInChildren
        }

        private GameObject cachedGameObject;
        private bool hasSearched = false;

        /// <summary>
        /// Gets the GameObject using the specified find method
        /// </summary>
        public GameObject GameObject
        {
            get
            {
                if (cachedGameObject != null && hasSearched)
                    return cachedGameObject;

                cachedGameObject = FindGameObject();
                hasSearched = true;
                return cachedGameObject;
            }
        }

        /// <summary>
        /// Forces a refresh of the cached GameObject reference
        /// </summary>
        public void RefreshReference()
        {
            hasSearched = false;
            cachedGameObject = null;
        }

        private GameObject FindGameObject()
        {
            switch (findMethod)
            {
                case FindMethod.DirectReference:
                    return directReference;
                    
                case FindMethod.ByName:
                    if (!string.IsNullOrEmpty(objectName))
                        return GameObject.Find(objectName);
                    break;
                    
                case FindMethod.ByTag:
                    if (!string.IsNullOrEmpty(tagName))
                        return GameObject.FindGameObjectWithTag(tagName);
                    break;
                    
                case FindMethod.ByNameInChildren:
                    if (!string.IsNullOrEmpty(objectName))
                    {
                        // This requires a parent context, which could be provided via a static context or parameter
                        return GameObjectHelper.FindInChildren(objectName);
                    }
                    break;
                    
                case FindMethod.ByTagInChildren:
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        return GameObjectHelper.FindByTagInChildren(tagName);
                    }
                    break;
            }
            
            return null;
        }

        /// <summary>
        /// Checks if the reference is valid and can find a GameObject
        /// </summary>
        public bool IsValid()
        {
            return GameObject != null;
        }
    }
}