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
        private GUIStyle nameStyle;
        private GUIStyle descriptionStyle;
        private GUIStyle conditionStyle;
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
                // Empty mount volumes must not hide components deeper in the case.
                // Select installed parts through their solid collider; only target
                // a socket trigger when carrying a compatible replacement.
                if (hit.collider.isTrigger && (socket == null || grabber.HeldPart == null ||
                    !socket.Accepts(grabber.HeldPart))) continue;
                if (socket != null)
                {
                    targetSocket = socket;
                    if (grabber.HeldPart == null && socket.Occupant != null && socket.Occupant.isActiveAndEnabled)
                        targetPart = socket.Occupant;
                }
                else if (part != null && part.isActiveAndEnabled) targetPart = part;
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
                    {
                        if (!targetSocket.RequirementMet) return "Install the required component first";
                        return targetSocket.CanInstall(grabber.HeldPart) ? "E — Install | LMB — Throw" : "Move the part closer to the socket";
                    }
                    return "E — Drop | LMB — Throw";
                }
                if (targetPart != null)
                {
                    if (!targetPart.CanPickUp)
                        return targetPart.Socket != null && targetPart.Socket.RemovalBlocker != null
                            ? "Remove installed components first" : "This part cannot be removed";
                    return "E — " + (targetPart.Socket != null ? "Remove: " : "Pick up: ") + targetPart.DisplayName;
                }
                return "";
            }
        }

        private void OnApplicationFocus(bool focused) { if (!focused) wasActive = false; }
        private void OnDisable() { wasActive = false; targetPart = null; targetSocket = null; }

        private PhysicalPart InspectedPart => grabber.HeldPart != null ? grabber.HeldPart : targetPart;

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
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
                promptStyle.normal.textColor = Color.white;
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
                nameStyle.normal.textColor = Color.white;
                descriptionStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                descriptionStyle.normal.textColor = new Color(.82f, .85f, .88f);
                conditionStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleRight };
            }
            var part = InspectedPart;
            var information = part != null ? part.GetComponent<ComponentInformation>() : null;
            if (prompt.Length == 0 && information == null) return;

            float width = Mathf.Min(490, Screen.width - 24);
            float innerWidth = width - 28;
            float promptHeight = promptStyle.CalcHeight(new GUIContent(prompt), innerWidth);
            float nameHeight = information != null ? Mathf.Max(28, nameStyle.CalcHeight(new GUIContent(part.DisplayName), innerWidth - 100)) : 0;
            float descriptionHeight = information != null ? descriptionStyle.CalcHeight(new GUIContent(information.Description), innerWidth) : 0;
            float height = 24 + promptHeight + (information != null ? nameHeight + descriptionHeight + 18 : 0);
            Rect panel = new Rect((Screen.width - width) / 2, Screen.height - height - 24, width, height);
            GUI.color = new Color(.045f, .055f, .065f, .95f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
            float y = panel.y + 12;
            if (information != null)
            {
                GUI.Label(new Rect(panel.x + 14, y, innerWidth - 100, nameHeight), part.DisplayName, nameStyle);
                bool working = information.Condition == ComponentCondition.Working;
                conditionStyle.normal.textColor = working ? new Color(.6f, .92f, .7f) : new Color(1, .58f, .48f);
                GUI.Label(new Rect(panel.xMax - 110, y, 96, 28), working ? "Working" : "Faulty", conditionStyle);
                y += nameHeight + 4;
                GUI.Label(new Rect(panel.x + 14, y, innerWidth, descriptionHeight), information.Description, descriptionStyle);
                y += descriptionHeight + 14;
            }
            GUI.Label(new Rect(panel.x + 14, y, innerWidth, promptHeight), prompt, promptStyle);
        }
    }
}
