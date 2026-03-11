using UnityEngine;

[CreateAssetMenu(menuName = "Hypercasual/Enemy Type", fileName = "EnemyType")]
public class EnemyType : ScriptableObject
{
    public string typeId = "Basic";
    public float maxHealth = 5f;
    public float moveSpeed = 4f;
    public int contactDamage = 2;
    public float scale = 1f;
    public Color tint = new Color(0.9f, 0.25f, 0.25f);
}
