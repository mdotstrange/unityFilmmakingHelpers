using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

// I used this script to get around Unitys lack of support for long audio clips ~2 hours for feature films
//Break the clip up into several pieces- make sure there are no gaps between clips!
// Use this wherever you need to play audio on the long clips
public class GiantAudioClip : MonoBehaviour
{
    public AudioSource AudioSource;
    public List<AudioClip> SourceClips = new List<AudioClip>();
    public List<VirtualAudioClip> virtualAudioClips = new List<VirtualAudioClip>();
    public bool AudioIsPlaying;
    public float CurrentPlaytime;
    public int CurrentPlayIndex;


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

    [Button]
    public void PlayAudioAtTime(float time)
    {
        for (int index = 0; index < virtualAudioClips.Count; index++)
        {
            var clip = virtualAudioClips[index];

            if(time >= clip.RangeOnMaster.x && time <= clip.RangeOnMaster.y)
            {          
                float remappedTime = RemapFloat(time, clip.RangeOnMaster.x, clip.RangeOnMaster.y, clip.OriginalTimeRange.x, clip.OriginalTimeRange.y);
                AudioSource.clip = clip.AudioClip;
                AudioSource.time = remappedTime;
                CurrentPlayIndex = index;        
                AudioSource.Play();
                AudioIsPlaying = true;
                return;            
            }
            else
            {
                Debug.Log("Out of range");
            }
        }
    }

    [Button]
    public void PlayAudio()
    {
        CurrentPlayIndex = 0;
        AudioSource.clip = virtualAudioClips[0].AudioClip;
        AudioSource.Play();
        AudioIsPlaying = true;
    }

    [Button]
   public void StopAudio()
    {
        CurrentPlayIndex = 0;
        AudioSource.Play();
        AudioIsPlaying = false;
    }

    private void Update()
    {

        if(AudioIsPlaying && AudioSource.isPlaying == false)
        {
            CurrentPlayIndex++;
        
            if(CurrentPlayIndex <= virtualAudioClips.Count -1 )
            {
                AudioSource.clip = virtualAudioClips[CurrentPlayIndex].AudioClip;
                AudioSource.time = 0f;
                AudioSource.Play();
            }
            else
            {
                AudioSource.Stop();
            }          

        }        
    }

    public  float RemapFloat(float s, float a1, float a2, float b1, float b2)
    {
        return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
    }
}

[System.Serializable]
public class VirtualAudioClip
{
    public AudioClip AudioClip;
    public Vector2 OriginalTimeRange;
    public Vector2 RangeOnMaster;

    public VirtualAudioClip(AudioClip clip, Vector2 masterRange)
    {
     
        AudioClip = clip;
        RangeOnMaster = masterRange;
        OriginalTimeRange = new Vector2(0f, AudioClip.length);
    }
}

