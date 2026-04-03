using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossContactDamage : MonoBehaviour
{
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float interval = 0.5f;
    [SerializeField] private string playerTag = "Player";

    private float nextTime;

    private void OnCollisionStay(Collision collision)
    {
        if (Time.time < nextTime) return;

        var other = collision.collider;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph == null) return;

        if (ph.ApplyDamage(contactDamage))
            nextTime = Time.time + Mathf.Max(0.05f, interval);
    }
}
