using TMPro;
using UnityEngine;

namespace ComputerRepairKiosk
{
    [DefaultExecutionOrder(10)]
    public sealed class RepairObjectiveHud : MonoBehaviour
    {
        [SerializeField] private RepairScenario scenario;
        [SerializeField] private PlayerInteraction player;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private RectTransform checklist;
        [SerializeField] private RepairChecklistItem checklistItemPrefab;
        [SerializeField] private TMP_Text result;
        [SerializeField] private Color successColor = new Color(.6f, .92f, .7f);
        [SerializeField] private Color warningColor = new Color(1f, .78f, .6f);

        private (RepairChecklistItem Item, RepairSetup.ObjectiveStep Definition)[] steps;

        private void LateUpdate()
        {
            bool visible = scenario != null && scenario.isActiveAndEnabled && player != null && player.ShowRepairObjective;
            panel.SetActive(visible);
            if (!visible) return;

            steps ??= CreateChecklistItems();
            title.text = scenario.ObjectiveTitle;
            description.text = scenario.ObjectiveInstructions;
            description.gameObject.SetActive(!scenario.IsComplete && description.text.Length > 0);
            foreach (var step in steps)
                step.Item.SetComplete(scenario.IsObjectiveStepComplete(step.Definition));
            result.text = scenario.ResultMessage;
            result.color = scenario.IsComplete ? successColor : warningColor;
            result.gameObject.SetActive(result.text.Length > 0);
        }

        private void OnDisable()
        {
            if (panel != null) panel.SetActive(false);
        }

        private (RepairChecklistItem Item, RepairSetup.ObjectiveStep Definition)[] CreateChecklistItems()
        {
            var definitions = scenario.ObjectiveSteps;
            var items = new (RepairChecklistItem, RepairSetup.ObjectiveStep)[definitions.Count];
            for (int i = 0; i < items.Length; i++)
            {
                var step = definitions[i];
                var item = Instantiate(checklistItemPrefab, checklist);
                item.SetCaption(step.Description);
                items[i] = (item, step);
            }
            return items;
        }
    }
}
