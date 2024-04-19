using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Python.Runtime;
using UnityEngine;
using UnityEngine.Assertions;

public record AudioDevice(int index, int sampleRate);

public class Radio : MonoBehaviour
{
    private AudioDevice _inputDevice;
    private AudioSample _sample;
    private byte[] _buffer;
    private int _nextIdx;
    private bool _recording;

    private void Start()
    {
        Assert.IsTrue(PythonEngine.IsInitialized, "Python must be initialized.");
        
        _inputDevice = GetInputDevice();
        _sample = GetAudioSample();
        _buffer = GetBuffer();
        _nextIdx = 0;
        
        Debug.Log($"Default input device: {_inputDevice.index} ({_inputDevice.sampleRate} Hz)");
    }
    
    private AudioDevice GetInputDevice()
    {
        AudioDevice inputDevice;
        
        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Import("pyaudio", "p");
            dynamic pyAudio = scope.Eval("p.PyAudio()");
            scope.Set("pyAudio", pyAudio);
            scope.Exec("device = pyAudio.get_default_input_device_info()");
            scope.Exec("idx = int(device(['index']))");
            scope.Exec("sampleRate = int(device['defaultSampleRate'])");
            int idx = scope.Get<int>("idx");
            int sampleRate = scope.Get<int>("sampleRate");
            inputDevice = new AudioDevice(idx, sampleRate);
            _sample = new AudioSample(
                channel: 1,
                rate: 16000,
                recordSecond: 30,
                chunk: 1024,
                format: scope.Eval<int>("p.paInt16")
            );
            pyAudio.terminate();            
        }

        return inputDevice;
    }

    private AudioSample GetAudioSample()
    {
        AudioSample sample;
        
        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Import("pyaudio", "p");
            dynamic pyAudio = scope.Eval("p.PyAudio()");
            scope.Set("pyAudio", pyAudio);
            sample = new AudioSample(
                channel: 1,
                rate: 16000,
                recordSecond: 30,
                chunk: 1024,
                format: scope.Eval<int>("p.paInt16")
            );
            pyAudio.terminate();            
        }

        return sample;
    }
    
    private byte[] GetBuffer()
    {
        return new byte[_sample.rate * _sample.recordSecond / _sample.chunk];
    }
    
    private bool IsBufferFull()
    {
        return _nextIdx >= _buffer.Length;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _recording = true;
            StartCoroutine(RecordCoroutine());
            Debug.Log("Recording start.");
        }
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            _recording = false;
            Debug.Log("Recording stop.");
        }
        // todo what if record starts before the coroutine ends?
    }
    
    [SuppressMessage("ReSharper", "IteratorNeverReturns")]
    private IEnumerator RecordCoroutine()
    {
        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Import("pyaudio", "p");
            dynamic pyAudio = scope.Eval("p.PyAudio()");
            dynamic stream = pyAudio.open(
                format: _sample.format,
                channels: _sample.channel,
                rate: _inputDevice.sampleRate,
                frames_per_buffer: _sample.chunk,
                input: true,
                input_device_index: _inputDevice.index
            );
            
            while (_recording)
            {
                Record(stream);
                yield return null;
            }

            if (_nextIdx > 0)
            {
                Transcribe();
            }

            stream.stop_stream();
            stream.close();
            pyAudio.terminate();
        }
    }
    
    private void Record(dynamic stream)
    {
        using var scope = Py.CreateScope();
        scope.Set("stream", stream);
        scope.Set("buffer", _buffer);
        _buffer[_nextIdx++] = scope.Eval("stream.read(buffer, True)").As<byte>();

        if (IsBufferFull())
        {
            Transcribe();
        }
    }

    private void Transcribe()
    {
        string text = ASR.instance.Process(_buffer, _inputDevice.sampleRate, _sample.rate);
        Debug.Log(text);
        _buffer = GetBuffer();
        _nextIdx = 0;
    }
    
}