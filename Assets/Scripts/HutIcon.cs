using UnityEngine;

/// <summary>
/// Attach this to a marker positioned above a hut (one of the HutRole_*
/// anchors under ImageTarget). It handles the idle bob/rotate animation for
/// the craft icon and reports taps to the VillageManager.
///
/// Setup per hut:
///  - Set craftName to something VillageManager's hutOrder list also uses
///    (e.g. "Weaver", "Carver", "Drummer", "Farmer", "Dancer").
///  - Add a child object with the actual icon sprite/mesh once it's
///    imported, so RefreshVisibility() has a Renderer to show/hide.
///  - Make sure this object (or a child) has a Collider so OnMouseDown can
///    detect taps - a small SphereCollider or BoxCollider set as trigger
///    works fine at this miniature AR scale.
/// </summary>
public class HutIcon : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Must match a name VillageManager's hutOrder list expects, e.g. \"Weaver\", \"Carver\", \"Drummer\", \"Farmer\", \"Dancer\".")]
    public string craftName;

    [Header("Idle Animation")]
    public bool bob = true;
    public float bobHeight = 0.01f; // small on purpose - this village is miniature scale
    public float bobSpeed = 1.5f;

    public bool rotate = true;
    public float rotateSpeed = 60f; // degrees per second

    [Header("State")]
    [Tooltip("Whether this icon is currently visible and tappable. VillageManager drives this at runtime; the checkbox here just sets the starting state.")]
    public bool isUnlocked = false;

    Vector3 startLocalPos;

    void Awake()
    {
        startLocalPos = transform.localPosition;
    }

    void OnEnable()
    {
        RefreshVisibility();
    }

    void Update()
    {
        if (!isUnlocked) return;

        if (bob)
        {
            float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = startLocalPos + new Vector3(0f, y, 0f);
        }

        if (rotate)
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);
        }
    }

    // Works out of the box for mouse clicks and single-finger touch, as long
    // as this object (or a child) has a Collider - no EventSystem needed.
    void OnMouseDown()
    {
        if (!isUnlocked) return;
        if (VillageManager.Instance != null)
            VillageManager.Instance.OnHutIconTapped(craftName);
    }

    public void Unlock()
    {
        isUnlocked = true;
        RefreshVisibility();
    }

    public void Lock()
    {
        isUnlocked = false;
        RefreshVisibility();
    }

    void RefreshVisibility()
    {
        // Hide the icon's visuals entirely when locked; show them once unlocked.
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = isUnlocked;
    }
}
