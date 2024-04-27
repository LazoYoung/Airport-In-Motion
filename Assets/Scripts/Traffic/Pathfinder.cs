using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using Layout;
using UnityEngine;

namespace Traffic
{
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
            var points = GetSplinePoints(aircraft, instruction);
            spline.SetPoints(points);
            RebuildImmediate();
            follow = true;
        }
        
        private SplinePoint[] GetSplinePoints(Aircraft aircraft, TaxiInstruction instruction)
        {
            var taxiways = instruction.Taxiways;
            
            if (taxiways == null || taxiways.Length == 0)
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

            int startPointIdx = firstSpline.PercentToPointIndex(joint.percent);
            startPos = joint.position;

            // Draw path along taxiways except the last one
            for (var t = 1; t < taxiways.Length; ++t)
            {
                var thisPath = taxiways[t - 1];
                var nextPath = taxiways[t];
                var thisSpline = thisPath.spline;
                var tuple = GetJoint(startPos, thisPath, nextPath);

                if (tuple.Item3 == null)
                    break;

                int thisPathJunctionIdx = tuple.Item1;
                int nextPathJunctionIdx = tuple.Item2;
                var junctionNode = tuple.Item3;
                var firstPos = thisSpline.GetPointPosition(startPointIdx + 1);
                bool isForward = thisSpline.GetPointPercent(thisPathJunctionIdx) >
                                 thisSpline.GetPointPercent(startPointIdx);

                // Evaluate previous intersecting point
                thisSpline.Evaluate(startPointIdx, ref joint);
                var forward = isForward ? joint.forward : -joint.forward;

                if (Vector3.Distance(startPos, firstPos) > aircraft.turnRadius)
                {
                    AddPoint(points, startPos + aircraft.turnRadius * forward);
                }

                for (int idx = startPointIdx + 1; idx < thisPathJunctionIdx; ++idx)
                {
                    AddPoint(points, thisSpline.GetPointPosition(idx));
                }

                // Evaluate next intersecting point
                thisSpline.Evaluate(thisPathJunctionIdx, ref joint);
                
                AddPoint(points, joint.position - aircraft.turnRadius * forward);
                AddPoint(points, joint.position);

                startPointIdx = nextPathJunctionIdx;
                startPos = junctionNode.transform.position;
            }
            
            // todo: draw path for last taxiway, considering holding point and runways

            return points.ToArray();
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