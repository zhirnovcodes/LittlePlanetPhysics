using UnityEngine;
using UnityEngine.InputSystem;

public class CameraKeyboardController : MonoBehaviour
{
    private CameraZoomController Zoom;
    private CameraInclineController Incline;
    private CameraGlobalRotationController Rotation;

    public float DepthScrollMaxSpeed = 0.1f;
    public float RotationSpeedMin = 0.1f;
    public float RotationSpeedMax = 0.2f;
    public float DepthScrollSlide = 0.2f;
    public float RotationSlide = 0.2f;
    public float InclineStartHeight = 0.8f;

    private float CurrentDepthScrollSpeed;
    private Vector3 CurrentRotationSpeed;

    private void Start()
    {
        Zoom = GetComponent<CameraZoomController>();
        Incline = GetComponent<CameraInclineController>();
        Rotation = GetComponent<CameraGlobalRotationController>();
    }

    void FixedUpdate()
    {
        UpdateZoom();
        UpdateIncline();
        UpdateGlobalRotation();
    }

    private void UpdateZoom()
    {
        var scrollDelta = 0f;
        var kb = Keyboard.current;

        if (kb.numpadPlusKey.isPressed || kb.equalsKey.isPressed)
        {
            scrollDelta += 1;
        }
        if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed)
        {
            scrollDelta += -1;
        }

        var cameraPosition = Zoom.Position;

        CurrentDepthScrollSpeed = Mathf.Lerp(CurrentDepthScrollSpeed, scrollDelta * DepthScrollMaxSpeed * Time.fixedDeltaTime, DepthScrollSlide);

        Zoom.Position = Mathf.Clamp01(CurrentDepthScrollSpeed + cameraPosition);
    }

    private void UpdateIncline()
    {
        /*
        var position = Zoom.Position;
        var t = 0f;
        if (position >= InclineStartHeight)
        {
            t = Mathf.InverseLerp(InclineStartHeight, 1, position);
        }

        Incline.InclineX = t * t;
    */
    }

    private void UpdateGlobalRotation()
    {
        var drag = Vector3.zero;
        var kb = Keyboard.current;
        var shift = kb.rightShiftKey.isPressed || kb.leftShiftKey.isPressed;

        if (kb.upArrowKey.isPressed)
        {
            drag += Vector3.up;
        }
        if (kb.downArrowKey.isPressed)
        {
            drag += Vector3.down;
        }

        if (kb.leftArrowKey.isPressed)
        {
            drag += shift ? Vector3.forward : Vector3.left;
        }
        if (kb.rightArrowKey.isPressed)
        {
            drag += shift ? Vector3.back : Vector3.right;
        }

        if (drag.sqrMagnitude <= 0 &&
            CurrentRotationSpeed.sqrMagnitude <= 0)
        {
            return;
        }

        var rotationSpeed = Mathf.Lerp(RotationSpeedMin, RotationSpeedMax, 1 - Zoom.Position);
        var rotation = new Vector3(drag.y, -drag.x, drag.z) * rotationSpeed * Time.fixedDeltaTime;

        CurrentRotationSpeed = Vector3.Lerp(CurrentRotationSpeed, rotation, RotationSlide);

        Rotation.AddRotationNormalizedGlobal(CurrentRotationSpeed);
    }
}