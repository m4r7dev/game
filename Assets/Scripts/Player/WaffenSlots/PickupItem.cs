using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [Header("Waffen Info")]
    public string weaponName = "Waffe";
    public WeaponType weaponType = WeaponType.Primary;

    [Header("Pickup")]
    public float pickupRange = 2.5f;
    public KeyCode pickupKey = KeyCode.E;

    private Transform player;
    private WeaponSlots weaponSlots;
    private bool inRange = false;

    private static PickupItem nearestPickup = null;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("Kein GameObject mit Tag 'Player' gefunden!");
            return;
        }
        player = playerObj.transform;
        weaponSlots = playerObj.GetComponent<WeaponSlots>();
        if (weaponSlots == null)
            Debug.LogError("WeaponSlots Component fehlt auf dem Player!");
    }

    void Update()
    {
        if (player == null || weaponSlots == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        inRange = distance <= pickupRange;

        if (inRange) nearestPickup = this;
        else if (nearestPickup == this) nearestPickup = null;

        if (inRange && Input.GetKeyDown(pickupKey))
            Pickup();
    }

    void Pickup()
    {
        if (nearestPickup == this) nearestPickup = null;
        weaponSlots.AddWeapon(gameObject, weaponType);
    }

    void OnGUI()
    {
        if (!inRange) return;
        if (nearestPickup != this) return;

        int cx = Screen.width / 2;
        int cy = Screen.height / 2;

        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;

        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.DrawTexture(new Rect(cx - 110, cy + 25, 220, 28), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx - 110, cy + 25, 220, 28),
            $"[E]  {weaponName} aufheben", style);
    }

    void OnDestroy()
    {
        if (nearestPickup == this) nearestPickup = null;
    }
}