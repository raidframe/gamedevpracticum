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
        public Collider[] RemovalColliders => removalColliders;
        public bool RequirementMet => requiredSocket == null || requiredSocket.Occupant != null;
        internal PhysicalPart InitialPart => initialPart;
        internal void ClearInitialPart() => initialPart = null;

        private void Start()
        {
            if (initialPart != null && Accepts(initialPart))
            {
                Occupant = initialPart;
                initialPart.Seat(this, transform);
            }
        }

        public bool Accepts(PhysicalPart part) => isActiveAndEnabled && Occupant == null &&
            part != null && part.CompatibilityId == compatibilityId;

        public bool CanInstall(PhysicalPart part) => Accepts(part) && RequirementMet && part.IsHeld && !part.IsInspecting &&
            Vector3.Distance(part.transform.position, transform.position) <= installationDistance;

        public bool TryInstall(PhysicalPart part)
        {
            if (!CanInstall(part)) return false;
            Occupant = part;
            part.Seat(this, transform);
            return true;
        }

        internal void Detach(PhysicalPart part)
        {
            if (Occupant == part) Occupant = null;
        }
    }
}
