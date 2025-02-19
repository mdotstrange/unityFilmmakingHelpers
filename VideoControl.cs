using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using Sirenix.OdinInspector;
using System;
using UnityEngine.SceneManagement;
using Rewired;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Audio;
using MEC;
using Steamworks;

[RequireComponent(typeof(VideoPlayer))]
[RequireComponent(typeof(AudioSource))]
public class VideoControl : MonoBehaviour
{
    public VideoPlayer VideoPlayer;

    public AudioSource SoundtrackAudioSource;
    public AudioSource VhsNoiseSource;

    public Camera VideoPlayerCamera;

    public List<MdollCustomAudioTrack> Soundtracks = new List<MdollCustomAudioTrack>();
    public int currentSoundtrackIndex;

    public int lastActiveSoundtrackIndex;
    public double LastActiveVideoTime;

    public SceneJumper SceneJumper;

    public bool IsBusy;

    public GameObject NftCanvas;
    public Text NftText;
    public GameObject SoundtrackCanvas;
    public CanvasGroup CanvasGroup;
    public Text SoundtrackInfo;

    public GameObject NftImage;

    public Image UiPointer;

    Tween activeTween;

    public AudioMixer AudioMixer;
    public AudioMixerGroup NormalGroup;
    public AudioMixerGroup VhsGroup;

    bool TweenAudioPitch;
    float liveAudioPitch;

    public bool IsVhs;

    CoroutineHandle tweener;

    public List<VirtualAudioClip> ActiveSoundtrackVClips = new List<VirtualAudioClip>();
    public bool AudioIsPlaying;
    public float CurrentPlaytime;
    public int CurrentPlayIndex;

    public string NftOwnerAddress;
    public static  VideoControl _VideoControl;

    public LookManager LookManager;

    public ColorCorrectionControl ColorCorrectionControl;

    CoroutineHandle syncer;
    private void Awake()
    {
        if(_VideoControl == null)
        {
            _VideoControl = this;
        }

        VideoPlayer.prepareCompleted += PlayAfterPreparingVideo;
        currentSoundtrackIndex = 0;
  
        LastActiveVideoTime = 0;

        VideoPlayer.playbackSpeed = 1f;

    }

    async void Start()
    {
        string chain = "ethereum";
        string network = "mainnet";
        string contract = "0x87A3cb4bA54e2d6Ee270ddafB3b0c5AE6B0736d0";
        string tokenId = "1";
        NftOwnerAddress = await ERC721.OwnerOf(chain, network, contract, tokenId);
        NftText.text = NftOwnerAddress + " owns this.";
    }


    private void OnEnable()
    {
        VideoPlayer.playbackSpeed = 1f;

        if(IsVhs)
        {
            VhsNoiseSource.Play();
           tweener = Timing.RunCoroutine(_TweenPitchToRandomAndWait().CancelWith(gameObject));
        }
        else
        {
            if(tweener != null)
            {
                tweener.IsRunning = false;
            }

            VhsNoiseSource.Stop();
        }
    }


    [Button]
    public void PlayVideoAndAudio()
    {
        VideoPlayer.Stop();
        VideoPlayer.time = LastActiveVideoTime;
        VideoPlayer.Prepare();
    }

    [Button]
    void StopVideoAndAudio()
    {
        VideoPlayer.Stop();
        StopAudio();
    }

    public void StopAndDisableCameraForILoad()
    {
        StopVideoAndAudio();
        VideoPlayerCamera.enabled = false;
  
        VideoPlayerCamera.gameObject.GetComponent<AudioListener>().enabled = false;
        VideoPlayerCamera.gameObject.GetComponent<ViewerInput>().enabled = false;
    }

    public void OnReturningToFilmFromGame()
    {      
        VideoPlayerCamera.enabled = true;
        VideoPlayerCamera.gameObject.GetComponent<AudioListener>().enabled = true;
        VideoPlayerCamera.gameObject.GetComponent<ViewerInput>().enabled = true;
    }

