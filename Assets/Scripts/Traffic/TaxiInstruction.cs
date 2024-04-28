using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Layout;
using UnityEngine;
using static System.Text.RegularExpressions.RegexOptions;

namespace Traffic
{
    public class TaxiInstruction
    {
        public readonly Queue<Taxiway> Taxiways;
        public readonly Queue<Runway> CrossRunways;
        public readonly Runway DepartRunway;
        public readonly Path HoldShort;

        // Expected format: [(RWY) via] {(TWY...)} [cross (RWY...)] [short (TWY/RWY)]
        // Example 1: 1R via B M short 1L
        // Example 2: 28R via A F cross 1L 1R short 28L
        public TaxiInstruction(string instruction)
        {
            TrimLeadingZeros(ref instruction);
            Taxiways = new Queue<Taxiway>();
            DepartRunway = GetDepartRunway(ref instruction);
            HoldShort = GetHoldShort(ref instruction);
            CrossRunways = GetCrossRunways(ref instruction);

            if (instruction.Length > 0)
                Debug.Log("Taxi via " + instruction);
            
            if (DepartRunway != null)
                Debug.Log("Depart runway: " + DepartRunway.identifier);
            
            if (CrossRunways.Count > 0)
                Debug.Log("Cross runway: " + string.Join(", ", CrossRunways.Select(r => r.identifier)));
            
            if (HoldShort != null)
                Debug.Log("Hold short: " + HoldShort.identifier);
            
            foreach (string identifier in instruction.Split(' '))
            {
                Taxiway.Find(identifier, out var taxiway);

                if (taxiway != null)
                {
                    Taxiways.Enqueue(taxiway);
                }
                else
                {
                    Debug.LogWarning($"Invalid taxiway: {identifier}");
                }
            }
        }

        private Queue<Runway> GetCrossRunways(ref string instruction)
        {
            var regexCross = new Regex("(?: cross (?:\\d+[LCR]?\\s?)+)", IgnoreCase);
            var matchCross = regexCross.Match(instruction);
            var queue = new Queue<Runway>();

            if (!matchCross.Success)
            {
                return queue;
            }
            
            var regexRunways = new Regex("(\\d+[LCR]?)", IgnoreCase);

            foreach (Match match in regexRunways.Matches(matchCross.Value))
            {
                string identifier = match.Value;
                Runway.Find(identifier, out var runway);

                if (runway != null)
                {
                    queue.Enqueue(runway);
                }
            }

            instruction = regexCross.Replace(instruction, "");
            return queue;
        }

        [CanBeNull]
        private Path GetHoldShort(ref string instruction)
        {
            var regexShort = new Regex("(?: short (\\w+))", IgnoreCase);
            var matchShort = regexShort.Match(instruction);
            Path holdShort = null;
            
            if (matchShort.Success)
            {
                string identifier = matchShort.Result("$1");
                instruction = regexShort.Replace(instruction, "");
                Runway.Find(identifier, out var runway);
                Taxiway.Find(identifier, out var taxiway);

                if (runway != null)
                {
                    holdShort = runway;
                }
                else if (taxiway != null)
                {
                    holdShort = taxiway;
                }
            }

            return holdShort;
        }

        [CanBeNull]
        private Runway GetDepartRunway(ref string instruction)
        {
            var regexDepart = new Regex("(?:(\\w+) via )", IgnoreCase);
            var matchDepart = regexDepart.Match(instruction);
            Runway runway = null;

            if (matchDepart.Success)
            {
                string identifier = matchDepart.Result("$1");
                Runway.Find(identifier, out runway);
                instruction = regexDepart.Replace(instruction, "");
            }

            return runway;
        }

        private void TrimLeadingZeros(ref string text)
        {
            var regex = new Regex("(0\\d+)");
            text = regex.Replace(text, eval => eval.Value.Substring(1));
        }
    }
}
