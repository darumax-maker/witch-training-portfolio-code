using UnityEngine;

public class PlayerContactDamage : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Damage")]
    [SerializeField] private int touchDamage = 1;

    [Tooltip("G‚ê‚½‘Šè‚ğEnemy‚Æ‚µ‚Äˆµ‚¤Layer")]
    [SerializeField] private LayerMask enemyMask;

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
    }

    private bool IsEnemy(Collider other)
    {
        return (enemyMask.value & (1 << other.gameObject.layer)) != 0;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (playerHealth == null) return;
        if (collision.collider == null) return;

        if (IsEnemy(collision.collider))
        {
            playerHealth.ApplyDamage(touchDamage);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerHealth == null) return;
        if (other == null) return;

        if (IsEnemy(other))
        {
            playerHealth.ApplyDamage(touchDamage);
        }
    }
}
