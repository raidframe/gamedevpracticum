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
        [SerializeField] private RepairScenario repairScenario;
        private PhysicalPart targetPart;
        private PartSocket targetSocket;
        private RepairScenario targetTestStation;
        private PcDiagnosticPanel diagnosticPanel;
        private Collider targetCollider;
        private GUIStyle promptStyle;
        private GUIStyle nameStyle;
        private GUIStyle descriptionStyle;
        private GUIStyle conditionStyle;
        private bool wasActive;
        private static readonly Color ReadyColor = new Color(.66f, .9f, .77f);
        private static readonly Color BlockedColor = new Color(.98f, .75f, .48f);
        private bool ControlsActive => Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        internal bool DiagnosticsOpen => diagnosticPanel != null && diagnosticPanel.IsOpen;

        private void LateUpdate()
        {
            ClearTarget();
            var keyboard = Keyboard.current;
            bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (DiagnosticsOpen)
            {
                if (escapePressed) CloseDiagnostics();
                else diagnosticPanel.RefreshSummary();
                return;
            }

            bool inspectPressed = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            if (grabber != null && grabber.IsInspecting)
            {
                if (inspectPressed || escapePressed || !ControlsActive || viewCamera == null)
                {
                    EndInspection();
                    if (escapePressed && Application.isFocused) FirstPersonController.SetCursorCaptured(true);
                }
                else if (Mouse.current != null)
                    grabber.HeldPart.RotateInspection(Mouse.current.delta.ReadValue());
                return;
            }

            bool active = ControlsActive && viewCamera != null && grabber != null && !escapePressed;
            // First click after focus loss captures the mouse instead of throwing.
            bool acceptInput = active && wasActive;
            wasActive = active;
            if (!active) return;
            if (acceptInput && inspectPressed && grabber.HeldPart != null &&
                grabber.HeldPart.TryBeginInspection(viewCamera)) return;

            FindTarget();
            if (!acceptInput) return;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (grabber.HeldPart != null)
                {
                    if (targetSocket != null && targetSocket.Accepts(grabber.HeldPart))
                    {
                        if (grabber.TryInstall(targetSocket)) ClearTarget();
                    }
                    else grabber.Drop(false);
                }
                else if (targetTestStation != null) OpenDiagnostics(targetTestStation);
                else if (targetPart != null && grabber.TryPickUp(targetPart)) ClearTarget();
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
                var testStation = hit.collider.GetComponentInParent<RepairScenario>();
                // Empty mount volumes must not hide components deeper in the case.
                // Select installed parts through their solid collider; only target
                // a socket trigger when carrying a compatible replacement.
                if (hit.collider.isTrigger && (socket == null || grabber.HeldPart == null ||
                    !socket.Accepts(grabber.HeldPart))) continue;
                targetCollider = hit.collider;
                if (testStation != null && testStation.isActiveAndEnabled)
                    targetTestStation = testStation;
                else if (socket != null)
                {
                    targetSocket = socket;
                    if (grabber.HeldPart == null && socket.Occupant != null && socket.Occupant.isActiveAndEnabled)
                        targetPart = socket.Occupant;
                }
                else if (part != null && part.isActiveAndEnabled) targetPart = part;
                break; // Nearest non-ignored hit blocks everything behind it.
            }
        }

        private (string Action, string Hint, Color Accent, bool Highlight) GetFeedback()
        {
            const string heldActions = "[LMB] Throw   [R] Inspect";
            const string dropAction = "[E] Drop   " + heldActions;
            var heldPart = grabber.HeldPart;
            if (heldPart != null)
            {
                if (grabber.IsInspecting) return ("[Mouse] Rotate   [R / Esc] Exit", "", Color.white, false);
                if (targetSocket != null && targetSocket.Accepts(heldPart))
                {
                    if (targetSocket.CanInstall(heldPart))
                        return ("[E] Install   " + heldActions, "", ReadyColor, true);
                    string reason = targetSocket.RequirementMet ? "Move the part closer" : "Install the required component first";
                    return (heldActions, reason, BlockedColor, false);
                }

                var occupant = targetSocket != null ? targetSocket.Occupant : null;
                if (occupant != null && occupant.CompatibilityId == heldPart.CompatibilityId)
                    return (dropAction, "Socket occupied", BlockedColor, false);
                return (dropAction, "", Color.white, false);
            }

            if (targetTestStation != null) return ("[E] Test PC", "", ReadyColor, false);
            if (targetPart != null)
            {
                if (!targetPart.CanPickUp)
                {
                    var blocker = targetPart.Socket != null ? targetPart.Socket.RemovalBlocker : null;
                    string reason = blocker != null ? "Remove " + blocker.Occupant.DisplayName + " first" : "Cannot remove this part";
                    return ("", reason, BlockedColor, false);
                }
                return (targetPart.Socket != null ? "[E] Remove" : "[E] Pick Up", "", ReadyColor, true);
            }
            return ("", "", Color.white, false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            wasActive = false;
            EndInspection();
        }

        private void OnDisable()
        {
            wasActive = false;
            ClearTarget();
            EndInspection();
            if (DiagnosticsOpen) CloseDiagnostics();
        }

        private void OnDestroy() => diagnosticPanel?.Destroy();

        private void OpenDiagnostics(RepairScenario station)
        {
            station.TestComputer();
            diagnosticPanel ??= new PcDiagnosticPanel(CloseDiagnostics);
            diagnosticPanel.Show(station);
            wasActive = false;
            FirstPersonController.SetCursorCaptured(false);
        }

        private void CloseDiagnostics()
        {
            diagnosticPanel.Hide();
            wasActive = false;
            if (isActiveAndEnabled && Application.isFocused) FirstPersonController.SetCursorCaptured(true);
        }

        private void EndInspection()
        {
            if (grabber != null && grabber.IsInspecting) grabber.HeldPart.EndInspection();
        }

        private void ClearTarget()
        {
            targetPart = null;
            targetSocket = null;
            targetTestStation = null;
            targetCollider = null;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || DiagnosticsOpen || !ControlsActive || grabber == null || viewCamera == null) return;
            var feedback = GetFeedback();
            Color previous = GUI.color;
            if (!grabber.IsInspecting)
            {
                if (feedback.Highlight && targetCollider != null)
                    DrawTargetBrackets(targetCollider, feedback.Accent);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(Screen.width / 2f - 3, Screen.height / 2f - 3, 6, 6), Texture2D.whiteTexture);
                GUI.color = feedback.Accent;
                GUI.DrawTexture(new Rect(Screen.width / 2f - 1, Screen.height / 2f - 1, 2, 2), Texture2D.whiteTexture);
                GUI.color = previous;
            }
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
            if (!grabber.IsInspecting) DrawRepairObjective();
            var part = grabber.HeldPart != null ? grabber.HeldPart : targetPart;
            var information = part != null ? part.GetComponent<ComponentInformation>() : null;
            if (feedback.Action.Length == 0 && feedback.Hint.Length == 0 && part == null) return;

            float width = Mathf.Min(information != null ? 420 : 320, Screen.width - 24);
            float innerWidth = width - 28;
            float promptHeight = feedback.Action.Length > 0 ? promptStyle.CalcHeight(new GUIContent(feedback.Action), innerWidth) : 0;
            float hintHeight = feedback.Hint.Length > 0 ? promptStyle.CalcHeight(new GUIContent(feedback.Hint), innerWidth) : 0;
            float nameWidth = information != null ? innerWidth - 100 : innerWidth;
            float nameHeight = part != null ? Mathf.Max(28, nameStyle.CalcHeight(new GUIContent(part.DisplayName), nameWidth)) : 0;
            float descriptionHeight = information != null ? descriptionStyle.CalcHeight(new GUIContent(information.Description), innerWidth) : 0;
            float height = 20 + nameHeight + descriptionHeight + promptHeight + hintHeight;
            Rect panel = new Rect((Screen.width - width) / 2, Screen.height - height - 24, width, height);
            GUI.color = new Color(.045f, .055f, .065f, .95f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
            float y = panel.y + 10;
            if (part != null)
                GUI.Label(new Rect(panel.x + 14, y, nameWidth, nameHeight), part.DisplayName, nameStyle);
            if (information != null)
            {
                bool working = information.Condition == ComponentCondition.Working;
                conditionStyle.normal.textColor = working ? new Color(.6f, .92f, .7f) : new Color(1, .58f, .48f);
                GUI.Label(new Rect(panel.xMax - 110, y, 96, 28), working ? "Working" : "Faulty", conditionStyle);
                GUI.Label(new Rect(panel.x + 14, y + nameHeight, innerWidth, descriptionHeight), information.Description, descriptionStyle);
            }
            y += nameHeight + descriptionHeight;
            if (promptHeight > 0)
                GUI.Label(new Rect(panel.x + 14, y, innerWidth, promptHeight), feedback.Action, promptStyle);
            if (hintHeight > 0)
            {
                GUI.color = BlockedColor;
                GUI.Label(new Rect(panel.x + 14, y + promptHeight, innerWidth, hintHeight), feedback.Hint, promptStyle);
                GUI.color = previous;
            }
        }

        private void DrawTargetBrackets(Collider collider, Color color)
        {
            var box = collider as BoxCollider;
            Bounds bounds = box != null ? new Bounds(box.center, box.size) : collider.bounds;
            Vector3 localMin = bounds.min;
            Vector3 localMax = bounds.max;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? localMin.x : localMax.x,
                    (corner & 2) == 0 ? localMin.y : localMax.y,
                    (corner & 4) == 0 ? localMin.z : localMax.z);
                Vector3 screen = viewCamera.WorldToScreenPoint(box != null ? box.transform.TransformPoint(point) : point);
                if (screen.z <= viewCamera.nearClipPlane)
                {
                    // Keep a small focus marker if the part crosses the near plane.
                    min = new Vector2(Screen.width, Screen.height) * .5f - Vector2.one * 9f;
                    max = min + Vector2.one * 18f;
                    break;
                }
                var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
                min = Vector2.Min(min, guiPoint);
                max = Vector2.Max(max, guiPoint);
            }

            // Keep the corners legible on thin parts, such as RAM seen edge-on.
            Vector2 center = (min + max) * .5f;
            Vector2 halfSize = Vector2.Max((max - min) * .5f + Vector2.one * 5f, Vector2.one * 11f);
            float left = Mathf.Round(Mathf.Max(8, center.x - halfSize.x));
            float right = Mathf.Round(Mathf.Min(Screen.width - 8, center.x + halfSize.x));
            float top = Mathf.Round(Mathf.Max(8, center.y - halfSize.y));
            float bottom = Mathf.Round(Mathf.Min(Screen.height - 8, center.y + halfSize.y));
            float length = Mathf.Round(Mathf.Clamp(Mathf.Min(right - left, bottom - top) * .22f, 4, 11));
            if (right - left < 2 * length || bottom - top < 2 * length) return;

            GUI.color = new Color(color.r, color.g, color.b, .85f);
            DrawCorner(left, top, length, length);
            DrawCorner(right, top, -length, length);
            DrawCorner(left, bottom, length, -length);
            DrawCorner(right, bottom, -length, -length);
        }

        private static void DrawCorner(float x, float y, float horizontal, float vertical)
        {
            const float thickness = 2;
            GUI.DrawTexture(new Rect(x + Mathf.Min(0, horizontal), y, Mathf.Abs(horizontal) + thickness, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + Mathf.Min(0, vertical), thickness, Mathf.Abs(vertical) + thickness), Texture2D.whiteTexture);
        }

        private void DrawRepairObjective()
        {
            if (repairScenario == null || !repairScenario.isActiveAndEnabled) return;
            float width = Mathf.Min(360, Screen.width - 24);
            float innerWidth = width - 28;
            string title = repairScenario.IsComplete ? "Repair complete" : "Job: Replace faulty RAM";
            string instructions = repairScenario.IsComplete ? "The PC passed all checks." :
                "Use the spare RAM on the workbench.\nReassemble the PC and use Test PC.";
            float titleHeight = nameStyle.CalcHeight(new GUIContent(title), innerWidth);
            float instructionsHeight = descriptionStyle.CalcHeight(new GUIContent(instructions), innerWidth);
            string result = repairScenario.IsComplete ? "" : repairScenario.ResultMessage;
            float resultHeight = result.Length > 0 ? descriptionStyle.CalcHeight(new GUIContent(result), innerWidth) + 12 : 0;
            Rect panel = new Rect(12, 12, width, 28 + titleHeight + instructionsHeight + resultHeight);
            Color previous = GUI.color;
            GUI.color = new Color(.045f, .055f, .065f, .95f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(26, 24, innerWidth, titleHeight), title, nameStyle);
            float y = 28 + titleHeight;
            GUI.Label(new Rect(26, y, innerWidth, instructionsHeight), instructions, descriptionStyle);
            if (resultHeight == 0) return;
            GUI.color = repairScenario.IsComplete ? new Color(.6f, .92f, .7f) : new Color(1, .78f, .6f);
            GUI.Label(new Rect(26, y + instructionsHeight + 12, innerWidth, resultHeight), result, descriptionStyle);
            GUI.color = previous;
        }
    }
}
