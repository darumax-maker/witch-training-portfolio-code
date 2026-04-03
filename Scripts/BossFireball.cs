using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossFireball : MonoBehaviour
{
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeSeconds = 5f;
    [SerializeField] private int damage = 2;
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        Destroy(gameObject, lifeSeconds);
    }

    private void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null) ph.ApplyDamage(damage);

        Destroy(gameObject);
    }
}
