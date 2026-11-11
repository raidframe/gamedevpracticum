using UnityEngine;
using UnityEngine.InputSystem;

namespace ComputerRepairKiosk
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.1f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 80f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private PhysicsGrabber grabber;
        private InputAction moveAction;
        private InputAction lookAction;
        private float pitch;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            grabber = GetComponent<PhysicsGrabber>();
            if (cameraTransform == null)
            {
                Debug.LogError("Assign the player's camera to FirstPersonController.", this);
                enabled = false;
                return;
            }

            // Own these actions so enabling this player does not affect shared UI actions.
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            lookAction?.Enable();
            SetCursorCaptured(true);
        }

        private void Update()
        {
            // Inspection owns mouse movement and Escape until it returns the held part.
            if (grabber != null && grabber.IsInspecting) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);
            else if (Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorCaptured(true);

            Vector3 horizontalVelocity = Vector3.zero;
            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked)
            {
                // Mouse delta is already motion accumulated this frame: no deltaTime here.
                Vector2 look = lookAction.ReadValue<Vector2>() * mouseSensitivity;
                transform.Rotate(Vector3.up, look.x);
                pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

                Vector2 move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
                horizontalVelocity = (transform.right * move.x + transform.forward * move.y) * moveSpeed;
            }

            // A small downward speed keeps the capsule in contact with the floor.
            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * Time.deltaTime, -50f);
            controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                SetCursorCaptured(false);
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            lookAction?.Disable();
            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            lookAction?.Dispose();
        }

        internal static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
