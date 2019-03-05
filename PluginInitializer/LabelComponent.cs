using System;
using UnityEngine;

class LabelComponent : MonoBehaviour
{
    Rect position;
    string message = "WAAA";
    float top = 20f;

    public void Start()
    {
        position = new Rect(20f, top, 400f, 100f);
        // Debug.Log("HI FROM INJECTED!");
        // System.IO.File.WriteAllText("testfile.txt", "some text!");
    }

    public void OnGUI()
    {
        position.Set(20f, top, 400, 100);
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