using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraGlobalRotationController : MonoBehaviour
{
    [Range(0, 1)]
    public float RotationX;
    [Range(0, 1)]
    public float RotationY;
    [Range(0, 1)]
    public float RotationZ;

    public Transform CameraHandle;

    public Quaternion Quaternion
    {
        get
        {
            return transform.localRotation;
        }
        set
        {
            transform.localRotation = value;
        }
    }

    public Vector3 Rotation
    {
        get
        {
            return new Vector3(RotationX, RotationY, RotationZ);
        }
        set
        {
            RotationX = value.x % 1;
            RotationY = value.y % 1;
            RotationZ = value.z % 1;

            RotationX = RotationX < 0 ? 1 + RotationX : RotationX;
            RotationY = RotationY < 0 ? 1 + RotationY : RotationY;
            RotationZ = RotationZ < 0 ? 1 + RotationZ : RotationZ;
        }
    }

    public void AddRotationNormalizedGlobal(Vector3 euler)
    {
        CameraHandle.transform.localRotation = CameraHandle.transform.localRotation * Quaternion.Euler(euler * 360f);

        Rotation = CameraHandle.transform.localRotation.eulerAngles;
    }

    public void SetFollowingPosition(Vector3 position)
    {
        // Calculate the orbital rotation
        Quaternion targetRotation = Quaternion.LookRotation(-position);
        
        // Apply the rotation to the camera handle
        CameraHandle.transform.localRotation = targetRotation;
    }
}
