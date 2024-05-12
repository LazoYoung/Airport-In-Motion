using JetBrains.Annotations;
using Layout;
using UnityEngine;

namespace Traffic
{
    public struct Segment
    {
        [CanBeNull] internal Path prevPath { get; set; }
        internal Path thisPath { get; set; }
        [CanBeNull] internal Path nextPath { get; set; }
        internal Vector3 startPosition { get; set; }
        internal int startPointIdx { get; set; }
        internal int endPointIdx { get; set; }
        internal bool isForward { get; set; }
    }
}