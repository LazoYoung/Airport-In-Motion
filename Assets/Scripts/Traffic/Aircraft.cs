﻿using System;
using Dreamteck.Splines;
using UnityEngine;
using UnityEngine.Serialization;

namespace Traffic
{
    public class Aircraft : MonoBehaviour
    {
        [SerializeField]
        public float taxiSpeed = 10f;
        
        [SerializeField]
        public float turnRadius = 30f;
        
        [FormerlySerializedAs("follower")] [HideInInspector] [SerializeField]
        private Pathfinder pathfinder;
        
        [HideInInspector] [SerializeField]
        private TaxiwayInterceptor interceptor;
        
        [Obsolete] [HideInInspector] [SerializeField]
        private Spline.Direction taxiDirection;

        public void JoinTaxiway(SplineComputer taxiway, Spline.Direction direction)
        {
            interceptor.Intercept(taxiway, direction, taxiSpeed, turnRadius);
        }

        public void Taxi(TaxiInstruction instruction)
        {
            pathfinder.Taxi(this, instruction);
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
        
            interceptor = gameObject.AddComponent<TaxiwayInterceptor>();
            interceptor.hideFlags = HideFlags.HideAndDontSave;
            interceptor.OnIntercept += OnIntercept;
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
