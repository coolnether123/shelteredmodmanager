using UnityEngine;

public class InitializerPlugin : IPlugin
{
    public InitializerPlugin() { }

    public string Name => "InitializerPlugin";
    public string Version => "0.0.1";


    public void initialize()
    {

    }

    public void start(GameObject root)
    {
        LabelComponent label = root.AddComponent<LabelComponent>();
        label.setTop(10);
        label.setText("Modding-API active");

    }
}
