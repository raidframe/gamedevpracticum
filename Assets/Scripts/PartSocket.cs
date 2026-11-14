using System.Collections.Generic;
using UnityEngine;

namespace ComputerRepairKiosk
{
    public sealed class PartSocket : MonoBehaviour
    {
        [SerializeField] private string displayName = "Socket";
        [SerializeField] private string compatibilityId = "part";
        [SerializeField] private PhysicalPart initialPart;
        [SerializeField, Min(0.05f)] private float installationDistance = 0.5f;
        [SerializeField] private bool allowRemoval = true;
        [SerializeField] private Collider[] removalColliders;
        [SerializeField] private PartSocket requiredSocket;
        [SerializeField] private PartSocket[] removalBlockers = new PartSocket[0];
        [SerializeField] private PartSocket[] carriedSockets = new PartSocket[0];
        [SerializeField] private bool ignoreInstalledPartsOnRemoval = true;
        private IReadOnlyList<PartSocket> assemblySockets;
        public PhysicalPart Occupant { get; private set; }
        public bool AllowRemoval => allowRemoval && RemovalBlocker == null;
        public PartSocket RemovalBlocker
        {
            get
            {
                foreach (var socket in removalBlockers)
                    if (socket != null && socket.Occupant != null) return socket;
                return null;
            }
        }
        public string DisplayName => displayName;
        public bool RequirementMet => requiredSocket == null || requiredSocket.Occupant != null;
        internal PhysicalPart InitialPart => initialPart;
        internal void ClearInitialPart() => initialPart = null;
        internal void SetAssembly(IReadOnlyList<PartSocket> sockets) => assemblySockets = sockets;

        internal Collider[] GetRemovalColliders()
        {
            var colliders = new List<Collider>();
            if (removalColliders != null) colliders.AddRange(removalColliders);
            if (ignoreInstalledPartsOnRemoval && assemblySockets != null)
            {
                foreach (var socket in assemblySockets)
                {
                    if (socket == null || socket == this || socket.Occupant == null) continue;
                    colliders.AddRange(socket.Occupant.SolidColliders);
                }
            }
            return colliders.ToArray();
        }

        private void Start()
        {
            if (initialPart != null && Accepts(initialPart))
            {
                Install(initialPart);
            }
            else UpdateCarriedSockets();
        }

        public bool Accepts(PhysicalPart part) => isActiveAndEnabled && Occupant == null &&
            part != null && part.CompatibilityId == compatibilityId;

        public bool CanInstall(PhysicalPart part) => Accepts(part) && RequirementMet && part.IsHeld && !part.IsInspecting &&
            Vector3.Distance(part.transform.position, transform.position) <= installationDistance;

        public bool TryInstall(PhysicalPart part)
        {
            if (!CanInstall(part)) return false;
            Install(part);
            return true;
        }

        internal void Detach(PhysicalPart part)
        {
            if (Occupant != part) return;
            Occupant = null;
            UpdateCarriedSockets();
        }

        private void Install(PhysicalPart part)
        {
            Occupant = part;
            part.Seat(this, transform);
            UpdateCarriedSockets();
        }

        private void UpdateCarriedSockets()
        {
            // Keep the authored local pose relative to the installed board or its mount.
            foreach (var socket in carriedSockets)
            {
                if (socket == null) continue;
                socket.transform.SetParent(Occupant != null ? Occupant.transform : transform, false);
                socket.gameObject.SetActive(Occupant != null);
            }
        }
    }
}
