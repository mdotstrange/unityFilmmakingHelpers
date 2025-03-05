using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;

public class HumanoidAnimationWindow : EditorWindow
{
    private Vector2 scrollPos;
    private string searchQuery = "";
    private AnimationClip selectedClip;
    private bool cacheNeedsRefresh = false;

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

    // Serializable class for disk caching
    [System.Serializable]
    private class SerializableClipInfo
    {
        public string clipGuid;
        public string clipName;
        public string nameNormalized;
        public string pathNormalized;
        public string assetPath;

        public SerializableClipInfo(string guid, string name, string normalizedName, string normalizedPath, string assetPath)
        {
            this.clipGuid = guid;
            this.clipName = name;
            this.nameNormalized = normalizedName;
            this.pathNormalized = normalizedPath;
            this.assetPath = assetPath;
        }
    }

    // Class to hold all serialized data
    [System.Serializable]
    private class CacheData
    {
        public List<SerializableClipInfo> clips = new List<SerializableClipInfo>();
    }

    // Static cache of *all* humanoid clips found in the project.
    private static List<AnimationClipInfo> cachedClips = null;

    // Filtered list used for display based on search terms.
    private List<AnimationClipInfo> filteredClips = new List<AnimationClipInfo>();

    // Path for cache file
    private static string CacheFilePath => Path.Combine(Application.dataPath, "../Library/HumanoidAnimationsCache.json");

    [MenuItem("Window/Humanoid Animations")]
    public static void ShowWindow()
    {
        GetWindow<HumanoidAnimationWindow>("Humanoid Animations");
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;

        // Try to load from cache first, only rebuild if loading fails
        if (!LoadCacheFromDisk())
        {
            RefreshAnimations();
        }

        ApplySearchFilter();
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        // Instead of rebuilding, set a flag that the cache might be stale
        cacheNeedsRefresh = true;
        Repaint();
    }

    // Force a (re)scan of humanoid clips in the project.
    private void RefreshAnimations(bool forceRebuild = false)
    {
        // Only rebuild if needed or forced
        if (cachedClips != null && !forceRebuild) return;

        EditorUtility.DisplayProgressBar("Scanning Animation Clips", "Building humanoid animations cache...", 0f);

        cachedClips = new List<AnimationClipInfo>();
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");

        for (int i = 0; i < guids.Length; i++)
        {
            // Show progress
            if (i % 10 == 0)
            {
                float progress = (float)i / guids.Length;
                EditorUtility.DisplayProgressBar("Scanning Animation Clips",
                    $"Processing clip {i + 1}/{guids.Length}", progress);
            }

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

        EditorUtility.ClearProgressBar();

        // Save to disk after building
        SaveCacheToDisk();

        // Clear the refresh flag since we've just refreshed
        cacheNeedsRefresh = false;
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

    // Save cache to disk
    private void SaveCacheToDisk()
    {
        try
        {
            CacheData data = new CacheData();

            foreach (var clipInfo in cachedClips)
            {
                string guid = AssetDatabase.AssetPathToGUID(clipInfo.assetPath);

                // Only add valid assets to the cache
                if (!string.IsNullOrEmpty(guid))
                {
                    data.clips.Add(new SerializableClipInfo(
                        guid,
                        clipInfo.clip.name,
                        clipInfo.nameNormalized,
                        clipInfo.pathNormalized,
                        clipInfo.assetPath
                    ));
                }
            }

            string json = JsonUtility.ToJson(data);
            File.WriteAllText(CacheFilePath, json);
            Debug.Log($"Humanoid animations cache saved with {data.clips.Count} clips.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving humanoid animations cache: {e.Message}");
        }
    }

    // Load cache from disk
    private bool LoadCacheFromDisk()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
            {
                return false;
            }

            string json = File.ReadAllText(CacheFilePath);
            CacheData data = JsonUtility.FromJson<CacheData>(json);

            // If no data, return false
            if (data == null || data.clips == null || data.clips.Count == 0)
            {
                return false;
            }

            List<AnimationClipInfo> loadedClips = new List<AnimationClipInfo>();
            int loadedCount = 0;
            int totalCount = data.clips.Count;

            EditorUtility.DisplayProgressBar("Loading Animation Cache",
                "Loading cached animation clips...", 0f);

            for (int i = 0; i < data.clips.Count; i++)
            {
                var serializedInfo = data.clips[i];

                // Show progress every few items
                if (i % 20 == 0)
                {
                    float progress = (float)i / totalCount;
                    EditorUtility.DisplayProgressBar("Loading Animation Cache",
                        $"Loading clip {i + 1}/{totalCount}", progress);
                }

                if (string.IsNullOrEmpty(serializedInfo.clipGuid))
                    continue;

                string assetPath = AssetDatabase.GUIDToAssetPath(serializedInfo.clipGuid);

                // Skip if the asset no longer exists
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

                // Skip if the clip can't be loaded
                if (clip == null)
                    continue;

                loadedClips.Add(new AnimationClipInfo(
                    clip,
                    serializedInfo.nameNormalized,
                    serializedInfo.pathNormalized,
                    assetPath
                ));

                loadedCount++;
            }

            EditorUtility.ClearProgressBar();

            if (loadedCount > 0)
            {
                cachedClips = loadedClips;
                Debug.Log($"Humanoid animations loaded from cache: {loadedCount} clips.");
                return true;
            }

            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading humanoid animations cache: {e.Message}");
            EditorUtility.ClearProgressBar();
            return false;
        }
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

        // Show a warning if the cache might be stale
        if (cacheNeedsRefresh)
        {
            EditorGUILayout.HelpBox("Project assets have changed. The animation cache might be out of date. Consider refreshing.", MessageType.Warning);
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
                    Color highlightColor = Color.green;
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

        // Changed to force a rebuild when clicked
        if (GUILayout.Button("Refresh", GUILayout.Width(position.width * 0.15f)))
        {
            RefreshAnimations(true); // Force rebuild
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
