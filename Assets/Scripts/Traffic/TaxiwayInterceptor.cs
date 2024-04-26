using System;
using Dreamteck.Splines;
using UnityEngine;

namespace Traffic
{
    internal class TaxiwayInterceptor : SplineFollower
    {
        internal event Action<SplineComputer, Spline.Direction> OnIntercept;
        [HideInInspector] [SerializeField] private SplineComputer taxiway;
        [HideInInspector] [SerializeField] private Spline.Direction taxiDirection;

        protected override void OnEnable()
        {
            base.OnEnable();
        
            follow = false;
        }

        internal void Intercept(SplineComputer taxiway, Spline.Direction taxiDirection, float speed, float turnRadius)
        {
            var sample = new SplineSample();
            this.taxiDirection = taxiDirection;
            this.taxiway = taxiway;
            this.taxiway.Project(transform.position, ref sample);
        
            var inbound = (sample.position - transform.position).normalized;
            var join = sample.position;
            var preJoin = join - turnRadius * inbound;
            this.taxiway.Evaluate(this.taxiway.Travel(sample.percent, turnRadius, taxiDirection), ref sample);
            var postJoin = sample.position;
            var points = new SplinePoint[4];
            points[0] = new SplinePoint(transform.position);
            points[1] = new SplinePoint(preJoin);
            points[2] = new SplinePoint(join);
            points[3] = new SplinePoint(postJoin);

            var tmpSpline = gameObject.AddComponent<SplineComputer>();
            tmpSpline.sampleMode = SplineComputer.SampleMode.Uniform;
            tmpSpline.sampleRate = 20;
            tmpSpline.type = Spline.Type.BSpline;
            tmpSpline.space = SplineComputer.Space.World;
            tmpSpline.SetPoints(points);

            spline = tmpSpline;
            RebuildImmediate();

            direction = Spline.Direction.Forward;
            wrapMode = Wrap.Default;
            followMode = FollowMode.Uniform;
            followSpeed = speed;
            follow = true;
            onEndReached += OnEndReached;
        }
    
        private void OnEndReached(double d)
        {
            onEndReached -= OnEndReached;
            follow = false;
            Destroy(spline);
            OnIntercept?.Invoke(taxiway, taxiDirection);
        }
    }
}
