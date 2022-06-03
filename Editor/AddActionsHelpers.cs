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
    public class AddActionsHelpers {
        public static bool isDraft = true;

        // TODO: Support other formats eg. prefabs
        public static List<Action> InsertAddonFileIntoAvatar(string pathToFbx, GameObject newParentGameObject) {
            List<Action> actions = new List<Action>();

            GameObject importedFbx = Utils.LoadAsset<GameObject>(pathToFbx);

            if (importedFbx == false) {
                throw new FailedToInsertGameObject("The asset does not seem to exist or is the incorrect format (fbx)");
            }

            Transform existingTransform = newParentGameObject.transform.Find(importedFbx.name);

            if (existingTransform != null) {
                throw new FailedToInsertGameObject("There is already a game object in the avatar with the same name");
            }

            string pathToParent = Utils.GetGameObjectPath(newParentGameObject);
            string finalPath = pathToParent + "/" + importedFbx.name;

            Transform transformToInsertInto = newParentGameObject.transform;

            if (isDraft) {
                actions.Add(new InsertGameObjectAction() {
                    pathToFbx = pathToFbx,
                    pathToParent = pathToParent,
                    gameObject = importedFbx
                });

                return actions;
            }

            GameObject createdGameObject = UnityEngine.Object.Instantiate(importedFbx, transformToInsertInto);
            createdGameObject.name = createdGameObject.name.Replace("(Clone)", "");

            actions.Add(new InsertGameObjectAction() {
                pathToFbx = pathToFbx,
                pathToParent = pathToParent,
                gameObject = createdGameObject
            });

            return actions;
        }

        public static List<Action> CopyBonesIntoArmature(string currentPathInsideTargetArmature, Transform targetArmature, Transform parentOfBonesToMove) {
            List<Action> actionsToReturn = new List<Action>();

            Debug.Log("Copying " + parentOfBonesToMove.childCount + " bones (" + currentPathInsideTargetArmature + ")...");

            if (currentPathInsideTargetArmature == "/") {
                throw new System.Exception("Current path cannot be slash! Use empty string for root");
            }

            List<Transform> bonesToOperateOn = new List<Transform>();

            // need to copy the transforms because when we move the bones they no longer become our children (for some reason) so it fails
            foreach (Transform boneToMove in parentOfBonesToMove) {
                // if (boneToMove.gameObject.name.Contains("_end")) {
                //     continue;
                // }
                bonesToOperateOn.Add(boneToMove);
            }

            foreach (Transform boneToMove in bonesToOperateOn) {
                string currentBoneName = boneToMove.gameObject.name;

                Debug.Log("Attempting to move bone " + currentBoneName + "");

                string pathToTargetParentBone = currentPathInsideTargetArmature;
                Transform targetParentBone = targetArmature.Find(pathToTargetParentBone);

                Debug.Log("Searching bone in target \"" + pathToTargetParentBone + "\" for any similar bones to this one...");

                if (targetParentBone == null) {
                    throw new System.Exception("Target parent bone does not exist at " + pathToTargetParentBone);
                }

                Transform newParent = null;

                foreach (Transform boneInsideTargetParentBone in targetParentBone) {
                    string boneName = boneInsideTargetParentBone.gameObject.name;

                    if (currentBoneName.Contains(boneName)) {
                        newParent = boneInsideTargetParentBone;
                    }
                }

                string newPathInsideTargetArmature;

                if (newParent == null) {
                    Debug.Log("Could not find any similar bones to this one so just dumping it into the target");
                    newParent = targetParentBone;

                    newPathInsideTargetArmature = (currentPathInsideTargetArmature != "" ? currentPathInsideTargetArmature + "/" : "") + currentBoneName;
                } else {
                    Debug.Log("We found a similar bone! Placing it under... " + currentBoneName + " => " + newParent.gameObject.name);

                    newPathInsideTargetArmature = (currentPathInsideTargetArmature != "" ? currentPathInsideTargetArmature + "/" : "") + newParent.gameObject.name;
                }

                string originalPath = Utils.GetGameObjectPath(boneToMove.gameObject);
                string newParentPath = Utils.GetGameObjectPath(newParent.gameObject);

                if (isDraft == false) {
                    boneToMove.SetParent(newParent);
                }

                actionsToReturn.Add(new MoveBoneAction() {
                    originalPath = originalPath,
                    newParentPath = newParentPath
                });


                var newActions = CopyBonesIntoArmature(newPathInsideTargetArmature, targetArmature, boneToMove);
                actionsToReturn.AddRange(newActions);
            }

            return actionsToReturn;
        }

        public static List<Action> RenameBones(Transform[] bonesToRename, GameObject baseGameObject, string suffix = "") {
            List<Action> actionsToReturn = new List<Action>();

            foreach (Transform bone in bonesToRename) {
                string originalName = bone.gameObject.name;
                string newName = GetNameOfNewBone(bone, baseGameObject, suffix);
                string originalPathToBone = Utils.GetGameObjectPath(bone.gameObject);

                actionsToReturn.Add(new RenameBoneAction() {
                    originalPathToBone = originalPathToBone,
                    originalName = originalName,
                    newName = newName
                });

                if (isDraft == false) {
                    RenameBone(bone, newName);
                }
            }

            return actionsToReturn;
        }

        public static void RenameBone(Transform bone, string newName) {
            bone.gameObject.name = newName;
        }

        public static string GenerateSuffix(GameObject baseGameObject) {
            List<string> charactersToUse;

            charactersToUse = baseGameObject.name.Where(character => System.Char.IsUpper(character)).Select(character => character.ToString()).ToList();

            if (charactersToUse.Count > 0) {
                charactersToUse = charactersToUse.GetRange(0, 2);
            }

            if (charactersToUse.Count == 0) {
                string[] chars = baseGameObject.name.Split('_').Select(word => word.Substring(0, 1)).ToArray();
                charactersToUse = chars.ToList();
            }

            if (charactersToUse.Count == 0) {
                charactersToUse = baseGameObject.name.Select(character => character.ToString()).ToList().GetRange(0, 4);
            }

            return System.String.Join("", charactersToUse.Select(character => character.ToUpper()).ToArray());
        }

        public static string GetNameOfNewBone(Transform bone, GameObject baseGameObject, string boneSuffix = "") {
            string currentName = bone.gameObject.name;

            // blender can add "ends" to bones
            if (boneSuffix == "") {
                boneSuffix = GenerateSuffix(baseGameObject);
            }

            return currentName + "_" + boneSuffix;
        }
    }
}