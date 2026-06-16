using UnityEngine;

public class Tracking : MonoBehaviour
{
    public Transform target; 

    public float followSpeed = 5f;
    public float rotateSpeed = 8f;

    public float followDistance = 3f; 
    public float height = 2f;        
    public float sideOffset = 1.5f;   

    public float hoverPower = 0.3f;
    public float hoverSpeed = 3f;

    Vector3 velocity;

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                target = player.transform;
        }
    }

    void Update()
    {
        if (target == null)
            return;

        FollowTarget();
        LookAtTarget();
    }

    void FollowTarget()
    {
        Vector3 targetPosition =
            target.position
            - target.forward * followDistance
            + target.right * sideOffset
            + Vector3.up * height;

        targetPosition.y += Mathf.Sin(Time.time * hoverSpeed) * hoverPower;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            1f / followSpeed
        );
    }

    void LookAtTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }
}