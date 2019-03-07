using UnityEngine;

/**
 * Author: benjaminfoo
 * See: https://github.com/benjaminfoo/shelteredmodmanager
 * 
 * This class defines the interface which every plugin must include in order to work within the application.
 */
public interface IPlugin
{
    string Name { get; }
    string Version { get; }

    void initialize();

    void start(GameObject root);


}