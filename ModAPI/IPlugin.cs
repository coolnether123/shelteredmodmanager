using UnityEngine;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }

    void initialize();

    void start(GameObject root);


}