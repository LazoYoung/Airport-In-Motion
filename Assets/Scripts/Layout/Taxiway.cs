using System.Collections.Generic;
using Dreamteck.Splines;
using JetBrains.Annotations;
using UnityEngine;

namespace Layout
{
    [RequireComponent(typeof(SplineComputer))]
    public class Taxiway : Path
    {
        private static readonly Dictionary<string, Taxiway> Cached = new();
        
        [CanBeNull]
        public static Taxiway Get(string identifier)
        {
            Cached.TryGetValue(identifier, out var taxiway);
            return taxiway;
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
