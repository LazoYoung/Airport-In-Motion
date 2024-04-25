using System;
using Dreamteck.Splines;
using UnityEngine;

[RequireComponent(typeof(SplineFollower))]
public class Aircraft : MonoBehaviour
{
    // todo apply changes to SplineFollower
    [SerializeField] private float taxiSpeed = 10f;
    [SerializeField] private float turnRadius = 30f;
    [HideInInspector] [SerializeField] private SplineFollower follower;
    [HideInInspector] [SerializeField] private SplineComputer taxiway;
    [HideInInspector] [SerializeField] private Spline.Direction taxiDirection;

    private void Awake()
    {
        tag = "Aircraft";
        follower = GetComponent<SplineFollower>();
        follower.follow = false;
    }

    public void JoinTaxiway(SplineComputer spline, Spline.Direction direction)
    {
        var sample = new SplineSample();
        taxiDirection = direction;
        taxiway = spline;
        taxiway.Project(transform.position, ref sample);
        
        var inbound = (sample.position - transform.position).normalized;
        var join = sample.position;
        var preJoin = join - turnRadius * inbound;
        taxiway.Evaluate(taxiway.Travel(sample.percent, turnRadius, direction), ref sample);
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

        follower.spline = tmpSpline;
        follower.RebuildImmediate();

        follower.direction = Spline.Direction.Forward;
        follower.wrapMode = SplineFollower.Wrap.Default;
        follower.followMode = SplineFollower.FollowMode.Uniform;
        follower.followSpeed = taxiSpeed;
        follower.follow = true;
        follower.onEndReached += OnTaxiwayJoin;
        Debug.Log("join twy");
    }

    public void StartTaxi(SplineComputer spline, Spline.Direction direction)
    {
        taxiway = spline;
        taxiDirection = direction;
        StartTaxi();
    }
    
    private void StartTaxi()
    {
        follower.spline = taxiway;
        follower.RebuildImmediate();

        var pos = transform.position;
        var sample = follower.spline.Project(pos);
        follower.SetPercent(sample.percent);
        follower.direction = taxiDirection;
        follower.wrapMode = SplineFollower.Wrap.Default;
        follower.followMode = SplineFollower.FollowMode.Uniform;
        follower.followSpeed = taxiSpeed;
        follower.follow = true;
        Debug.Log($"taxi start at {sample.percent}");
    }

    private void OnTaxiwayJoin(double d)
    {
        follower.onEndReached -= OnTaxiwayJoin;
        follower.follow = false;
        Destroy(follower.spline);
        StartTaxi();
    }
}
