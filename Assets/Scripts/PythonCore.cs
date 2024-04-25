using System;
using System.IO;
using JetBrains.Annotations;
using Python.Runtime;
using UnityEngine;

public class PythonCore : MonoBehaviour
{
    private static readonly string PythonPath =
        Application.streamingAssetsPath + "/python-3.11.9-embed-amd64/python311.dll";

    private PythonCore()
    {
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnGameStart()
    {
        if (!File.Exists(PythonPath))
            Debug.LogError("Python engine not found!");

        new GameObject("Python Core").AddComponent<PythonCore>();
        Runtime.PythonDLL = PythonPath;
        PythonEngine.Initialize();
        print("Python engine initialized.");
    }
    
    private void OnApplicationQuit()
    {
        if (PythonEngine.IsInitialized)
        {
            print("Python engine shutdown.");
            PythonEngine.Shutdown();
        }
    }
}
