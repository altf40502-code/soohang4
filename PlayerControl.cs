using System.Data.SqlTypes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerControl : MonoBehaviour
{
    public float speed = 15;
    float hAxis;
    float vAxis;
    bool wDown;
    bool jDown;
    public UnityEngine.Camera followCamera;
    
    bool dDown;
    public bool isJump;
    public bool isDodge;
    public bool isReload; // 재장전 중 이동 잠금
    public ItemInteraction itemInteraction;

    Vector3 moveVec;
    Vector3 dodgeVec;
    Rigidbody rigid;
    Animator anim;
    UnityEngine.AI.NavMeshAgent navAgent;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rigid = GetComponent<Rigidbody>();
        navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (itemInteraction == null)
            itemInteraction = GetComponent<ItemInteraction>();
    }

    void Update()
    {
        GetInput();
        Move();
        Jump();
        Dodge();
    }
    void GetInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            hAxis = 0f;
            vAxis = 0f;
            wDown = false;
            jDown = false;
            dDown = false;
            return;
        }

        hAxis = 0f;
        vAxis = 0f;

        if (keyboard.aKey.isPressed)
            hAxis = -1f;
        else if (keyboard.dKey.isPressed)
            hAxis = 1f;

        if (keyboard.sKey.isPressed)
            vAxis = -1f;
        else if (keyboard.wKey.isPressed)
            vAxis = 1f;

        // 걷기: Left Shift를 누르면 천천히 이동
        wDown = keyboard.leftShiftKey.isPressed;

        // 점프
        jDown = keyboard.spaceKey.wasPressedThisFrame;

        // 회피
        dDown = keyboard.leftCtrlKey.wasPressedThisFrame;
    }

    void Move()
    {
        // 카메라(백뷰) 기준 이동 — WASD가 화면에서 보이는 방향과 일치하도록 변환
        UnityEngine.Camera cam = followCamera != null ? followCamera : UnityEngine.Camera.main;
        if (cam != null)
        {
            Vector3 camF = cam.transform.forward; camF.y = 0f; camF.Normalize();
            Vector3 camR = cam.transform.right;   camR.y = 0f; camR.Normalize();
            moveVec = (camF * vAxis + camR * hAxis).normalized;
        }
        else
        {
            moveVec = new Vector3(hAxis, 0f, vAxis).normalized;
        }

        if (isDodge)
            moveVec = dodgeVec;

        if (itemInteraction != null && itemInteraction.isSwap)
            moveVec = Vector3.zero;

        if (isReload)
            moveVec = Vector3.zero;

        transform.position += moveVec * speed * (wDown ? 0.3f : 1f) * Time.deltaTime;

        if (anim != null)
        {
            anim.SetBool("isRun", moveVec != Vector3.zero);
            anim.SetBool("isWalk", wDown);
        }
    }

    void Jump()
    {
        if (jDown && !isJump && !isDodge && !(itemInteraction?.isSwap ?? false))
        {
            if (navAgent != null && navAgent.enabled)
                navAgent.enabled = false;

            if (rigid != null)
                rigid.AddForce(Vector3.up * 15, ForceMode.Impulse);

            if (anim != null)
            {
                anim.SetBool("isJump", true);
                anim.SetTrigger("doJump");
            }

            isJump = true;
        }
    }

    void Dodge()
    {
        if (dDown 
            && moveVec != Vector3.zero 
            && !isJump 
            && !isDodge
            && !(itemInteraction?.isSwap ?? false))
        {
            dodgeVec = moveVec;
            speed *= 2;

            if (anim != null)
                anim.SetTrigger("doDodge");

            isDodge = true;

            Invoke("DodgeOut", 0.4f);
        }
    }

    void DodgeOut()
    {
        speed *= 0.5f;
        isDodge = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Floor")
        {
            if (anim != null)
                anim.SetBool("isJump", false);
            isJump = false;

            if (navAgent != null && !navAgent.enabled)
            {
                navAgent.enabled = true;
                if (navAgent.isOnNavMesh)
                    navAgent.Warp(transform.position);
            }
        }
    }

    // 외부에서 이동 방향 접근용 (playerBugFixed에서 사용)
    public Vector3 MoveVec => moveVec;
}
