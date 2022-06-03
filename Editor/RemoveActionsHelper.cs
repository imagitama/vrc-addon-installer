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
    public class RemoveActionsHelpers {
        public static bool isDraft = true;

        // TODO: Recursively look through the gameobject for any skinnedmeshrenderers
        public static List<Action> RemoveBonesFromImmediateSkinnedMeshRenderers(GameObject gameObjectToRemove) {
            List<Action> actionsToReturn = new List<Action>();

            foreach (Transform child in gameObjectToRemove.transform) {
                SkinnedMeshRenderer skinnedMeshRenderer = child.GetComponent<SkinnedMeshRenderer>();

                if (skinnedMeshRenderer != null) {
                    foreach (Transform bone in skinnedMeshRenderer.bones) {
                        if (bone != null) {
                            actionsToReturn.Add(new RemoveBoneAction() {
                                pathToBone = Utils.GetGameObjectPath(bone.gameObject)
                            });
                            
                            RemoveBone(bone);
                        }
                    }
                }
            }
            
            return actionsToReturn;
        }

        public static void RemoveGameObject(GameObject objectToRemove) {
            if (isDraft) {
                return;
            }
            GameObject.DestroyImmediate(objectToRemove);
        }

        public static void RemoveBone(Transform bone) {
            if (isDraft) {
                return;
            }
            GameObject.DestroyImmediate(bone.gameObject);
        }
    }
}