    void PlayAfterPreparingVideo(VideoPlayer vp)
    {
        VideoPlayerCamera.enabled = true;
        VideoPlayer.time = LastActiveVideoTime;
        VideoPlayer.Play();

      //  Debug.Log("current soundtrack index " + currentSoundtrackIndex);

        if (currentSoundtrackIndex > Soundtracks.Count - 1)
        {
            Debug.Log("Set soundtrack index to 0 ");
            currentSoundtrackIndex = 0;
        }

        StopAudio();

        ActiveSoundtrackVClips = Soundtracks[currentSoundtrackIndex].virtualAudioClips;

        PlayAudioAtTime((float)LastActiveVideoTime);

        if (SoundtrackAudioSource.isPlaying == false)
        {
            SoundtrackAudioSource.Play();
        }
       

        IsBusy = false;

        if(syncer != null)
        {
            if(syncer.IsRunning)
            {
                syncer.IsRunning = false;
            }
        }

      syncer =  Timing.RunCoroutine(_KeepAudioSync());

    }


    private void Update()
    {
        if(VideoPlayer.isPlaying)
        {
            if(ColorCorrectionControl.IsHidden == true)
            {
                UiPointer.enabled = false;
            }
            else
            {
                UiPointer.enabled = true;
            }

            if(TweenAudioPitch)
            {
                AudioMixer.SetFloat("Pitch", liveAudioPitch);
            }

            if (AudioIsPlaying && SoundtrackAudioSource.isPlaying == false)
            {
                CurrentPlayIndex++;

                if (CurrentPlayIndex <= ActiveSoundtrackVClips.Count - 1)
                {
                    SoundtrackAudioSource.clip = ActiveSoundtrackVClips[CurrentPlayIndex].AudioClip;
                    SoundtrackAudioSource.time = 0f;
                    SoundtrackAudioSource.Play();
                }
                else
                {
                    SoundtrackAudioSource.Stop();
                }

            }

            if(VideoPlayer.time >= 34 && VideoPlayer.time <= 35)
            {
                if (SteamManager.Initialized)
                {
                    bool ach;
                    Steamworks.SteamUserStats.GetAchievement("IRABBIT", out ach);

                    if (ach == false)
                    {
                        SteamUserStats.SetAchievement("IRABBIT");
                    }

                    SteamUserStats.StoreStats();
                }
            }
            else if(VideoPlayer.time >= 7000 && VideoPlayer.time <= 7030)
            {
                if (SteamManager.Initialized)
                {
                    bool ach;
                    Steamworks.SteamUserStats.GetAchievement("Completionist", out ach);

                    if (ach == false)
                    {
                        SteamUserStats.SetAchievement("Completionist");
                    }

                    SteamUserStats.StoreStats();
                }
            }


            //sync time


        }
    }
    public void PauseOrPlay()
    {
      //  Debug.Log("PAUSE/PLAY");

        if (VideoPlayerCamera.enabled == true)
        {
            if (VideoPlayer.isPlaying)
            {
                PauseVideoAndAudio();
            }
            else if (VideoPlayer.isPaused)
            {
                UnpauseAndPlay();
            }
        }
    }

    public void PauseVideoAndAudio()
    {
        SoundtrackAudioSource.Pause();
        VideoPlayer.Pause();
    }
    public void UnpauseAndPlay()
    {
        VideoPlayer.Play();
        SoundtrackAudioSource.Play();
    }

    //public void Rewind()
    //{
    //    IsBusy = true;
    //    // Debug.Log("Rewind");

    //    var seekFrame = VideoPlayer.time - 1f;
    //    float newTime = (float)seekFrame;

    //    if (newTime < 0)
    //    {
    //        newTime = 0;
    //    }
    //    // Debug.Log("Rewind new time " + newTime);

    //    LastActiveVideoTime = newTime;
    //    VideoPlayer.time = LastActiveVideoTime;
    //    SoundtrackAudioSource.time = (float)LastActiveVideoTime;
    //    IsBusy = false;


    //}

    //public void Ffwd()
    //{
    //    IsBusy = true;


    //    var seekFrame = VideoPlayer.time + 1f;
    //    float newTime = (float)seekFrame;

    //    if (newTime > VideoPlayer.length)
    //    {
    //        return;
    //    }
    //    // Debug.Log("FFwd new time " + newTime);
    //    LastActiveVideoTime = newTime;
    //    VideoPlayer.time = LastActiveVideoTime;
    //    SoundtrackAudioSource.time = (float)LastActiveVideoTime;
    //    IsBusy = false;

