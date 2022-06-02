using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fighter : MonoBehaviour
{
    public Transform camera;
    public Rigidbody rigidbody;
    protected Animator animator;
    protected AnimatorStateInfo stateInfo;
    protected CapsuleCollider capsule;

    public Vector3 moveDir;

    public float moveSpeed = 1f;

    public float jumpPower = 10f;
    public float jumpUpPower = 2f;
    protected float timeSinceJump = 10f;
    public float rotSpeed = 3f;
    protected float groundedThreshold = 0.15f;
    protected bool grounded = true;

    protected void GetCommonComponents()
    {
        rigidbody = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        capsule = GetComponent<CapsuleCollider>();
    }
    
    protected void FighterUpdate()
    {
        stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        CheckGrounded();
        SetAnimatorMoveParams();
        ApplyRootRotation();
    }

    private void ApplyRootRotation()
    {
        if(moveDir.magnitude > 10e-3)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, 
                                                targetRotation, 
                                                Time.deltaTime * rotSpeed);
        }
    }
    
    private void CheckGrounded()
    {
        Ray ray = new Ray();
        ray.direction = Vector3.down;
        Vector3 rayOrigin = transform.position + groundedThreshold * Vector3.up;
        grounded = false;

        for (float xOffset = -1f; xOffset <= 1f; xOffset += 1f)
        {
            for (float zOffset = -1f; zOffset <= 1f; zOffset += 1f)
            {
                Vector3 offset = new Vector3(xOffset, 0f, zOffset).normalized * capsule.radius;
                ray.origin = rayOrigin + offset;
                if (Physics.Raycast(ray, 2f * groundedThreshold))
                {
                    grounded = true;
                }
            }
        }
        animator.SetBool("Grounded", grounded);
    }
    
    private void OnAnimatorMove()
    {
        ApplyRootMotion();
    }

    private void ApplyRootMotion()
    {
        float velocityY = rigidbody.velocity.y;
        if(!animator.GetBool("Grounded") || timeSinceJump < 0.25f)
        {
            float jumpDirControlBlendF = Mathf.Pow(Mathf.Clamp01(timeSinceJump), 4f);
            rigidbody.velocity = Vector3.Lerp(moveDir * moveSpeed, rigidbody.velocity, jumpDirControlBlendF);
        }
        else
            rigidbody.velocity = animator.deltaPosition / Time.deltaTime;
        rigidbody.velocity = new Vector3(rigidbody.velocity.x, velocityY, rigidbody.velocity.z);
    }
    
    private void SetAnimatorMoveParams()
    {
        var characterSpaceMoveDir = transform.InverseTransformDirection(moveDir);
        animator.SetFloat("Forward", characterSpaceMoveDir.z, 0.2f, Time.deltaTime);
        animator.SetFloat("Right", characterSpaceMoveDir.x, 0.2f, Time.deltaTime);
    }
}
