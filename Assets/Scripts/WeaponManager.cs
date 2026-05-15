using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("Waffen")]
    public GameObject primaryWeapon;   // Hauptwaffe
    public GameObject secondaryWeapon; // Sekundärwaffe
    public GameObject knife;           // Messer

    [Header("Aktuelle Waffe")]
    private GameObject currentWeapon;

    void Start()
    {
        // Standardmäßig das Messer aktivieren
        SwitchToWeapon(knife);
    }

    void Update()
    {
        // Tasten zum Waffenwechsel
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SwitchToWeapon(primaryWeapon);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            SwitchToWeapon(secondaryWeapon);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            SwitchToWeapon(knife);
    }

    void SwitchToWeapon(GameObject newWeapon)
    {
        // Aktuelle Waffe deaktivieren
        if (currentWeapon != null)
            currentWeapon.SetActive(false);

        // Neue Waffe aktivieren
        if (newWeapon != null)
        {
            currentWeapon = newWeapon;
            currentWeapon.SetActive(true);
        }
    }
}   