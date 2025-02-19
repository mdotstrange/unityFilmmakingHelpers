using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;
using MEC;
using UnityEngine.UI;
using Rewired;
using System.Linq;
using System;


// class that handles going out to interactive scenes
public class SceneJumper : MonoBehaviour
{
    public VideoControl VideoControl;
   // public int LoaderSceneIndex;
    [InlineEditor(InlineEditorModes.FullEditor)]
    [Space]
    public List<InteractiveScene> IScenes = new List<InteractiveScene>();

    public int lastAddedSceneIndex;
   // public int GoToSceneIndex;

    public bool sceneloadFinished;
    public bool sceneUnloadFinished;
    public float _loadingProgress;

    public GameObject PreloaderCanvas;
    public Image LoadingBar;
    public GameObject MainMenu;

    public static SceneJumper JumperInstance;

   // public VirtualMouseInput virtualMouseInput;

    public List<float> ChapterStartTimes = new List<float>();

    public int playerId = 0;
    private Player player; // The Rewired Player

    public List<float> StartTimeList = new List<float>();

    public int CurrentChapterIndex;

    public List<ChapterMarker> ChapterMarkers = new List<ChapterMarker>();

    public FreeCam FreeCam;
    private void Awake()
    {
        if (JumperInstance == null)
        {
            JumperInstance = this;
        }

        player = ReInput.players.GetPlayer(playerId);

        CurrentChapterIndex = 0;

    }

    [Button]
    void SetChapterMarkes()
    {
        ChapterMarkers.Clear();

        for (int index = 0; index < IScenes.Count; index++)
        {
            var iScene = IScenes[index];

            ChapterMarker chapterMarker = new ChapterMarker();
            chapterMarker.BuildIndex = iScene.SceneIndex;
            chapterMarker.ListIndex = index;
            chapterMarker.MarkerTime = iScene.TimeRange.x;

            ChapterMarkers.Add(chapterMarker);
        }
    }

    [Button]
    void MakeStartTimeList()
    {
        foreach (var item in IScenes)
        {
            StartTimeList.Add(item.TimeRange.x);
        }
    }
    public int ReturnSceneIndexToLoad(float currentTime)
    {
        for (int index = 0; index < IScenes.Count; index++)
        {
            var i = IScenes[index];

            if(currentTime > i.TimeRange.x && currentTime < i.TimeRange.y)
            {
                PlayerPrefs.SetString(i.Name, i.Name);
                return i.SceneIndex;
            }

        }

        return 99;
    }

    //public int ReturnListIndexToLoad()
    //{
    //    float currentTime = (float)VideoControl.VideoPlayer.time;

    //    Debug.Log("Curren time " + currentTime);

    //    for (int index = 0; index < ChapterMarkers.Count; index++)
    //    {
    //        var i = ChapterMarkers[index];

    //        Debug.Log("Start " + i.MarkerTime + " end " + i.MarkerTime);

    //        if (currentTime >= i.TimeRange.x && currentTime <= i.TimeRange.y)
    //        {
    //            PlayerPrefs.SetString(i.Name, i.Name);
    //            return index;
    //        }

    //    }

    //    Debug.Log("---------------------------------NO match");
    //    return 0;
    //}

    public void LoadInteractiveScene(int sceneIndexFromBuilds)
    {
        //set scene index in preloader
        //stop Video, disable camera

        var desiredScene = SceneManager.GetSceneByBuildIndex(sceneIndexFromBuilds);

        if (desiredScene == null)
        {
            Debug.Log("Scene index " + sceneIndexFromBuilds + " doesn't exist yet");
            return;
        }
        else
        {
           
        }

        VideoControl.LastActiveVideoTime = VideoControl.VideoPlayer.time;
        lastAddedSceneIndex = sceneIndexFromBuilds;

      //  Debug.Log("last added index " + lastAddedSceneIndex);

        VideoControl.StopAndDisableCameraForILoad();
        EnableGameCategory();
        Timing.RunCoroutine(_LoadSceneRoutine(sceneIndexFromBuilds));
    }

