using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using JetBrains.Annotations;
using Layout;
using UnityEngine;
using static Dreamteck.Splines.Spline.Direction;

namespace Traffic
{
    internal record Segment
    {
        [CanBeNull] internal Path prevPath { get; set; }
        internal Path thisPath { get; set; }
        [CanBeNull] internal Path nextPath { get; set; }
        internal Vector3 startPosition { get; set; }
        internal int startPointIdx { get; set; }
        internal int endPointIdx { get; set; }
        internal bool isForward { get; set; }
    }

    public class Pathfinder : SplineFollower
    {
        public event Action<Path> EnterPath;

        public event Action<Path> CrossPath;
        
        protected override void Start()
        {
            base.Start();

            spline = gameObject.AddComponent<SplineComputer>();
            spline.sampleRate = 20;
            spline.type = Spline.Type.BSpline;
            spline.space = SplineComputer.Space.World;
            direction = Forward;
            wrapMode = Wrap.Default;
            followMode = FollowMode.Uniform;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Destroy(spline);
        }

        public bool CreateTaxiPath(Aircraft aircraft, TaxiInstruction instruction)
        {
            var points = new List<SplinePoint>();
            var segments = new List<Segment>();
            
            if (instruction.taxiways.Count == 0)
                return false;
            
            var tf = aircraft.transform;
            var startPos = tf.position;
            var turnPos = startPos + aircraft.GetTurnRadius() * tf.forward;
            var joint = new SplineSample();
            var node = instruction.taxiways.First;
            var taxiway = node.Value;
            
            taxiway.spline.Project(turnPos, ref joint);
            AddSplinePoint(points, startPos);
            AddSplinePoint(points, turnPos);
            AddSplinePoint(points, joint.position);

            var startPointIdx = -1;
            startPos = joint.position;

            while (node != null)
            {
                var segment = new Segment
                {
                    prevPath = node.Previous?.Value,
                    thisPath = node.Value,
                    startPosition = startPos
                };

                if (node.Next != null && FindJunction(startPos, segment.thisPath, node.Next.Value, out var junction))
                {
                    // Proceed to next taxiway
                    segment.nextPath = node.Next.Value;
                }
                else if (FindJunction(startPos, segment.thisPath, instruction.departRunway, out junction))
                {
                    // Proceed to runway
                    segment.nextPath = instruction.departRunway;
                } 
                else
                {
                    segment.nextPath = null;
                    segment.startPointIdx = startPointIdx;
                    segment.endPointIdx = segment.thisPath.spline.pointCount - 1;
                    segment.isForward = segment.endPointIdx >= segment.startPointIdx;
                    segments.Add(segment);
                    AddSplinePoints(points, segment, aircraft);
                    break;
                }

                if (startPointIdx < 0)
                {
                    var spl = segment.thisPath.spline;
                    spl.Project(startPos, ref joint);
                    var isForward = spl.GetPointPercent(junction.Item1) > joint.percent;
                    
                    int start = isForward ? 0 : spl.pointCount - 1;
                    int delta = isForward ? 1 : -1;
                    bool CheckRange(int idx) => (idx >= 0 && idx < spl.pointCount);
                    bool Matches(double percent) => isForward ? (percent >= joint.percent) : (percent <= joint.percent);
                    
                    for (var idx = start; CheckRange(idx); idx += delta)
                    {
                        if (Matches(spl.GetPointPercent(idx)))
                        {
                            startPointIdx = idx;
                            break;
                        }
                    }
                }
                
                var thisPathJunctionIdx = junction.Item1;
                var nextPathJunctionIdx = junction.Item2;
                var junctionNode = junction.Item3;
                segment.startPointIdx = startPointIdx;
                segment.endPointIdx = thisPathJunctionIdx;
                segment.isForward = segment.endPointIdx >= segment.startPointIdx;
                startPos = junctionNode.transform.position;
                startPointIdx = nextPathJunctionIdx;
                node = node.Next;

                segments.Add(segment);
                AddSplinePoints(points, segment, aircraft);
            }
            
            useTriggers = true;
            spline.SetPoints(points.ToArray());
            spline.RebuildImmediate();
            SetPercent(0);
            RemoveTriggers();
            AddTriggers(segments, aircraft);
            
            return true;
        }
        
