using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using Layout;
using UnityEngine;

namespace Traffic
{
    internal record Segment
    {
        internal SplineComputer spline { get; init; }
        internal Vector3 startPosition { get; init; }
        internal int startPointIdx { get; init; }
        internal int endPointIdx { get; init; }
    }
    
    public class Pathfinder : SplineFollower
    {
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            Destroy(spline);
        }

        public void Taxi(Aircraft aircraft, TaxiInstruction instruction)
        {
            var points = GetTaxiPath(aircraft, instruction);
            spline.SetPoints(points);
            RebuildImmediate();
            follow = true;
        }
        
        private SplinePoint[] GetTaxiPath(Aircraft aircraft, TaxiInstruction instruction)
        {
            var taxiways = new Taxiway[instruction.Taxiways.Count];
            instruction.Taxiways.CopyTo(taxiways, 0);
            
            if (taxiways.Length == 0)
                return Array.Empty<SplinePoint>();

            var tf = aircraft.transform;
            var points = new List<SplinePoint>();
            var startPos = tf.position;
            var turnPos = startPos + aircraft.turnRadius * tf.forward;
            var firstSpline = taxiways[0].spline;
            var joint = new SplineSample();

            firstSpline.Project(turnPos, ref joint);
            AddPoint(points, startPos);
            AddPoint(points, turnPos);
            AddPoint(points, joint.position);

            Segment segment;
            List<SplinePoint> segmentPoints;
            Tuple<int, int, Node> jointTuple;
            int startPointIdx = firstSpline.PercentToPointIndex(joint.percent);
            startPos = joint.position;

            // Draw path along taxiways except the last one
            for (var t = 1; t < taxiways.Length; ++t)
            {
                var thisPath = taxiways[t - 1];
                var nextPath = taxiways[t];
                var thisSpline = thisPath.spline;
                jointTuple = GetJoint(startPos, thisPath, nextPath);

                if (jointTuple.Item3 == null)
                    break;

                int thisPathJunctionIdx = jointTuple.Item1;
                int nextPathJunctionIdx = jointTuple.Item2;
                var junctionNode = jointTuple.Item3;
                segment = new Segment()
                {
                    spline = thisSpline,
                    startPosition = startPos,
                    startPointIdx = startPointIdx,
                    endPointIdx = thisPathJunctionIdx
                };
                
                // todo: check hold short instruction
                
                segmentPoints = GetSegmentPoints(segment, aircraft, ref joint);
                points.AddRange(segmentPoints);
                
                startPointIdx = nextPathJunctionIdx;
                startPos = junctionNode.transform.position;
            }
            
            var runway = instruction.DepartRunway;
            jointTuple = runway != null ? GetJoint(startPos, taxiways[^1], runway) : null;

            if (jointTuple != null)
            {
                segment = new Segment()
                {
                    spline = taxiways[^1].spline,
                    startPosition = startPos,
                    startPointIdx = startPointIdx,
                    endPointIdx = jointTuple.Item1
                };
                // todo: do not penetrate the runway
            }
            else
            {
                var lastSpline = taxiways[^1].spline;
                segment = new Segment()
                {
                    spline = lastSpline,
                    startPosition = startPos,
                    startPointIdx = startPointIdx,
                    endPointIdx = lastSpline.pointCount - 1
                };
                // todo: check hold short instruction
            }
            
            segmentPoints = GetSegmentPoints(segment, aircraft, ref joint);
            points.AddRange(segmentPoints);
            
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
         * Returns a tuple consisting of the following items:   <br/>
         * 1. Index of the intersecting point for thisPath      <br/>
         * 2. Index of the intersecting point for nextPath      <br/>
         * 3. The node connecting two paths                     <br/>
         */
        private Tuple<int, int, Node> GetJoint(Vector3 origin, Path thisPath, Path nextPath)
        {
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

            return new Tuple<int, int, Node>(thisPointIdx, nextPointIdx, junction);
        }

        private void AddPoint(List<SplinePoint> list, Vector3 element)
        {
            list.Add(new SplinePoint(element));
        }
    }
}