using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using JetBrains.Annotations;
using Layout;
using UnityEngine;

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
        public event Action TaxiHold;
        
        public event Action<Path> EnterPath;
        
        protected override void Start()
        {
            base.Start();

            spline = gameObject.AddComponent<SplineComputer>();
            spline.hideFlags = HideFlags.HideAndDontSave;
            spline.sampleRate = 20;
            spline.type = Spline.Type.BSpline;
            spline.space = SplineComputer.Space.World;
            direction = Spline.Direction.Forward;
            wrapMode = Wrap.Default;
            followMode = FollowMode.Uniform;
            onEndReached += OnEndReached;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Destroy(spline);
            onEndReached -= OnEndReached;
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
                var junction = FindJunction(startPos, node.Value, instruction.holdShort);
                var segment = new Segment
                {
                    thisPath = node.Value,
                    startPosition = startPos,
                    startPointIdx = startPointIdx
                };

                if (junction != null)
                {
                    // Proceed to holding point
                    terminate = true;
                    segment.nextPath = instruction.holdShort;
                }
                else if (node.Next != null)
                {
                    // Proceed to next taxiway
                    junction = FindJunction(startPos, segment.thisPath, node.Next.Value);
                    segment.nextPath = node.Next.Value;
                }
                else
                {
                    // Proceed to runway
                    var runway = instruction.departRunway;
                    junction = FindJunction(startPos, segment.thisPath, runway);
                    segment.nextPath = runway;
                }

                if (junction == null)
                {
                    terminate = true;
                    segment.nextPath = null;
                    segment.endPointIdx = segment.thisPath.spline.pointCount - 1;
                }
                else
                {
                    var thisPathJunctionIdx = junction.Item1;
                    var nextPathJunctionIdx = junction.Item2;
                    var junctionNode = junction.Item3;
                    segment.endPointIdx = thisPathJunctionIdx;
                    startPos = junctionNode.transform.position;
                    startPointIdx = nextPathJunctionIdx;
                }
                
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

            var group = spline.AddTriggerGroup();
            group.name = "enter";

            foreach (var pair in triggerPoints)
            {
                var path = pair.Item1;
                var pointIndex = pair.Item2;
                var percent = spline.GetPointPercent(pointIndex);
                var trigger = group.AddTrigger(percent, SplineTrigger.Type.Forward);
                trigger.workOnce = true;
                trigger.AddListener(_ =>
                {
                    EnterPath?.Invoke(path);
                });
            }

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
         * If two paths intersect, the following items are returned:   <br/>
         * 1. Index of the intersecting point for thisPath             <br/>
         * 2. Index of the intersecting point for nextPath             <br/>
         * 3. The node connecting two paths                            <br/>
         */
        [CanBeNull]
        private Tuple<int, int, Node> FindJunction(Vector3 origin, Path thisPath, Path nextPath)
        {
            if (thisPath == null || nextPath == null)
                return null;
            
            var thisSpline = thisPath.spline;
            var nextSpline = nextPath.spline;
            var distance = float.MaxValue;
            var thisPointIdx = -1;
            var nextPointIdx = -1;
            Node junction = null;

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
                        junction = node;
                    }

                    break;
                }
            }

            if (junction == null)
                return null;

            return new Tuple<int, int, Node>(thisPointIdx, nextPointIdx, junction);
        }

        private void AddPoint(List<SplinePoint> list, Vector3 element)
        {
            list.Add(new SplinePoint(element));
        }

        private void OnEndReached(double d)
        {
            TaxiHold?.Invoke();
        }
    }
}