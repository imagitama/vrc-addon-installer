using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEditor.Animations;
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

namespace VRCAddonInstaller {
    public class Action {
    }

    public class MoveBoneAction : Action {
        public string originalPath;
        public string newParentPath;
    }

    public class RenameBoneAction : Action {
        public string originalPathToBone;
        public string originalName;
        public string newName;
    }

    public class InsertGameObjectAction : Action {
        public string pathToAsset;
        public string pathToParent;
        public GameObject gameObject;
    }
    
    public class RemoveGameObjectAction : Action {
        public string pathToGameObject;
    }

    public class RemoveBoneAction : Action {
        public string pathToBone;
    }

    public class PruneLooseBoneAction : Action {
        public string pathToBone;
    }
}