    public void ReturnToWatchingFilm()
    {
        //unload additive scene

        Timing.RunCoroutine(_ReturnToWatchingFilm());
    }

    IEnumerator<float> _ReturnToWatchingFilm()
    {
      //  Debug.Log("Unload " + lastAddedSceneIndex);
        SceneManager.UnloadSceneAsync(lastAddedSceneIndex);
        //Timing.RunCoroutine(_UnloadSceneRoutine(lastAddedSceneIndex));
        EnableFilmIsPlayingCategory();
        VideoControl.OnReturningToFilmFromGame();
        VideoControl.PlayVideoAndAudio();
       
        yield break;
    }

    public void ReturnToMainMenu()
    {
        //  mainmen
        Debug.Log("Return to MAIN MENU");
        VideoControl.VhsNoiseSource.Stop();        
        VideoControl.SoundtrackAudioSource.outputAudioMixerGroup = VideoControl.NormalGroup;
        VideoControl.gameObject.SetActive(false);
        MainMenu.SetActive(true);
     //   virtualMouseInput.enabled = true;
       // Cursor.lockState = CursorLockMode.None;
    }

    IEnumerator<float> _LoadSceneRoutine(int sceneIndex)
    {
       

        sceneloadFinished = false;
        PreloaderCanvas.SetActive(true);

        LoadingBar.fillAmount = 0;
        SceneManager.sceneLoaded += OnSceneLoaded;

        var asyncScene = SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Additive);
        asyncScene.allowSceneActivation = false;

        while (!asyncScene.isDone)
        {
            // loading bar progress
            _loadingProgress = Mathf.Clamp((asyncScene.progress / 0.9f) * 100f, 0f, 100f);


            if (asyncScene.progress >= 0.9f)
            {
                LoadingBar.fillAmount = asyncScene.progress;
                // we finally show the scene
                asyncScene.allowSceneActivation = true;
            }

            LoadingBar.fillAmount = 1;

           yield return Timing.WaitForOneFrame;


        }

        while (sceneloadFinished == false)
        {
            yield return Timing.WaitForOneFrame;
        }

        PreloaderCanvas.SetActive(false);

