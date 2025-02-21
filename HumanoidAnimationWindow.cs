using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class HumanoidAnimationWindow : EditorWindow
{
    private Vector2 scrollPos;
    private string searchQuery = "";
    private string lastSearchQuery = "";

    private AnimationClip selectedClip;

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

        // Arrow-key handling

        HandleKeyboardInput();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (filteredClips != null)
        {
            foreach (var clipInfo in filteredClips)
            {
                // Reserve a rect for a single row
                Rect lineRect = EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight));
                float buttonWidth = 60f;
                Rect objectFieldRect = new Rect(lineRect.x, lineRect.y, lineRect.width - buttonWidth, lineRect.height);
                Rect buttonRect = new Rect(lineRect.x + lineRect.width - buttonWidth, lineRect.y, buttonWidth, lineRect.height);

                // Draw highlight behind the row if it's selected
                if (selectedClip == clipInfo.clip)
                {
                    Color oldColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 0f, 0.2f); // semi-transparent yellow
                    GUI.Box(lineRect, GUIContent.none);
                    GUI.color = oldColor;
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

                // Draw the clip in a read-only object field
                EditorGUI.ObjectField(objectFieldRect, clipInfo.clip, typeof(AnimationClip), false);

                // "Select" button now actively selects the clip in the Project & Inspector
                if (GUI.Button(buttonRect, "Select"))
                {
                    SelectClip(clipInfo);
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

        if (GUILayout.Button("Refresh", GUILayout.Width(position.width * 0.15f)))
        {
            cachedClips = null;
            RefreshAnimations();
            ApplySearchFilter();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void HandleKeyboardInput()

    {

        Event e = Event.current;

        if (e.type == EventType.KeyDown)

        {

            if (filteredClips.Count == 0) return;



            int currentIndex = filteredClips.FindIndex(info => info.clip == selectedClip);



            if (e.keyCode == KeyCode.UpArrow)

            {

                e.Use();

                // If nothing is selected, or index is -1, select the first item when pressing Up.

                currentIndex = (currentIndex < 0) ? 0 : currentIndex - 1;

                if (currentIndex < 0) currentIndex = 0;

                SelectClip(filteredClips[currentIndex]);

            }

            else if (e.keyCode == KeyCode.DownArrow)

            {

                e.Use();

                // If nothing is selected, or index is -1, select the first item when pressing Down.

                currentIndex = (currentIndex < 0) ? 0 : currentIndex + 1;

                if (currentIndex >= filteredClips.Count) currentIndex = filteredClips.Count - 1;

                SelectClip(filteredClips[currentIndex]);

            }

        }

    }



    private void SelectClip(AnimationClipInfo clipInfo)

    {

        selectedClip = clipInfo.clip;

        Selection.activeObject = clipInfo.clip;

        EditorGUIUtility.PingObject(clipInfo.clip);

        Debug.Log(clipInfo.clip.name + " with path: " + clipInfo.assetPath);

    }
}
