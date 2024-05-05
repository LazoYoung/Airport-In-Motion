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

        public bool Equals(Path other)
        {
            return other != null && base.GetType() == other.GetType() && identifier == other.identifier;
        }
    }
}