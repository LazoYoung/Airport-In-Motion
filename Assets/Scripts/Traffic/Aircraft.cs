using System;
using Dreamteck.Splines;
using Layout;
using UnityEngine;
using UnityEngine.Serialization;
using static Dreamteck.Splines.SplineFollower.FollowMode;
using Time = UnityEngine.Time;

namespace Traffic
{
    public class Aircraft : MonoBehaviour
    {
        [Range(0, 30)] [SerializeField]
        private float taxiSpeed = 15f;

        [Range(0, 50)] [SerializeField]
        private float turnRadius = 30f;

        [Range(0, 100)] [SerializeField]
        private float length = 50f;

        [Range(0, 1)] [SerializeField]
        private float accelFactor = 0.3f;

        [Range(0, 1)] [SerializeField]
        private float decelFactor = 0.6f;

        [SerializeField]
        private bool move;
        
        [HideInInspector] [SerializeField]
        private Pathfinder pathfinder;

        [HideInInspector] [SerializeField]
        private TaxiInstruction taxiInstruction;

        [HideInInspector] [SerializeField]
        private Path presentPath;

        private float _speed;

        public float GetTurnRadius()
        {
            return turnRadius;
        }
        
        public float GetLength()
        {
            return length;
        }
        
        public void Taxi(string instruction)
        {
            if (!taxiInstruction.active)
            {
                taxiInstruction.Init(instruction);
            }
            else
            {
                taxiInstruction.Amend(instruction, presentPath);
            }

            pathfinder.CreateTaxiPath(this, taxiInstruction);
            move = true;
        }

        private void OnEnterPath(Path path)
        {
            Debug.Log($"Path enter: {path.identifier}");
            presentPath = path;
        }

        private void OnCrossPath(Path path)
        {
            Debug.Log($"Path cross: {path.identifier}");

            if (taxiInstruction.CanCross(path))
            {
                taxiInstruction.Cross(path);
            }
            else
            {
                move = false;
            }
        }
        
        private void OnEndReached(double d)
        {
            move = false;
        }

        private void OnEnable()
        {
            tag = "Aircraft";
            taxiInstruction = new TaxiInstruction();
            pathfinder = gameObject.AddComponent<Pathfinder>();
            pathfinder.hideFlags = HideFlags.HideAndDontSave;
            pathfinder.follow = false;
            pathfinder.EnterPath += OnEnterPath;
            pathfinder.CrossPath += OnCrossPath;
            pathfinder.onEndReached += OnEndReached;
        }

        private void OnDisable()
        {
            pathfinder.EnterPath -= OnEnterPath;
            pathfinder.CrossPath -= OnCrossPath;
            pathfinder.onEndReached -= OnEndReached;
            Destroy(pathfinder);
        }

        private void FixedUpdate()
        {
            UpdateSpeed();
            pathfinder.follow = move;
            pathfinder.followSpeed = _speed;
            pathfinder.followMode = Uniform;
        }

        private void UpdateSpeed()
        {
            if (move)
            {
                _speed += accelFactor * taxiSpeed * Time.fixedDeltaTime;
            }
            else
            {
                _speed -= decelFactor * taxiSpeed * Time.fixedDeltaTime;
            }

            _speed = Math.Clamp(_speed, 0, taxiSpeed);
        }
    }
}
