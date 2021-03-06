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
using VRC.SDKBase.Editor;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Editor;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;
using VRC.SDKBase.Validation.Performance;
using VRC.SDKBase.Validation;
using VRC.SDKBase.Validation.Performance.Stats;
using VRCStation = VRC.SDK3.Avatars.Components.VRCStation;
using VRC.SDK3.Validation;
using VRC.Core;
using VRCSDK2;
using VRCAddonInstaller;

public class VRC_Addon_Installer : EditorWindow
{
    [UnityEngine.SerializeField]
    VRCAvatarDescriptor sourceVrcAvatarDescriptor;
    Vector2 scrollPosition;

    // actions
    List<Action> actionsToPerform = new List<Action>();

    // errors
    List<System.Exception> errors = new List<System.Exception>();

    // adding
    string pathToAsset = "";
    bool isClothing = false;
    string existingSuffix = "";
    bool needToAddSuffix = true;
    string boneSuffix = "";
    [UnityEngine.SerializeField]
    GameObject gameObjectToInsertInto;
    GameObject insertedGameObject;

    // removing
    [UnityEngine.SerializeField]
    GameObject gameObjectToRemove;

    // tools
    List<string> pathsOfUnparentedBones;

    [MenuItem("PeanutTools/VRC Addon Installer")]
    public static void ShowWindow()
    {
        var window = GetWindow<VRC_Addon_Installer>();
        window.titleContent = new GUIContent("VRC Addon Installer");
        window.minSize = new Vector2(250, 50);
    }

    void HorizontalRule() {
       Rect rect = EditorGUILayout.GetControlRect(false, 1);
       rect.height = 1;
       EditorGUI.DrawRect(rect, new Color ( 0.5f,0.5f,0.5f, 1 ) );
    }

    void HandleError(System.Exception exception) {
        Debug.LogException(exception);
        AddError(exception);
        CleanupTempGameObject();
    }

    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUIStyle italicStyle = new GUIStyle(GUI.skin.label);
        italicStyle.fontStyle = FontStyle.Italic;

        GUILayout.Label("VRC Addon Installer", EditorStyles.boldLabel);
        GUILayout.Label("Helps you install addons for your VRChat avatar", italicStyle);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        HorizontalRule();
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUILayout.Label("Select your avatar", EditorStyles.boldLabel);

        VRCAvatarDescriptor oldSourceVrcAvatarDescriptor = sourceVrcAvatarDescriptor;
        sourceVrcAvatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", sourceVrcAvatarDescriptor, typeof(VRCAvatarDescriptor));

