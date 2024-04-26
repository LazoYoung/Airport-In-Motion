using System.Collections.Generic;
using Dreamteck.Splines;
using JetBrains.Annotations;

namespace Layout
{
    public class Runway : Path
    {
        private static Dictionary<string, Runway> _cached;

        [CanBeNull]
        public static Runway Get(string identifier)
        {
            return _cached[identifier];
        }
        
        private void OnEnable()
        {
            _cached.Add(identifier, this);
            tag = "Runway";
            spline = GetComponent<SplineComputer>();
            
            if (string.IsNullOrEmpty(identifier))
                identifier = name;
        }

        private void OnDisable()
        {
            _cached.Remove(identifier);
        }
    }
}
