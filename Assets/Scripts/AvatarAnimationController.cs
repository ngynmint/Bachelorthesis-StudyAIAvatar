using UnityEngine;

public class AvatarAnimationController : MonoBehaviour
{
    [Header("Components")]
    public Animator animator;
    public AudioSource avatarAudioSource;

    private bool isTalking = false;
    private string lastLoggedState = "";

    void Update()
    {
        bool audioPlaying = avatarAudioSource != null && avatarAudioSource.isPlaying;

        if (audioPlaying && !isTalking)
            StartTalking();
        else if (!audioPlaying && isTalking)
            StopTalking();

        // Log only when state changes (not every frame)
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        string currentState = "UNKNOWN";
        if (stateInfo.IsName("Idle")) currentState = "IDLE";
        else if (stateInfo.IsName("Talking1")) currentState = "TALKING 1";
        else if (stateInfo.IsName("Talking2")) currentState = "TALKING 2";

        if (currentState != lastLoggedState)
        {
            Debug.Log($"[Anim] Switched to: {currentState}");
            lastLoggedState = currentState;
        }
    }

    private void StartTalking()
    {
        isTalking = true;
        bool useTalking2 = Random.value > 0.5f;
        animator.SetBool("UseTalking2", useTalking2);
        animator.SetBool("IsTalking", true);
        Debug.Log($"[Anim] StartTalking → UseTalking2={useTalking2}");
    }

    private void StopTalking()
    {
        isTalking = false;
        animator.SetBool("IsTalking", false);
        Debug.Log("[Anim] StopTalking");
    }
}