using UnityEngine;

public class CameraInclineController : MonoBehaviour
{
    [Range(0, 1)]
    public float InclineX = 0.5f;

    [Range(0, 1)]
    public float InclineY = 0.5f;

    public Transform CameraRotationHandle;
    public float MaxAngle = 70;

    public Vector2 Incline
    {
        get
        {
            return new Vector2(InclineX, InclineY);
        }
        set
        {
            InclineX = Mathf.Clamp01( value.x );
            InclineY = Mathf.Clamp01( value.y );
        }
    }

    public void ResetIncline()
    {
        Incline = Vector2.one * 0.5f;
    }

    private void Update()
    {
        var angleX = Mathf.Lerp(-MaxAngle, MaxAngle, InclineX);
        var angleY = Mathf.Lerp(-MaxAngle, MaxAngle, InclineY);
        CameraRotationHandle.localRotation = Quaternion.Euler(angleX, angleY, 0);
    }
}
