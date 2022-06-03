using UnityEngine;
using UnityEditor;
using System.IO;

namespace VRCAddonInstaller {
    public class FailedToInsertGameObject : System.Exception {
        public FailedToInsertGameObject(string message) : base(message) {
        }
        public string pathToFbx;
        public string pathToParent;
    }
}