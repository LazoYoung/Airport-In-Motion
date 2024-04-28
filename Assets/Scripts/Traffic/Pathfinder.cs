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
        
        public event Action<Taxiway> TaxiwayEnter;
        
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
                follow = true;
            }
        }

        private bool CreateTaxiPath(Aircraft aircraft, TaxiInstruction instruction)
        {
            var points = new List<SplinePoint>();
            var triggerPoints = new Dictionary<Taxiway, int>();
            
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
                
                AddSegmentPoints(ref points, out int triggerPointIndex, segment, aircraft);
                triggerPoints.Add(taxiway, triggerPointIndex);
            }
            
            spline.SetPoints(points.ToArray());
            RebuildImmediate();
            
            var group = spline.AddTriggerGroup();
            group.color = Color.green;
            group.name = "enter";

            foreach (var pair in triggerPoints)
            {
                var twy = pair.Key;
                int pointIndex = pair.Value;
                double percent = spline.GetPointPercent(pointIndex);
                var trigger = group.AddTrigger(percent, SplineTrigger.Type.Forward);
                trigger.AddListener(_ => TaxiwayEnter?.Invoke(twy));
            }

            return true;
        }

        private void AddSegmentPoints(ref List<SplinePoint> points, out int triggerPointIndex, Segment segment, Aircraft aircraft)
        {
            var spl = segment.spline;
            var startPos = segment.startPosition;
            int startPointIdx = segment.startPointIdx;
            int endPointIdx = segment.endPointIdx;
            var joint = new SplineSample();
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

            triggerPointIndex = points.Count;
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