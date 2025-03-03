using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public class FBXConfiguratorMenuItem
{
    [MenuItem("Assets/ProcessBodyMocapFbxFiles")]
    private static void ConfigureFBX()
    {
        // Get the selected asset
        Object[] selectedAssets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);

        foreach (Object obj in selectedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);

            // Check if it's an FBX file
            if (Path.GetExtension(assetPath).ToLower() == ".fbx")
            {
                // Get the model importer
                ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

                if (modelImporter != null)
                {
                    // Set rig type to Humanoid
                    modelImporter.animationType = ModelImporterAnimationType.Human;

                    // Turn off animation compression
                    modelImporter.animationCompression = ModelImporterAnimationCompression.Off;

                    // Get the file name without extension to use as animation clip name
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);

                    // Get all animation clips
                    ModelImporterClipAnimation[] clipAnimations = modelImporter.defaultClipAnimations;

                    // If there are no clips defined yet, create a new one
                    if (clipAnimations == null || clipAnimations.Length == 0)
                    {
                        // Get the animation info from the file
                        ModelImporterClipAnimation[] clipInfo = modelImporter.clipAnimations;
                        if (clipInfo == null || clipInfo.Length == 0)
                        {
                            // Create a default clip
                            clipAnimations = new ModelImporterClipAnimation[1];
                            clipAnimations[0] = new ModelImporterClipAnimation();
                            clipAnimations[0].name = fileName;
                            clipAnimations[0].loopTime = true;
                            clipAnimations[0].firstFrame = 0;
                            clipAnimations[0].lastFrame = modelImporter.clipAnimations.Length > 0 ?
                                modelImporter.clipAnimations[0].lastFrame : 100; // Default to 100 if we can't determine
                        }
                        else
                        {
                            // Use existing clip info but modify the name and looping
                            clipAnimations = new ModelImporterClipAnimation[clipInfo.Length];
                            for (int i = 0; i < clipInfo.Length; i++)
                            {
                                clipAnimations[i] = clipInfo[i];
                                clipAnimations[i].name = fileName;
                                clipAnimations[i].loopTime = true;
                            }
                        }
                    }
                    else
                    {
                        // Modify existing clips
                        for (int i = 0; i < clipAnimations.Length; i++)
                        {
                            clipAnimations[i].name = fileName;
                            clipAnimations[i].loopTime = true;
                        }
                    }

                    

                    // Apply the clip animations
                    modelImporter.clipAnimations = clipAnimations;

                    // Apply changes and reimport
                    modelImporter.SaveAndReimport();

                    Debug.Log($"Configured {fileName}.fbx: Humanoid rig, no compression, looping enabled");

                    // Process the filename to follow the new pattern
                    string processedFileName = ProcessFileName(fileName);

                    // Extract animation clips as standalone .anim files
                    string bodyMocapPath = ExtractAnimationClips(assetPath, processedFileName);

                    // Process the face mocap file
                    if (!string.IsNullOrEmpty(bodyMocapPath))
                    {
                        string faceMocapPath = ProcessFaceMocapFile(processedFileName);

                        // Copy both files to the new directory structure
                        if (!string.IsNullOrEmpty(faceMocapPath))
                        {
                            CopyFilesToNewDirectory(bodyMocapPath, faceMocapPath);
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"Skipping {assetPath} - not an FBX file");
            }
        }
    }

    private static string ProcessFileName(string originalName)
    {
        // Replace "animation" with "BodyMocap"
        string processedName = originalName.Replace("animation", "BodyMocap");

        // Find the last underscore and remove everything after it
        int lastUnderscoreIndex = processedName.LastIndexOf('_');
        if (lastUnderscoreIndex > 0)
        {
            processedName = processedName.Substring(0, lastUnderscoreIndex);
        }

        return processedName;
    }

    private static string ExtractAnimationClips(string assetPath, string fileName)
    {
        string createdClipPath = "";

        // Get all animation clips from the asset
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (Object asset in assets)
        {
            // Check if the asset is an animation clip
            if (asset is AnimationClip clip)
            {
                // Skip clips that Unity auto-generates (they have the __preview__ prefix)
                if (clip.name.Contains("__preview__"))
                    continue;

                // Get the directory of the FBX file
                string directory = Path.GetDirectoryName(assetPath);

                // Create a unique path for the new animation clip in the same directory
                string newClipPath = Path.Combine(directory, $"{fileName}.anim");

                // Check if file already exists and create a unique name if needed
                int counter = 1;
                while (File.Exists(newClipPath))
                {
                    newClipPath = Path.Combine(directory, $"{fileName}_{counter}.anim");
                    counter++;
                }

                // Create a copy of the clip
                AnimationClip newClip = new AnimationClip();
                EditorUtility.CopySerialized(clip, newClip);

                // Ensure the new clip is set to loop
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(newClip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(newClip, settings);

                // Save the new clip as an asset
                AssetDatabase.CreateAsset(newClip, newClipPath);
                Debug.Log($"Created standalone animation clip: {newClipPath}");

                // Store the path of the created clip
                createdClipPath = newClipPath;
                break; // We just need to process one clip
            }
        }

        // Refresh the asset database
        AssetDatabase.Refresh();

        return createdClipPath;
    }

    private static string ProcessFaceMocapFile(string bodyMocapFileName)
    {
        // Extract the identifier part from BodyMocap_BigDance_DuckGirl_2025-03-02_17-46-11
        // We need BigDance_DuckGirl_2025-03-02_17-46-11
        string identifierPart = bodyMocapFileName;
        if (identifierPart.StartsWith("BodyMocap_"))
        {
            identifierPart = identifierPart.Substring("BodyMocap_".Length);
        }

        // Look for matching directory in Assets/Takes
        string takesDirectory = "Assets/Takes";
        if (!Directory.Exists(takesDirectory))
        {
            Debug.LogError($"Directory not found: {takesDirectory}");
            return "";
        }

        // Look for a directory that contains the identifier part
        string matchingDirectory = "";
        foreach (string dir in Directory.GetDirectories(takesDirectory))
        {
            if (Path.GetFileName(dir) == identifierPart)
            {
                matchingDirectory = dir;
                break;
            }
        }

        if (string.IsNullOrEmpty(matchingDirectory))
        {
            Debug.LogError($"No matching directory found in {takesDirectory} for {identifierPart}");
            return "";
        }

        // Find .anim files in the matching directory
        string[] animFiles = Directory.GetFiles(matchingDirectory, "*.anim");
        string faceMocapPath = "";

        foreach (string animFile in animFiles)
        {
            string filename = Path.GetFileNameWithoutExtension(animFile);

            // Check if it matches the pattern "SampleHead [BigDance_DuckGirl_2025-03-02_17-46-11] [001]"
            if (filename.Contains("[") && filename.Contains("]"))
            {
                // Extract the part between first [ and ]
                int startIndex = filename.IndexOf('[') + 1;
                int endIndex = filename.IndexOf(']');

                if (startIndex > 0 && endIndex > startIndex)
                {
                    string extractedPart = filename.Substring(startIndex, endIndex - startIndex);

                    // Check if this contains our identifier part
                    if (extractedPart.Contains(identifierPart) || identifierPart.Contains(extractedPart))
                    {
                        // Create the new name: "FaceMocap_BigDance_DuckGirl_2025-03-02_17-46-11"
                        string newFileName = "FaceMocap_" + extractedPart;
                        string newFilePath = Path.Combine(Path.GetDirectoryName(animFile), newFileName + ".anim");

                        // Rename the file
                        AssetDatabase.CopyAsset(animFile, newFilePath);
                        Debug.Log($"Renamed face mocap file: {newFilePath}");

                        faceMocapPath = newFilePath;
                        break;
                    }
                }
            }
        }

        return faceMocapPath;
    }

    private static void CopyFilesToNewDirectory(string bodyMocapPath, string faceMocapPath)
    {
        // Get the current scene name
        string sceneName = SceneManager.GetActiveScene().name;

        // Create the new directory structure
        string baseDir = "Assets/NewMocapRecordings";
        string sceneDir = Path.Combine(baseDir, sceneName);

        // Create directories if they don't exist
        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
            AssetDatabase.Refresh();
        }

        if (!Directory.Exists(sceneDir))
        {
            Directory.CreateDirectory(sceneDir);
            AssetDatabase.Refresh();
        }

        // Copy the body mocap file
        string bodyFileName = Path.GetFileName(bodyMocapPath);
        string newBodyPath = Path.Combine(sceneDir, bodyFileName);
        AssetDatabase.CopyAsset(bodyMocapPath, newBodyPath);
        Debug.Log($"Copied body mocap file to: {newBodyPath}");

        // Copy the face mocap file
        string faceFileName = Path.GetFileName(faceMocapPath);
        string newFacePath = Path.Combine(sceneDir, faceFileName);
        AssetDatabase.CopyAsset(faceMocapPath, newFacePath);
        Debug.Log($"Copied face mocap file to: {newFacePath}");

        AssetDatabase.Refresh();
    }

    // This validates the menu item - only show for FBX files
    [MenuItem("Assets/ProcessBodyMocapFbxFiles", true)]
    private static bool ValidateConfigureFBX()
    {
        // Check if any selected asset is an FBX
        foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (Path.GetExtension(path).ToLower() == ".fbx")
            {
                return true;
            }
        }
        return false;
    }
}