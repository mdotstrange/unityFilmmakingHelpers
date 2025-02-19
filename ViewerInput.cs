using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Rewired.Components;
using UnityEngine.UI;
using Rewired;

public class ViewerInput : MonoBehaviour
{
    public VideoControl VideoControl;
    public SceneJumper SceneJumper;
    public Rewired.Components.PlayerMouse PlayerMouse;
    public bool VhsActive;

    public Image UiPointer;

    public static ViewerInput _ViewerInput;


    public int playerId = 0;
    private Player player; // The Rewired Player

    public ColorCorrectionControl ColorCorrectionControl;

    private void Awake()
    {

        if (_ViewerInput == null)
        {
            _ViewerInput = this;
        }

        player = ReInput.players.GetPlayer(playerId);
    }



    void PauseOrPlay()
    {
        if (VideoControl.IsBusy == false && VideoControl.ColorCorrectionControl.IsHidden)
        {
            VideoControl.PauseOrPlay();
        }
    }

    void JumpIntoFilm()
    {
        if (VideoControl.IsBusy == false)
        {
            ColorCorrectionControl.HideHelpCCCanvas();
            VideoControl.JumpIntoFilm();
        }
    }

    void NextSoundtrack()
    {
        VideoControl.NextSoundtrack();
    }

    void NextChapter()
    {
        if (VhsActive == true)
        {
            Debug.Log("VHS IS ACTIVE");
            return;
        }

        if (VideoControl.IsBusy == false)
        {
            VideoControl.NextChapter();
        }
    }

    void PreviousChapter()
    {
        if (VhsActive == true)
        {
            return;
        }

        if (VideoControl.IsBusy == false)
        {
            VideoControl.PrevChapter();
        }
    }
 
    void NextLook()
    {
        VideoControl.NextLook();
    }

    void ToggleCC()
    {
        VideoControl.ToggleCC();
    }

    void ReturnToMainMenu()
    {
        UiPointer.enabled = true;
        SceneJumper.ReturnToMainMenu();
    }

    //void Rewind()
    //{
    //    if (VideoControl.IsBusy == false)
    //    {
    //        VideoControl.Rewind();
    //    }
    //}

    //void Ffwd()
    //{
    //    if (VideoControl.IsBusy == false)
    //    {
    //        VideoControl.Ffwd();
    //    }
    //}


  
    private void Update()
    {
        if(VideoControl.ColorCorrectionControl.IsHidden)
        {
            UiPointer.enabled = false;
        }
        else
        {
            UiPointer.enabled = true;
        }
       



        if (player.GetButtonDown("PausePlay"))
        {
            //Debug.Log("PAUSE/PLAY");
            PauseOrPlay();
        }
        else if (player.GetButtonDown("Rewind") && VhsActive == false)
        {
         //   Debug.Log("Prev chapter");
            PreviousChapter();
            
           
        }
        else if (player.GetButtonDown("FFwd") && VhsActive == false)
        {

           // Debug.Log("Next chapter");
            NextChapter();
            
        }   
        else if (player.GetButtonDown("NextSoundtrack"))
        {
            NextSoundtrack();
        }
        else if (player.GetButtonDown("JumpIntoFilm"))
        {
            Debug.Log("Jump into film");
            JumpIntoFilm();
        }    
        else if (player.GetButtonDown("BackToMenu"))
        {
            Debug.Log("back to menu");
            ReturnToMainMenu();
        }

        if(player.GetButtonDown("NextLook"))
        {
            NextLook();
        }

        if(player.GetButtonDown("ToggleCC"))
        {
            ToggleCC();
        }



        // huss
        VideoControl.VerifyNft(player.GetButton("Verify"));
     
    }
}
