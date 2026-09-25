using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaypointTarget : MonoBehaviour
{
    internal static readonly List<WaypointTarget> Targets = new List<WaypointTarget>();

    [SerializeField] private bool visible;
    [SerializeField] private string label = "Destination";
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.7f, 0f);
    [SerializeField] private int priority;
    [SerializeField] private bool showOnScreen = true;

    public bool Visible => visible;
    public string Label => label;
    public int Priority => priority;
    public bool ShowOnScreen => showOnScreen;
    public Vector3 Position => transform.position + worldOffset;

    private void OnEnable()
    {
        Targets.Remove(this);
        Targets.Add(this);
    }

    private void OnDisable()
    {
        Targets.Remove(this);
    }

    public void Show()
    {
        visible = true;
        if (isActiveAndEnabled)
        {
            Targets.Remove(this);
            Targets.Add(this);
        }
    }

    public void Hide()
    {
        visible = false;
    }
}
