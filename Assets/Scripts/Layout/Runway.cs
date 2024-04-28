using System.Collections.Generic;
using Dreamteck.Splines;

namespace Layout
{
    public class Runway : Path
    {
        private static readonly Dictionary<string, Runway> Cached = new();

        public static void Find(string identifier, out Runway runway)
        {
            Cached.TryGetValue(identifier, out runway);
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
