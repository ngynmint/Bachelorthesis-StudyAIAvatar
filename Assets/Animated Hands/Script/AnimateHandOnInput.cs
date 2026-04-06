using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class AnimateHandOninput : MonoBehaviour
{
    [SerializeField] private InputActionProperty triggerValue;
    [SerializeField] private InputActionProperty gripValue;

    [SerializeField] private Animator handAnimator;

    private static readonly int TriggerHash = Animator.StringToHash("Trigger");
    private static readonly int GripHash = Animator.StringToHash("Grip");

    private void Awake()
    {
        if (handAnimator == null)
            handAnimator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (triggerValue != null && triggerValue.action != null)
            triggerValue.action.Enable();

        if (gripValue != null && gripValue.action != null)
            gripValue.action.Enable();
    }

    private void OnDisable()
    {
        if (triggerValue != null && triggerValue.action != null)
            triggerValue.action.Disable();

        if (gripValue != null && gripValue.action != null)
            gripValue.action.Disable();
    }

    private void Update()
    {
        if (handAnimator == null)
            return;

        float trigger = 0f;
        float grip = 0f;

        if (triggerValue != null && triggerValue.action != null)
            trigger = triggerValue.action.ReadValue<float>();

        if (gripValue != null && gripValue.action != null)
            grip = gripValue.action.ReadValue<float>();

        handAnimator.SetFloat(TriggerHash, trigger);
        handAnimator.SetFloat(GripHash, grip);
    }
}