using System.IO;
using Python.Runtime;
using UnityEngine;

public class PythonCore : MonoBehaviour
{
    private static readonly string PythonPath =
        Application.dataPath + "/StreamingAssets/python-3.11.9-embed-amd64/python311.dll";
 
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnGameStart()
    {
        if (!File.Exists(PythonPath))
            Debug.LogError("Python engine not found!");

        Runtime.PythonDLL = PythonPath;
        PythonEngine.Initialize();
        print("Python engine initialized.");
    }
}