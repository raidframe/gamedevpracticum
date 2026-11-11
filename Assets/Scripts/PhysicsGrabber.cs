using UnityEngine;

namespace ComputerRepairKiosk
{
    public sealed class PhysicsGrabber : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField] private Collider[] playerColliders;
        [SerializeField] private Vector3 holdOffset = new Vector3(0.22f, -0.18f, 0.8f);
        [SerializeField, Min(1)] private float positionGain = 12f;
        [SerializeField, Min(1)] private float maxSpeed = 4f;
        [SerializeField, Min(1)] private float acceleration = 45f;
        [SerializeField, Min(0)] private float throwSpeed = 5f;
        [SerializeField, Min(0.5f)] private float breakDistance = 1.5f;
        private Quaternion heldRotation;
        public PhysicalPart HeldPart { get; private set; }
        internal bool IsInspecting => HeldPart != null && HeldPart.IsInspecting;

        public bool TryPickUp(PhysicalPart part)
        {
            if (view == null || HeldPart != null || part == null || !part.TryPickUp(playerColliders)) return false;
            HeldPart = part;
            heldRotation = Quaternion.Inverse(view.rotation) * part.transform.rotation;
            return true;
        }

        public void Drop(bool throwForward)
        {
            if (HeldPart == null || IsInspecting) return;
            HeldPart.Release(throwForward && view != null ? view.forward * throwSpeed : Vector3.zero);
            HeldPart = null;
        }

        public bool TryInstall(PartSocket socket)
        {
            if (HeldPart == null || socket == null || !socket.TryInstall(HeldPart)) return false;
            HeldPart = null;
            return true;
        }

        private void FixedUpdate()
        {
            if (HeldPart == null) return;
            if (!HeldPart.isActiveAndEnabled || !HeldPart.IsHeld || view == null)
            {
                HeldPart.EndInspection();
                Drop(false);
                return;
            }
            if (IsInspecting) return;
            Vector3 target = view.TransformPoint(holdOffset);
            Vector3 error = target - HeldPart.Body.worldCenterOfMass;
            if (error.magnitude > breakDistance)
            {
                Drop(false);
                return;
            }
            // Bounded acceleration lets the physics solver resolve contacts.
            Vector3 desired = Vector3.ClampMagnitude(error * positionGain, maxSpeed);
            Vector3 correction = (desired - HeldPart.Body.linearVelocity) / Time.fixedDeltaTime;
            HeldPart.Body.AddForce(Vector3.ClampMagnitude(correction, acceleration), ForceMode.Acceleration);
            Quaternion delta = view.rotation * heldRotation * Quaternion.Inverse(HeldPart.Body.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            Vector3 desiredSpin = angle == 0 ? Vector3.zero : axis.normalized * (angle * Mathf.Deg2Rad * 8f);
            HeldPart.Body.angularVelocity = Vector3.MoveTowards(HeldPart.Body.angularVelocity,
                Vector3.ClampMagnitude(desiredSpin, 8f), 30f * Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            if (HeldPart != null) HeldPart.EndInspection();
            Drop(false);
        }
    }
}