    //}

    public void NextChapter()
    {
        IsBusy = true;

        int NextChapterIndex = SceneJumper.CurrentChapterIndex + 1;

        foreach (var item in SceneJumper.ChapterMarkers)
        {
            if(item.ListIndex == NextChapterIndex)
            {
                LastActiveVideoTime = item.MarkerTime;
                SceneJumper.CurrentChapterIndex++;
            }
        }

        //LastActiveVideoTime = SceneJumper.ReturnChapterTime(true);
        // StopVideo();
        IsBusy = false;
        PlayVideoAndAudio();
    }

    public void PrevChapter()
    {
        IsBusy = true;

        int NextChapterIndex = SceneJumper.CurrentChapterIndex - 1;

        foreach (var item in SceneJumper.ChapterMarkers)
        {
            if (item.ListIndex == NextChapterIndex)
            {
                LastActiveVideoTime = item.MarkerTime;
                SceneJumper.CurrentChapterIndex--;
            }
        }
        // StopVideo();
        IsBusy = false;
        PlayVideoAndAudio();
    }

    public void JumpIntoFilm()
    {
        if (VideoPlayerCamera.enabled == true)
        {
            float currentTime = ReturnCurrentVideoTimeAsFloat();
            int sceneIndexToJumpTo = SceneJumper.ReturnSceneIndexToLoad(currentTime);

            if (sceneIndexToJumpTo != 99)
            {

                if (SteamManager.Initialized)
                {
                    bool ach;
                    Steamworks.SteamUserStats.GetAchievement("IScene", out ach);

                    if (ach == false)
                    {
                        SteamUserStats.SetAchievement("IScene");
                    }

                    SteamUserStats.StoreStats();
                }



                SceneJumper.LoadInteractiveScene(sceneIndexToJumpTo);
            }
            else
            {
                Debug.Log("Out of range scene");
            }
        }
    }

    [Button]
    public void NextSoundtrack()
    {
        currentSoundtrackIndex++;

        if (currentSoundtrackIndex > Soundtracks.Count - 1)
        {
            currentSoundtrackIndex = 0;
        }

        var activeSoundtrack = Soundtracks[currentSoundtrackIndex];

        ActiveSoundtrackVClips = activeSoundtrack.virtualAudioClips;

        //  SoundtrackAudioSource.clip = activeSoundtrack.AudioClip;
        //SoundtrackAudioSource.time = (float)VideoPlayer.time;
        //SoundtrackAudioSource.Play();

        PlayAudioAtTime((float)VideoPlayer.time);

        if(activeTween != null)
        {
            activeTween.Kill();
        }

        SoundtrackInfo.text = activeSoundtrack.Name;

        foreach (var item in LookManager.Looks)
        {
            if(item.LockToSoundtrack && item.SoundtrackIndexmatch == currentSoundtrackIndex)
            {
                Debug.Log("THis is locked " + item.LookName + " to " + activeSoundtrack.Name);

                LookManager.DisableAllLooks();

                LookManager.ActivateLookByName(item.LookName);
            }
        }

        Timing.RunCoroutine(_WaitThenFadeOutTitle().CancelWith(gameObject));
    
    }

    IEnumerator<float> _KeepAudioSync()
    {
        yield return Timing.WaitForSeconds(300f);    

        if (VideoPlayer.isPlaying)
        {
           // Debug.Log("Syncer");
            PlayAudioAtTime((float)VideoPlayer.time);
            syncer = Timing.RunCoroutine(_KeepAudioSync().CancelWith(gameObject));
        }

        yield break;
    }

    public void NextLook()
    {
        LookManager.NextLook();
    }

    public void ToggleCC()
    {
        ColorCorrectionControl.TogglePalette();
    }

    public void VerifyNft(bool IsOn)
    {
        //DoSteamStuff.SetAchievementTrue("NFT");

        NftCanvas.SetActive(IsOn);
        NftImage.SetActive(IsOn);
    }

    void SoundTrackInfoDone()
    {
        SoundtrackCanvas.SetActive(false);
    }

