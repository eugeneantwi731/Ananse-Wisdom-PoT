using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central, persistent-per-session place that tracks which crafts Ananse has
/// learned and reacts to hut icon taps. Lives outside the AR-tracked
/// ImageTarget hierarchy on purpose, so its state survives the image target
/// being lost and re-found.
///
/// For now, tapping a hut icon just logs and immediately "completes" that
/// craft so the unlock chain can be tested end to end. Once the real
/// mini-games exist, replace the TODO in OnHutIconTapped with loading the
/// matching mini-game scene, and call CompleteCraft(craftName) when that
/// mini-game reports a win instead of completing it immediately on tap.
/// </summary>
public class VillageManager : MonoBehaviour
{
    public static VillageManager Instance { get; private set; }

    [Tooltip("Huts in unlock order. The first entry starts unlocked; each other one unlocks when the previous craft is completed. Drag the 5 HutRole_* objects in here and reorder as needed.")]
    public List<HutIcon> hutOrder = new List<HutIcon>();

    readonly HashSet<string> learnedCrafts = new HashSet<string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        for (int i = 0; i < hutOrder.Count; i++)
        {
            if (hutOrder[i] == null) continue;
            if (i == 0) hutOrder[i].Unlock();
            else hutOrder[i].Lock();
        }
    }

    public void OnHutIconTapped(string craftName)
    {
        Debug.Log($"[VillageManager] Tapped hut icon: {craftName}");

        // TODO: replace with loading the real mini-game scene for craftName,
        // and call CompleteCraft(craftName) from that scene's win condition
        // instead of doing it here immediately.
        CompleteCraft(craftName);
    }

    public void CompleteCraft(string craftName)
    {
        if (learnedCrafts.Contains(craftName)) return;
        learnedCrafts.Add(craftName);

        int index = hutOrder.FindIndex(h => h != null && h.craftName == craftName);
        if (index >= 0 && index + 1 < hutOrder.Count && hutOrder[index + 1] != null)
        {
            hutOrder[index + 1].Unlock();
        }
    }

    public bool HasLearned(string craftName) => learnedCrafts.Contains(craftName);
}
