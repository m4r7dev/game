using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [Header("Waffen Info")]
    public string weaponName = "Waffe";
    public WeaponType weaponType = WeaponType.Primary;
    public GameObject weaponPrefab; // Das Prefab das in den Slot kommt

    [Header("Pickup")]
    public float pickupRange = 2.5f;
    public KeyCode pickupKey = KeyCode.E;

    private Transform player;
    private WeaponSlots weaponSlots;
    private bool inRange = false;

    void Start()
    {
        player = GameObject.FindWithTag("Player").transform;
        weaponSlots = player.GetComponent<WeaponSlots>();
    }

    void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);
        inRange = distance <= pickupRange;

        if (inRange && Input.GetKeyDown(pickupKey))
            Pickup();
    }

    void Pickup()
    {
        weaponSlots.AddWeapon(weaponPrefab, weaponType);
        Destroy(gameObject); // Waffe vom Boden entfernen
    }

    void OnGUI()
    {
        if (inRange)
        {
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 20, 200, 30),
                $"[E] {weaponName} aufheben");
        }
    }
}