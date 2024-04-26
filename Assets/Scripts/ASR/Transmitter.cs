using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Python.Runtime;
using UnityEngine;
using UnityEngine.Assertions;

namespace ASR
{
    public class Transmitter : MonoBehaviour
    {
        private SpeechRecognizer _model;
        private AudioDevice _inputDevice;
        private AudioSample _sample;
        private bool _recording;

        private void Start()
        {
            Assert.IsTrue(PythonEngine.IsInitialized, "Python must be initialized.");
        
            _inputDevice = GetInputDevice();
            _sample = GetAudioSample();
            _model = new SpeechRecognizer(this, _inputDevice, _sample);
            _model.TranscriptReady += OnTranscriptReady;
        
            Debug.Log($"Default input device: {_inputDevice.index} ({_inputDevice.sampleRate} Hz)");
        }

        private void OnTranscriptReady(object sender, string transcript)
        {
            Debug.Log(transcript);
        }
    
        private void StartRecording()
        {
            StartCoroutine(RecordCoroutine());
        }
    
        private void StopRecording()
        {
            _recording = false;
            _model.Flush();
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
                scope.Exec("idx = int(device['index'])");
                scope.Exec("sampleRate = int(device['defaultSampleRate'])");
                int idx = scope.Get<int>("idx");
                int sampleRate = scope.Get<int>("sampleRate");
                inputDevice = new AudioDevice(idx, sampleRate);
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
                scope.Exec("format = p.paInt16");
                scope.Exec("audio = p.PyAudio()");
                sample = new AudioSample(
                    channel: 1,
                    rate: 16000,
                    recordSecond: 30,
                    chunk: 1024,
                    size: scope.Eval<int>("audio.get_sample_size(format)"),
                    format: scope.Get<int>("format")
                );
                scope.Exec("audio.terminate()");
            }

            return sample;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _recording = true;
                StartRecording();
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                StopRecording();
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
                PyObject stream = pyAudio.open(
                    format: _sample.format,
                    channels: _sample.channel,
                    rate: _inputDevice.sampleRate,
                    frames_per_buffer: _sample.chunk,
                    input: true,
                    input_device_index: _inputDevice.index
                );

                while (_recording)
                {
                    _model.Feed(stream);
                    yield return null;
                }

                stream.InvokeMethod("stop_stream");
                stream.InvokeMethod("close");
                pyAudio.terminate();
            }
        }
    }
}
