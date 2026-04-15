using UnityEngine;

public class CameraFollowController : MonoBehaviour
{
    public CameraGlobalRotationController RotationController;
    public CameraInclineController InclineController;

    public float Slide = 0.6f;

    private Vector3 LastPositon;

    public void SetFollowingPosition(Vector3 position)
    {
        var newPosition = Vector3.Lerp(LastPositon, position, Slide);
        LastPositon = newPosition;
        RotationController.SetFollowingPosition(newPosition);
        InclineController.ResetIncline();
    }
}
