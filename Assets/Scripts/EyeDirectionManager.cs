using UnityEngine;

public class EyeDirectionManager : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float minInterval = 5f;
    [SerializeField] private float maxInterval = 10f;

    private float timer;
    private static readonly string[] triggers = { "PlayLeft", "PlayRight", "PlayDown" };

    void Start()
    {
        ScheduleNext();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            string chosen = triggers[Random.Range(0, triggers.Length)];
            animator.SetTrigger(chosen);
            ScheduleNext();
        }
    }

    void ScheduleNext()
    {
        timer = Random.Range(minInterval, maxInterval);
    }
}