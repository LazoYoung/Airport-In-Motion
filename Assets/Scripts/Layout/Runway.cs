using System.Collections.Generic;
using Dreamteck.Splines;
using JetBrains.Annotations;

namespace Layout
{
    public class Runway : Path
    {
        private static readonly Dictionary<string, Runway> Cached = new();

        [CanBeNull]
        public static Runway Get(string identifier)
        {
            Cached.TryGetValue(identifier, out var runway);
            return runway;
        }
        
        private void OnEnable()
        {
            tag = "Runway";
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
