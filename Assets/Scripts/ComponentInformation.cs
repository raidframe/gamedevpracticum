using UnityEngine;

namespace ComputerRepairKiosk
{
    public enum ComponentCondition { Working, Faulty }

    [RequireComponent(typeof(PhysicalPart))]
    public sealed class ComponentInformation : MonoBehaviour
    {
        [SerializeField, TextArea(2, 4)] private string description = "Computer component.";
        [SerializeField] private ComponentCondition condition = ComponentCondition.Working;

        public string Description => description;
        public ComponentCondition Condition => condition;
        internal void SetCondition(ComponentCondition value) => condition = value;
    }
}
