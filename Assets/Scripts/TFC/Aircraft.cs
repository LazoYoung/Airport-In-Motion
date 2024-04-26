using Dreamteck.Splines;
using UnityEngine;

namespace TFC
{
    public class Aircraft : MonoBehaviour
    {
        [SerializeField] private float taxiSpeed = 10f;
        [SerializeField] private float turnRadius = 30f;
        [HideInInspector] [SerializeField] private TaxiwayFollower follower;
        [HideInInspector] [SerializeField] private TaxiwayInterceptor interceptor;
        [HideInInspector] [SerializeField] private Spline.Direction taxiDirection;

        public void JoinTaxiway(SplineComputer taxiway, Spline.Direction direction)
        {
            interceptor.Intercept(taxiway, direction, taxiSpeed, turnRadius);
        }

        public void StartTaxi(SplineComputer spline, Spline.Direction direction)
        {
            var pos = transform.position;
            var sample = spline.Project(pos);
            taxiDirection = direction;
            follower.spline = spline;
            follower.RebuildImmediate();
            follower.SetPercent(sample.percent);
            follower.direction = taxiDirection;
            follower.wrapMode = SplineFollower.Wrap.Default;
            follower.followMode = SplineFollower.FollowMode.Uniform;
            follower.follow = true;
        }
    
        private void OnIntercept(SplineComputer taxiway, Spline.Direction direction)
        {
            StartTaxi(taxiway, direction);
        }
    
        private void OnEnable()
        {
            tag = "Aircraft";
        
            follower = gameObject.AddComponent<TaxiwayFollower>();
            follower.hideFlags = HideFlags.HideAndDontSave;
            follower.follow = false;
        
            interceptor = gameObject.AddComponent<TaxiwayInterceptor>();
            interceptor.hideFlags = HideFlags.HideAndDontSave;
            interceptor.OnIntercept += OnIntercept;
        }

        private void OnDisable()
        {
            Destroy(follower);
            Destroy(interceptor);
        }

        private void Update()
        {
            follower.followSpeed = taxiSpeed;
        }
    }
}
