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
        // Slot 1 = Primary, Slot 2 = Secondary, Slot 3 = Melee
        EquipSlot(1);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) EquipSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) EquipSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) EquipSlot(3);
    }

    public bool AddWeapon(GameObject weaponPrefab, WeaponType type)
    {
        Transform slot = GetSlot(type);
        ref GameObject current = ref GetWeaponRef(type);

        // Slot bereits belegt — droppen
        if (current != null)
            DropWeapon(current, slot);

        // Waffe in Slot setzen
        current = Instantiate(weaponPrefab, slot.position, slot.rotation, slot);
        current.GetComponent<Rigidbody>().isKinematic = true;
        current.GetComponent<Collider>().enabled = false;

        HideAllExcept(activeWeapon);
        return true;
    }

    void DropWeapon(GameObject weapon, Transform slot)
    {
        weapon.transform.SetParent(null);
        Rigidbody rb = weapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(Camera.main.transform.forward * 3f, ForceMode.Impulse);
        }
        Collider col = weapon.GetComponent<Collider>();
        if (col != null) col.enabled = true;
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