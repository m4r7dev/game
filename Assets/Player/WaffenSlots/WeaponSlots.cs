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
        if (Input.GetKeyDown(KeyCode.G))      DropActiveWeapon();
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

        Rigidbody rb = current.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        foreach (Collider col in current.GetComponentsInChildren<Collider>())
            col.enabled = false;

        PickupItem pickup = current.GetComponent<PickupItem>();
        if (pickup != null) Destroy(pickup);

        SetLayerRecursively(current, LayerMask.NameToLayer("weapon"));

        HideAllExcept(current);
        activeWeapon = current;
        return true;
    }

    void DropActiveWeapon()
    {
        if (activeWeapon == null) return;

        if (activeWeapon == primaryWeapon)
        {
            DropWeapon(primaryWeapon);
            primaryWeapon = null;
        }
        else if (activeWeapon == secondaryWeapon)
        {
            DropWeapon(secondaryWeapon);
            secondaryWeapon = null;
        }
        else
        {
            DropWeapon(meleeWeapon);
            meleeWeapon = null;
        }

        activeWeapon = null;
    }

    void DropWeapon(GameObject weapon)
    {
        WeaponType type = GetWeaponType(weapon);

        weapon.transform.SetParent(null);

        Rigidbody rb = weapon.AddComponent<Rigidbody>();
        rb.AddForce(Camera.main.transform.forward * 3f, ForceMode.Impulse);

        foreach (Collider col in weapon.GetComponentsInChildren<Collider>())
            col.enabled = true;

        SetLayerRecursively(weapon, LayerMask.NameToLayer("Default"));

        PickupItem pickup = weapon.AddComponent<PickupItem>();
        pickup.weaponName = weapon.name;
        pickup.weaponType = type;
    }

    WeaponType GetWeaponType(GameObject weapon)
    {
        if (weapon == primaryWeapon)   return WeaponType.Primary;
        if (weapon == secondaryWeapon) return WeaponType.Secondary;
        return WeaponType.Melee;
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

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
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