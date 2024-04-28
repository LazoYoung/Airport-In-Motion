using System;
using Dreamteck.Splines;
using UnityEngine;

namespace Layout
{
    public class Path : MonoBehaviour
    {
        [SerializeField]
        public string identifier;
        
        [HideInInspector] [SerializeField]
        public SplineComputer spline;
    }
}