using System;
using Dreamteck.Splines;
using Layout;
using UnityEngine;

namespace Traffic
{
    public class Aircraft : MonoBehaviour
    {
        [SerializeField] public float taxiSpeed = 10f;

        [SerializeField] public float turnRadius = 30f;

        [HideInInspector] [SerializeField] private Pathfinder pathfinder;

        [HideInInspector] [SerializeField] private TaxiwayInterceptor interceptor;

        [Obsolete] [HideInInspector] [SerializeField]
        private Spline.Direction taxiDirection;

        private TaxiInstruction _taxiInstruction;

        public void JoinTaxiway(SplineComputer taxiway, Spline.Direction direction)
        {
            interceptor.Join(taxiway, direction, taxiSpeed, turnRadius);
        }

        public void Taxi(string instruction)
        {
            if (_taxiInstruction == null)
            {
                _taxiInstruction = new TaxiInstruction(instruction);
            }
            else
            {
                _taxiInstruction.Amend(instruction);
            }

            pathfinder.Taxi(this, _taxiInstruction);
        }

        [Obsolete("Use Aircraft.Taxi() instead.")]
        public void StartTaxi(SplineComputer spline, Spline.Direction direction)
        {
            var pos = transform.position;
            var sample = spline.Project(pos);
            taxiDirection = direction;
            pathfinder.spline = spline;
            pathfinder.RebuildImmediate();
            pathfinder.SetPercent(sample.percent);
            pathfinder.direction = taxiDirection;
            pathfinder.wrapMode = SplineFollower.Wrap.Default;
            pathfinder.followMode = SplineFollower.FollowMode.Uniform;
            pathfinder.follow = true;
        }

        private void OnTaxiHold()
        {
            Debug.Log("Holding position");
        }
        
        private void OnTaxiwayEnter(Taxiway twy)
        {
            Debug.Log($"Entering taxiway {twy.identifier}");
        }
        
        private void OnIntercept(SplineComputer taxiway, Spline.Direction direction)
        {
            StartTaxi(taxiway, direction);
        }

        private void OnEnable()
        {
            tag = "Aircraft";

            pathfinder = gameObject.AddComponent<Pathfinder>();
            pathfinder.hideFlags = HideFlags.HideAndDontSave;
            pathfinder.follow = false;
            pathfinder.TaxiHold += OnTaxiHold;
            pathfinder.TaxiwayEnter += OnTaxiwayEnter;

            interceptor = gameObject.AddComponent<TaxiwayInterceptor>();
            interceptor.hideFlags = HideFlags.HideAndDontSave;
            interceptor.Intercept += OnIntercept;
        }
        
        private void OnDisable()
        {
            Destroy(pathfinder);
            Destroy(interceptor);
        }

        private void Update()
        {
            pathfinder.followSpeed = taxiSpeed;
        }
    }
}