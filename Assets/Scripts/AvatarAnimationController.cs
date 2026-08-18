using UnityEngine;

public class AvatarAnimationController : MonoBehaviour
{
    public Animator animator;
    public AudioSource avatarAudioSource;
    [SerializeField] private float minInterval = 5f;
    [SerializeField] private float maxInterval = 10f;
    private bool isTalking = false;
    private string lastLoggedState = "";
    private float eyeDirectionTimer;
    private float thinkingStartTime = -1f;
    private static readonly string[] eyeDirectionTriggers = { "PlayLeft", "PlayRight", "PlayDown" };
    
    void Start()
    {
        WhenNextEyeDirection();
    }
    void Update()
    {
        bool audioPlaying = false;
        
        if (avatarAudioSource != null)
        {
            audioPlaying = avatarAudioSource.isPlaying;
        }
        
        if (audioPlaying && !isTalking)
        {
            StartTalking();
        }
        else if (!audioPlaying && isTalking)
        {
            StopTalking();
        }

        eyeDirectionTimer -= Time.deltaTime;
        if (eyeDirectionTimer <= 0f)
        {
            string chosen = eyeDirectionTriggers[Random.Range(0, eyeDirectionTriggers.Length)];
            animator.SetTrigger(chosen);
            Debug.Log($"[EyeDirection] Playing: {chosen}");
            WhenNextEyeDirection();
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(1);
        string currentState = "pending";

        if (stateInfo.IsName("Idle")) {
            currentState = "IDLE";
        }
        else if (stateInfo.IsName("Talking1")){
            currentState = "TALKING 1";
        }
        else if (stateInfo.IsName("Talking2")){
            currentState = "TALKING 2";
        }

        if (currentState != lastLoggedState)
        {
            Debug.Log($"[Anim] Switched to: {currentState}");
            lastLoggedState = currentState;
        }
    }

    private void WhenNextEyeDirection()
    {
        eyeDirectionTimer = Random.Range(minInterval, maxInterval);
    }
    
    private void StartTalking()
    {
        isTalking = true;

        bool useOtherAnimation = false;
        int r = Random.Range(0, 2);
        if (r == 1)
        {
            useOtherAnimation = true;
        }
        
        animator.SetBool("UseOtherAnimation", useOtherAnimation);
        animator.SetBool("IsTalking", true);
        Debug.Log("[Anim] StartTalking → UseOtherAnimation=" + useOtherAnimation);
    }

    private void StopTalking()
    {
        isTalking = false;
        animator.SetBool("IsTalking", false);
        animator.SetBool("UseOtherAnimation", false);
        Debug.Log("[Anim] StopTalking");
    }
    public void SetThinking(bool thinking)
    {
        if (thinking)
        {
            animator.SetTrigger("StartThinking");
            Debug.Log("[Anim] START thinking");
        }
        else
        {
            animator.SetTrigger("StopThinking");
            thinkingStartTime = -1f;
            Debug.Log("[Anim] STOP thinking");
        }
    }
}