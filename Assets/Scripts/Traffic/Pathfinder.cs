using Dreamteck.Splines;
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
            var points = instruction.GetSplinePoints(aircraft);
            spline.SetPoints(points);
            RebuildImmediate();
            follow = true;
        }
    }
}