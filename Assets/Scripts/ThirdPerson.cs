using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPerson : MonoBehaviour
{
    float yaw, pitch;
    private Transform target;
    public Vector3 cameraOffset;
    public Vector3 aimingOffset;
    public float followSpeed = 3f;

    // Update is called once per frame

    public void setTarget(Transform target)
    {
        this.target = target;
    }

    void LateUpdate()
    {
        transform.rotation = target.rotation;

        Vector3 newCameraPosition = target.position + transform.TransformDirection(cameraOffset);
        transform.position = Vector3.Lerp(transform.position, 
                                        newCameraPosition, 
                                        Mathf.Clamp01(Time.deltaTime * followSpeed));
    }
}