        private void AddTriggers(List<Segment> segments, Aircraft aircraft)
        {
            var sample = new SplineSample();
            var group = spline.AddTriggerGroup();
        
            foreach (var seg in segments)
            {
                if (seg.nextPath != null)
                {
                    var pos = seg.thisPath.spline.GetPointPosition(seg.endPointIdx);
                    spline.Project(pos, ref sample);
                    var trigger = CreateTrigger(group, sample.percent);
                    trigger.AddListener(_ => EnterPath?.Invoke(seg.nextPath));
                }
        
                var spl = seg.thisPath.spline;
                var from = spl.GetPointPercent(seg.isForward ? seg.startPointIdx : seg.endPointIdx);
                var to = spl.GetPointPercent(seg.isForward ? seg.endPointIdx : seg.startPointIdx);
                
                if (seg.startPointIdx == seg.endPointIdx)
                {
                    if (seg.nextPath == null)
                        return;
                    
                    // It's probably safe to assume the segment is not reversed
                    // because single-point-segment happens to be the first segment.
                    var margin = seg.nextPath.GetSafetyMargin(aircraft);
                    double percent = spl.Travel(to, margin, Backward);
                    var pos = spl.EvaluatePosition(percent);
                    spline.Project(pos, ref sample);
                    var trigger = CreateTrigger(group, sample.percent);
                    trigger.AddListener(_ => CrossPath?.Invoke(seg.nextPath));
                    return;
                }
                
                foreach (var pair in spl.GetJunctions(Math.Min(from, to), Math.Max(from, to)))
                {
                    var pointIdx = pair.Key;
        
                    foreach (var conn in pair.Value)
                    {
                        var prevSpline = seg.prevPath?.spline;
                    
                        if (conn.spline == spl || prevSpline && conn.spline == prevSpline)
                            continue;

                        if (Path.Find(conn.spline.name, out Path path))
                        {
                            var margin = path.GetSafetyMargin(aircraft);
                            var start = spl.GetPointPercent(pointIdx);
                            var backward = seg.isForward ? Backward : Forward;
                            double percent = spl.Travel(start, margin, backward);
                            var pos = spl.EvaluatePosition(percent);
                            spline.Project(pos, ref sample);
                            var trigger = CreateTrigger(group, sample.percent);
                            trigger.AddListener(_ => CrossPath?.Invoke(path));   
                        }
                    }
                }
            }
        }

        private SplineTrigger CreateTrigger(TriggerGroup group, double percent)
        {
            var trigger = group.AddTrigger(percent, SplineTrigger.Type.Forward);
            trigger.workOnce = true;
            return trigger;
        }

        private void AddSplinePoints(List<SplinePoint> points, Segment segment, Aircraft aircraft)
        {
            var thisSpline = segment.thisPath.spline;
            var startPos = segment.startPosition;
            var startPointIdx = segment.startPointIdx;
            var endPointIdx = segment.endPointIdx;
            var joint = new SplineSample();
            Vector3 forward;

            if (startPointIdx == endPointIdx)
            {
                thisSpline.Evaluate(endPointIdx, ref joint);
                forward = (joint.position - startPos).normalized;
            }
            else
            {
                var isForward = startPointIdx < endPointIdx;
                var delta = isForward ? 1 : -1;
                var firstPos = thisSpline.GetPointPosition(startPointIdx + delta);

                // Evaluate previous intersecting point
                thisSpline.Evaluate(startPointIdx, ref joint);
                forward = isForward ? joint.forward : -joint.forward;

                if (Vector3.Distance(startPos, firstPos) > aircraft.GetTurnRadius())
                {
                    AddSplinePoint(points, startPos + aircraft.GetTurnRadius() * forward);
                }

                var from = isForward ? startPointIdx + 1 : startPointIdx - 1;
                bool CheckRange(int idx) => isForward ? idx < endPointIdx : idx > endPointIdx;

                for (var idx = from; CheckRange(idx); idx += delta)
                {
                    AddSplinePoint(points, thisSpline.GetPointPosition(idx));
                }
                
                // Evaluate next intersecting or final point
                thisSpline.Evaluate(endPointIdx, ref joint);
                forward = isForward ? joint.forward : -joint.forward;
            }

            var nextPath = segment.nextPath;
            var margin = nextPath != null ? nextPath.GetSafetyMargin(aircraft) : 0f;
            
            if (margin > aircraft.GetTurnRadius())
            {
                AddSplinePoint(points, joint.position - margin * forward);
            }
            
            AddSplinePoint(points, joint.position - aircraft.GetTurnRadius() * forward);
            AddSplinePoint(points, joint.position);
        }
        
        private void AddSplinePoint(List<SplinePoint> list, Vector3 element)
        {
            list.Add(new SplinePoint(element));
        }

        /**
         * If two paths do intersect,
         * the function returns true with the following items in Tuple:   <br/>
         * 1. Index of the intersecting point for thisPath                <br/>
         * 2. Index of the intersecting point for nextPath                <br/>
         * 3. The node connecting two paths                               <br/>
         */
        private bool FindJunction(Vector3 origin, Path thisPath, Path nextPath, out Tuple<int, int, Node> junction)
        {
            if (thisPath == null || nextPath == null)
            {
                junction = default;
                return false;
            }
            
            var thisSpline = thisPath.spline;
            var nextSpline = nextPath.spline;
            var distance = float.MaxValue;
            var thisPointIdx = -1;
            var nextPointIdx = -1;
            Node junctionNode = null;

            foreach (var pair in thisSpline.GetJunctions())
            {
                var pointIdx = pair.Key;
                var node = thisSpline.GetNode(pointIdx);

                foreach (var connection in pair.Value)
                {
                    if (connection.spline != nextSpline)
                        continue;

                    var dist = Vector3.Distance(origin, node.transform.position);

                    if (dist < distance)
                    {
                        distance = dist;
                        thisPointIdx = pointIdx;
                        nextPointIdx = connection.pointIndex;
                        junctionNode = node;
                    }

                    break;
                }
            }

            if (junctionNode == null)
            {
                junction = default;
                return false;
            }

            junction = new Tuple<int, int, Node>(thisPointIdx, nextPointIdx, junctionNode);
            return true;
        }
        
        private void RemoveTriggers()
        {
            foreach (var group in spline.triggerGroups)
            {
                for (var idx = 0; idx < group.triggers.Length; ++idx)
                {
                    group.RemoveTrigger(idx);
                }
            }

            spline.triggerGroups = Array.Empty<TriggerGroup>();
        }
    }
}
