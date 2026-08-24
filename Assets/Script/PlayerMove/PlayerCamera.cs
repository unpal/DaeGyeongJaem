using Cinemachine;
using UnityEngine;

public class PlayerCamera
{
    private readonly CinemachineVirtualCamera camera;
    private readonly float sensitivity;

    private float xRotation;

    public PlayerCamera(
        CinemachineVirtualCamera camera,
        float sensitivity)
    {
        this.camera = camera;
        this.sensitivity = sensitivity;
    }

    public void SetActive(bool active)
    {
        if (camera != null)
            camera.gameObject.SetActive(active);
    }

    public void UpdateLook(Vector2 look)
    {
        if (camera == null)
            return;

        float mouseY =
            look.y * sensitivity;

        xRotation -= mouseY;

        xRotation =
            Mathf.Clamp(
                xRotation,
                -90f,
                90f);

        camera.transform.localRotation =
            Quaternion.Euler(
                xRotation,
                0f,
                0f);
    }

    public void Reset()
    {
        xRotation = 0f;

        if (camera != null)
        {
            camera.transform.localRotation =
                Quaternion.identity;

            camera.transform.localScale =
                Vector3.one;
        }
    }
}