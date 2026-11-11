using UnityEngine;

namespace ComputerRepairKiosk
{
    internal sealed class ComponentInspection
    {
        private const float RotationSensitivity = .35f;
        private readonly Rigidbody body;
        private readonly Camera view;
        private readonly Vector3 heldPosition;
        private readonly Quaternion heldRotation;
        private readonly Vector3 heldVelocity;
        private readonly Vector3 heldAngularVelocity;
        private readonly RigidbodyInterpolation interpolation;
        private readonly CollisionDetectionMode collisionMode;
        private readonly bool detectCollisions;
        private readonly Vector3 centerOffset;
        private readonly float radius;
        private Quaternion rotation;

        public ComponentInspection(PhysicalPart part, Camera view)
        {
            body = part.Body;
            this.view = view;
            heldPosition = part.transform.position;
            heldRotation = part.transform.rotation;
            heldVelocity = body.linearVelocity;
            heldAngularVelocity = body.angularVelocity;
            interpolation = body.interpolation;
            collisionMode = body.collisionDetectionMode;
            detectCollisions = body.detectCollisions;

            Bounds bounds = GetVisualBounds(part);
            centerOffset = Quaternion.Inverse(heldRotation) * (bounds.center - heldPosition);
            radius = bounds.extents.magnitude;
            rotation = heldRotation;

            body.detectCollisions = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            Rotate(Vector2.zero);
        }

        public void Rotate(Vector2 mouseDelta)
        {
            Quaternion yaw = Quaternion.AngleAxis(-mouseDelta.x * RotationSensitivity, view.transform.up);
            Quaternion pitch = Quaternion.AngleAxis(mouseDelta.y * RotationSensitivity, view.transform.right);
            rotation = (yaw * pitch * rotation).normalized;

            // Fit the rotating part at its original scale and leave room for the card below.
            float verticalTangent = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float fitAngle = Mathf.Atan(verticalTangent * Mathf.Min(.72f, view.aspect * .9f));
            float distance = Mathf.Max(.28f, Mathf.Max(radius / Mathf.Sin(fitAngle), radius + view.nearClipPlane + .03f));
            Vector3 center = view.transform.TransformPoint(new Vector3(0, .08f * distance * verticalTangent, distance));
            SetPose(center - rotation * centerOffset, rotation);
        }

        public void Restore()
        {
            SetPose(heldPosition, heldRotation);
            body.isKinematic = false;
            body.collisionDetectionMode = collisionMode;
            body.interpolation = interpolation;
            body.detectCollisions = detectCollisions;
            body.linearVelocity = heldVelocity;
            body.angularVelocity = heldAngularVelocity;
        }

        private void SetPose(Vector3 position, Quaternion orientation)
        {
            body.transform.SetPositionAndRotation(position, orientation);
            body.position = position;
            body.rotation = orientation;
        }

        private static Bounds GetVisualBounds(PhysicalPart part)
        {
            var bounds = new Bounds(part.transform.position, Vector3.zero);
            bool hasRenderer = false;
            foreach (var renderer in part.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.GetComponentInParent<PhysicalPart>() != part) continue;
                if (hasRenderer) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                hasRenderer = true;
            }
            return bounds;
        }
    }
}
