# place the unity editor dir in your PATH

& 'Unity.exe' `
    -batchmode `
    -nographics `
    -quit `
    -projectPath ../../../ `
    -exportPackage "Assets/PeanutTools" `
    "Assets/PeanutTools/VRC_Addon_Installer/peanuttools_vrcaddoninstaller_VERSION.unitypackage"