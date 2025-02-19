using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu]
[System.Serializable]
public class InteractiveScene : ScriptableObject
{
    //[PreviewField(150, ObjectFieldAlignment.Right)]
    //public Sprite RefImage;
    public string Name;
    public int SceneIndex;
    public Vector2 TimeRange;
    [Space]
    public int FrameStart;
    public int FrameEnd;


    [Button]
    void StartFramesToSecondCalculator()
    {
        TimeRange.x = FrameStart / 24;
        TimeRange.y = FrameEnd / 24;
    }
}