using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMouseController : MonoBehaviour
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
    public float InclineGlide = 0.3f;
    public float InclineMaxDeviationScreen = 0.2f;

    private Vector2 ScreenGrabPoint;
    private float CurrentDepthScrollSpeed;
    private Vector3 CurrentRotationSpeed;
    private Quaternion StartRotation;

    private Vector2 InclineGrabPoint;
    private Vector2 InclineStart;

    private void Start()
    {
        Zoom = GetComponent<CameraZoomController>();
        Incline = GetComponent<CameraInclineController>();
        Rotation = GetComponent<CameraGlobalRotationController>();
    }

    void Update()
    {
        UpdateZoom();
        UpdateIncline();
        UpdateGlobalRotation();
    }

    private void UpdateZoom()
    {
        // New Input System scroll is in pixels (~120 per notch); normalize to match old Input.mouseScrollDelta scale
        var scrollDelta = Mouse.current.scroll.ReadValue().y; /// 120f;
        var cameraPosition = Zoom.Position;

        CurrentDepthScrollSpeed = Mathf.Lerp(CurrentDepthScrollSpeed, scrollDelta * DepthScrollMaxSpeed, DepthScrollSlide);

        if (CurrentDepthScrollSpeed != 0)
        {
            Zoom.Position = Mathf.Clamp01(CurrentDepthScrollSpeed * Time.deltaTime + cameraPosition);
        }
    }

    private void UpdateIncline()
    {
        return;
        var mouse = Mouse.current;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            InclineGrabPoint = mouse.position.ReadValue();
            InclineStart = Incline.Incline;
        }

        var newIncline = Incline.Incline;

        if (mouse.rightButton.isPressed)
        {
            var screenPosition = mouse.position.ReadValue();
            var screenSize = new Vector2(Screen.width, Screen.height);
            var drag = screenPosition - InclineGrabPoint;
            var dragNormalized = new Vector2(drag.x / screenSize.x, drag.y / screenSize.y);
            dragNormalized.x = Mathf.Clamp(dragNormalized.x, -InclineMaxDeviationScreen, InclineMaxDeviationScreen);
            dragNormalized.y = Mathf.Clamp(dragNormalized.y, -InclineMaxDeviationScreen, InclineMaxDeviationScreen);
            dragNormalized /= InclineMaxDeviationScreen;
            dragNormalized /= 2;
            dragNormalized = new Vector2(-dragNormalized.y, dragNormalized.x);

            newIncline = InclineStart + dragNormalized;
        }

        var depthFactor = Mathf.InverseLerp(InclineStartHeight, 1, Zoom.Position);
        var offset = Mathf.Lerp(0.5f, 0, depthFactor);

        var x = Mathf.Clamp(newIncline.x, offset, 1 - offset);
        var y = Mathf.Clamp(newIncline.y, offset, 1 - offset);

        Incline.Incline = Vector2.Lerp(Incline.Incline, new Vector2(x, y), InclineGlide);
    }

    private void UpdateGlobalRotation()
    {
        var mouse = Mouse.current;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            ScreenGrabPoint = mouse.position.ReadValue();
            CurrentRotationSpeed = Vector3.zero;
            StartRotation = Rotation.Quaternion;
        }

        if (mouse.leftButton.isPressed)
        {
            var screenPosition = mouse.position.ReadValue();
            var drag = screenPosition - ScreenGrabPoint;
            var rotationSpeed = Mathf.Lerp(RotationSpeedMin, RotationSpeedMax, 1 - Zoom.Position);
            var rotation = new Vector3(-drag.y, drag.x, 0) * rotationSpeed;

            CurrentRotationSpeed = Vector3.Lerp(CurrentRotationSpeed, rotation, Mathf.Clamp01(RotationSlide));

            Rotation.Quaternion = StartRotation * Quaternion.Euler(CurrentRotationSpeed);
        }
    }
}
