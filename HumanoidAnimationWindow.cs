using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

public class HumanoidAnimationWindow : EditorWindow
{
    private Vector2 scrollPos;
    private string searchQuery = "";
    private AnimationClip selectedClip;
    private bool cacheNeedsRefresh = false;
    private bool isLoading = false;
    private float loadProgress = 0f;
    private bool showOnlyFavorites = false; // For favorites filter toggle
    
    // For virtualized scrolling
    private float itemHeight = EditorGUIUtility.singleLineHeight;
    private const int BUFFER_ITEMS = 5; // Buffer items above and below visible area
    private int lastRepaintItemCount = 0;

    // Memory management
    private const int MAX_LOADED_CLIPS = 1000; // Increased from 100 to 1000
    private List<string> recentlyUsedGuids = new List<string>(); // For LRU cache
    
    // A small helper class to cache information about each clip
    [System.Serializable]
    private class AnimationClipInfo
    {
        public string guid;
        public string clipName;
        public string nameNormalized;
        public string pathNormalized;
        public string assetPath;
        public bool isFavorite; // Track favorite status
        
        [System.NonSerialized]
        public AnimationClip clip;
        
        [System.NonSerialized]
        public bool isLoaded;
        
        [System.NonSerialized]
        public bool isLoading;
        
        public AnimationClipInfo(string guid, string name, string normalizedName, string normalizedPath, string path)
        {
            this.guid = guid;
            this.clipName = name;
            this.nameNormalized = normalizedName;
            this.pathNormalized = normalizedPath;
            this.assetPath = path;
            this.isLoaded = false;
            this.isLoading = false;
            this.isFavorite = false;
        }
        
        public bool LoadClip()
        {
            if (isLoaded && clip != null)
                return true;
                
            if (isLoading)
                return false;
                
            isLoading = true;
            
            string path = string.IsNullOrEmpty(assetPath) ? 
                AssetDatabase.GUIDToAssetPath(guid) : assetPath;
                
            if (!string.IsNullOrEmpty(path))
            {
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                
                // Double-check this is actually a humanoid animation
                if (clip != null && !clip.isHumanMotion)
                {
                    // Not humanoid, unload it
                    clip = null;
                    isLoaded = false;
                    isLoading = false;
                    return false;
                }
                
                isLoaded = clip != null;
            }
            
            isLoading = false;
            return isLoaded && clip != null;
        }
    }
    
    // Cache data container
    [System.Serializable]
    private class CacheData
    {
        public List<AnimationClipInfo> clips = new List<AnimationClipInfo>();
        public string cacheVersion = "3.0";
    }
    
    // Static cache of animation clip info (not the actual clips)
    private static List<AnimationClipInfo> cachedClips = null;
    
    // Filtered list of clip info (references to cachedClips items)
    private List<AnimationClipInfo> filteredClips = new List<AnimationClipInfo>();
    
    // Path to cache file in project Library folder
    private string CacheFilePath => Path.Combine(Application.dataPath, "../Library/HumanoidAnimationsCache.json");
    
    // Batch processing state
    private int currentIndex = 0;
    private string[] pendingGuids = null;
    private System.Action postBuildAction = null;
    private const int BATCH_SIZE = 100; // Process 100 clips per frame during build
    
    [MenuItem("Window/Humanoid Animations")]
    public static void ShowWindow()
    {
        GetWindow<HumanoidAnimationWindow>("Humanoid Animations");
    }
    
    // Star icon for favorites
    private Texture2D starIcon;
    
    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;
        
        // Initialize recentlyUsedGuids if needed
        if (recentlyUsedGuids == null)
        {
            recentlyUsedGuids = new List<string>();
        }
        
        // Load the star icon
        starIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FavoritesWindow/Editor/Star.png");
        
