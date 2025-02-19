using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu]
public class VideoContainer : ScriptableObject
{
    public string VideoClipUrl;
    public VideoClip VideoClip;
}
