using UnityEngine;

// Put this on anything that should hurt the player (spikes, enemies, lava...).
// The object needs a Collider (2D or 3D). Your player must have the tag "Player".
public class DamageOnTouch : MonoBehaviour
{
    public string playerTag = "Player";

    void Hit(GameObject other)
    {
        if (other.CompareTag(playerTag) && LifeManager.Instance != null) LifeManager.Instance.Fail();
    }

    void OnTriggerEnter2D(Collider2D other) => Hit(other.gameObject);
    void OnCollisionEnter2D(Collision2D c) => Hit(c.gameObject);
    void OnTriggerEnter(Collider other) => Hit(other.gameObject);
    void OnCollisionEnter(Collision c) => Hit(c.gameObject);
}
