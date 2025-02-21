using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class HumanoidAnimationWindow : EditorWindow
{
    private Vector2 scrollPos;
    private string searchQuery = "";
    private AnimationClip selectedClip;

    // A small helper class to cache information about each clip.
    private class AnimationClipInfo
    {
        public AnimationClip clip;
        public string nameNormalized; // Lower-case, underscores replaced, etc.
        public string pathNormalized; // Also lower-case, etc.
        public string assetPath;

        public AnimationClipInfo(AnimationClip clip, string normalizedName, string normalizedPath, string assetPath)
        {
            this.clip = clip;
            this.nameNormalized = normalizedName;
            this.pathNormalized = normalizedPath;
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
        // Only rebuild if needed
        if (cachedClips != null) return;

        cachedClips = new List<AnimationClipInfo>();
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null)
            {
                // Try to get the importer for this asset
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;

                // If it's an FBX (or similar) with humanoid rig
                if (importer != null && importer.animationType == ModelImporterAnimationType.Human)
                {
                    AddToCache(clip, assetPath);
                }
                else
                {
                    // If it's a .anim or something else, we can check if the clip is actually humanoid via isHumanMotion
                    // Note: isHumanMotion == true if the clip drives a humanoid rig (i.e., has muscle curves, etc.)
                    // This should also return .anim files that are humanoid.
                    if (clip.isHumanMotion)
                    {
                        AddToCache(clip, assetPath);
                    }
                }
            }
        }
    }

    private void AddToCache(AnimationClip clip, string assetPath)
    {
        // Normalize the clip name and path by lowercasing and converting underscores/hyphens/slashes to spaces
        string nameLower = clip.name.ToLower();
        string pathLower = assetPath.ToLower();

        nameLower = Regex.Replace(nameLower, @"[_\-]+", " ");
        pathLower = Regex.Replace(pathLower, @"[\\/_\-]+", " ");

        cachedClips.Add(new AnimationClipInfo(
            clip,
            nameLower,
            pathLower,
            assetPath
        ));
    }

    // Only re-filter when needed
    private void ApplySearchFilter()
    {
        // If the cache is invalid or doesn't exist, clear
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

        // Normalize the search query: ignore case, treat underscores/hyphens as spaces
        string normalizedQuery = searchQuery.ToLower().Trim();
        normalizedQuery = Regex.Replace(normalizedQuery, @"[_\-]+", " ");
        normalizedQuery = Regex.Replace(normalizedQuery, @"\s+", " "); // collapse multiple spaces

        // Split into tokens for AND matching
        string[] tokens = normalizedQuery.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

        List<AnimationClipInfo> matches = new List<AnimationClipInfo>();
        List<AnimationClipInfo> exactMatches = new List<AnimationClipInfo>();

        // Evaluate each cached humanoid clip
        foreach (var clipInfo in cachedClips)
        {
            bool allTokensMatch = true;

            // For each token, check if it appears in EITHER the normalized clip name or the normalized path
            foreach (string token in tokens)
            {
                if (!clipInfo.nameNormalized.Contains(token) && 
                    !clipInfo.pathNormalized.Contains(token))
                {
                    allTokensMatch = false;
                    break;
                }
            }

            if (!allTokensMatch) continue;

            // If it matches, add to 'matches'
            matches.Add(clipInfo);

            // Check exact match: if the entire normalized name == the entire normalized query
            if (clipInfo.nameNormalized == normalizedQuery)
            {
                exactMatches.Add(clipInfo);
            }
        }

        // Sort results: exact matches first
        List<AnimationClipInfo> sortedResults = new List<AnimationClipInfo>();

        // Sort the exact matches (alphabetically if you wish)
        exactMatches.Sort((a, b) => string.Compare(a.clip.name, b.clip.name, System.StringComparison.OrdinalIgnoreCase));
        sortedResults.AddRange(exactMatches);

        // Now add the partial matches
        foreach (var m in matches)
        {
            if (!exactMatches.Contains(m))
            {
                sortedResults.Add(m);
            }
        }

        filteredClips = sortedResults;
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

            // Auto-select first clip if any results exist
            if (filteredClips.Count > 0)
            {
                SelectClip(filteredClips[0]);
            }
        }

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
                    GUI.color = new Color(1f, 1f, 0f, 0.2f);
                    GUI.Box(lineRect, GUIContent.none);
                    GUI.color = oldColor;
                }

                // Highlight the currently selected clip with a border
                if (selectedClip == clipInfo.clip)
                {
                    Color highlightColor = Color.yellow;
                    float thickness = 20f;
                    EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y, lineRect.width, thickness), highlightColor); 
                    EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y + lineRect.height - thickness, lineRect.width, thickness), highlightColor);
                    EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y, thickness, lineRect.height), highlightColor);
                    EditorGUI.DrawRect(new Rect(lineRect.x + lineRect.width - thickness, lineRect.y, thickness, lineRect.height), highlightColor);
                }

                // Draw the clip in a read-only object field
                EditorGUI.ObjectField(objectFieldRect, clipInfo.clip, typeof(AnimationClip), false);

                // "Select" button
                if (GUI.Button(buttonRect, "Select"))
                {
                    SelectClip(clipInfo);
                }

                // Drag-and-drop support
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
                currentIndex = (currentIndex < 0) ? 0 : currentIndex - 1;
                if (currentIndex < 0) currentIndex = 0;
                SelectClip(filteredClips[currentIndex]);
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                e.Use();
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
