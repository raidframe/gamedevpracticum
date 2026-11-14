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

        public string Name => name;
        public string Instructions => instructions;

        public void Apply(IReadOnlyList<PartSocket> requiredSockets)
        {
            var pending = new HashSet<PartSocket>(requiredSockets);
            foreach (var issue in issues)
            {
                if (issue.Socket == null || !pending.Remove(issue.Socket))
                {
                    Debug.LogWarning($"Repair setup '{name}' has an unassigned, duplicate or non-required socket.");
                    continue;
                }
                issue.Apply();
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

            public void Apply()
            {
                var part = socket.InitialPart;
                if (part == null) return;

                if (state == StartingState.Missing)
                {
                    if (workbenchSpot == null)
                    {
                        Debug.LogWarning("Assign a workbench spot for the missing component.", socket);
                        return;
                    }
                    socket.ClearInitialPart();
                    part.PlaceLooseAtStart(workbenchSpot);
                    return;
                }

                if (!part.TryGetComponent<ComponentInformation>(out var information))
                {
                    Debug.LogWarning("A faulty component needs Component Information.", part);
                    return;
                }

                information.SetCondition(ComponentCondition.Faulty);
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
