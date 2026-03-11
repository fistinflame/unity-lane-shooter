using UnityEngine;

public class SpriteWobble : MonoBehaviour
{
    [SerializeField] private float angleAmplitude = 5f;
    [SerializeField] private float wobbleFrequency = 6f;
    [SerializeField] private Vector3 wobbleAxis = Vector3.forward;
    [SerializeField] private bool randomizePhase = true;

    private Quaternion _baseRotation = Quaternion.identity;
    private float _phaseOffset;

    private void OnEnable()
    {
        _baseRotation = transform.localRotation;
        if (randomizePhase)
        {
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void LateUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning)
        {
            transform.localRotation = _baseRotation;
            return;
        }

        Vector3 axis = wobbleAxis.sqrMagnitude > 0.0001f ? wobbleAxis.normalized : Vector3.forward;
        float phase = (Time.time * Mathf.Max(0.01f, wobbleFrequency)) + _phaseOffset;
        float angle = Mathf.Sin(phase) * angleAmplitude;
        transform.localRotation = _baseRotation * Quaternion.AngleAxis(angle, axis);
    }

    public void Configure(float amplitude, float frequency, Vector3 axis)
    {
        angleAmplitude = Mathf.Max(0f, amplitude);
        wobbleFrequency = Mathf.Max(0.01f, frequency);
        wobbleAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;

        _baseRotation = transform.localRotation;
        if (randomizePhase)
        {
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    public void ResetBaseRotation(Quaternion rotation)
    {
        _baseRotation = rotation;
        transform.localRotation = rotation;
    }
}
