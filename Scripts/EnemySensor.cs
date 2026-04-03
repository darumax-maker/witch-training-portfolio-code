using System.Collections.Generic;
using UnityEngine;

public class EnemySensor : MonoBehaviour
{
    [SerializeField] private LayerMask playerMask = ~0;

    // 索敵範囲内にいる候補（複数プレイヤー/召喚物にも対応できる形）
    private readonly HashSet<Transform> targets = new HashSet<Transform>();

    public Transform CurrentTarget { get; private set; }

    private void Reset()
    {
        // 付けた瞬間に Trigger 推奨設定に寄せる（任意）
        var col = GetComponent<SphereCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, playerMask)) return;

        // ルートを Player とみなす（必要なら other.transform でも可）
        Transform t = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        targets.Add(t);
        RecomputeClosest();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsInMask(other.gameObject.layer, playerMask)) return;

        Transform t = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        targets.Remove(t);
        if (CurrentTarget == t) CurrentTarget = null;
        RecomputeClosest();
    }

    private void LateUpdate()
    {
        // 破棄等で null が混じることがあるので掃除（軽量）
        if (targets.Count == 0) return;
        bool removed = false;

        // HashSet は foreach 中に Remove できないので一旦リスト化
        // 索敵対象が少ない前提なら問題なし。気になるなら別管理にします。
        var temp = ListPool<Transform>.Get();
        temp.AddRange(targets);

        for (int i = 0; i < temp.Count; i++)
        {
            if (temp[i] == null)
            {
                targets.Remove(temp[i]);
                removed = true;
            }
        }

        ListPool<Transform>.Release(temp);

        if (removed) RecomputeClosest();
    }

    private void RecomputeClosest()
    {
        Transform best = null;
        float bestSqr = float.PositiveInfinity;

        Vector3 p = transform.position;

        foreach (var t in targets)
        {
            if (t == null) continue;
            float sqr = (t.position - p).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        CurrentTarget = best;
    }

    private static bool IsInMask(int layer, LayerMask mask)
        => (mask.value & (1 << layer)) != 0;

    // 小さなListプール（GC削減用）
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new Stack<List<T>>();

        public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(8);
        public static void Release(List<T> list)
        {
            list.Clear();
            pool.Push(list);
        }
    }
}
