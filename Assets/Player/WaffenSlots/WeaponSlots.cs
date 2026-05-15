using UnityEngine;

public enum WeaponType { Primary, Secondary, Melee }

public class WeaponSlots : MonoBehaviour
{
    [Header("Slots")]
    public Transform primarySlot;
    public Transform secondarySlot;
    public Transform meleeSlot;

    [HideInInspector] public GameObject primaryWeapon;
    [HideInInspector] public GameObject secondaryWeapon;
    [HideInInspector] public GameObject meleeWeapon;

    private GameObject activeWeapon;

    void Start()
    {
        EquipSlot(1);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) EquipSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) EquipSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) EquipSlot(3);
    }

    public bool AddWeapon(GameObject weaponObject, WeaponType type)
    {
        Transform slot = GetSlot(type);
        ref GameObject current = ref GetWeaponRef(type);

        if (current != null)
            DropWeapon(current);

        current = weaponObject;
        current.transform.SetParent(slot);
        current.transform.localPosition = Vector3.zero;
        current.transform.localRotation = Quaternion.identity;

        // Rigidbody komplett entfernen
        Rigidbody rb = current.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        // Collider deaktivieren
        foreach (Collider col in current.GetComponentsInChildren<Collider>())
            col.enabled = false;

        PickupItem pickup = current.GetComponent<PickupItem>();
        if (pickup != null) Destroy(pickup);

        HideAllExcept(current);
        activeWeapon = current;
        return true;
    }

    void DropWeapon(GameObject weapon)
    {
        weapon.transform.SetParent(null);

        Rigidbody rb = weapon.AddComponent<Rigidbody>();
        rb.AddForce(Camera.main.transform.forward * 3f, ForceMode.Impulse);

        foreach (Collider col in weapon.GetComponentsInChildren<Collider>())
            col.enabled = true;
    }

    public void EquipSlot(int slot)
    {
        activeWeapon = slot switch
        {
            1 => primaryWeapon,
            2 => secondaryWeapon,
            3 => meleeWeapon,
            _ => null
        };
        HideAllExcept(activeWeapon);
    }

    void HideAllExcept(GameObject active)
    {
        if (primaryWeapon != null)   primaryWeapon.SetActive(primaryWeapon == active);
        if (secondaryWeapon != null) secondaryWeapon.SetActive(secondaryWeapon == active);
        if (meleeWeapon != null)     meleeWeapon.SetActive(meleeWeapon == active);
    }

    Transform GetSlot(WeaponType type) => type switch
    {
        WeaponType.Primary   => primarySlot,
        WeaponType.Secondary => secondarySlot,
        WeaponType.Melee     => meleeSlot,
        _ => primarySlot
    };

    ref GameObject GetWeaponRef(WeaponType type)
    {
        if (type == WeaponType.Primary)   return ref primaryWeapon;
        if (type == WeaponType.Secondary) return ref secondaryWeapon;
        return ref meleeWeapon;
    }
}