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
    public class PruneLooseBonesActionsHelpers {
        public static bool isDraft = true;

        public static List<Action> PruneLooseBones(Transform avatarRoot) {
            List<Action> actions = new List<Action>();

            Transform armature = Utils.FindArmature(avatarRoot);

            Debug.Log("Found armature: " + Utils.GetGameObjectPath(armature.gameObject));

            Transform[] allChildren = Utils.GetAllChildren(armature);

            Debug.Log("Found " + allChildren.Length + " game objects");

            Transform[] bones = GetAllRealBones(avatarRoot);

            Debug.Log("Found " + bones.Length + " real bones");

            List<Transform> childrenToDelete = new List<Transform>();

            foreach (Transform child in allChildren) {
                Debug.Log(Utils.GetGameObjectPath(child.gameObject));

                var match = false;

                foreach (Transform bone in bones) {
                    if (child == bone) {
                        match = true;
                    }
                }

                var components = child.GetComponents(typeof(Component));

                // note: always a transform component
                if (match == false && components.Length == 1) {
                    childrenToDelete.Add(child);
                }
            }

            Debug.Log("Deleting " + childrenToDelete.Count + " loose bones...");

            foreach (Transform childToDelete in childrenToDelete) {
                    // if a parent is deleted then this one will be empty
                if (childToDelete == null || childToDelete.gameObject == null) {
                    continue;
                }

                string pathToBone = Utils.GetGameObjectPath(childToDelete.gameObject);

                actions.Add(new PruneLooseBoneAction() {
                    pathToBone = pathToBone
                });
                
                if (isDraft == false) {
                    GameObject.DestroyImmediate(childToDelete.gameObject);
                }
            }

            return actions;
        }

        public static Transform[] GetAllRealBones(Transform avatarRoot) {
            SkinnedMeshRenderer[] skinnedMeshRenderers = Utils.GetAllSkinnedMeshRenderers(avatarRoot);
            List<Transform> bones = new List<Transform>();

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers) {
                bones.AddRange(skinnedMeshRenderer.bones.ToList());
            }

            return bones.ToArray();
        }
    }
}