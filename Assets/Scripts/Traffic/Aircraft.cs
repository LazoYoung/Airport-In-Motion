using System;
using Layout;
using UnityEngine;
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
        private PathFollower pathFollower;

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

            if (pathFollower.CreateTaxiPath(this, taxiInstruction))
            {
                move = true;
            }
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
            pathFollower = gameObject.AddComponent<PathFollower>();
            pathFollower.hideFlags = HideFlags.HideAndDontSave;
            pathFollower.follow = false;
            pathFollower.EnterPath += OnEnterPath;
            pathFollower.CrossPath += OnCrossPath;
            pathFollower.onEndReached += OnEndReached;
        }

        private void OnDisable()
        {
            pathFollower.EnterPath -= OnEnterPath;
            pathFollower.CrossPath -= OnCrossPath;
            pathFollower.onEndReached -= OnEndReached;
            Destroy(pathFollower);
        }

        private void FixedUpdate()
        {
            UpdateSpeed();
            pathFollower.follow = move;
            pathFollower.followSpeed = _speed;
            pathFollower.followMode = Uniform;
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