        if (sourceVrcAvatarDescriptor != oldSourceVrcAvatarDescriptor && sourceVrcAvatarDescriptor != null) {
            gameObjectToInsertInto = sourceVrcAvatarDescriptor.gameObject;
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        HorizontalRule();

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        GUILayout.Label("Add an addon", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(sourceVrcAvatarDescriptor == null);
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUILayout.Label("Path to asset:");
        pathToAsset = EditorGUILayout.TextField(pathToAsset);

        if (GUILayout.Button("Select File", GUILayout.Width(75), GUILayout.Height(25))) {
            // from https://docs.unity3d.com/2020.1/Documentation/Manual/3D-formats.html
            string pathResult = EditorUtility.OpenFilePanel("Select a file", Application.dataPath, "fbx,obj,prefab,dae,3ds,ma,mb,max,c4d,blend");

            if (pathResult != "") {
                pathToAsset = Utils.GetPathRelativeToAssets(pathResult);
                existingSuffix = "";
                ForceRefresh();
            }
        }
        GUILayout.Label("Note it must be inside your project.", italicStyle);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GameObject oldGameObjectToInsertInto = gameObjectToInsertInto;
        gameObjectToInsertInto = (GameObject)EditorGUILayout.ObjectField("GameObject to insert into:", gameObjectToInsertInto, typeof(GameObject));
        GUILayout.Label("The mesh will be inserted into this game object. Can be any GameObject including the root.", italicStyle);

        bool isGameObjectToInsertIntoValid = GetIsGameObjectTargetValid(gameObjectToInsertInto);

        if (oldGameObjectToInsertInto != gameObjectToInsertInto) {
            ClearActionsAndErrors();

            if (gameObjectToInsertInto != null && isGameObjectToInsertIntoValid == false) {
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                
                HandleError(new System.Exception("The GameObject is not a child of your avatar"));
            }
        }

        EditorGUI.BeginDisabledGroup(isGameObjectToInsertIntoValid == false);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        isClothing = EditorGUILayout.Toggle("Is clothing", isClothing);
        GUILayout.Label("Merge the bones into your armature so your clothing moves with your body.", italicStyle);

        if (isClothing) {
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            GUILayout.Label("Existing suffix (including any underscores):");
            existingSuffix = EditorGUILayout.TextField(existingSuffix);
            GUILayout.Label("If you know the bones have a suffix then the merge will not work without it.", italicStyle);

            if (GUILayout.Button("Detect", GUILayout.Width(50), GUILayout.Height(25))) {
                DetectSuffix();
            }

            EditorGUILayout.Space();

            needToAddSuffix = EditorGUILayout.Toggle("Add suffix", needToAddSuffix);
            GUILayout.Label("Will add a suffix to the end of ALL bones to help identify them.", italicStyle);

            EditorGUI.BeginDisabledGroup(needToAddSuffix == false);

            GUILayout.Label("Bone suffix:");
            boneSuffix = EditorGUILayout.TextField(boneSuffix);
            GUILayout.Label("Leave blank to generate using root name (eg. RexouiumShirt would be \"Hips_RS\").", italicStyle);
            
            EditorGUI.EndDisabledGroup();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(AreThereErrors() == true || AddActionsHelpers.isInDraftMode == true || isGameObjectToInsertIntoValid == false || pathToAsset == "");
        if (GUILayout.Button("Draft Add", GUILayout.Width(100), GUILayout.Height(50)))
        {
            try {
                DraftAddAddon();
            } catch (System.Exception exception) {

                HandleError(exception);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(AreThereErrors() == true || AddActionsHelpers.isInDraftMode == false);
        if (GUILayout.Button("Perform", GUILayout.Width(100), GUILayout.Height(50)))
        {
            try {
                AddAddon();
            } catch (System.Exception exception) {
                HandleError(exception);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.EndDisabledGroup();
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        HorizontalRule();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUILayout.Label("Remove an addon", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(sourceVrcAvatarDescriptor == null);
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GameObject originalGameObjectToRemove = gameObjectToRemove;

        gameObjectToRemove = (GameObject)EditorGUILayout.ObjectField("Game object to remove:", gameObjectToRemove, typeof(GameObject));
        GUILayout.Label("The game object and any bones that belong to immediate child skinned mesh renderers will be removed.", italicStyle);

        if (originalGameObjectToRemove != gameObjectToRemove) {
            ClearActionsAndErrors();
            CancelDraft();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(gameObjectToRemove == null);

        EditorGUI.BeginDisabledGroup(RemoveActionsHelpers.isInDraftMode == true);
        if (GUILayout.Button("Draft Remove", GUILayout.Width(100), GUILayout.Height(50)))
        {
            try {
                DraftRemoveAddon();
            } catch (System.Exception exception) {
                HandleError(exception);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(RemoveActionsHelpers.isInDraftMode == false || AreThereErrors() == true);
        if (GUILayout.Button("Perform", GUILayout.Width(100), GUILayout.Height(50)))
        {
            try {
                RemoveAddon();
            } catch (System.Exception exception) {
                HandleError(exception);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.EndDisabledGroup();
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        HorizontalRule();

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        GUILayout.Label("Tools", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        EditorGUI.BeginDisabledGroup(sourceVrcAvatarDescriptor == null);
        
        GUILayout.Label("Prune loose bones", EditorStyles.boldLabel);
        GUILayout.Label("Finds any GameObjects that have no components and are not bones.", italicStyle);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(PruneLooseBonesActionsHelpers.isInDraftMode == true);
        if (GUILayout.Button("Draft Prune", GUILayout.Width(100), GUILayout.Height(50)))
        {
            try {
                DraftPruneLooseBones();
            } catch (System.Exception exception) {
                HandleError(exception);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(PruneLooseBonesActionsHelpers.isInDraftMode == false || AreThereErrors() == true);
        if (GUILayout.Button("Prune", GUILayout.Width(100), GUILayout.Height(50)))
        {
            try {
                PruneLooseBones();
            } catch (System.Exception exception) {
                HandleError(exception);
            }
        }
        GUILayout.Label("", italicStyle);
        EditorGUI.EndDisabledGroup();
        
        EditorGUI.EndDisabledGroup();

        HorizontalRule();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (GUILayout.Button("Restart", GUILayout.Width(100), GUILayout.Height(50)))
        {
            Restart();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        RenderErrors();

        RenderActions();

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        GUILayout.Label("Links:");

        RenderLink("  Download new versions from GitHub", "https://github.com/imagitama/vrc-addon-installer");
        RenderLink("  Get support from my Discord", "https://discord.gg/R6Scz6ccdn");
        RenderLink("  Follow me on Twitter", "https://twitter.com/@HiPeanutBuddha");
        
        EditorGUILayout.EndScrollView();
    }

    void DetectSuffix() {
        GameObject importedAsset = Utils.LoadAsset<GameObject>(pathToAsset);
        Transform armature = Utils.FindArmature(importedAsset.transform);
        Transform firstBone = armature.GetChild(0);
        string boneName = firstBone.gameObject.name;

        Debug.Log("Detecting suffix using 1st bone \"" + boneName + "\"...");

        if (boneName.Count(f => (f == '_')) != 1) {
            return;
        }

        string[] chunks = boneName.Split('_');
        string suffix = chunks[1];

        existingSuffix = "_" + suffix;

        ForceRefresh();
    }

    void ForceRefresh() {
        GUI.FocusControl(null);
    }

    void Restart() {
        ClearActionsAndErrors();
        CancelDraft();
    }

    bool AreThereErrors() {
        return errors.Count > 0;
    }

    bool GetIsGameObjectTargetValid(GameObject gameObject) {
        if (gameObject == null) {
            return false;
        }

        if (gameObject == sourceVrcAvatarDescriptor.gameObject) {
            return true;
        }

        var allChildren = sourceVrcAvatarDescriptor.gameObject.GetComponentsInChildren<Transform>(true);

        foreach(Transform child in allChildren) {
            if (child == gameObject.transform) {
                return true;
            }
        }
        
        return false;
    }

    void CancelDraft() {
        AddActionsHelpers.isInDraftMode = false;
        RemoveActionsHelpers.isInDraftMode = false;
        PruneLooseBonesActionsHelpers.isInDraftMode = false;
    }

    void AddError(System.Exception exception) {
        errors.Add(exception);
    }

    void RenderErrors() {
        if (errors.Count == 0) {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUILayout.Label("Errors:");

        GUIStyle guiStyle = new GUIStyle() {
            // fontSize = 10
        };
        guiStyle.normal.textColor = Color.red;

        foreach (System.Exception exception in errors) {
            string message = "";

            // TODO: Move to the exceptions themselves (add toString method)
            if (exception is FailedToInsertGameObject) {
                string pathToExistingGameObject = (exception as FailedToInsertGameObject).pathToExistingGameObject;
                message = message + "Failed to insert GameObject: " + exception.Message + "\nImport path: " + (exception as FailedToInsertGameObject).pathToAsset + (pathToExistingGameObject != "" ? "\nExisting object: " + pathToExistingGameObject : "");
            } else if (exception is FailedToCopyBoneIntoArmature) {
                message = message + "Failed to copy bone into armature: " + exception.Message + "\nBone source: " + (exception as FailedToCopyBoneIntoArmature).pathToBone + "\nTarget: " + (exception as FailedToCopyBoneIntoArmature).pathToTarget;
            } else {
                message = exception.Message;
            }

            GUILayout.Label(message, guiStyle);
        }
    }

    void RenderLink(string label, string url) {
        Rect rect = EditorGUILayout.GetControlRect();

        if (rect.Contains(Event.current.mousePosition)) {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseUp) {
                Help.BrowseURL(url);
            }
        }

        GUIStyle style = new GUIStyle();
        style.normal.textColor = new Color(0.5f, 0.5f, 1);

        GUI.Label(rect, label, style);
    }

    void RenderActions() {
        if (actionsToPerform.Count == 0) {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        HorizontalRule();
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUILayout.Label("Actions to perform:");

        foreach (Action action in actionsToPerform) {
            RenderAction(action);
        }
    }

    void RenderAction(Action action) {
        string label = "";

        if (action is MoveBoneAction) {
            string originalPath = (action as MoveBoneAction).originalPath;
            string newParentPath = (action as MoveBoneAction).newParentPath;
            
            label = "Move bone " + originalPath + " to " + newParentPath;
        } else if (action is RenameBoneAction) {
            string originalPathToBone = (action as RenameBoneAction).originalPathToBone;
            string originalName = (action as RenameBoneAction).originalName;
            string newName = (action as RenameBoneAction).newName;
            
            label = "Rename bone " + originalPathToBone + " from " + originalName + " to " + newName;
        } else if (action is InsertGameObjectAction) {
            string pathToAsset = (action as InsertGameObjectAction).pathToAsset;
            string pathToParent = (action as InsertGameObjectAction).pathToParent;
           
            label = "Insert game object " + pathToAsset + " into " + pathToParent;
        } else if (action is RemoveGameObjectAction) {
            string pathToGameObject = (action as RemoveGameObjectAction).pathToGameObject;
            
            label = "Remove game object " + pathToGameObject;
        } else if (action is RemoveBoneAction) {
            string pathToBone = (action as RemoveBoneAction).pathToBone;
            
            label = "Remove bone " + pathToBone;
        } else if (action is PruneLooseBoneAction) {
            string pathToBone = (action as PruneLooseBoneAction).pathToBone;
            
            label = "Prune bone " + pathToBone;
        } else {
            throw new System.Exception("Cannot perform action - unknown action type: " + nameof(action));
        }

        GUILayout.Label(label);
    }

    void AddActions(List<Action> newActions) {
        Debug.Log("Adding " + newActions.Count + " actions");
        actionsToPerform.AddRange(newActions);
    }
    
    void ClearActionsAndErrors() {
        actionsToPerform = new List<Action>();
        errors = new List<System.Exception>();
    }

    void DraftAddAddon() {
        AddActionsHelpers.isInDraftMode = true;

        ClearActionsAndErrors();

        List<Action> actions = AddActionsHelpers.InsertAddonFileIntoAvatar(pathToAsset, gameObjectToInsertInto);
        Action firstAction = actions[0];
        GameObject importedAsset = (firstAction as InsertGameObjectAction).gameObject;

        AddActions(actions);

        if (isClothing == false) {
            return;
        }

        Debug.Log("Is clothing so merging into armature...");

        Transform[] avatarBones = Utils.GetSkinnedMeshRendererFromAnyChild(sourceVrcAvatarDescriptor.transform).bones;
        Transform armature = Utils.GetRootOfBones(avatarBones);

        if (armature == null) {
            throw new System.Exception("Failed to find avatar armature!");
        }

        SkinnedMeshRenderer skinnedMeshRenderer = Utils.GetSkinnedMeshRendererFromAnyChild(importedAsset.transform);

        if (skinnedMeshRenderer == null) {
            throw new System.Exception("Could not find skinned mesh renderer!");
        }

        Transform[] importedBones = skinnedMeshRenderer.bones;
    
        Transform firstBone = importedBones[0];
        Transform targetArmature = Utils.FindArmature(sourceVrcAvatarDescriptor.transform);

        if (targetArmature == null) {
            throw new System.Exception("Armature not detected!");
        }

        CleanupTempGameObject();

        GameObject tempGameObject = CreateTempGameObject();

        // warning this creates them in the root of the scene
        GameObject fakeArmatureGameObject = UnityEngine.Object.Instantiate(armature.gameObject, tempGameObject.transform);
        fakeArmatureGameObject.name = fakeArmatureGameObject.name.Replace("(Clone)", "");
        Transform fakeArmatureTransform = fakeArmatureGameObject.transform;

        // the name is important for output
        Transform importedArmature = Utils.FindArmature(importedAsset.transform);
        GameObject fakeImportedArmature = new UnityEngine.GameObject(importedArmature.gameObject.name);
        fakeImportedArmature.transform.parent = tempGameObject.transform;

        GameObject fakeFirstBoneGameObject = UnityEngine.Object.Instantiate(firstBone.gameObject, fakeImportedArmature.transform);
        fakeFirstBoneGameObject.name = fakeFirstBoneGameObject.name.Replace("(Clone)", "");
        Transform fakeFirstBoneTransform = fakeFirstBoneGameObject.transform;

        AddActionsHelpers.IsFirstBone = true;

        // we need it to think it is real as we are using a virtual armature
        AddActionsHelpers.isInDraftMode = false;
        AddActions(
            AddActionsHelpers.CopyBonesIntoArmature(
                "", 
                fakeArmatureTransform, 
                fakeImportedArmature.transform,
                fakeImportedArmature.transform.name,
                existingSuffix
            )
        );
        AddActionsHelpers.isInDraftMode = true;

        if (needToAddSuffix) {
            AddActions(AddActionsHelpers.RenameBones(
                importedBones,
                importedAsset,
                boneSuffix
            ));
        }

        CleanupTempGameObject();
    }

    void AddAddon() {
        AddActionsHelpers.isInDraftMode = false;

        ClearActionsAndErrors();

        List<Action> actions = AddActionsHelpers.InsertAddonFileIntoAvatar(pathToAsset, gameObjectToInsertInto);
        Action firstAction = actions[0];
        GameObject insertedGameObject = (firstAction as InsertGameObjectAction).gameObject;

        if (isClothing) {
            Debug.Log("Is clothing so merging into armature...");

            Transform importedArmature = Utils.FindArmature(insertedGameObject.transform);
            Transform targetArmature = Utils.FindArmature(sourceVrcAvatarDescriptor.transform);

            AddActionsHelpers.IsFirstBone = true;

            AddActionsHelpers.CopyBonesIntoArmature(
                "", 
                targetArmature, 
                importedArmature,
                targetArmature.gameObject.name,
                existingSuffix
            );
            
            SkinnedMeshRenderer skinnedMeshRenderer = Utils.GetSkinnedMeshRendererFromAnyChild(insertedGameObject.transform);
            Transform[] importedBones = skinnedMeshRenderer.bones;

            if (needToAddSuffix) {
                AddActionsHelpers.RenameBones(
                    importedBones,
                    insertedGameObject,
                    boneSuffix
                );
            }
        }
    }

    void DraftRemoveAddon() {
        RemoveActionsHelpers.isInDraftMode = true;

        ClearActionsAndErrors();

        AddActions(RemoveActionsHelpers.RemoveBonesFromImmediateSkinnedMeshRenderers(gameObjectToRemove));

        AddActions(new List<Action>() {
            new RemoveGameObjectAction() {
                pathToGameObject = Utils.GetGameObjectPath(gameObjectToRemove)
            }
        });
    }
    
    void RemoveAddon() {
        RemoveActionsHelpers.isInDraftMode = false;

        ClearActionsAndErrors();
        
        RemoveActionsHelpers.RemoveBonesFromImmediateSkinnedMeshRenderers(gameObjectToRemove);
        RemoveActionsHelpers.RemoveGameObject(gameObjectToRemove);
    }

    void DraftPruneLooseBones() {
        PruneLooseBonesActionsHelpers.isInDraftMode = true;

        ClearActionsAndErrors();

        AddActions(PruneLooseBonesActionsHelpers.PruneLooseBones(sourceVrcAvatarDescriptor.transform));
    }
    
    void PruneLooseBones() {
        PruneLooseBonesActionsHelpers.isInDraftMode = false;

        ClearActionsAndErrors();

        PruneLooseBonesActionsHelpers.PruneLooseBones(sourceVrcAvatarDescriptor.transform);
    }

    void CleanupTempGameObject() {
        GameObject temporaryGameObject = GameObject.Find("/VRC_Addon_Installer");

        if (temporaryGameObject != null) {
            DestroyImmediate(temporaryGameObject);
        }
    }

    GameObject CreateTempGameObject() {
        return new UnityEngine.GameObject("VRC_Addon_Installer");
    }
}
