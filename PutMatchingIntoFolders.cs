using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class PutMatchingIntoFolders : EditorWindow
{
    [MenuItem("Assets/Put Matching Files Into Folders")]
    private static void OrganizeMatchingFiles()
    {
        // Get the selected folder path
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (string.IsNullOrEmpty(selectedPath) || !AssetDatabase.IsValidFolder(selectedPath))
        {
            EditorUtility.DisplayDialog("Error", "Please select a folder.", "OK");
            return;
        }

        ProcessFolder(selectedPath);
    }

    private static void ProcessFolder(string folderPath)
    {
        // Get all files in the selected folder (excluding meta files)
        string[] allFilePaths = Directory.GetFiles(folderPath);
        List<string> filePaths = new List<string>();

        foreach (string path in allFilePaths)
        {
            if (!path.EndsWith(".meta"))
            {
                filePaths.Add(path);
            }
        }

        Debug.Log($"Found {filePaths.Count} files in {folderPath}");

        // Dictionary to group files by their date-time suffix
        Dictionary<string, List<string>> fileGroups = new Dictionary<string, List<string>>();

        foreach (string filePath in filePaths)
        {
            // Get filename without extension
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            Debug.Log($"Processing file: {fileNameWithoutExt}{extension}");

            // Check if filename is long enough for the date-time pattern
            if (fileNameWithoutExt.Length >= 19)
            {
                // Extract the last 19 characters (expected date-time format: "2025-03-02_20-20-49")
                string suffix = fileNameWithoutExt.Substring(fileNameWithoutExt.Length - 19);

                Debug.Log($"Extracted suffix: {suffix}");

                // Validate the suffix format (check for date-time pattern)
                if (IsValidDateTimeSuffix(suffix))
                {
                    Debug.Log($"Valid date-time suffix found: {suffix}");

                    // Add to the appropriate group
                    if (!fileGroups.ContainsKey(suffix))
                    {
                        fileGroups[suffix] = new List<string>();
                    }

                    fileGroups[suffix].Add(filePath);
                }
                else
                {
                    Debug.Log($"Invalid date-time format for suffix: {suffix}");
                }
            }
            else
            {
                Debug.Log($"Filename too short to contain date-time pattern: {fileNameWithoutExt}");
            }
        }

        // Log the groups found
        Debug.Log($"Found {fileGroups.Count} date-time groups:");
        foreach (var group in fileGroups)
        {
            Debug.Log($"Group '{group.Key}' has {group.Value.Count} files:");
            foreach (var file in group.Value)
            {
                Debug.Log($"  - {Path.GetFileName(file)}");
            }
        }

        // Move files to new folders
        int foldersCreated = 0;
        int filesMoved = 0;

        foreach (var group in fileGroups)
        {
            // Only create folders for groups with multiple files
            if (group.Value.Count > 1)
            {
                string dateTimeSuffix = group.Key;
                string newFolderPath = Path.Combine(folderPath, dateTimeSuffix);

                Debug.Log($"Creating folder: {newFolderPath}");

                // Create the folder if it doesn't exist
                if (!Directory.Exists(newFolderPath))
                {
                    Directory.CreateDirectory(newFolderPath);
                    foldersCreated++;
                }

                // Move files to the new folder
                foreach (string filePath in group.Value)
                {
                    string fileName = Path.GetFileName(filePath);
                    string destinationPath = Path.Combine(newFolderPath, fileName);

                    Debug.Log($"Moving file: {fileName} to {destinationPath}");

                    // Move the file
                    File.Move(filePath, destinationPath);
                    filesMoved++;

                    // Also move the corresponding meta file if it exists
                    string metaFilePath = filePath + ".meta";
                    if (File.Exists(metaFilePath))
                    {
                        string metaDestinationPath = destinationPath + ".meta";
                        File.Move(metaFilePath, metaDestinationPath);
                    }
                }
            }
            else
            {
                Debug.Log($"Skipping group {group.Key} with only {group.Value.Count} file");
            }
        }

        // Refresh the AssetDatabase to update the Unity editor
        AssetDatabase.Refresh();

        string resultMessage = $"Created {foldersCreated} folders and moved {filesMoved} files.";
        Debug.Log(resultMessage);

        // Display a summary dialog
        EditorUtility.DisplayDialog("Organization Complete", resultMessage, "OK");
    }

    private static bool IsValidDateTimeSuffix(string suffix)
    {
        // Simple pattern check for "YYYY-MM-DD_HH-MM-SS"
        if (suffix.Length != 19)
        {
            return false;
        }

        // Check format: "2025-03-02_20-20-49"
        return suffix[4] == '-' &&
               suffix[7] == '-' &&
               suffix[10] == '_' &&
               suffix[13] == '-' &&
               suffix[16] == '-';
    }

    // Add a menu item validation function to ensure the menu item only shows up for folders
    [MenuItem("Assets/Put Matching Files Into Folders", true)]
    private static bool ValidateOrganizeMatchingFiles()
    {
        // Only allow the menu item to be clicked if the selection is a folder
        return Selection.activeObject != null &&
               AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }
}