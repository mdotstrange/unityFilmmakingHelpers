using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class HumanoidAnimationWindow : EditorWindow
{
    private Vector2 scrollPos;
    private string searchQuery = "";
    private string lastSearchQuery = "";

    private AnimationClip selectedClip;

    // Add this near the other private fields
    private int selectedIndex = -1;


    // A small helper class to cache information about each clip.
    private class AnimationClipInfo
    {
        public AnimationClip clip;
        public string normalizedName; // Lower-case, underscores replaced with spaces.
        public string assetPath;

        public AnimationClipInfo(AnimationClip clip, string normalizedName, string assetPath)
        {
            this.clip = clip;
            this.normalizedName = normalizedName;
            this.assetPath = assetPath;
        }
    }

    // Static cache of *all* humanoid clips found in the project.
    private static List<AnimationClipInfo> cachedClips = null;

    // Filtered list used for display based on search terms.
    private List<AnimationClipInfo> filteredClips = new List<AnimationClipInfo>();

    [MenuItem("Window/Humanoid Animations")]
    public static void ShowWindow()
    {
        GetWindow<HumanoidAnimationWindow>("Humanoid Animations");
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;
        RefreshAnimations();
        ApplySearchFilter();
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        cachedClips = null;
        RefreshAnimations();
        ApplySearchFilter();
        Repaint();
    }

    // Force a (re)scan of humanoid clips in the project.
    private void RefreshAnimations()
    {
        if (cachedClips != null) return; // Only rebuild if needed

        cachedClips = new List<AnimationClipInfo>();
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null)
            {
                // Check if it's a humanoid clip
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null && importer.animationType == ModelImporterAnimationType.Human)
                {
                    string normalizedName = clip.name.ToLower().Replace("_", " ");
                    cachedClips.Add(new AnimationClipInfo(clip, normalizedName, assetPath));
                }
            }
        }
    }

    // Only re-filter when the search query changes or when the cache is invalidated
    private void ApplySearchFilter()
    {
        if (cachedClips == null)
        {
            filteredClips.Clear();
            return;
        }

        // If there's no search query, just show everything
        if (string.IsNullOrEmpty(searchQuery))
        {
            filteredClips = new List<AnimationClipInfo>(cachedClips);
            return;
        }

        // Preprocess the search query
        string[] terms = searchQuery.ToLower().Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        filteredClips.Clear();
        foreach (var clipInfo in cachedClips)
        {
            bool allMatch = true;
            foreach (string term in terms)
            {
                if (!clipInfo.normalizedName.Contains(term))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch) filteredClips.Add(clipInfo);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        string newQuery = EditorGUILayout.TextField(searchQuery);
        EditorGUILayout.EndHorizontal();

        // Only re-apply filter if the query has changed
        if (newQuery != searchQuery)
        {
            searchQuery = newQuery;
            ApplySearchFilter();
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);


        if (filteredClips != null)
        {


            // Check for arrow key presses to move selection up/down
            Event e = Event.current;
            if (e.type == EventType.KeyDown && filteredClips.Count > 0)
            {
                if (e.keyCode == KeyCode.DownArrow)
                {
                    selectedIndex++;
                    if (selectedIndex >= filteredClips.Count)
                        selectedIndex = 0;

                    // Update selectedClip to match the new index
                    selectedClip = filteredClips[selectedIndex].clip;
                    Selection.activeObject = selectedClip;
                    EditorGUIUtility.PingObject(selectedClip);

                    e.Use(); // Consume the event
                }
                else if (e.keyCode == KeyCode.UpArrow)
                {
                    selectedIndex--;
                    if (selectedIndex < 0)
                        selectedIndex = filteredClips.Count - 1;

                    // Update selectedClip to match the new index
                    selectedClip = filteredClips[selectedIndex].clip;
                    Selection.activeObject = selectedClip;
                    EditorGUIUtility.PingObject(selectedClip);

                    e.Use(); // Consume the event
                }
            }





            for (int i = 0; i < filteredClips.Count; i++)
            {
                var clipInfo = filteredClips[i];

                // Reserve a rect for a single row
                Rect lineRect = EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight));
                float buttonWidth = 60f;
                Rect objectFieldRect = new Rect(lineRect.x, lineRect.y, lineRect.width - buttonWidth, lineRect.height);
                Rect buttonRect = new Rect(lineRect.x + lineRect.width - buttonWidth, lineRect.y, buttonWidth, lineRect.height);


                // If this clip matches the selected index, highlight it
                bool isSelected = (i == selectedIndex);
                if (isSelected)
                {
                    Color oldColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 0f, 0.2f);
                    GUI.Box(lineRect, GUIContent.none);
                    GUI.color = oldColor;

                    // Optional: draw thick yellow borders or any custom highlight as before
                    // (omitted here for brevity)

                    // If user clicks on this row (or the "Select" button), update the index & selectedClip
                    if (Event.current.type == EventType.MouseDown && lineRect.Contains(Event.current.mousePosition))
                    {
                        selectedIndex = i;
                        selectedClip = clipInfo.clip;
                        Selection.activeObject = selectedClip;
                        EditorGUIUtility.PingObject(selectedClip);
                    }


                    // Highlight the currently selected clip.
                    if (selectedClip == clipInfo.clip)
                    {
                        Color highlightColor = Color.yellow;
                        float thickness = 20f;
                        // Draw borders around the row.
                        EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y, lineRect.width, thickness), highlightColor);           // Top
                        EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y + lineRect.height - thickness, lineRect.width, thickness), highlightColor); // Bottom
                        EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y, thickness, lineRect.height), highlightColor);           // Left
                        EditorGUI.DrawRect(new Rect(lineRect.x + lineRect.width - thickness, lineRect.y, thickness, lineRect.height), highlightColor);  // Right
                    }
                }


               




                // Draw the clip in a read-only object field
                EditorGUI.ObjectField(objectFieldRect, clipInfo.clip, typeof(AnimationClip), false);

                // "Select" button
                if (GUI.Button(buttonRect, "Select"))
                {
                    selectedIndex = i;
                    selectedClip = clipInfo.clip;
                    Selection.activeObject = selectedClip;
                    EditorGUIUtility.PingObject(selectedClip);
                    Debug.Log(clipInfo.clip.name + " with path: " + clipInfo.assetPath);
                }





                // Optional drag-and-drop support
                if (Event.current.type == EventType.MouseDrag && objectFieldRect.Contains(Event.current.mousePosition))
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { clipInfo.clip };
                    DragAndDrop.StartDrag(clipInfo.clip.name);
                    Event.current.Use();
                }
            }
        }
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh", GUILayout.Width(position.width * 0.5f)))
        {
            cachedClips = null;
            RefreshAnimations();
            ApplySearchFilter();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
}
