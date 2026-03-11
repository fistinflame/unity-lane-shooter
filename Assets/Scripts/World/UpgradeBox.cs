using UnityEngine;

public class UpgradeBox : MonoBehaviour
{
    [SerializeField] private int hitsToUnlock = 3;
    [SerializeField] private int fireRateUpgradeLevels = 1;
    [SerializeField] private Color lockedColor = new Color(1f, 0.55f, 0.1f);
    [SerializeField] private Color unlockedColor = new Color(0.25f, 1f, 0.35f);

    private float _progress;
    private bool _isUnlocked;
    private bool _isConsumed;
    private TextMesh _label;

    private void Awake()
    {
        EnsurePhysics();
        EnsureLabel();
    }

    private void OnEnable()
    {
        _progress = 0f;
        _isUnlocked = false;
        _isConsumed = false;
        RefreshVisual();
    }

    public void Configure(int unlockHits, int upgradeLevels)
    {
        hitsToUnlock = Mathf.Max(1, unlockHits);
        fireRateUpgradeLevels = Mathf.Max(1, upgradeLevels);
        _progress = 0f;
        _isUnlocked = false;
        _isConsumed = false;
        RefreshVisual();
    }

    public bool ReceiveBulletDamage(float damage)
    {
        if (_isConsumed)
        {
            return false;
        }

        if (_isUnlocked)
        {
            return false; // Unlocked box should be shoot-through.
        }

        _progress += Mathf.Max(0.1f, damage);
        if (_progress >= hitsToUnlock)
        {
            _isUnlocked = true;
        }

        RefreshVisual();
        return true; // Locked box blocks bullets.
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isConsumed || !_isUnlocked)
        {
            return;
        }

        var squad = other.GetComponent<SquadController>();
        if (squad == null)
        {
            return;
        }

        GameManager.Instance.ApplyRunFireRateUpgrade(fireRateUpgradeLevels);
        _isConsumed = true;
        gameObject.SetActive(false);
    }

    private void RefreshVisual()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = _isUnlocked ? unlockedColor : lockedColor;
        }

        if (_label == null)
        {
            return;
        }

        if (_isUnlocked)
        {
            _label.text = "FIRE+";
            _label.color = Color.white;
            return;
        }

        int hitsLeft = Mathf.Clamp(Mathf.CeilToInt(hitsToUnlock - _progress), 0, hitsToUnlock);
        _label.text = "x" + hitsLeft;
        _label.color = Color.black;
    }

    private void EnsurePhysics()
    {
        var collider = GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true;
        collider.size = new Vector3(1.4f, 1.4f, 1.4f);

        var body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
    }

    private void EnsureLabel()
    {
        if (_label != null)
        {
            return;
        }

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        _label = labelGo.AddComponent<TextMesh>();
        _label.fontSize = 70;
        _label.characterSize = 0.08f;
        _label.anchor = TextAnchor.MiddleCenter;
        _label.alignment = TextAlignment.Center;
    }
}
