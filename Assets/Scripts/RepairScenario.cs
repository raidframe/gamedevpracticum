using UnityEngine;

namespace ComputerRepairKiosk
{
    public sealed class RepairScenario : MonoBehaviour
    {
        [SerializeField] private PartSocket[] requiredSockets = new PartSocket[0];
        private PhysicalPart[] testedParts;
        private ComponentCondition[] testedConditions;
        private bool[] testedActive;
        public bool HasTestResult { get; private set; }
        public bool IsComplete { get; private set; }
        public string ResultMessage { get; private set; } = "";

        public void TestComputer()
        {
            IsComplete = false;
            HasTestResult = true;
            testedParts = new PhysicalPart[requiredSockets.Length];
            testedConditions = new ComponentCondition[requiredSockets.Length];
            testedActive = new bool[requiredSockets.Length];
            for (int i = 0; i < requiredSockets.Length; i++)
            {
                var part = requiredSockets[i] != null ? requiredSockets[i].Occupant : null;
                testedParts[i] = part;
                testedActive[i] = part != null && part.isActiveAndEnabled;
                var info = part != null ? part.GetComponent<ComponentInformation>() : null;
                testedConditions[i] = info != null ? info.Condition : ComponentCondition.Working;
            }
            if (requiredSockets.Length == 0)
            {
                ResultMessage = "Unable to run the test.";
                return;
            }
            for (int i = 0; i < requiredSockets.Length; i++)
            {
                if (requiredSockets[i] == null)
                {
                    ResultMessage = "Unable to run the test.";
                    return;
                }
                if (testedParts[i] == null || !testedParts[i].isActiveAndEnabled)
                {
                    ResultMessage = "Test failed: install " + requiredSockets[i].DisplayName.Replace(" socket", "") + ".";
                    return;
                }
                if (testedConditions[i] == ComponentCondition.Faulty)
                {
                    ResultMessage = "Test failed: " + testedParts[i].DisplayName + " is faulty.";
                    return;
                }
            }
            IsComplete = true;
            ResultMessage = "Repair complete";
        }

        private void LateUpdate()
        {
            if (!HasTestResult) return;
            for (int i = 0; i < requiredSockets.Length; i++)
            {
                var current = requiredSockets[i] != null ? requiredSockets[i].Occupant : null;
                var info = current != null ? current.GetComponent<ComponentInformation>() : null;
                var condition = info != null ? info.Condition : ComponentCondition.Working;
                bool active = current != null && current.isActiveAndEnabled;
                if (current == testedParts[i] && condition == testedConditions[i] && active == testedActive[i]) continue;
                HasTestResult = false;
                IsComplete = false;
                ResultMessage = "Assembly changed. Test the PC again.";
                return;
            }
        }
    }
}