        // Initialize without blocking UI
        Initialize();
    }
    
    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        
        // Make sure to clear any pending operations
        EditorApplication.update -= ProcessNextBatch;
        
        // Clear clip references to help with memory
        if (filteredClips != null)
        {
            foreach (var clip in filteredClips)
            {
                clip.clip = null;
                clip.isLoaded = false;
            }
        }
        
        // Encourage garbage collection
        Resources.UnloadUnusedAssets();
    }
    
    private void OnProjectChanged()
    {
        // Instead of rebuilding, set a flag that the cache might be stale
        cacheNeedsRefresh = true;
        Repaint();
    }
    
    private void Initialize()
    {
        // Try to load from cache file first
        if (!LoadCacheFromDisk())
        {
            // If cache loading fails, build the cache gradually
            BuildCache();
        }
        else 
        {
            ApplySearchFilter();
            LoadFavorites(); // Load favorites after cache is loaded
        }
    }
    
    private bool LoadCacheFromDisk()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
                return false;
                
            string json = File.ReadAllText(CacheFilePath);
            CacheData data = JsonUtility.FromJson<CacheData>(json);
            
            if (data == null || data.clips == null || data.clips.Count == 0)
                return false;
                
            cachedClips = data.clips;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading humanoid animations cache: {e.Message}");
            return false;
        }
    }
    
    private void SaveCacheToDisk()
    {
        try
        {
            if (cachedClips == null || cachedClips.Count == 0)
                return;
                
            // Ensure the Library folder exists
            string directory = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            CacheData data = new CacheData();
            data.clips = cachedClips;
            
            string json = JsonUtility.ToJson(data);
            File.WriteAllText(CacheFilePath, json);
            
            Debug.Log($"Humanoid animations cache saved with {cachedClips.Count} clips.");
            
            // Save favorites separately
            SaveFavorites();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving humanoid animations cache: {e.Message}");
        }
    }
    
    // Path for favorites
    private string FavoritesFilePath => Path.Combine(Application.dataPath, "../Library/HumanoidAnimationsFavorites.json");
    
    // Save favorites to disk
    private void SaveFavorites()
    {
        try
        {
            if (cachedClips == null)
                return;
                
            // Create a dictionary of GUIDs to favorite status
            Dictionary<string, bool> favorites = new Dictionary<string, bool>();
            
            foreach (var clip in cachedClips)
            {
                if (clip.isFavorite)
                {
                    favorites[clip.guid] = true;
                }
            }
            
            // Serialize to JSON (manually since Dictionary isn't directly serializable)
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("{\"favorites\":[");
            
            bool first = true;
            foreach (var kvp in favorites)
            {
                if (!first) sb.Append(",");
                sb.Append("{\"guid\":\"").Append(kvp.Key).Append("\"}");
                first = false;
            }
            
            sb.Append("]}");
            
            // Write to disk
            File.WriteAllText(FavoritesFilePath, sb.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving favorites: {e.Message}");
        }
    }
    
    // Load favorites from disk
    private void LoadFavorites()
    {
        try
        {
            if (!File.Exists(FavoritesFilePath) || cachedClips == null)
                return;
                
            string json = File.ReadAllText(FavoritesFilePath);
            
            // Parse manually (using simple string operations for robustness)
            HashSet<string> favoriteGuids = new HashSet<string>();
            
            // Extract guids using regex for simplicity
            Regex guidRegex = new Regex("\"guid\":\"([^\"]+)\"");
            MatchCollection matches = guidRegex.Matches(json);
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    favoriteGuids.Add(match.Groups[1].Value);
                }
            }
            
            // Apply favorites to cached clips
            foreach (var clip in cachedClips)
            {
                clip.isFavorite = favoriteGuids.Contains(clip.guid);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading favorites: {e.Message}");
        }
    }
    
    private void BuildCache(bool forceRebuild = false)
    {
        // Only rebuild if needed
        if (cachedClips != null && !forceRebuild)
            return;
            
        if (isLoading)
            return;
            
        isLoading = true;
        
        // Find all animation clips in project
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        pendingGuids = guids;
        currentIndex = 0;
        loadProgress = 0f;
        
        // Create new cache list
        cachedClips = new List<AnimationClipInfo>();
        recentlyUsedGuids = new List<string>();
        
        // Set callback for when building completes
        postBuildAction = () => {
            // Verify all clips are actually humanoid
            VerifyHumanoidClips();
            
            // Save to disk after build completes
            SaveCacheToDisk();
            
            // Reset flag since we've just refreshed
            cacheNeedsRefresh = false;
            
            // Apply search filter after cache is built
            ApplySearchFilter();
            
            // Load favorites
            LoadFavorites();
            
            // Done loading
            isLoading = false;
            
            // Force repaint
            Repaint();
        };
        
        // Start batch processing
        EditorApplication.update += ProcessNextBatch;
    }
    
    private void ProcessNextBatch()
    {
        if (pendingGuids == null || currentIndex >= pendingGuids.Length)
        {
            // We've processed all items, complete the build
            EditorApplication.update -= ProcessNextBatch;
            
            if (postBuildAction != null)
            {
                postBuildAction();
                postBuildAction = null;
            }
            
            return;
        }
        
        // Process a batch of items
        int endIndex = Mathf.Min(currentIndex + BATCH_SIZE, pendingGuids.Length);
        for (int i = currentIndex; i < endIndex; i++)
        {
            string guid = pendingGuids[i];
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            
            // Update progress (for display)
            loadProgress = (float)i / pendingGuids.Length;
            
            if (string.IsNullOrEmpty(assetPath))
                continue;
                
            // Check if it's a humanoid animation
            if (IsHumanoidAnimation(assetPath))
            {
                // Get clip name from path
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                
                // Normalize for search
                string nameLower = fileName.ToLower();
                string pathLower = assetPath.ToLower();
                
                nameLower = Regex.Replace(nameLower, @"[_\-]+", " ");
                pathLower = Regex.Replace(pathLower, @"[\\/_\-]+", " ");
                
                // Add to cache without loading the actual clip yet
                cachedClips.Add(new AnimationClipInfo(
                    guid,
                    fileName,
                    nameLower,
                    pathLower,
                    assetPath
                ));
            }
        }
        
        // Move to next batch
        currentIndex = endIndex;
        
        // Force repaint to update progress bar
        Repaint();
    }
    
    private bool IsHumanoidAnimation(string assetPath)
    {
        // First, try to check with importer (for FBX files)
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer != null)
        {
            return importer.animationType == ModelImporterAnimationType.Human;
        }
        
        // For .anim files, we need to load the clip to be certain
        if (assetPath.EndsWith(".anim"))
        {
            try
            {
                // First try to load just a bit of the file to check if it might be humanoid
                // This is faster than loading the whole clip
                string header = "";
                using (FileStream fs = new FileStream(assetPath, FileMode.Open, FileAccess.Read))
                using (StreamReader reader = new StreamReader(fs))
                {
                    // Read just the first 2KB which should contain humanoid identifiers if present
                    char[] buffer = new char[2048];
                    reader.Read(buffer, 0, 2048);
                    header = new string(buffer);
                }
                
                // Quick check for humanoid animation keywords
                if (header.Contains("m_HumanCurve") || 
                    header.Contains("RootT") || 
                    header.Contains("HipsTranslation") ||
                    header.Contains("m_Human"))
                {
                    // This is a more reliable check - load the clip and verify
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                    bool isHumanoid = clip != null && clip.isHumanMotion;
                    
                    // Unload the clip immediately to save memory
                    Resources.UnloadUnusedAssets();
                    
                    return isHumanoid;
                }
                
                return false; // Definitely not humanoid if keywords aren't found
            }
            catch
            {
                // If the file parsing fails, fall back to loading the clip directly
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                bool isHumanoid = clip != null && clip.isHumanMotion;
                
                // Unload the clip
                Resources.UnloadUnusedAssets();
                
                return isHumanoid;
            }
        }
        
        return false;
    }
    
    // For final verification that clips are humanoid
    private void VerifyHumanoidClips()
    {
        if (cachedClips == null || cachedClips.Count == 0)
            return;
            
        List<AnimationClipInfo> verifiedClips = new List<AnimationClipInfo>();
        
        for (int i = 0; i < cachedClips.Count; i++)
        {
            var clipInfo = cachedClips[i];
            
            // Load clip to check
            string assetPath = string.IsNullOrEmpty(clipInfo.assetPath) ? 
                AssetDatabase.GUIDToAssetPath(clipInfo.guid) : 
                clipInfo.assetPath;
                
            if (string.IsNullOrEmpty(assetPath))
                continue;
                
            // Quick check for FBX files (we already verified these)
            if (assetPath.EndsWith(".fbx"))
            {
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null && importer.animationType == ModelImporterAnimationType.Human)
                {
                    verifiedClips.Add(clipInfo);
                }
                continue;
            }
            
            // For .anim files, do a definitive check
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null && clip.isHumanMotion)
            {
                verifiedClips.Add(clipInfo);
            }
            
            // Cleanup to avoid memory issues
            if (i % 20 == 0)
            {
                Resources.UnloadUnusedAssets();
            }
        }
        
        // Replace with verified clips only
        cachedClips = verifiedClips;
        
        // Force cleanup
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }
    
    private void ApplySearchFilter()
    {
        if (cachedClips == null)
        {
            filteredClips = new List<AnimationClipInfo>();
            return;
        }
        
        // Start with all clips or just favorites
        List<AnimationClipInfo> baseList = new List<AnimationClipInfo>();
        
        if (showOnlyFavorites)
        {
            // Filter to only favorites
            foreach (var clip in cachedClips)
            {
                if (clip.isFavorite)
                {
                    baseList.Add(clip);
                }
            }
        }
        else
        {
            // Show all clips
            baseList = cachedClips;
        }
        
        // If no search, show the base list
        if (string.IsNullOrEmpty(searchQuery))
        {
            filteredClips = baseList;
            return;
        }
        
        // Normalize search query
        string normalizedQuery = searchQuery.ToLower().Trim();
        normalizedQuery = Regex.Replace(normalizedQuery, @"[_\-]+", " ");
        normalizedQuery = Regex.Replace(normalizedQuery, @"\s+", " ");
        
        // Split into tokens for AND matching
        string[] tokens = normalizedQuery.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        
        // Find matches
        List<AnimationClipInfo> matches = new List<AnimationClipInfo>();
        List<AnimationClipInfo> exactMatches = new List<AnimationClipInfo>();
        
        foreach (var clipInfo in baseList)
        {
            bool allTokensMatch = true;
            
            foreach (string token in tokens)
            {
                if (!clipInfo.nameNormalized.Contains(token) && 
                    !clipInfo.pathNormalized.Contains(token))
                {
                    allTokensMatch = false;
                    break;
                }
            }
            
            if (!allTokensMatch)
                continue;
                
            matches.Add(clipInfo);
            
            // Check for exact match
            if (clipInfo.nameNormalized == normalizedQuery)
            {
                exactMatches.Add(clipInfo);
            }
        }
        
        // Prepare result list with exact matches first
        filteredClips = new List<AnimationClipInfo>();
        
        // Add exact matches first
        filteredClips.AddRange(exactMatches.OrderBy(a => a.clipName));
        
        // Then add remaining matches
        foreach (var match in matches)
        {
            if (!exactMatches.Contains(match))
            {
                filteredClips.Add(match);
            }
        }
        
        // Reset scroll position when filter changes significantly
        scrollPos = Vector2.zero;
    }
    
    private void ManageMemory(int startIndex, int endIndex)
    {
        // Skip if no clips
        if (filteredClips == null || filteredClips.Count == 0)
            return;
            
        // Ensure indices are in valid range
        startIndex = Mathf.Max(0, startIndex);
        endIndex = Mathf.Min(endIndex, filteredClips.Count);
        
        // Pre-load items in batches when scrolling to bottom of list
        if (endIndex > filteredClips.Count - 50 && filteredClips.Count > 1000)
        {
            // Approaching the end of a large list, load in smaller batches to prevent freezing
            int batchSize = 10;
            int preloadEnd = Mathf.Min(endIndex + 50, filteredClips.Count);
            
            for (int i = endIndex; i < preloadEnd; i += batchSize)
            {
                int batchEnd = Mathf.Min(i + batchSize, preloadEnd);
                
                for (int j = i; j < batchEnd; j++)
                {
                    if (j < filteredClips.Count)
                    {
                        filteredClips[j].LoadClip();
                    }
                }
                
                // Small delay to prevent UI freeze
                if (i > endIndex + 20)
                {
                    // Unload any clips we don't need from earlier in the list
                    for (int k = 0; k < startIndex - 100; k += 10)
                    {
                        if (k >= 0 && k < filteredClips.Count && 
                            filteredClips[k].isLoaded && 
                            filteredClips[k].clip != selectedClip)
                        {
                            filteredClips[k].clip = null;
                            filteredClips[k].isLoaded = false;
                        }
                    }
                    
                    // Force occasional cleanup when nearing end of large lists
                    if (Random.value < 0.2f)
                    {
                        Resources.UnloadUnusedAssets();
                    }
                }
            }
        }
        
        // Keep track of visible clips
        HashSet<string> visibleGuids = new HashSet<string>();
        
        // Mark visible clips and ensure they are loaded
        for (int i = startIndex; i < endIndex; i++)
        {
            if (i >= 0 && i < filteredClips.Count)
            {
                AnimationClipInfo clipInfo = filteredClips[i];
                visibleGuids.Add(clipInfo.guid);
                
                // Update LRU list
                if (recentlyUsedGuids.Contains(clipInfo.guid))
                {
                    recentlyUsedGuids.Remove(clipInfo.guid);
                }
                recentlyUsedGuids.Insert(0, clipInfo.guid);
                
                // Load if needed
                clipInfo.LoadClip();
            }
        }
        
        // Add selected clip to visible list if any
        if (selectedClip != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(selectedClip);
            if (!string.IsNullOrEmpty(selectedPath))
            {
                string selectedGuid = AssetDatabase.AssetPathToGUID(selectedPath);
                if (!string.IsNullOrEmpty(selectedGuid))
                {
                    visibleGuids.Add(selectedGuid);
                    
                    // Move to front of recently used list
                    if (recentlyUsedGuids.Contains(selectedGuid))
                    {
                        recentlyUsedGuids.Remove(selectedGuid);
                    }
                    recentlyUsedGuids.Insert(0, selectedGuid);
                }
            }
        }
        
        // If we have too many loaded clips, unload those not visible and least recently used
        if (recentlyUsedGuids.Count > MAX_LOADED_CLIPS)
        {
            // Start from the end of LRU list
            for (int i = recentlyUsedGuids.Count - 1; i >= MAX_LOADED_CLIPS; i--)
            {
                // Skip if it's in visible range
                if (i < 0 || i >= recentlyUsedGuids.Count) continue;
                
                string guid = recentlyUsedGuids[i];
                if (visibleGuids.Contains(guid)) continue;
                
                // Find and unload this clip
                foreach (var clipInfo in cachedClips)
                {
                    if (clipInfo.guid == guid && clipInfo.isLoaded && clipInfo.clip != null)
                    {
                        // Don't unload selected clip
                        if (clipInfo.clip != selectedClip)
                        {
                            clipInfo.clip = null;
                            clipInfo.isLoaded = false;
                        }
                        break;
                    }
                }
                
                // Remove from LRU list
                recentlyUsedGuids.RemoveAt(i);
            }
            
            // Occasionally force garbage collection
            if (Random.value < 0.1f)
            {
                Resources.UnloadUnusedAssets();
            }
        }
    }
    
    private void OnGUI()
    {
        // Search bar with favorites toggle
        EditorGUILayout.BeginHorizontal();
        
        // Search field
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        string newQuery = EditorGUILayout.TextField(searchQuery);
        
        // Favorites toggle
        GUIContent toggleContent = new GUIContent("Favorites", "Show only favorite animations");
        bool newShowOnlyFavorites = GUILayout.Toggle(showOnlyFavorites, toggleContent, "Button", GUILayout.Width(80));
        
        EditorGUILayout.EndHorizontal();
        
        // Apply filter if search or favorites toggle changes
        if (newQuery != searchQuery || newShowOnlyFavorites != showOnlyFavorites)
        {
            searchQuery = newQuery;
            showOnlyFavorites = newShowOnlyFavorites;
            ApplySearchFilter();
            
            // Auto-select first result
            if (filteredClips.Count > 0)
            {
                SelectClipByIndex(0);
            }
        }
        
        // Show cache status warnings/info
        if (cacheNeedsRefresh)
        {
            EditorGUILayout.HelpBox("Project assets have changed. The animation cache might be out of date. Consider refreshing.", MessageType.Warning);
        }
        
        // Show loading progress
        if (isLoading)
        {
            EditorGUILayout.HelpBox($"Building animation cache... {(loadProgress * 100):F0}%", MessageType.Info);
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(progressRect, loadProgress, "Loading...");
            return;
        }
        
        // Handle keyboard navigation
        HandleKeyboardInput();
        
        // Calculate visible items and total content height
        float viewHeight = position.height - 80; // Space for search bar, buttons, etc.
        int itemsPerPage = Mathf.FloorToInt(viewHeight / itemHeight);
        int totalItems = filteredClips != null ? filteredClips.Count : 0;
        float contentHeight = totalItems * itemHeight;
        
        // Begin scroll view with virtual height
        Rect scrollViewRect = GUILayoutUtility.GetRect(0, position.width, 0, viewHeight);
        scrollPos = GUI.BeginScrollView(scrollViewRect, scrollPos, new Rect(0, 0, scrollViewRect.width - 20, contentHeight));
        
        // Calculate visible range
        int startIndex = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / itemHeight) - BUFFER_ITEMS);
        int endIndex = Mathf.Min(startIndex + itemsPerPage + BUFFER_ITEMS * 2, totalItems);
        
        // Save item count for this repaint to avoid layout mismatches
        lastRepaintItemCount = endIndex - startIndex;
        
        // Manage memory for visible range
        ManageMemory(startIndex, endIndex);
        
        // Draw visible items
        for (int i = startIndex; i < endIndex; i++)
        {
            // Calculate position for this item
            Rect itemRect = new Rect(0, i * itemHeight, scrollViewRect.width - 20, itemHeight);
            
            // Skip if not visible
            if (itemRect.y > scrollPos.y + viewHeight || itemRect.y + itemRect.height < scrollPos.y)
                continue;
                
            DrawClipItem(i, itemRect);
        }
        
        GUI.EndScrollView();
        
        // Status bar
        EditorGUILayout.BeginHorizontal();
        
        // Show stats
        if (cachedClips != null)
        {
            string statsText = $"Showing {filteredClips.Count} of {cachedClips.Count} clips";
            EditorGUILayout.LabelField(statsText, EditorStyles.miniLabel);
        }
        
        GUILayout.FlexibleSpace();
        
        // Memory cleanup button
        if (GUILayout.Button("Clear Memory", GUILayout.Width(position.width * 0.15f)))
        {
            // Unload all non-selected clips
            foreach (var clipInfo in filteredClips)
            {
                if (clipInfo.clip != selectedClip)
                {
                    clipInfo.clip = null;
                    clipInfo.isLoaded = false;
                }
            }
            recentlyUsedGuids.Clear();
            if (selectedClip != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selectedClip);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    string selectedGuid = AssetDatabase.AssetPathToGUID(selectedPath);
                    if (!string.IsNullOrEmpty(selectedGuid))
                    {
                        recentlyUsedGuids.Add(selectedGuid);
                    }
                }
            }
            
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }
        
        // Refresh button
        if (GUILayout.Button("Refresh", GUILayout.Width(position.width * 0.15f)))
        {
            BuildCache(true); // Force rebuild
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawClipItem(int index, Rect itemRect)
    {
        if (index < 0 || index >= filteredClips.Count)
            return;
            
        var clipInfo = filteredClips[index];
        bool isSelected = false;
        
        // Check if this item is selected before loading
        if (selectedClip != null && clipInfo.isLoaded && clipInfo.clip == selectedClip)
        {
            isSelected = true;
        }
        
        // Try to load clip (but don't force it)
        bool isLoaded = clipInfo.LoadClip();
        AnimationClip clip = clipInfo.clip;


        
        // Define dimensions for controls - moved up before use
        float starWidth = 20f;
        float buttonWidth = 120f; // Wider select button (2x)
        float objectFieldWidth = itemRect.width - buttonWidth - starWidth - 10; // Half width for clip field

        // Define rects for controls - new layout with star button
        Rect starRect = new Rect(itemRect.x, itemRect.y, starWidth, itemRect.height);
        Rect objectFieldRect = new Rect(itemRect.x + starWidth + 5, itemRect.y, objectFieldWidth, itemRect.height);
        Rect buttonRect = new Rect(itemRect.x + starWidth + objectFieldWidth + 10, itemRect.y, buttonWidth, itemRect.height);

        // If clip is still loading or failed to load, show a placeholder
        if (!isLoaded || clip == null)
        {
            // Show clip name as text
            EditorGUI.LabelField(objectFieldRect, clipInfo.clipName);

            // Show load button
            if (GUI.Button(buttonRect, "Load"))
            {
                clipInfo.clip = null;
                clipInfo.isLoaded = false;
                clipInfo.LoadClip();
            }
        }
        else
        {
            // Regular object field for clips that are loaded
            EditorGUI.ObjectField(objectFieldRect, clip, typeof(AnimationClip), false);

            // Select button
            if (GUI.Button(buttonRect, "Select"))
            {
                SelectClipByIndex(index);
            }

            // Drag support
            Event evt = Event.current;
            if (evt.type == EventType.MouseDrag && objectFieldRect.Contains(evt.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { clip };
                DragAndDrop.StartDrag(clip.name);
                evt.Use();
            }
        }


        // Draw box and selection highlight
        if (isSelected)
        {
            // Create a highlight rectangle that covers the entire row
            Rect highlightRect = new Rect(
                itemRect.x, // Start from the left edge
                itemRect.y,
                itemRect.width * 10, // Cover the full width
                itemRect.height
            );

     
            
            // Draw green highlight rectangle across the entire row
            EditorGUI.DrawRect(highlightRect, new Color(0.3f, 0.8f, 0.3f, 0.5f)); // Light green with transparency
        }
        
       
        
        // Draw favorite star button
        Color oldColor = GUI.color;
        if (clipInfo.isFavorite)
        {
            GUI.color = Color.yellow; // Yellow for favorites
        }
        else
        {
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.7f); // Grey for non-favorites
        }
        
        // Use the star icon from the project
        if (starIcon != null)
        {
            // Draw star button with image
            if (GUI.Button(starRect, new GUIContent(starIcon)))
            {
                clipInfo.isFavorite = !clipInfo.isFavorite;
                SaveFavorites(); // Save when favorite status changes
                
                // If we're in favorites-only mode and unfavoriting, reapply filter
                if (showOnlyFavorites && !clipInfo.isFavorite)
                {
                    ApplySearchFilter();
                }
            }
        }
        else
        {
            // Fallback to text if image not found
            if (GUI.Button(starRect, "★"))
            {
                clipInfo.isFavorite = !clipInfo.isFavorite;
                SaveFavorites(); // Save when favorite status changes
                
                // If we're in favorites-only mode and unfavoriting, reapply filter
                if (showOnlyFavorites && !clipInfo.isFavorite)
                {
                    ApplySearchFilter();
                }
            }
        }
        GUI.color = oldColor;
        
      
    }
    
    private void HandleKeyboardInput()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown)
            return;
            
        if (filteredClips == null || filteredClips.Count == 0)
            return;
            
        // Find current selection index
        int currentIndex = -1;
        if (selectedClip != null)
        {
            for (int i = 0; i < filteredClips.Count; i++)
            {
                if (filteredClips[i].isLoaded && filteredClips[i].clip == selectedClip)
                {
                    currentIndex = i;
                    break;
                }
            }
        }
        
        // Default to first item if none selected
        if (currentIndex < 0)
            currentIndex = 0;
            
        bool handled = true;
        
        switch (e.keyCode)
        {
            case KeyCode.UpArrow:
                currentIndex = Mathf.Max(0, currentIndex - 1);
                break;
                
            case KeyCode.DownArrow:
                currentIndex = Mathf.Min(filteredClips.Count - 1, currentIndex + 1);
                break;
                
            case KeyCode.Home:
                currentIndex = 0;
                scrollPos = Vector2.zero;
                break;
                
            case KeyCode.End:
                currentIndex = filteredClips.Count - 1;
                scrollPos = new Vector2(0, float.MaxValue);
                break;
                
            case KeyCode.PageUp:
                currentIndex = Mathf.Max(0, currentIndex - 10);
                break;
                
            case KeyCode.PageDown:
                currentIndex = Mathf.Min(filteredClips.Count - 1, currentIndex + 10);
                break;
                
            default:
                handled = false;
                break;
        }
        
        if (handled)
        {
            e.Use();
            SelectClipByIndex(currentIndex);
            EnsureClipIsVisible(currentIndex);
        }
    }
    
    private void EnsureClipIsVisible(int index)
    {
        if (index < 0 || index >= filteredClips.Count)
            return;
            
        // Calculate item position
        float itemTop = index * itemHeight;
        float itemBottom = itemTop + itemHeight;
        
        // Calculate visible area
        float visibleTop = scrollPos.y;
        float visibleBottom = scrollPos.y + (position.height - 80);
        
        // Scroll if needed
        if (itemTop < visibleTop)
        {
            scrollPos.y = itemTop;
        }
        else if (itemBottom > visibleBottom)
        {
            scrollPos.y = itemBottom - (position.height - 80);
        }
        
        Repaint();
    }
    
    private void SelectClipByIndex(int index)
    {
        if (index < 0 || index >= filteredClips.Count)
            return;
            
        var clipInfo = filteredClips[index];
        
        // Ensure the clip is loaded
        bool isLoaded = clipInfo.LoadClip();
        if (!isLoaded || clipInfo.clip == null)
            return;
            
        // Set the selection
        selectedClip = clipInfo.clip;
        Selection.activeObject = clipInfo.clip;
        EditorGUIUtility.PingObject(clipInfo.clip);
    }
}
