using UnityEngine;

namespace ComputerRepairKiosk
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicalPart : MonoBehaviour
    {
        [SerializeField] private string displayName = "Part";
        [SerializeField] private string compatibilityId = "part";
        private Rigidbody body;
        private Collider[] ownColliders;
        private Collider[] ignoredPlayerColliders;
        private Collider[] removalColliders;
        private ComponentInspection inspection;
        public string DisplayName => displayName;
        public string CompatibilityId => compatibilityId;
        public PartSocket Socket { get; private set; }
        public bool IsHeld { get; private set; }
        internal bool IsInspecting => inspection != null;
        public bool CanPickUp => isActiveAndEnabled && !IsHeld && (Socket == null || Socket.AllowRemoval);
        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();

        private void Awake()
        {
            ownColliders = GetOwnColliders();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = Body.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
        }

        public bool TryPickUp(Collider[] playerColliders)
        {
            if (!CanPickUp) return false;
            if (Socket != null)
            {
                removalColliders = Socket.RemovalColliders == null ? null : (Collider[])Socket.RemovalColliders.Clone();
                if (removalColliders != null)
                    foreach (var a in ownColliders)
                        foreach (var b in removalColliders)
                            if (a != null && b != null) Physics.IgnoreCollision(a, b, true);
                Socket.Detach(this);
            }
            transform.SetParent(null, true);
            Socket = null;
            IsHeld = true;
            Body.isKinematic = false;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.useGravity = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            RestorePlayerCollisions();
            ignoredPlayerColliders = playerColliders;
            SetPlayerCollisions(true);
            return true;
        }

        public void Release(Vector3 velocity)
        {
            if (!IsHeld || IsInspecting) return;
            IsHeld = false;
            Body.useGravity = true;
            Body.linearVelocity = velocity;
            // Restore player collisions after separation, avoiding a kick when dropped.
        }

        internal bool TryBeginInspection(Camera view)
        {
            if (!isActiveAndEnabled || !IsHeld || IsInspecting || view == null || Body.isKinematic) return false;
            inspection = new ComponentInspection(this, view);
            return true;
        }

        internal void RotateInspection(Vector2 mouseDelta) => inspection?.Rotate(mouseDelta);

        internal void EndInspection()
        {
            if (inspection == null) return;
            inspection.Restore();
            inspection = null;
        }

        internal void Seat(PartSocket socket, Transform anchor)
        {
            IsHeld = false;
            Socket = socket;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.isKinematic = true;
            Body.useGravity = false;
            transform.SetParent(anchor, true);
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            // Teleport the physics pose too, so interpolation cannot restore the
            // previous position after snapping (including the initial seating).
            Body.position = anchor.position;
            Body.rotation = anchor.rotation;
            RestorePlayerCollisions();
            RestoreRemovalCollisions(true);
        }

        private void RestoreRemovalCollisions(bool force = false)
        {
            if (removalColliders == null) return;
            // While extracting, wait until the part clears the assembly as a
            // whole. Restoring neighbours one at a time can trap a RAM stick
            // when the hand pulls it diagonally across the adjacent slots.
            if (IsHeld && !force)
                foreach (var b in removalColliders)
                    if (b != null)
                        foreach (var a in ownColliders)
                            if (a != null && a.bounds.Intersects(b.bounds)) return;
            for (int i = 0; i < removalColliders.Length; i++)
            {
                var b = removalColliders[i];
                if (b == null) continue;
                bool overlap = false;
                foreach (var a in ownColliders)
                    if (a != null && a.bounds.Intersects(b.bounds)) overlap = true;
                if (overlap && !force) continue;
                foreach (var a in ownColliders)
                    if (a != null) Physics.IgnoreCollision(a, b, false);
                removalColliders[i] = null;
            }
        }

        private void FixedUpdate()
        {
            if (IsInspecting) return;
            RestoreRemovalCollisions();
            if (IsHeld || ignoredPlayerColliders == null) return;
            foreach (var a in ownColliders)
                foreach (var b in ignoredPlayerColliders)
                    if (a != null && b != null && a.enabled && b.enabled &&
                        Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                            b, b.transform.position, b.transform.rotation, out _, out _)) return;
            RestorePlayerCollisions();
        }

        private void SetPlayerCollisions(bool ignore)
        {
            if (ownColliders == null) ownColliders = GetOwnColliders();
            if (ignoredPlayerColliders == null) return;
            foreach (var a in ownColliders)
                foreach (var b in ignoredPlayerColliders)
                    if (a != null && b != null) Physics.IgnoreCollision(a, b, ignore);
        }

        private void RestorePlayerCollisions()
        {
            SetPlayerCollisions(false);
            ignoredPlayerColliders = null;
        }

        private Collider[] GetOwnColliders()
        {
            // Mounted components have their own bodies; socket triggers are not
            // part of the physical surface we ignore while moving this part.
            return System.Array.FindAll(GetComponentsInChildren<Collider>(true),
                collider => !collider.isTrigger && collider.attachedRigidbody == Body);
        }

        private void OnDisable()
        {
            EndInspection();
            if (IsHeld) Release(Vector3.zero);
            RestorePlayerCollisions();
            RestoreRemovalCollisions(true);
        }
    }
}