    IEnumerator<float> _WaitThenFadeOutTitle()
    {
        SoundtrackCanvas.SetActive(true);
        CanvasGroup.alpha = 0f;

        activeTween = DOTween.To(x => CanvasGroup.alpha = x, 0f, 1f, 2f);

        yield return Timing.WaitForSeconds(6f);

        activeTween = DOTween.To(x => CanvasGroup.alpha = x, 1f, 0f, 2f).OnComplete(SoundTrackInfoDone);

        yield break;
    }

    public float ReturnCurrentVideoTimeAsFloat()
    {
        return (float)VideoPlayer.time;
    }

    public float FramesToSecondCalculator(int frame)
    {
        return frame / 24;

    }

    IEnumerator<float> _TweenPitchToRandomAndWait()
    {
        yield return Timing.WaitForSeconds(UnityEngine.Random.Range(5f, 20f));
        float randomPitch = UnityEngine.Random.Range(0.85f, 1.10f);
        float randomTweenTime = UnityEngine.Random.Range(2f, 5f);
        float currentPitch = 1f;
        AudioMixer.GetFloat("Pitch", out currentPitch);
        TweenAudioPitch = true;
        DOTween.To(x => liveAudioPitch = x, currentPitch, randomPitch, randomTweenTime).OnComplete(PitchChangeDone);    
        yield break;
    }

    void PitchChangeDone()
    {
        float randomTweenTime = UnityEngine.Random.Range(4f, 15f);
        float currentPitch = 1f;
        AudioMixer.GetFloat("Pitch", out currentPitch);
        DOTween.To(x => liveAudioPitch = x, currentPitch, 1f, randomTweenTime).OnComplete(BackToNormalPitchDone);
    }



    void BackToNormalPitchDone()
    {
        TweenAudioPitch = false;
        tweener = Timing.RunCoroutine(_TweenPitchToRandomAndWait().CancelWith(gameObject));
    }

    public void PlayVClipAudio()
    {
        CurrentPlayIndex = 0;
        SoundtrackAudioSource.clip = ActiveSoundtrackVClips[0].AudioClip;
        SoundtrackAudioSource.Play();
        AudioIsPlaying = true;
    }

    public void StopAudio()
    {
        CurrentPlayIndex = 0;
        SoundtrackAudioSource.Stop();
        AudioIsPlaying = false;
    }
    public void PlayAudioAtTime(float time)
    {
        for (int index = 0; index < ActiveSoundtrackVClips.Count; index++)
        {
            var clip = ActiveSoundtrackVClips[index];

            if (time >= clip.RangeOnMaster.x && time <= clip.RangeOnMaster.y)
            {
                float remappedTime = MUtil.RemapFloat(time, clip.RangeOnMaster.x, clip.RangeOnMaster.y, clip.OriginalTimeRange.x, clip.OriginalTimeRange.y);
                SoundtrackAudioSource.clip = clip.AudioClip;
                SoundtrackAudioSource.time = remappedTime;
                CurrentPlayIndex = index;
                SoundtrackAudioSource.Play();
                AudioIsPlaying = true;
                return;
            }
            else
            {
               // Debug.Log("Out of range");
            }
        }
    }


}


[System.Serializable]
public class MdollCustomAudioTrack
{
    public string Name;
    public List<AudioClip> SourceClips = new List<AudioClip>();
    public List<VirtualAudioClip> virtualAudioClips = new List<VirtualAudioClip>();


    [Button]
    public void CombineClips()
    {
        float rangeEnd = 0f;
        virtualAudioClips.Clear();
        float lastClipStartTime = 0f;

        for (int index = 0; index < SourceClips.Count; index++)
        {
            var i = SourceClips[index];

            rangeEnd += i.length;

            if (index == 0)
            {
                Vector2 newRange = new Vector2(0f, rangeEnd);
                var vclip = new VirtualAudioClip(i, newRange);
                virtualAudioClips.Add(vclip);
                lastClipStartTime = rangeEnd;
            }
            else
            {
                Vector2 newRange = new Vector2(lastClipStartTime, rangeEnd);
                var vclip = new VirtualAudioClip(i, newRange);
                virtualAudioClips.Add(vclip);
                lastClipStartTime = rangeEnd;
            }
        }
    }

}
