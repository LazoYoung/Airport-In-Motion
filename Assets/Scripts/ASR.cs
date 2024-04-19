using System;
using System.ComponentModel;
using JetBrains.Annotations;
using Python.Runtime;
using UnityEngine;
using UnityEngine.Assertions;

namespace System.Runtime.CompilerServices
{
    [UsedImplicitly]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit
    {
    }
}

public record AudioSample(
    int channel,
    int rate,
    int recordSecond,
    int chunk,
    int format
);

public class ASR : MonoBehaviour
{
    public static ASR instance => LazyInstance.Value;
    
    private static readonly Lazy<ASR> LazyInstance = new(() => new GameObject().AddComponent<ASR>());

    private static readonly string ModelID = "Jzuluaga/wav2vec2-large-960h-lv60-self-en-atc-uwb-atcc-and-atcosim";
    
    private dynamic _model;
    private dynamic _processor;

    private ASR()
    {
    }

    private void Start()
    {
        Assert.IsTrue(PythonEngine.IsInitialized, "Python must be initialized.");
        
        using (Py.GIL())  // global interpreter lock is mandatory while using Python
        {
            using (var scope = Py.CreateScope())
            {
                InitTransformer(scope);
            }
        }
    }
    
    public string Process(byte[] buffer, int sampleRateFrom, int sampleRateTo)
    {
        string text = null;

        using var scope = Py.CreateScope();
        dynamic torch = scope.Import("torch");
        scope.Import("torchaudio.functional", "F");
        scope.Import("numpy", "np");
        scope.Set("buffer", buffer);
        scope.Exec("buffer = b''.join(buffer)");
        scope.Exec("ndarray = np.frombuffer(buffer, dtype=np.int16).astype(np.float32) / 32767.0");
        scope.Exec("tensor = torch.from_numpy(ndarray)");
        scope.Exec($"wave = F.resample(tensor, {sampleRateFrom}, {sampleRateTo}).numpy()");
        dynamic input = scope.Eval($"processor(wave, sampling_rate={sampleRateTo}, return_tensors='pt').input_values");

        Py.With(scope.Eval("torch.no_grad()"), new Action<PyObject>(_ =>
        {
            dynamic logits = _model(input).logits;
            dynamic onehot = torch.argmax(logits, dim: -1);
            dynamic transcript = _processor.batch_decode(onehot);
            scope.Set("transcript", transcript);
            text = scope.Eval("transcript[0]").As<string>();
        }));

        return text;
    }

    private void InitTransformer(PyModule scope)
    {
        scope.Import("transformers.AutoModelForCTC", "ctc");
        scope.Import("transformers.Wav2Vec2Processor", "w2v2");
        _model = scope.Eval($"ctc.from_pretrained({ModelID})");
        _processor = scope.Eval($"w2v2.from_pretrained({ModelID})");
    }

    private void OnDisable()
    {
        if (PythonEngine.IsInitialized)
        {
            print("Python engine shutdown.");
            PythonEngine.Shutdown();
        }
    }
}