        yield break;
    }

    IEnumerator<float> _UnloadSceneRoutine(int sceneIndex)
    {

        //SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.UnloadSceneAsync(sceneIndex);   
        Debug.Log("Unload of " + sceneIndex + " completed");

        yield break;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        sceneloadFinished = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;

    }

    //void OnSceneUnloaded(Scene scene)
    //{

    //    sceneUnloadFinished = true;
    //    SceneManager.sceneUnloaded -= OnSceneUnloaded;

    //}

    //public float ReturnChapterTime(bool next)
    //{
    //    if(next)
    //    {
    //      //  Debug.Log("Last added index " + GoToSceneIndex);
    //        int nextIndex = ReturnListIndexToLoad() + 1;

    //        if(nextIndex > IScenes.Count - 1)
    //        {
    //            Debug.Log("Next chapter out of range " + nextIndex );
    //           // GoToSceneIndex = 0;
    //            lastAddedSceneIndex = 0;
    //            return 0f;
    //        }
    //        else
    //        {
    //            Debug.Log("Next chapter index " + nextIndex + " time " + FramesToSecondCalculator(IScenes[nextIndex].FrameStart));
    //           // GoToSceneIndex = nextIndex;
    //            return FramesToSecondCalculator(IScenes[nextIndex].FrameStart);
    //        }
    //    }
    //    else
    //    {
    //        Debug.Log("Current " + ReturnListIndexToLoad());

    //        int prevIndex = (ReturnListIndexToLoad()) - 1;

    //        Debug.Log("prev is " + prevIndex);

    //        if(prevIndex < 0)
    //        {
    //            Debug.Log("Prev chapter index " + (IScenes.Count - 1).ToString() + " time " + FramesToSecondCalculator(IScenes[IScenes.Count - 1].FrameStart)); ;
    //            //GoToSceneIndex = IScenes.Count - 1;
    //            return FramesToSecondCalculator(IScenes[IScenes.Count -1].FrameStart);
    //        }
    //        else
    //        {
    //            Debug.Log("Prev chapter index " + prevIndex + " time " + FramesToSecondCalculator(IScenes[prevIndex].FrameStart));
    //           // GoToSceneIndex = prevIndex;
    //            return FramesToSecondCalculator(IScenes[prevIndex].FrameStart);
    //        }
    //    }
    //}

     float FramesToSecondCalculator(int frame)
    {
        return frame / 24;
       
    }

    //[Button]
    //public int GetCurrentChapterSceneIndex()
    //{
    //    float currentTime = (float)VideoControl.VideoPlayer.time;
    //   // Debug.Log("Current time is " + currentTime);

    //    for (int index = 0; index < IScenes.Count; index++)
    //    {
    //        var scene = IScenes[index];

    //      //  Debug.Log(" x " + scene.TimeRange.x + " and y " + scene.TimeRange.y);

    //        if(currentTime >= scene.TimeRange.x && currentTime <= scene.TimeRange.y )
    //        {
    //         //   Debug.Log("Current index is " + scene.SceneIndex + " which is " + scene.Name);
    //            return scene.SceneIndex;
    //        }
    //    }

    //   // Debug.Log("Current index is " + "0");
    //    return 0;
    //}

    public int GetCurrentChapterIndex()
    {
        float currentTime = (float)VideoControl.VideoPlayer.time;
        Debug.Log("Current time is " + currentTime);

        return GetClosestStartTimeIndex();

        //for (int index = 0; index < IScenes.Count; index++)
        //{
        //    var scene = IScenes[index];

        //      //Debug.Log(" x " + scene.TimeRange.x + " and y " + scene.TimeRange.y);

        //    if (currentTime >= scene.TimeRange.x && currentTime <= scene.TimeRange.y)
        //    {
        //           Debug.Log("Current scene index is " + scene.SceneIndex + " which is " + scene.Name + " at index " + index);
        //        return index;
        //    }
        //}

        // Debug.Log("Current index is " + "0");
        // return 0;
    }

    public int GetClosestStartTimeIndex()
    {
        float currentTime = (float)VideoControl.VideoPlayer.time;
     
        float closest = StartTimeList.OrderBy(item => Math.Abs(currentTime - item)).First();

        for (int index = 0; index < IScenes.Count; index++)
        {
            var scene = IScenes[index];

            if(scene.FrameStart == closest)
            {
                return index;
            }
        }

        Debug.Log("-------------------NO MATCH!!!!");
        return 0;
    }

    public void EnableFilmIsPlayingCategory()
    {
        EnableCategory("FilmIsPlaying", true);
        EnableCategory("Default", true);
        EnableCategory("Game", false);
        EnableCategory("UI", true);
    }

    public void EnableGameCategory()
    {
        EnableCategory("FilmIsPlaying", false);
        EnableCategory("Default", false);
        EnableCategory("Game", true);
        EnableCategory("UI", false);
    }

    public void EnableCategory(string category, bool enable)
    {
        foreach (ControllerMap map in player.controllers.maps.GetAllMapsInCategory(category))
        {
            map.enabled = enable; // set the enabled state on the map
        }
    }

    private void OnEnable()
    {
        CheckForAllIScenesCompleted();

        
    }

    void CheckForAllIScenesCompleted()
    {
        foreach (var item in IScenes)
        {
            if (PlayerPrefs.HasKey(item.Name) == false)
            {
                Debug.Log("Not all scenes played");
                return;
            }

        }

      //  DoSteamStuff.SetAchievementTrue("InteractiveBoss");
        Debug.Log("All scenes played");
    }

    public void PlayFilmAndJumpToChapter(int chapterIndex)
    {
        //Debug.Log("Go to Chapter " + chapterIndex);
        FreeCam.StartMovieFromChapter(chapterIndex);
    }

}

[System.Serializable]
public class ChapterMarker
{
    public float MarkerTime;
    public int BuildIndex;
    public int ListIndex;
}

