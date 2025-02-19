using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class HumanoidAnimationWindow : EditorWindow
{
    private Vector2 scrollPos;
    private List<AnimationClip> humanoidAnimations = new List<AnimationClip>();
    private string searchQuery = "";
    private AnimationClip selectedClip;

    [MenuItem("Window/Humanoid Animations")]
    public static void ShowWindow()
    {
        GetWindow<HumanoidAnimationWindow>("Humanoid Animations");
    }

    private void OnEnable()
    {
        RefreshAnimations();
    }

    private void RefreshAnimations()
    {
        humanoidAnimations.Clear();

        // Find all AnimationClip assets in the project.
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null)
            {
                // Try to get the ModelImporter for this asset.
                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                // If the asset has a ModelImporter and its animation type is set to Humanoid, add it.
                if (importer != null && importer.animationType == ModelImporterAnimationType.Human)
                {
                    humanoidAnimations.Add(clip);
                }
            }
        }
    }

    private void OnGUI()
    {
        //if (GUILayout.Button("Refresh"))
        //{
        //    RefreshAnimations();
        //}

        // Search field to filter animations by name.
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        searchQuery = EditorGUILayout.TextField(searchQuery);
        EditorGUILayout.EndHorizontal();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (AnimationClip clip in humanoidAnimations)
        {
            // Filter based on the search query (ignoring case).
            if (!string.IsNullOrEmpty(searchQuery) && !clip.name.ToLower().Contains(searchQuery.ToLower()))
                continue;

            // Reserve a rect for a single row.
            Rect lineRect = EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight));
            float buttonWidth = 60f;
            Rect objectFieldRect = new Rect(lineRect.x, lineRect.y, lineRect.width - buttonWidth, lineRect.height);
            Rect buttonRect = new Rect(lineRect.x + lineRect.width - buttonWidth, lineRect.y, buttonWidth, lineRect.height);

            // If this clip is the currently selected one, draw a yellow border around its row.
            if (selectedClip == clip)
            {
                Color highlightColor = Color.yellow;
                float thickness = 20f;
                // Top border
                EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y, lineRect.width, thickness), highlightColor);
                // Bottom border
                EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y + lineRect.height - thickness, lineRect.width, thickness), highlightColor);
                // Left border
                EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y, thickness, lineRect.height), highlightColor);
                // Right border
                EditorGUI.DrawRect(new Rect(lineRect.x + lineRect.width - thickness, lineRect.y, thickness, lineRect.height), highlightColor);
            }

            // Draw the object field (read-only) for the clip.
            EditorGUI.ObjectField(objectFieldRect, clip, typeof(AnimationClip), false);

            // Draw the "Select" button.
            if (GUI.Button(buttonRect, "Select"))
            {
                Selection.activeObject = clip;
                string assetPath = AssetDatabase.GetAssetPath(clip);
                Debug.Log("AnimationClip: " + clip.name);
                Debug.Log("AnimationClip path: " + assetPath);
                selectedClip = clip;
            }

            // Allow dragging from the object field area.
            if (Event.current.type == EventType.MouseDrag && objectFieldRect.Contains(Event.current.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { clip };
                DragAndDrop.StartDrag(clip.name);
                Event.current.Use();
            }
        }
        EditorGUILayout.EndScrollView();

        // Push the Refresh button to the bottom.
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh", GUILayout.Width(position.width * 0.5f)))
        {
            RefreshAnimations();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
}
