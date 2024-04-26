using System.ComponentModel;
using JetBrains.Annotations;

namespace System.Runtime.CompilerServices
{
    [UsedImplicitly]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit
    {
    }
}

public record AudioDevice(int index, int sampleRate);

public record AudioSample(
    int channel,
    int rate,
    int recordSecond,
    int chunk,
    int size,
    int format
);
