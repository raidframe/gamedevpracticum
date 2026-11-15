using System;
using System.Collections.Generic;
using UnityEngine;

namespace ComputerRepairKiosk
{
    [Serializable]
    internal sealed class RepairSetup
    {
        [SerializeField] private string name = "New repair";
        [SerializeField, TextArea] private string instructions = "";
        [SerializeField] private StartingIssue[] issues = Array.Empty<StartingIssue>();
        [SerializeField] private ObjectiveStep[] objectiveSteps = Array.Empty<ObjectiveStep>();

        public string Name => name;
        public string Instructions => instructions;
        public IReadOnlyList<ObjectiveStep> ObjectiveSteps => objectiveSteps;

        internal enum StepCondition
        {
            FaultyPartRemoved = 0,
            WorkingPartInstalled = 1,
            DiagnosticsPassed = 2
        }

        [Serializable]
        internal struct ObjectiveStep
        {
            [SerializeField, TextArea(1, 3)] private string description;
            [SerializeField] private StepCondition completionCondition;
            [SerializeField, Tooltip("Choose the relevant socket for a component step. Leave this empty for Diagnostics Passed.")]
            private PartSocket socket;

            public string Description => description;
            public StepCondition CompletionCondition => completionCondition;
            public PartSocket Socket => socket;
        }

        public void Apply(IReadOnlyList<PartSocket> requiredSockets, Dictionary<PartSocket, PhysicalPart> startingFaults)
        {
            var pending = new HashSet<PartSocket>(requiredSockets);
            foreach (var issue in issues)
            {
                if (issue.Socket == null || !pending.Remove(issue.Socket))
                {
                    Debug.LogWarning($"Repair setup '{name}' has an unassigned, duplicate or non-required socket.");
                    continue;
                }
                issue.Apply(startingFaults);
            }
        }

        private enum StartingState { Faulty, Missing }

        [Serializable]
        private struct StartingIssue
        {
            [SerializeField] private PartSocket socket;
            [SerializeField] private StartingState state;
            [SerializeField, Tooltip("For Missing, place the original component here.")]
            private Transform workbenchSpot;
            [SerializeField, Tooltip("For Faulty, optionally enable a compatible, inactive scene spare.")]
            private PhysicalPart spare;

            public PartSocket Socket => socket;

            public void Apply(Dictionary<PartSocket, PhysicalPart> startingFaults)
            {
                var part = socket.InitialPart;

                if (state == StartingState.Missing)
                {
                    if (part != null)
                    {
                        if (workbenchSpot == null)
                        {
                            Debug.LogWarning("Assign a workbench spot for the missing component.", socket);
                            return;
                        }
                        socket.ClearInitialPart();
                        part.PlaceLooseAtStart(workbenchSpot);
                    }
                    return;
                }

                if (part == null) return;
                if (!part.TryGetComponent<ComponentInformation>(out var information))
                {
                    Debug.LogWarning("A faulty component needs Component Information.", part);
                    return;
                }

                information.SetCondition(ComponentCondition.Faulty);
                startingFaults.Add(socket, part);
                if (spare == null) return;
                if (spare == part || spare.gameObject.activeInHierarchy || spare.CompatibilityId != part.CompatibilityId ||
                    !spare.TryGetComponent<ComponentInformation>(out var spareInformation))
                {
                    Debug.LogWarning("Assign an inactive compatible spare with Component Information.", socket);
                    return;
                }

                spareInformation.SetCondition(ComponentCondition.Working);
                spare.gameObject.SetActive(true);
            }
        }
    }
}
