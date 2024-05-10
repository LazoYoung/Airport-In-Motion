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
        internal Path thisPath { get; set; }
        [CanBeNull] internal Path nextPath { get; set; }
        internal Vector3 startPosition { get; set; }
        internal int startPointIdx { get; set; }
        internal int endPointIdx { get; set; }
    }

    public class Pathfinder : SplineFollower
    {
        public event Action<Path> EnterPath;

        public event Action<Path> CrossPath;
        
        protected override void Start()
        {
            base.Start();

            spline = gameObject.AddComponent<SplineComputer>();
            spline.hideFlags = HideFlags.HideAndDontSave;
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

        public void Taxi(Aircraft aircraft, TaxiInstruction instruction)
        {
            if (CreateTaxiPath(aircraft, instruction))
            {
                SetPercent(0);
                useTriggers = true;
                follow = true;
            }
        }

        private bool CreateTaxiPath(Aircraft aircraft, TaxiInstruction instruction)
        {
            var points = new List<SplinePoint>();
            var segments = new List<Segment>();
            var triggerPoints = new List<Tuple<Path, int>>();
            
            if (instruction.taxiways.Count == 0)
                return false;
            
            var tf = aircraft.transform;
            var startPos = tf.position;
            var turnPos = startPos + aircraft.turnRadius * tf.forward;
            var joint = new SplineSample();
            var node = instruction.taxiways.First;
            var taxiway = node.Value;
            
            taxiway.spline.Project(turnPos, ref joint);
            AddPoint(points, startPos);
            AddPoint(points, turnPos);
            AddPoint(points, joint.position);

            var terminate = false;
            var startPointIdx = taxiway.spline.PercentToPointIndex(joint.percent);
            startPos = joint.position;

            while (!terminate && node != null)
            {
                var segment = new Segment
                {
                    thisPath = node.Value,
                    startPosition = startPos,
                    startPointIdx = startPointIdx
                };
                segments.Add(segment);

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
                    terminate = true;
                    segment.nextPath = null;
                    segment.endPointIdx = segment.thisPath.spline.pointCount - 1;
                }
                
                var thisPathJunctionIdx = junction.Item1;
                var nextPathJunctionIdx = junction.Item2;
                var junctionNode = junction.Item3;
                segment.endPointIdx = thisPathJunctionIdx;
                startPos = junctionNode.transform.position;
                startPointIdx = nextPathJunctionIdx;
                
                if (!terminate)
                {
                    node = node.Next;
                }
                
                AddSegmentPoints(ref points, out var triggerPointIndex, segment, aircraft);

                if (segment.nextPath != null)
                {
                    triggerPoints.Add(new Tuple<Path, int>(segment.nextPath, triggerPointIndex));
                }
            }
            
            spline.SetPoints(points.ToArray());
            RemoveTriggers();
            RebuildImmediate();

            // var enterGroup = spline.AddTriggerGroup();
            // enterGroup.name = "enter";
            //
            // foreach (var pair in triggerPoints)
            // {
            //     var path = pair.Item1;
            //     var pointIndex = pair.Item2;
            //     var percent = spline.GetPointPercent(pointIndex);
            //     var trigger = enterGroup.AddTrigger(percent, SplineTrigger.Type.Forward);
            //     trigger.workOnce = true;
            //     trigger.AddListener(_ =>
            //     {
            //         EnterPath?.Invoke(path);
            //     });
            // }
            AddTriggers(segments, aircraft);

            return true;
        }
        
        private void AddTriggers(List<Segment> segments, Aircraft aircraft)
        {
            var enterGroup = spline.AddTriggerGroup();
            var crossGroup = spline.AddTriggerGroup();
            enterGroup.name = "enter";
            crossGroup.name = "cross";

            foreach (var seg in segments)
            {
                if (seg.nextPath != null)
                {
                    var percent = seg.thisPath.spline.GetPointPercent(seg.endPointIdx);
                    AddTrigger(enterGroup, seg.nextPath, percent);
                }

                var spl = seg.thisPath.spline;
                var from = spl.GetPointPercent(seg.startPointIdx);
                var to = spl.GetPointPercent(seg.endPointIdx);
                
                if (seg.startPointIdx == seg.endPointIdx)
                {
                    if (seg.nextPath == null)
                        return;
                    
                    // It's probably safe to assume the segment is not reversed
                    // because single-point-segment happens to be the first segment.
                    // todo: store directional info on every Segment
                    var margin = seg.nextPath.GetSafetyMargin(aircraft);
                    double percent = spl.Travel(to, margin, Backward);
                    AddTrigger(crossGroup, seg.nextPath, percent);
                    return;
                }
                
                foreach (var pair in spl.GetNodes(Math.Min(from, to), Math.Max(from, to)))
                {
                    var pointIdx = pair.Key;
                    var node = pair.Value;

                    foreach (var conn in node.GetConnections())
                    {
                        if (conn.spline == spl || !Path.Find(conn.spline.name, out Path path))
                            return;

                        var margin = path.GetSafetyMargin(aircraft);
                        var start = spl.GetPointPercent(pointIdx);
                        double percent = spl.Travel(start, margin, from < to ? Backward : Forward);
                        AddTrigger(crossGroup, path, percent);
                    }
                }
            }
        }

        private void AddTrigger(TriggerGroup group, Path path, double percent)
        {
            var enterTrigger = group.AddTrigger(percent, SplineTrigger.Type.Forward);
            enterTrigger.workOnce = true;
            enterTrigger.AddListener(_ => EnterPath?.Invoke(path));
        }

        private void AddSegmentPoints(ref List<SplinePoint> points, out int triggerPointIndex, Segment segment, Aircraft aircraft)
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

                if (Vector3.Distance(startPos, firstPos) > aircraft.turnRadius)
                {
                    AddPoint(points, startPos + aircraft.turnRadius * forward);
                }

                var from = isForward ? startPointIdx + 1 : startPointIdx - 1;
                bool CheckRange(int idx) => isForward ? idx < endPointIdx : idx > endPointIdx;

                for (var idx = from; CheckRange(idx); idx += delta)
                {
                    AddPoint(points, thisSpline.GetPointPosition(idx));
                }
                
                // Evaluate next intersecting or final point
                thisSpline.Evaluate(endPointIdx, ref joint);
                forward = isForward ? joint.forward : -joint.forward;
            }

            var nextPath = segment.nextPath;
            var margin = nextPath != null ? nextPath.GetSafetyMargin(aircraft) : 0f;
            triggerPointIndex = points.Count;
            
            if (margin > aircraft.turnRadius)
            {
                AddPoint(points, joint.position - margin * forward);
            }
            
            AddPoint(points, joint.position - aircraft.turnRadius * forward);
            AddPoint(points, joint.position);
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

        private void AddPoint(List<SplinePoint> list, Vector3 element)
        {
            list.Add(new SplinePoint(element));
        }
    }
}
