using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ComputerRepairKiosk
{
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PhysicsGrabber grabber;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2f;
        [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;
        private PhysicalPart targetPart;
        private PartSocket targetSocket;
        private GUIStyle promptStyle;
        private bool wasActive;
        private bool ControlsActive => Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

        private void LateUpdate()
        {
            targetPart = null;
            targetSocket = null;
            bool active = ControlsActive && viewCamera != null && grabber != null &&
                !(Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
            // First click after focus loss captures the mouse instead of throwing.
            bool acceptInput = active && wasActive;
            wasActive = active;
            if (!active) return;
            FindTarget();
            if (!acceptInput) return;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (grabber.HeldPart != null)
                {
                    if (targetSocket != null && targetSocket.Accepts(grabber.HeldPart))
                        grabber.TryInstall(targetSocket); // Too far: keep holding.
                    else grabber.Drop(false);
                }
                else if (targetPart != null) grabber.TryPickUp(targetPart);
            }
            else if (grabber.HeldPart != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                grabber.Drop(true);
        }

        private void FindTarget()
        {
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            var hits = Physics.RaycastAll(ray, interactionDistance, raycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform.root)) continue;
                var part = hit.collider.GetComponentInParent<PhysicalPart>();
                if (part != null && part == grabber.HeldPart) continue;
                var socket = hit.collider.GetComponentInParent<PartSocket>();
                if (hit.collider.isTrigger && socket == null) continue;
                if (socket != null)
                {
                    targetSocket = socket;
                    if (grabber.HeldPart == null && socket.Occupant != null && socket.Occupant.CanPickUp)
                        targetPart = socket.Occupant;
                }
                else if (part != null && part.CanPickUp) targetPart = part;
                break; // Nearest non-ignored hit blocks everything behind it.
            }
        }

        private string Prompt
        {
            get
            {
                if (grabber.HeldPart != null)
                {
                    if (targetSocket != null && targetSocket.Accepts(grabber.HeldPart))
                        return targetSocket.CanInstall(grabber.HeldPart) ? "E — Install | LMB — Throw" : "Move the part closer to the socket";
                    return "E — Drop | LMB — Throw";
                }
                if (targetPart != null)
                    return "E — " + (targetPart.Socket != null ? "Remove: " : "Pick up: ") + targetPart.DisplayName;
                return "";
            }
        }

        private void OnApplicationFocus(bool focused) { if (!focused) wasActive = false; }
        private void OnDisable() { wasActive = false; targetPart = null; targetSocket = null; }

        private void OnGUI()
        {
            if (!ControlsActive || grabber == null || viewCamera == null) return;
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(Screen.width / 2f - 3, Screen.height / 2f - 3, 6, 6), Texture2D.whiteTexture);
            GUI.color = targetPart != null || targetSocket != null ? new Color(.65f, 1, .75f) : Color.white;
            GUI.DrawTexture(new Rect(Screen.width / 2f - 1, Screen.height / 2f - 1, 2, 2), Texture2D.whiteTexture);
            GUI.color = previous;
            string prompt = Prompt;
            if (prompt.Length == 0) return;
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 18, wordWrap = true };
                promptStyle.normal.textColor = Color.white;
            }
            float width = Mathf.Min(490, Screen.width - 20);
            GUI.Box(new Rect((Screen.width - width) / 2, Screen.height / 2f + 38, width, 54), prompt, promptStyle);
        }
    }
}
