using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IgnoreAttackFish : MonoBehaviour
{
    internal static readonly List<IgnoreAttackFish> Active = new List<IgnoreAttackFish>();

    private Collider[] objectColliders;
    private readonly HashSet<(Collider fish, Collider other)> ignoredPairs = new HashSet<(Collider fish, Collider other)>();

    private void OnEnable()
    {
        RefreshColliders();
        Active.Remove(this);
        Active.Add(this);
    }

    public void RefreshColliders()
    {
        objectColliders = GetComponentsInChildren<Collider>(true);
    }

    internal void IgnoreWith(Collider[] fishColliders)
    {
        if (objectColliders == null || fishColliders == null)
            return;

        foreach (Collider fish in fishColliders)
        {
            if (!CanUse(fish))
                continue;

            foreach (Collider other in objectColliders)
            {
                if (!CanUse(other) || fish == other || Physics.GetIgnoreCollision(fish, other))
                    continue;

                Physics.IgnoreCollision(fish, other, true);
                ignoredPairs.Add((fish, other));
            }
        }
    }

    internal static bool CanUse(Collider collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger;
    }

    private void OnDisable()
    {
        Active.Remove(this);

        foreach (var pair in ignoredPairs)
        {
            if (CanUse(pair.fish) && CanUse(pair.other))
                Physics.IgnoreCollision(pair.fish, pair.other, false);
        }

        ignoredPairs.Clear();
    }
}
