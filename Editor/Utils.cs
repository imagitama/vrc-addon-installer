using System.Linq;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.Rendering;
using UnityEditorInternal;

namespace VRCAddonInstaller {
    public class Utils {
        public static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }

        public static GameObject GetGameObjectAtPath(string pathToSearch) {
            if (pathToSearch.Substring(0, 1) != "/") {
                throw new System.Exception("First character of path to find game object must be slash!");
            }

            return GameObject.Find(pathToSearch);
        }

        public static string GetRelativeGameObjectPath(GameObject obj, GameObject relativeTo) {
            string absolutePath = GetGameObjectPath(obj);
            string relativePath = absolutePath.Replace("/" + relativeTo.name + "/", "/");
            relativePath = relativePath.Replace(relativeTo.name + "/", "/");
            relativePath = relativePath.Replace("/" + relativeTo.name, "/");
            relativePath = relativePath.Replace(relativeTo.name, "/");
            return relativePath;
        }

        public static GameObject GetGameObjectInside(GameObject obj, string pathToSearch) {
            if (pathToSearch.Substring(0, 1) != "/") {
                throw new System.Exception("First character of path to find game object must be slash!");
            }

            pathToSearch = pathToSearch.Substring(1);

            Debug.Log("Search: " + GetGameObjectPath(obj) + "/" + pathToSearch);

            Transform result = obj.transform.Find(pathToSearch);

            if (result == null) {
                Debug.Log("Failed to find - returning null");
                return null;
            }

            return result.gameObject;
        }

        public static string GetPathRelativeToAssets(string path) {
            return Path.GetFullPath(path).Replace(Path.GetFullPath(Application.dataPath), "Assets");
        }

        public static string GetDirectoryPathRelativeToAssets(string path) {
            return GetPathRelativeToAssets(Directory.GetParent(path).FullName);
        }

        public static int StringToInt(string val) {
            return System.Int32.Parse(val);
        }

        public static Transform FindChild(Transform source, string pathToChild) {
            if (pathToChild.Substring(0, 1) == "/") {
                if (pathToChild.Length == 1) {
                    return source;
                }

                pathToChild = pathToChild.Substring(1);
            }

            return source.Find(pathToChild);
        }

        public static Transform GetSkinnedMeshRendererChild(Transform transformToSearch) {
            // TODO: What if it contains multiple SMRs?
            foreach (Transform child in transformToSearch) {
                if (child.gameObject.GetComponent<SkinnedMeshRenderer>() != null) {
                    return child;
                }
            }

            return null;
        }

        public static SkinnedMeshRenderer GetSkinnedMeshRendererFromAnyChild(Transform transformToSearch) {
            return GetSkinnedMeshRendererChild(transformToSearch).GetComponent<SkinnedMeshRenderer>();
        }

        public static Transform GetRootOfBones(Transform[] boneTransforms) {
            if (boneTransforms.Length == 0) {
                throw new System.Exception("Cannot get root of bones without bones!");
            }

            Debug.Log("Getting root of " + boneTransforms.Length + " bones...");

            Transform firstBoneTransform = boneTransforms[0];

            string absolutePathToFirstBone = GetGameObjectPath(firstBoneTransform.gameObject);

            string[] chunksInPath = absolutePathToFirstBone.Split('/');

            for (int i = chunksInPath.Length; i >= 0; i--) {
                string currentPath = "";

                for (int x = 0; x < i; x++) {
                    if (currentPath == "/") {
                        currentPath = currentPath.Substring(1);
                    }
                    currentPath = currentPath + "/" + chunksInPath[x];
                }

                List<Transform> newBones = boneTransforms.ToList();
                newBones.RemoveAt(0);
                Transform[] remainingBoneTransforms = newBones.ToArray();

                var hasAMatch = false;

                foreach (Transform boneTransform in remainingBoneTransforms) {
                    string absolutePathToThisBone = GetGameObjectPath(firstBoneTransform.gameObject);
                    
                    if (currentPath == absolutePathToThisBone) {
                        hasAMatch = true;
                    }
                }

                if (hasAMatch == false) {
                    return GameObject.Find(currentPath).transform;
                }
            }

            return null;
        }

        public static T LoadAsset<T>(string absolutePathToAsset) where T : UnityEngine.Object {
            string relativePathToAsset = absolutePathToAsset.Replace(Application.dataPath, "Assets");
            return AssetDatabase.LoadAssetAtPath<T>(relativePathToAsset);
        }

        public static Transform[] GetChildTransforms(Transform thing) {
            Transform[] results = new Transform[thing.childCount];
            for (int i = 0; i < results.Length; i++) {
                results[i] = thing.GetChild(i);
            }
            return results;
        }

        public static Transform FindArmature(Transform thing) {
            foreach (Transform child in thing) {
                var components = child.GetComponents(typeof(Component));

                // big assumption here but it should work
                // all gameobjects have a transform component
                if (components.Length == 1) {
                    return child;
                }
            }

            return null;
        }

        public static Transform[] GetAllChildren(Transform thing) {
            Transform[] allChildrenAndSelf = thing.GetComponentsInChildren<Transform>();
            Transform[] allChildren = new Transform[allChildrenAndSelf.Length - 1];
            int o = 0;

            for (int i = 0; i < allChildrenAndSelf.Length; i++) {
                Transform childOrSelf = allChildrenAndSelf[i];

                if (childOrSelf.gameObject.GetInstanceID() != thing.gameObject.GetInstanceID()) {
                    allChildren[o] = childOrSelf;
                    o++;
                }
            }


            return allChildren;
        }

        public static SkinnedMeshRenderer[] GetAllSkinnedMeshRenderers(Transform thing) {
            return thing.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        }
    }
}