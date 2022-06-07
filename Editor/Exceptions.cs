using UnityEngine;
using UnityEditor;
using System.IO;

namespace VRCAddonInstaller {
    public class FailedToInsertGameObject : System.Exception {
        public FailedToInsertGameObject(string message) : base(message) {
        }
        public string pathToAsset;
        public string pathToExistingGameObject;
    }

    public class FailedToCopyBoneIntoArmature : System.Exception {
        public FailedToCopyBoneIntoArmature(string message) : base(message) {
        }
        public string pathToBone;
        public string pathToTarget;
    }
}