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
        internal SplineComputer spline { get; set; }
        internal Vector3 startPosition { get; set; }
        internal int startPointIdx { get; set; }
        internal int endPointIdx { get; set; }
    }

    public class Pathfinder : SplineFollower
    {
        public event Action TaxiHold;
        
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
            var points = GetTaxiPath(aircraft, instruction);

            if (points.Length > 0)
            {
                spline.SetPoints(points);
                SetPercent(0);
                RebuildImmediate();
                follow = true;
            }
        }

        private SplinePoint[] GetTaxiPath(Aircraft aircraft, TaxiInstruction instruction)
        {
            if (instruction.taxiways.Count == 0)
                return Array.Empty<SplinePoint>();

            var tf = aircraft.transform;
            var points = new List<SplinePoint>();
            var startPos = tf.position;
            var turnPos = startPos + aircraft.turnRadius * tf.forward;
            var joint = new SplineSample();
            var node = instruction.taxiways.First;
            var taxiway = node.Value;
            
            taxiway.spline.Project(turnPos, ref joint);
            AddPoint(points, startPos);
            AddPoint(points, turnPos);
            AddPoint(points, joint.position);

            int startPointIdx = taxiway.spline.PercentToPointIndex(joint.percent);
            startPos = joint.position;
            var terminate = false;

            while (!terminate && node != null)
            {
                taxiway = node.Value;
                var segment = new Segment();
                var junction = FindJunction(startPos, taxiway, instruction.holdShort);

                if (junction != null)
                {
                    // Proceed to holding point
                    terminate = true;
                }
                else if (node.Next != null)
                {
                    // Proceed to next taxiway
                    junction = FindJunction(startPos, taxiway, node.Next.Value);
                }
                else
                {
                    // Proceed to runway
                    var runway = instruction.departRunway;
                    junction = FindJunction(startPos, taxiway, runway);
                }
                
                if (junction == null)
                {
                    terminate = true;
                    segment.spline = taxiway.spline;
                    segment.startPosition = startPos;
                    segment.startPointIdx = startPointIdx;
                    segment.endPointIdx = taxiway.spline.pointCount - 1;
                }
                else
                {
                    int thisPathJunctionIdx = junction.Item1;
                    int nextPathJunctionIdx = junction.Item2;
                    var junctionNode = junction.Item3;
                    segment.spline = taxiway.spline;
                    segment.startPosition = startPos;
                    segment.startPointIdx = startPointIdx;
                    segment.endPointIdx = thisPathJunctionIdx;
                    startPos = junctionNode.transform.position;
                    startPointIdx = nextPathJunctionIdx;
                }
                
                if (!terminate)
                {
                    node = node.Next;
                }
                
                var range = GetSegmentPoints(segment, aircraft, ref joint);
                points.AddRange(range);
            }

            return points.ToArray();
        }

        private List<SplinePoint> GetSegmentPoints(Segment segment, Aircraft aircraft, ref SplineSample joint)
        {
            var points = new List<SplinePoint>();
            var spl = segment.spline;
            var startPos = segment.startPosition;
            int startPointIdx = segment.startPointIdx;
            int endPointIdx = segment.endPointIdx;
            var firstPos = spl.GetPointPosition(startPointIdx + 1);
            bool isForward = spl.GetPointPercent(endPointIdx) >
                             spl.GetPointPercent(startPointIdx);

            // Evaluate previous intersecting point
            spl.Evaluate(startPointIdx, ref joint);
            var forward = isForward ? joint.forward : -joint.forward;

            if (Vector3.Distance(startPos, firstPos) > aircraft.turnRadius)
            {
                AddPoint(points, startPos + aircraft.turnRadius * forward);
            }

            for (int idx = startPointIdx + 1; idx < endPointIdx; ++idx)
            {
                AddPoint(points, spl.GetPointPosition(idx));
            }

            // Evaluate next intersecting or final point
            spl.Evaluate(endPointIdx, ref joint);

            AddPoint(points, joint.position - aircraft.turnRadius * forward);
            AddPoint(points, joint.position);
            return points;
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
            int thisPointIdx = -1;
            int nextPointIdx = -1;
            Node junction = null;

            foreach (var pair in thisSpline.GetJunctions())
            {
                int pointIdx = pair.Key;
                var node = thisSpline.GetNode(pointIdx);

                foreach (var connection in pair.Value)
                {
                    if (connection.spline != nextSpline)
                        continue;

                    float dist = Vector3.Distance(origin, node.transform.position);

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