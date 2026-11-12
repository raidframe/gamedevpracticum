using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ComputerRepairKiosk
{
    internal sealed class PcDiagnosticPanel
    {
        private static readonly Color Surface = new Color(.08f, .09f, .11f);
        private static readonly Color Working = new Color(.66f, .9f, .77f);
        private static readonly Color Warning = new Color(1f, .77f, .55f);
        private readonly GameObject root;
        private readonly Font font;
        private readonly Text summary;
        private readonly ScrollRect scroll;
        private readonly RectTransform rows;
        private RepairScenario scenario;

        public bool IsOpen => root != null && root.activeSelf;

        public PcDiagnosticPanel(Action close)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = new GameObject("PC Diagnostic Results", typeof(RectTransform));
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.AddComponent<GraphicRaycaster>();

            var shade = CreateRect("Backdrop", root.transform);
            Stretch(shade);
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .65f);

            var panel = CreateRect("Panel", shade);
            panel.anchorMin = new Vector2(.5f, .07f);
            panel.anchorMax = new Vector2(.5f, .93f);
            panel.sizeDelta = new Vector2(660, 0);
            panel.gameObject.AddComponent<Image>().color = Surface;
            var layout = AddVerticalLayout(panel, 8);
            layout.padding = new RectOffset(22, 22, 20, 20);

            AddLabel("Title", panel, "PC Diagnostic Results", 26, 36).fontStyle = FontStyle.Bold;
            summary = AddLabel("Summary", panel, "", 18, 34);
            AddLabel("List heading", panel, "Required components", 16, 24).fontStyle = FontStyle.Bold;

            var list = CreateRect("Component list", panel);
            list.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            var viewport = CreateRect("Viewport", list);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;
            rows = CreateRect("Rows", viewport);
            rows.anchorMin = new Vector2(0, 1);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(.5f, 1);
            rows.sizeDelta = Vector2.zero;
            AddVerticalLayout(rows, 2);
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rows;

            var button = CreateRect("Close", panel);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            var image = button.gameObject.AddComponent<Image>();
            image.color = new Color(.21f, .24f, .29f);
            var closeButton = button.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = image;
            closeButton.onClick.AddListener(() => close());
            var buttonLabel = AddLabel("Label", button, "Close (Esc)", 17);
            Stretch(buttonLabel.rectTransform);
            buttonLabel.alignment = TextAnchor.MiddleCenter;
        }

        public void Show(RepairScenario result)
        {
            scenario = result;
            RefreshSummary();
            foreach (Transform row in rows)
            {
                row.gameObject.SetActive(false);
                Object.Destroy(row.gameObject);
            }

            foreach (var slot in result.TestedSockets)
            {
                var row = CreateRect("Component result", rows);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
                row.gameObject.AddComponent<Image>().color = new Color(.13f, .15f, .18f);

                string name = slot.Socket != null ? slot.Socket.DisplayName.Replace(" socket", "") : "Required slot";
                var label = AddLabel("Component", row, name, 16);
                Stretch(label.rectTransform, 0, .34f, 8);
                bool faulty = slot.Condition == ComponentCondition.Faulty;
                string status;
                if (slot.Socket == null) status = "Error — slot not configured";
                else if (!slot.Installed) status = "Missing — install a compatible component";
                else if (faulty) status = "Installed / Faulty — replace " + slot.Part.DisplayName;
                else status = "Installed / Working";
                var statusLabel = AddLabel("Status", row, status, 16);
                Stretch(statusLabel.rectTransform, .34f, 1, 8);
                statusLabel.color = slot.Installed && !faulty ? Working : Warning;
            }

            root.SetActive(true);
            if (EventSystem.current == null)
            {
                var events = new GameObject("Diagnostic UI Input");
                events.transform.SetParent(root.transform, false);
                events.AddComponent<EventSystem>();
                events.AddComponent<InputSystemUIInputModule>();
            }
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        public void RefreshSummary()
        {
            if (scenario == null) return;
            summary.text = scenario.IsComplete ? "Repair Complete" : scenario.ResultMessage;
            summary.color = scenario.IsComplete ? Working : Warning;
        }

        public void Hide()
        {
            root.SetActive(false);
            scenario = null;
        }

        public void Destroy() => Object.Destroy(root);

        private Text AddLabel(string name, Transform parent, string value, int size, float height = 0)
        {
            var rect = CreateRect(name, parent);
            if (height > 0) rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.text = value;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        private static VerticalLayoutGroup AddVerticalLayout(RectTransform rect, float spacing)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float left = 0, float right = 1, float inset = 0)
        {
            rect.anchorMin = new Vector2(left, 0);
            rect.anchorMax = new Vector2(right, 1);
            rect.offsetMin = new Vector2(inset, 0);
            rect.offsetMax = new Vector2(-inset, 0);
        }
    }
}
