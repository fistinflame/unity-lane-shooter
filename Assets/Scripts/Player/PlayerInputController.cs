using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInputController : MonoBehaviour
{
    [SerializeField] private SquadController squadController;
    [SerializeField] private float deadZone = 0.03f;

    public void SetSquadController(SquadController squad)
    {
        squadController = squad;
    }

    private void Update()
    {
        if (squadController == null || !GameManager.Instance.IsRunning)
        {
            return;
        }

        Vector2? pointer = ReadPointerPosition();
        if (!pointer.HasValue)
        {
            return;
        }

        float normalizedX = (pointer.Value.x / Mathf.Max(1f, Screen.width)) * 2f - 1f;
        if (Mathf.Abs(normalizedX) < deadZone)
        {
            normalizedX = 0f;
        }

        squadController.SetLateralInput(Mathf.Clamp(normalizedX, -1f, 1f));
    }

    private static Vector2? ReadPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return Mouse.current.position.ReadValue();
        }

        return null;
#else
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                return touch.position;
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(0))
        {
            return Input.mousePosition;
        }
#endif

        return null;
#endif
    }
}
