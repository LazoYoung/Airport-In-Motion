using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using JetBrains.Annotations;
using Layout;
using UnityEngine;

namespace Traffic
{
    public class TaxiInstruction
    {
        public Taxiway[] Taxiways;
        public Runway[] CrossRunways;
        public Runway DepartRunway;
        public Path HoldingPoint;

        public TaxiInstruction(Taxiway[] taxiways)
        {
            Taxiways = taxiways;
        }
        
        public static Taxiway[] GetTaxiways(string taxiways)
        {
            var taxiwayList = new List<Taxiway>();
            
            foreach (string identifier in taxiways.Split(' '))
            {
                var taxiway = Taxiway.Get(identifier);

                if (taxiway == null)
                {
                    taxiwayList.Clear();
                    break;
                }
                
                taxiwayList.Add(taxiway);
            }

            return taxiwayList.ToArray();
        }
        
        public SplinePoint[] GetSplinePoints(Aircraft aircraft)
        {
            if (Taxiways == null || Taxiways.Length == 0)
                return Array.Empty<SplinePoint>();
            
            var tf = aircraft.transform;
            var points = new List<SplinePoint>();
            var startPos = tf.position;
            var turnPos = startPos + aircraft.turnRadius * tf.forward;
            var spline = Taxiways[0].spline;
            var joint = new SplineSample();
            spline.Project(turnPos, ref joint);
            Vector3 entryPoint;
            // var entryPoint = joint.position - aircraft.turnRadius * joint.forward;
            
            AddPoint(points, startPos);
            AddPoint(points, turnPos);
            // AddPoint(points, entryPoint);
            AddPoint(points, joint.position);

            // double originPercent = joint.percent;
            int originPointIdx = spline.PercentToPointIndex(joint.percent);
            var origin = joint.position;
            
            for (var t = 1; t < Taxiways.Length; ++t)
            {
                var thisPath = Taxiways[t - 1];
                var nextPath = Taxiways[t];
                var junction = GetJunction(origin, thisPath, nextPath);

                if (junction == null)
                    break;

                int jointPointIdx = junction.Item1;
                var node = junction.Item2;
                
                // double jointPercent = spline.GetPointPercent(pointIdx);
                // var direction = jointPercent > originPercent ? Spline.Direction.Forward : Spline.Direction.Backward;

                for (int idx = originPointIdx + 1; idx < jointPointIdx; ++idx)
                {
                    AddPoint(points, spline.GetPointPosition(idx));
                }

                spline.Evaluate(jointPointIdx, ref joint);
                entryPoint = joint.position - aircraft.turnRadius * joint.forward;
                AddPoint(points, entryPoint);
                AddPoint(points, joint.position);
                
                originPointIdx = jointPointIdx;
                origin = node.transform.position;
                spline = nextPath.spline;
            }
            
            
            // todo consider holding point, crossing/departing runway
            
            return points.ToArray();
        }
        
        /**
         * Returns a tuple consisting of the `point index` for thisPath and the `node`.
         * In case any junction were not found, null is returned.
         */
        [CanBeNull]
        private Tuple<int, Node> GetJunction(Vector3 origin, Path thisPath, Path nextPath)
        {
            var nextSpline = nextPath.spline;
            var distance = float.MaxValue;
            Tuple<int, Node> junction = null;
            
            foreach (var pair in thisPath.spline.GetNodes())
            {
                var node = pair.Value;
                
                foreach (var connection in node.GetConnections())
                {
                    if (connection.spline != nextSpline)
                        continue;
                    
                    float dist = Vector3.Distance(origin, node.transform.position);

                    if (dist < distance)
                    {
                        distance = dist;
                        junction = new Tuple<int, Node>(pair.Key, node);
                    }
                    
                    break;
                }
            }

            return junction;
        }
        
        private void AddPoint(List<SplinePoint> list, Vector3 element)
        {
            list.Add(new SplinePoint(element));
        }
    }
}
