using System.Collections.Generic;
using UnityEngine;

namespace ComputerRepairKiosk
{
    // Apply the selected setup before PartSocket.Start seats the initial parts.
    [DefaultExecutionOrder(-10)]
    public sealed class RepairScenario : MonoBehaviour
    {
        [SerializeField] private PartSocket[] requiredSockets = new PartSocket[0];
        [SerializeField, Min(0), Tooltip("Zero-based index in Scenarios. Changes take effect when Play Mode starts.")]
        private int scenarioIndex;
        [SerializeField] private RepairSetup[] scenarios = System.Array.Empty<RepairSetup>();
        private RepairSetup activeSetup;
        private readonly Dictionary<PartSocket, PhysicalPart> startingFaults = new();

        internal string ObjectiveTitle => activeSetup?.Name ?? "Repair the PC";
        internal string ObjectiveInstructions => activeSetup?.Instructions ?? "Check the PC and replace any missing or faulty parts.\nUse Test PC to verify the repair.";
        internal IReadOnlyList<RepairSetup.ObjectiveStep> ObjectiveSteps =>
            activeSetup?.ObjectiveSteps ?? System.Array.Empty<RepairSetup.ObjectiveStep>();

        internal bool IsObjectiveStepComplete(RepairSetup.ObjectiveStep step)
        {
            return step.CompletionCondition switch
            {
                RepairSetup.StepCondition.FaultyPartRemoved => step.Socket != null &&
                    startingFaults.TryGetValue(step.Socket, out var originalFaultyPart) && IsFaultCleared(originalFaultyPart),
                RepairSetup.StepCondition.WorkingPartInstalled => new SocketResult(step.Socket).IsWorking,
                RepairSetup.StepCondition.DiagnosticsPassed => IsComplete,
                _ => false
            };
        }

        private bool IsFaultCleared(PhysicalPart originalFaultyPart)
        {
            var socket = originalFaultyPart != null ? originalFaultyPart.Socket : null;
            if (socket == null || System.Array.IndexOf(requiredSockets, socket) < 0) return true;

            var current = new SocketResult(socket);
            return !current.Installed || current.Condition != ComponentCondition.Faulty;
        }

        private void Start()
        {
            foreach (var socket in requiredSockets)
                if (socket != null) socket.SetAssembly(requiredSockets);

            if (scenarios.Length == 0) return;
            int index = Mathf.Clamp(scenarioIndex, 0, scenarios.Length - 1);
            activeSetup = scenarios[index];
            activeSetup?.Apply(requiredSockets, startingFaults);
        }

        internal readonly struct SocketResult
        {
            public readonly PartSocket Socket;
            public readonly PhysicalPart Part;
            public readonly bool Installed;
            public readonly ComponentCondition Condition;
            public bool IsWorking => Installed && Condition == ComponentCondition.Working;

            public SocketResult(PartSocket socket)
            {
                Socket = socket;
                Part = socket != null ? socket.Occupant : null;
                Installed = Part != null && Part.isActiveAndEnabled;
                var information = Part != null ? Part.GetComponent<ComponentInformation>() : null;
                Condition = information != null ? information.Condition : ComponentCondition.Working;
            }
        }

        private SocketResult[] testedSockets = System.Array.Empty<SocketResult>();
        internal IReadOnlyList<SocketResult> TestedSockets => testedSockets;
        public bool HasTestResult { get; private set; }
        public bool IsComplete { get; private set; }
        public string ResultMessage { get; private set; } = "";

        public void TestComputer()
        {
            IsComplete = false;
            HasTestResult = true;
            testedSockets = new SocketResult[requiredSockets.Length];
            for (int i = 0; i < requiredSockets.Length; i++)
                testedSockets[i] = new SocketResult(requiredSockets[i]);
            if (requiredSockets.Length == 0)
            {
                ResultMessage = "Unable to run the test.";
                return;
            }
            foreach (var result in testedSockets)
            {
                if (result.Socket == null)
                {
                    ResultMessage = "Unable to run the test.";
                    return;
                }
                if (!result.Installed)
                {
                    ResultMessage = "Test failed: install " + result.Socket.DisplayName.Replace(" socket", "") + ".";
                    return;
                }
                if (!result.IsWorking)
                {
                    ResultMessage = "Test failed: " + result.Part.DisplayName + " is faulty.";
                    return;
                }
            }
            IsComplete = true;
            ResultMessage = "Repair Complete";
        }

        private void LateUpdate()
        {
            if (!HasTestResult) return;
            for (int i = 0; i < requiredSockets.Length; i++)
            {
                var current = new SocketResult(requiredSockets[i]);
                var tested = testedSockets[i];
                if (current.Socket == tested.Socket && current.Part == tested.Part &&
                    current.Condition == tested.Condition && current.Installed == tested.Installed) continue;
                HasTestResult = false;
                IsComplete = false;
                ResultMessage = "Assembly changed. Test the PC again.";
                return;
            }
        }
    }
}
