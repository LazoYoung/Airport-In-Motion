using System;
using System.Collections;
using Python.Runtime;
using UnityEngine;
using UnityEngine.Assertions;

namespace ASR
{
    public class SpeechRecognizer
    {
        private static readonly string ModelID = "Jzuluaga/wav2vec2-large-960h-lv60-self-en-atc-uwb-atcc-and-atcosim";

        public event EventHandler<string> TranscriptReady;
        private readonly AudioDevice _inputDevice;
        private readonly AudioSample _sample;
        private byte[][] _buffer;
        private int _rows;
        private int _nextIdx;
        private bool _flush;

        public SpeechRecognizer(MonoBehaviour context, AudioDevice inputDevice, AudioSample sample)
        {
            Assert.IsTrue(PythonEngine.IsInitialized, "Python engine not initialized.");

            _inputDevice = inputDevice;
            _sample = sample;
            ResetBuffer();
            context.StartCoroutine(Transcribe());
        }

        public void Feed(PyObject stream)
        {
            using var scope = Py.CreateScope();
            scope.Set("stream", stream);
            scope.Set("chunk", _sample.chunk);
            _buffer[_nextIdx++] = scope.Eval("stream.read(chunk, True)").As<byte[]>();
        }

        public void Flush()
        {
            _flush = true;
        }

        private IEnumerator Transcribe()
        {
            // global interpreter lock is mandatory while using Python
            using var state = Py.GIL();
            using var scope = Py.CreateScope();
            scope.Exec("from transformers import AutoModelForCTC, Wav2Vec2Processor");
            var model = scope.Eval($"AutoModelForCTC.from_pretrained('{ModelID}')");
            dynamic processor = scope.Eval($"Wav2Vec2Processor.from_pretrained('{ModelID}')");
            int rateFrom = _inputDevice.sampleRate;
            int rateTo = _sample.rate;

            while (true)
            {
                if (!_flush && !IsBufferFull())
                {
                    yield return null;
                    continue;
                }

                dynamic bytes = scope.Import("builtins").GetAttr("bytes");
                dynamic torch = scope.Import("torch");
                scope.Import("torchaudio.functional", "F");
                scope.Import("numpy", "np");
                scope.Exec("buffer = []");
                var buffer = scope.Get("buffer");

                foreach (byte[] row in _buffer)
                {
                    if (row == null) break;

                    buffer.InvokeMethod("append", bytes(row.ToPython()));
                }

                scope.Exec("buffer = b''.join(buffer)");
                scope.Exec("ndarray = np.frombuffer(buffer, dtype=np.int16).astype(np.float32) / 32767.0");
                scope.Exec("tensor = torch.from_numpy(ndarray)");
                scope.Exec($"wave = F.resample(tensor, {rateFrom}, {rateTo}).numpy()");
                dynamic wave = scope.Get("wave");
                dynamic input = processor(wave, sampling_rate: rateTo, return_tensors: "pt").input_values;
                string transcript = null;

                Py.With(scope.Eval("torch.no_grad()"), new Action<PyObject>(_ =>
                {
                    dynamic logits = model.Invoke(input).logits;
                    dynamic onehot = torch.argmax(logits, dim: -1);
                    dynamic decoded = processor.batch_decode(onehot);
                    scope.Set("decoded", decoded);
                    transcript = scope.Eval("decoded[0]").As<string>();
                }));

                _flush = false;
                ResetBuffer();
                TranscriptReady?.Invoke(this, transcript);
                yield return null;
            }
        }

        private bool IsBufferFull()
        {
            return _nextIdx >= _rows;
        }

        private void ResetBuffer()
        {
            _rows = _inputDevice.sampleRate * _sample.recordSecond / _sample.chunk;
            _buffer = new byte[_rows][];
            _nextIdx = 0;
        }
    }
}
