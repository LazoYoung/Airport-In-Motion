﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Layout;
using UnityEngine;
using static System.Text.RegularExpressions.RegexOptions;

namespace Traffic
{
    [Serializable]
    public class TaxiInstruction
    {
        public static readonly Regex tokenRegex = new("(via)|(short)|(cross)|(expect)", IgnoreCase);
        public static readonly Regex runwayRegex = new("(?<=^|\\s)(\\d{1,2}[LCR]?)(?=$|\\s)");

        public LinkedList<Taxiway> taxiways { get; private set; }
        public Queue<Runway> crossRunways { get; private set; }
        public Runway departRunway { get; private set; }
        public Path holdShort { get; private set; }
        public bool active { get; private set; }

        // Expected format: [(RWY) via] {(TWY...)} [cross (RWY...)] [short (TWY/RWY)]
        // Example 1: 1R via B M short 1L
        // Example 2: 28R via A F cross 1L 1R short 28L
        public void Init(string instruction)
        {
            TrimLeadingZeros(ref instruction);
            departRunway = GetDepartRunway(ref instruction);
            holdShort = GetHoldShort(ref instruction);
            crossRunways = GetCrossRunways(ref instruction);
            var taxiInstruction = instruction.Trim();
            taxiways = GetTaxiways(taxiInstruction);
            active = true;

            Debug.Log("Taxi"
                      + $"{(departRunway == null ? "" : $" to runway {departRunway.identifier}")}"
                      + $"{(taxiways.Count > 0 ? " via " + string.Join(", ", taxiways.Select(t => t.identifier)) : "")}"
                      + $"{(crossRunways.Count > 0 ? " cross " + string.Join(", ", crossRunways.Select(r => r.identifier)) : "")}"
                      + $"{(holdShort != null ? $" hold {holdShort.identifier}" : "")}"
            );
        }

        public void Amend(string instruction, [CanBeNull] Path presentPath)
        {
            TrimLeadingZeros(ref instruction);
            departRunway = GetDepartRunway(ref instruction) ?? departRunway;
            holdShort = GetHoldShort(ref instruction);
            crossRunways = GetCrossRunways(ref instruction);
            var newTaxiways = GetTaxiways(instruction.Trim());

            if (newTaxiways.Count > 0)
            {
                taxiways = newTaxiways;
            }
            else if (presentPath != null)
            {
                var node = taxiways.First;
                
                while (node != null)
                {
                    if (node.Value.Equals(presentPath))
                        break;

                    node = node.Next;
                    taxiways.RemoveFirst();
                }
            }

            active = true;

            Debug.Log("Amend taxi"
                      + $"{(departRunway == null ? "" : $" to runway {departRunway.identifier}")}"
                      + $"{(taxiways.Count > 0 ? " via " + string.Join(", ", taxiways.Select(t => t.identifier)) : "")}"
                      + $"{(crossRunways.Count > 0 ? " cross " + string.Join(", ", crossRunways.Select(r => r.identifier)) : "")}"
                      + $"{(holdShort != null ? $" hold {holdShort.identifier}" : "")}"
            );
        }

        public bool CanCross(Path path)
        {
            if (path is Runway runway)
            {
                return crossRunways.TryPeek(out Runway first) && runway.Equals(first);
            }
            else if (path is Taxiway taxiway)
            {
                return !taxiway.Equals(holdShort);
            }
            
            return false;
        }

        public void Cross(Path path)
        {
            if (path is Runway runway && crossRunways.TryPeek(out Runway first) && runway.Equals(first))
            {
                crossRunways.Dequeue();
            }
            else if (path is Taxiway taxiway && taxiway.Equals(holdShort))
            {
                holdShort = null;
            }
        }

        public void Reset()
        {
            active = false;
        }

        private LinkedList<Taxiway> GetTaxiways(string instruction)
        {
            var list = new LinkedList<Taxiway>();
            instruction = instruction.Trim();
            
            if (string.IsNullOrEmpty(instruction))
                return list;

            var regex = new Regex("(?:^|[\\s])([A-Z]+\\d*)");
                
            foreach (Match match in regex.Matches(instruction))
            {
                var identifier = match.Result("$1");

                if (Path.Find(identifier, out Taxiway taxiway))
                {
                    list.AddLast(taxiway);
                }
                else
                {
                    Debug.LogWarning($"Invalid taxiway: {identifier}");
                }
            }

            return list;
        }

        private Queue<Runway> GetCrossRunways(ref string instruction)
        {
            var regexCross = new Regex("(cross (?:\\d+[LCR]?\\s?)+\\s*)", IgnoreCase);
            var matchCross = regexCross.Match(instruction);
            var queue = new Queue<Runway>();

            if (!matchCross.Success)
            {
                return queue;
            }
            
            var regexRunways = new Regex("(\\d+[LCR]?)", IgnoreCase);

            foreach (Match match in regexRunways.Matches(matchCross.Result("$1")))
            {
                var identifier = match.Value;

                if (Path.Find(identifier, out Runway runway))
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
            var regexShort = new Regex("(?:short (\\w+)\\s*)", IgnoreCase);
            var matchShort = regexShort.Match(instruction);
            Path path = null;
            
            if (matchShort.Success)
            {
                var identifier = matchShort.Result("$1");
                instruction = regexShort.Replace(instruction, "");

                if (Path.Find(identifier, out Runway runway))
                {
                    path = runway;
                }
                else if (Path.Find(identifier, out Taxiway taxiway))
                {
                    path = taxiway;
                }
            }

            return path;
        }

        private Runway GetDepartRunway(ref string instruction)
        {
            var regexDepart = new Regex("(\\w+ via)|(expect \\w+)", IgnoreCase);
            var matchDepart = regexDepart.Match(instruction);

            if (!matchDepart.Success)
                return null;
            
            var runwayMatch = runwayRegex.Match(matchDepart.Result("$&"));

            if (!runwayMatch.Success)
                return null;
            
            var identifier = runwayMatch.Result("$1");
            instruction = regexDepart.Replace(instruction, "");

            return Path.Find(identifier, out Runway runway) ? runway : null;
        }

        private void TrimLeadingZeros(ref string text)
        {
            var regex = new Regex("(0\\d+)");
            text = regex.Replace(text, eval => eval.Value.Substring(1));
        }
    }
}
