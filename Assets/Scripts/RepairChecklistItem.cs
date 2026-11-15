using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerRepairKiosk
{
    public sealed class RepairChecklistItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image statusIcon;
        [SerializeField] private Sprite pendingIcon;
        [SerializeField] private Sprite completeIcon;
        [SerializeField] private Color pendingColor = new Color(.67f, .67f, .67f);
        [SerializeField] private Color completeColor = new Color(0f, .83f, .49f);

        internal void SetCaption(string caption) => label.text = caption;

        internal void SetComplete(bool complete)
        {
            statusIcon.sprite = complete ? completeIcon : pendingIcon;
            statusIcon.color = complete ? completeColor : pendingColor;
        }
    }
}
