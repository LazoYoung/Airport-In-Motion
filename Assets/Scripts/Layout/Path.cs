using System.Collections.Generic;
using Dreamteck.Splines;
using Traffic;
using UnityEngine;

namespace Layout
{
    public class Path : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("(Optional) Unique path identifier")]
        public string identifier;
        
        [HideInInspector] [SerializeField]
        public SplineComputer spline;

        private static readonly Dictionary<string, Path> PathCache = new();

        public static bool Find<T>(string identifier, out T result)
        {
            if (PathCache.TryGetValue(identifier, out var path))
            {
                if (path is T value)
                {
                    result = value;
                    return true;
                }
            }

            result = default;
            return false;
        }
        
        public bool Equals(Path other)
        {
            return other != null && GetType() == other.GetType() && identifier == other.identifier;
        }

        public float GetSafetyMargin(Aircraft aircraft)
        {
            return (GetWidth() + aircraft.length) / 2f;
        }

        protected virtual float GetWidth()
        {
            return 50f;
        }
        
        protected virtual void OnEnable()
        {
            if (string.IsNullOrEmpty(identifier))
            {
                identifier = name;
            }
            
            if (PathCache.ContainsKey(identifier))
            {
                Debug.LogError($"Duplicate path identifier: {identifier}");
                enabled = false;
                return;
            }
            
            spline = GetComponent<SplineComputer>();
            spline.name = identifier;
            PathCache.Add(identifier, this);
        }

        protected virtual void OnDisable()
        {
            PathCache.Remove(identifier);
        }
    }
}
