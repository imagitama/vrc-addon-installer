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

    // adding
    string pathToFbx = "";
    bool isClothing = false;
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

        sourceVrcAvatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", sourceVrcAvatarDescriptor, typeof(VRCAvatarDescriptor));
        
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

        GUILayout.Label("Path to FBX:");
        pathToFbx = EditorGUILayout.TextField(pathToFbx);

        if (GUILayout.Button("Select File", GUILayout.Width(75), GUILayout.Height(25))) {
            string pathResult = EditorUtility.OpenFilePanel("Select a FBX", Application.dataPath, "fbx");

            if (pathResult != "") {
                pathToFbx = Utils.GetPathRelativeToAssets(pathResult);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        gameObjectToInsertInto = (GameObject)EditorGUILayout.ObjectField("Game object to insert into:", gameObjectToInsertInto, typeof(GameObject));
        GUILayout.Label("The mesh in the FBX will be inserted into this game object. Can be the root avatar or any bone.", italicStyle);

        EditorGUI.BeginDisabledGroup(gameObjectToInsertInto == null);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        isClothing = EditorGUILayout.Toggle("Is clothing", isClothing);
        GUILayout.Label("Enabling this will merge the bones into your armature so your clothing moves with your body.", italicStyle);

        if (isClothing) {
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            GUILayout.Label("Bone suffix (if not set):");
            boneSuffix = EditorGUILayout.TextField(boneSuffix);
            GUILayout.Label("Add a suffix to help you identify the bones.", italicStyle);
            GUILayout.Label("Leave blank to generate using the accessory name (eg. RexouiumShirt would be \"Hips_RS\").", italicStyle);
            GUILayout.Label("Your suffix will be ignored if the bones already have them.", italicStyle);
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(AddActionsHelpers.isDraft == true);
        if (GUILayout.Button("Draft Add", GUILayout.Width(100), GUILayout.Height(50)))
        {
            DraftAddAddon();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(AddActionsHelpers.isDraft == false);
        if (GUILayout.Button("Perform", GUILayout.Width(100), GUILayout.Height(50)))
        {
            AddAddon();
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
            ClearActions();
            CancelDraft();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(gameObjectToRemove == null);

        EditorGUI.BeginDisabledGroup(RemoveActionsHelpers.isDraft == true);
        if (GUILayout.Button("Draft Remove", GUILayout.Width(100), GUILayout.Height(50)))
        {
            DraftRemoveAddon();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(RemoveActionsHelpers.isDraft == false);
        if (GUILayout.Button("Perform", GUILayout.Width(100), GUILayout.Height(50)))
        {
            RemoveAddon();
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

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(PruneLooseBonesActionsHelpers.isDraft == true);
        if (GUILayout.Button("Draft Prune", GUILayout.Width(100), GUILayout.Height(50)))
        {
            DraftPruneLooseBones();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(PruneLooseBonesActionsHelpers.isDraft == false);
        if (GUILayout.Button("Prune", GUILayout.Width(100), GUILayout.Height(50)))
        {
            PruneLooseBones();
        }
        GUILayout.Label("", italicStyle);
        EditorGUI.EndDisabledGroup();
        
        EditorGUI.EndDisabledGroup();

        HorizontalRule();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (GUILayout.Button("Restart", GUILayout.Width(100), GUILayout.Height(50)))
        {
            CancelDraft();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        RenderActions();

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        
        GUILayout.Label("Links:");

        RenderLink("  Download new versions from GitHub", "https://github.com/imagitama/vrc-addon-installer");
        RenderLink("  Get support from my Discord", "https://discord.gg/R6Scz6ccdn");
        RenderLink("  Follow me on Twitter", "https://twitter.com/@HiPeanutBuddha");
        
        EditorGUILayout.EndScrollView();
    }

    void CancelDraft() {
        AddActionsHelpers.isDraft = false;
        RemoveActionsHelpers.isDraft = false;
        PruneLooseBonesActionsHelpers.isDraft = false;
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
            string pathToFbx = (action as InsertGameObjectAction).pathToFbx;
            string pathToParent = (action as InsertGameObjectAction).pathToParent;
           
            label = "Insert game object " + pathToFbx + " into " + pathToParent;
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

    void DraftAddAddon() {
        AddActionsHelpers.isDraft = true;
        ClearActions();

        List<Action> actions = AddActionsHelpers.InsertAddonFileIntoAvatar(pathToFbx, sourceVrcAvatarDescriptor.gameObject);
        Action firstAction = actions[0];
        GameObject importedFbx = (firstAction as InsertGameObjectAction).gameObject;

        AddActions(actions);

        if (isClothing) {
            Debug.Log("Is clothing so merging into armature...");

            Transform[] avatarBones = Utils.GetSkinnedMeshRendererFromAnyChild(sourceVrcAvatarDescriptor.transform).bones;
            Transform armature = Utils.GetRootOfBones(avatarBones);

            if (armature == null) {
                throw new System.Exception("Failed to find avatar armature!");
            }

            SkinnedMeshRenderer skinnedMeshRenderer = Utils.GetSkinnedMeshRendererFromAnyChild(importedFbx.transform);

            if (skinnedMeshRenderer == null) {
                throw new System.Exception("Could not find skinned mesh renderer!");
            }

            Transform[] importedBones = skinnedMeshRenderer.bones;
        
            Transform firstBone = importedBones[0];
            Transform targetArmature = Utils.FindArmature(sourceVrcAvatarDescriptor.transform);

            if (targetArmature == null) {
                throw new System.Exception("Armature not detected!");
            }

            GameObject fakeArmatureGameObject = UnityEngine.Object.Instantiate(armature.gameObject);
            fakeArmatureGameObject.name = fakeArmatureGameObject.name.Replace("(Clone)", "");
            Transform fakeArmatureTransform = fakeArmatureGameObject.transform;

            GameObject fakeFirstBoneGameObject = UnityEngine.Object.Instantiate(firstBone.gameObject, fakeArmatureTransform);
            fakeFirstBoneGameObject.name = fakeFirstBoneGameObject.name.Replace("(Clone)", "");
            Transform fakeFirstBoneTransform = fakeFirstBoneGameObject.transform;

            // we need it to think it is real as we are using a virtual armature
            AddActionsHelpers.isDraft = false;
            AddActions(
                AddActionsHelpers.CopyBonesIntoArmature(
                    avatarBones[0].gameObject.name, 
                    fakeArmatureTransform, 
                    fakeFirstBoneTransform
                )
            );
            AddActionsHelpers.isDraft = true;

            AddActions(AddActionsHelpers.RenameBones(
                importedBones,
                importedFbx,
                boneSuffix
            ));

            // TODO: If error happens this wont be cleaned up
            DestroyImmediate(fakeArmatureGameObject);
            DestroyImmediate(fakeFirstBoneGameObject);
        }
    }

    void ClearActions() {
        actionsToPerform = new List<Action>();
    }

    void AddAddon() {
        AddActionsHelpers.isDraft = false;
        ClearActions();

        List<Action> actions = AddActionsHelpers.InsertAddonFileIntoAvatar(pathToFbx, sourceVrcAvatarDescriptor.gameObject);
        Action firstAction = actions[0];
        GameObject insertedGameObject = (firstAction as InsertGameObjectAction).gameObject;

        if (isClothing) {
            Debug.Log("Is clothing so merging into armature...");

            Transform importedArmature = Utils.FindArmature(insertedGameObject.transform);
            Transform targetArmature = Utils.FindArmature(sourceVrcAvatarDescriptor.transform);

            AddActionsHelpers.CopyBonesIntoArmature(
                "", 
                targetArmature, 
                importedArmature
            );
            
            SkinnedMeshRenderer skinnedMeshRenderer = Utils.GetSkinnedMeshRendererFromAnyChild(insertedGameObject.transform);
            Transform[] importedBones = skinnedMeshRenderer.bones;

            AddActionsHelpers.RenameBones(
                importedBones,
                insertedGameObject,
                boneSuffix
            );
        }
    }

    void DraftRemoveAddon() {
        RemoveActionsHelpers.isDraft = true;
        ClearActions();

        AddActions(RemoveActionsHelpers.RemoveBonesFromImmediateSkinnedMeshRenderers(gameObjectToRemove));

        AddActions(new List<Action>() {
            new RemoveGameObjectAction() {
                pathToGameObject = Utils.GetGameObjectPath(gameObjectToRemove)
            }
        });
    }
    
    void RemoveAddon() {
        RemoveActionsHelpers.isDraft = false;
        ClearActions();
        
        RemoveActionsHelpers.RemoveBonesFromImmediateSkinnedMeshRenderers(gameObjectToRemove);
        RemoveActionsHelpers.RemoveGameObject(gameObjectToRemove);
    }

    void DraftPruneLooseBones() {
        PruneLooseBonesActionsHelpers.isDraft = true;
        ClearActions();

        AddActions(PruneLooseBonesActionsHelpers.PruneLooseBones(sourceVrcAvatarDescriptor.transform));
    }
    
    void PruneLooseBones() {
        PruneLooseBonesActionsHelpers.isDraft = false;
        ClearActions();

        PruneLooseBonesActionsHelpers.PruneLooseBones(sourceVrcAvatarDescriptor.transform);
    }
}
