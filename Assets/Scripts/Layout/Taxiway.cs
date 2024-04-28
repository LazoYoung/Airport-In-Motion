using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

namespace Layout
{
    [RequireComponent(typeof(SplineComputer))]
    public class Taxiway : Path
    {
        private static readonly Dictionary<string, Taxiway> Cached = new();
        
        public static void Find(string identifier, out Taxiway taxiway)
        {
            Cached.TryGetValue(identifier, out taxiway);
        }
        
        private void OnEnable()
        {
            tag = "Taxiway";
            spline = GetComponent<SplineComputer>();
            
            if (string.IsNullOrEmpty(identifier))
                identifier = name;
            
            Cached.Add(identifier, this);
        }

        private void OnDisable()
        {
            Cached.Remove(identifier);
        }
    }
}
