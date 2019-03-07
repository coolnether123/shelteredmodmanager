using System;
using UnityEngine;

/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* 
* This monobehaviour is used to display textual information on the screen at runtime.
*/
class LabelComponent : MonoBehaviour
{
    Rect position;
    string message = "None";
    float top = 20f;

    public void Start()
    {
        position = new Rect(20f, top, 400f, 100f);
    }

    public void OnGUI()
    {
        position.Set(20f, top, 400, 300);
        GUI.Label(position, this.message);
    }

    public void setText(String newMessage)
    {
        this.message = newMessage.ToString();
    }

    public void setTop(int newTop)
    {
        this.top = newTop;
    }

}