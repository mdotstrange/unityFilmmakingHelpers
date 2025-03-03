#Requires AutoHotkey v2.0
SendMode "Input"
SetTitleMatchMode 2  ; Partial window title match

; TRIGGER: F11 key
F11:: {
    ; Store the current active window to restore focus at the end
    ;previousWindow := WinGetID("A")
    
    ; Get Unity project title (needs to be done when Unity window exists)
    if WinExist("ahk_exe Unity.exe") {
        ; Handle Unity F10 command
        if WinActive("ahk_exe Unity.exe") {
            SendInput("{F10}")
        } else {
            ControlSend("{F10}",, "ahk_exe Unity.exe")
        }
        
        ; Get Unity project title for naming
        unityTitle := WinGetTitle("Unity ")
        parts := StrSplit(unityTitle, "-")
        unityProjectTitle := Trim(parts[2])
    } else {
        unityProjectTitle := "UnknownProject"
    }
    
    ; Get current date and time
    fileDate := FormatTime(, "yyyy-MM-dd_HH-mm-ss")
    trackName := "MyN"  ; Track name to set
    
    ; Handle Axis Studio
    if WinExist("ahk_exe axis_studio.exe") {
        if WinActive("ahk_exe axis_studio.exe") {
            SendInput("r")
        } else {
            ControlSend("r",, "ahk_exe axis_studio.exe")
        }
    }
    
    ; For Audacity, we need to activate it since it involves multiple sequential commands
    if WinExist("ahk_exe Audacity.exe") {
        ; Save current window before switching
        WinActivate("ahk_exe Audacity.exe")
        WinWaitActive("ahk_exe Audacity.exe", , 2)  ; Wait up to 2 seconds
        
        ; Now perform all the Audacity commands
        SendInput("+r")
        Sleep(50)
        SendInput("+{F10}")
        Sleep(50)
        SendInput("n")
        Sleep(50)
        
        ; Prepare combined string for the track name
        unityProjectTitle := unityProjectTitle . "_"
        combinedString := trackName . unityProjectTitle
        
        ; Send the track name
        SendInput(unityProjectTitle)
        Sleep(50)
        SendInput(fileDate)
        Sleep(50)
        SendInput("{Enter}")
    }
    
    ; Return focus to the original window
    ;WinActivate("ahk_id " previousWindow)
}

; TRIGGER: F12 key
F12:: {
    ; Store the current active window to restore focus at the end
    ;previousWindow := WinGetID("A")
    
    ; Handle Unity
    if WinExist("ahk_exe Unity.exe") {
        if WinActive("ahk_exe Unity.exe") {
            SendInput("{F10}")
        } else {
            ControlSend("{F10}",, "ahk_exe Unity.exe")
        }
    }
    
    ; Handle Axis Studio
    if WinExist("ahk_exe axis_studio.exe") {
        if WinActive("ahk_exe axis_studio.exe") {
            SendInput("r")
        } else {
            ControlSend("r",, "ahk_exe axis_studio.exe")
        }
    }
    
    ; Handle Audacity
    WinActivate ("ahk_exe Audacity.exe")
	WinWaitActive("ahk_exe Audacity.exe")
    ControlSend("space",, "ahk_exe Audacity.exe")
    
    ; Return focus to the original window
    ;WinActivate("ahk_id " previousWindow)
}

F8:: {
    if WinExist("ahk_exe Unity.exe") {
        if WinActive("ahk_exe Unity.exe") {
            SendInput("^p")
        } else {
            ControlSend("^p",, "ahk_exe Unity.exe")
        }
    } else {
        ; Optional: Show a notification if Unity isn't running
        MsgBox("Unity is not running.")
    }
}