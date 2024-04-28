using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Layout;
using UnityEngine;
using static System.Text.RegularExpressions.RegexOptions;

namespace Traffic
{
    public class TaxiInstruction
    {
        public readonly Taxiway[] Taxiways;
        public readonly Runway[] CrossRunways;
        public readonly Runway DepartRunway;
        public readonly Path HoldShort;

        // Expected format: [(RWY) via] {(TWY...)} [short (TWY/RWY)] [cross (RWY)]
        // Example 1: 1R via B M short 1L
        // Example 2: 28R via A F cross 1L 1R short 28L
        public TaxiInstruction(string instruction)
        {
            var taxiwayList = new List<Taxiway>();
            
            TrimLeadingZeros(ref instruction);
            DepartRunway = GetDepartRunway(ref instruction);
            HoldShort = GetHoldShort(ref instruction);
            CrossRunways = GetCrossRunways(ref instruction);
            
            Debug.Log("Taxi via: " + instruction);
            
            foreach (string identifier in instruction.Split(' '))
            {
                Taxiway.Find(identifier, out var taxiway);

                if (taxiway == null)
                {
                    taxiwayList.Clear();
                    break;
                }

                taxiwayList.Add(taxiway);
            }

            Taxiways = taxiwayList.ToArray();
        }

        private Runway[] GetCrossRunways(ref string instruction)
        {
            var regexCross = new Regex("(?: cross (?:\\d+[LCR]?\\s?)+)", IgnoreCase);
            var matchCross = regexCross.Match(instruction);

            if (!matchCross.Success)
            {
                return Array.Empty<Runway>();
            }
            
            var regexRunways = new Regex("(\\d+[LCR]?)", IgnoreCase);
            var crossRunways = new List<Runway>();

            foreach (Match match in regexRunways.Matches(matchCross.Value))
            {
                string identifier = match.Value;
                Runway.Find(identifier, out var runway);

                if (runway != null)
                {
                    crossRunways.Add(runway);
                    Debug.Log("Cross runway: " + identifier);
                }
            }

            instruction = regexCross.Replace(instruction, "");
            return crossRunways.ToArray();
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
                
                Debug.Log("Hold short: " + identifier);
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

                Debug.Log("Depart runway: " + identifier);
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
