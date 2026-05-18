using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour
{
    // Camera Rotation
    public float mouseSensitivity = 2f;
    private float verticalRotation = 0f;
    private Transform cameraTransform;

    // Camera Collision
    private Vector3 cameraDefaultPos;
    public float cameraCollisionRadius = 0.1f;
    public LayerMask cameraCollisionMask;

    // Ground Movement
    private Rigidbody rb;
    public float walkSpeed = 5f;
    public float sprintSpeed = 10f;
    public float sneakSpeed = 2f;
    public float sneakHeight = 0.5f;
    private float normalHeight;
    private float normalCameraHeight;
    private Vector2 moveInput;

    // Sliding
    public float slideThreshold = 8f;
    public float slideFriction = 0.95f;
    private bool isSliding = false;

    // Jumping
    public float jumpForce = 10f;
    public float fallMultiplier = 2.5f;
    public float ascendMultiplier = 2f;
    private bool isGrounded = true;
    public LayerMask groundLayer;
    private float groundCheckTimer = 0f;
    private float groundCheckDelay = 0.3f;
    private float playerHeight;
    private float raycastDistance;

    // Input
    private PlayerInputActions inputActions;
    private Vector2 lookInput;
    private bool isSprinting;
    private bool isSneaking;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Jump.performed   += OnJump;
        inputActions.Player.Sprint.performed += ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled  += ctx => isSprinting = false;
        inputActions.Player.Sneak.performed  += ctx => isSneaking = true;
        inputActions.Player.Sneak.canceled   += ctx => isSneaking = false;
        inputActions.Player.Slide.performed  += ctx => CheckSlideStart();
        inputActions.Player.Slide.canceled   += ctx => isSliding = false;
    }

    void OnDisable()
    {
        inputActions.Player.Jump.performed   -= OnJump;
        inputActions.Player.Sprint.performed -= ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled  -= ctx => isSprinting = false;
        inputActions.Player.Sneak.performed  -= ctx => isSneaking = true;
        inputActions.Player.Sneak.canceled   -= ctx => isSneaking = false;
        inputActions.Player.Slide.performed  -= ctx => CheckSlideStart();
        inputActions.Player.Slide.canceled   -= ctx => isSliding = false;
        inputActions.Player.Disable();
    }

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        cameraTransform = Camera.main.transform;

        CapsuleCollider col = GetComponent<CapsuleCollider>();
        normalHeight = col.height;
        normalCameraHeight = cameraTransform.localPosition.y;
        cameraDefaultPos = cameraTransform.localPosition;

        playerHeight = col.height * transform.localScale.y;
        raycastDistance = (playerHeight / 2) + 0.2f;

        SetupWeaponCamera();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!IsOwner)
        {
            // Nicht-eigene Spieler können die Kamera nicht steuern
            Destroy(cameraTransform.GetComponent<Camera>());
            return;
        }
    }

    void SetupWeaponCamera()
    {
        int weaponLayer = LayerMask.NameToLayer("weapon");
        if (weaponLayer == -1)
        {
            Debug.LogError("Layer 'weapon' nicht gefunden! Bitte in Tags and Layers erstellen.");
            return;
        }

        // Hauptkamera rendert alles außer weapon Layer
        Camera.main.cullingMask &= ~(1 << weaponLayer);

        // Weapon Camera erstellen
        GameObject weaponCamObj = new GameObject("WeaponCamera");
        weaponCamObj.transform.SetParent(cameraTransform);
        weaponCamObj.transform.localPosition = Vector3.zero;
        weaponCamObj.transform.localRotation = Quaternion.identity;

        Camera weaponCam = weaponCamObj.AddComponent<Camera>();
        weaponCam.cullingMask = 1 << weaponLayer;
        weaponCam.clearFlags = CameraClearFlags.Depth;
        weaponCam.depth = Camera.main.depth + 1;
        weaponCam.fieldOfView = Camera.main.fieldOfView;
    }

    void Update()
    {
        if (!IsOwner) return;

        moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        lookInput = inputActions.Player.Look.ReadValue<Vector2>();

        RotateCamera();
        UpdateHeight();
        CheckSlide();
        CameraCollision();

        if (groundCheckTimer > 0f)
            groundCheckTimer -= Time.deltaTime;

        if (!isGrounded && groundCheckTimer <= 0f)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
            isGrounded = Physics.Raycast(rayOrigin, Vector3.down, raycastDistance, groundLayer);
        }
    }

    void FixedUpdate()
    {
        MovePlayer();
        ApplyJumpPhysics();
    }

    void CameraCollision()
    {
        Vector3 desiredPos = transform.TransformPoint(cameraDefaultPos);
        RaycastHit hit;

        if (Physics.SphereCast(transform.position, cameraCollisionRadius,
            (desiredPos - transform.position).normalized,
            out hit, Vector3.Distance(transform.position, desiredPos),
            cameraCollisionMask))
        {
            cameraTransform.position = hit.point + hit.normal * cameraCollisionRadius;
        }
        else
        {
            cameraTransform.localPosition = cameraDefaultPos;
        }
    }

    float GetCurrentSpeed()
    {
        if (isSneaking) return sneakSpeed;
        if (isSprinting) return sprintSpeed;
        return walkSpeed;
    }

    void CheckSlideStart()
    {
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        if (horizontalSpeed >= slideThreshold && isGrounded)
            isSliding = true;
    }

    void CheckSlide()
    {
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        if (horizontalSpeed < 1f)
            isSliding = false;
    }

    void UpdateHeight()
    {
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        float targetHeight  = (isSneaking || isSliding) ? normalHeight * sneakHeight : normalHeight;
        float targetCameraY = (isSneaking || isSliding) ? normalCameraHeight * sneakHeight : normalCameraHeight;

        col.height = Mathf.Lerp(col.height, targetHeight, Time.deltaTime * 10f);

        cameraDefaultPos.y = Mathf.Lerp(cameraDefaultPos.y, targetCameraY, Time.deltaTime * 10f);
        cameraTransform.localPosition = cameraDefaultPos;
    }

    void MovePlayer()
    {
        if (isSliding)
        {
            Vector3 vel = rb.linearVelocity;
            vel.x *= slideFriction;
            vel.z *= slideFriction;
            rb.linearVelocity = vel;
            return;
        }

        Vector3 movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        Vector3 targetVelocity = movement * GetCurrentSpeed();

        Vector3 velocity = rb.linearVelocity;
        velocity.x = targetVelocity.x;
        velocity.z = targetVelocity.z;
        rb.linearVelocity = velocity;

        if (isGrounded && moveInput == Vector2.zero)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    void RotateCamera()
    {
        float horizontalRotation = lookInput.x * mouseSensitivity;
        transform.Rotate(0, horizontalRotation, 0);

        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    void OnJump(InputAction.CallbackContext ctx)
    {
        if (isGrounded)
        {
            isSliding = false;
            isGrounded = false;
            groundCheckTimer = groundCheckDelay;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }

    void ApplyJumpPhysics()
    {
        if (rb.linearVelocity.y < 0)
            rb.linearVelocity += Vector3.up * Physics.gravity.y * fallMultiplier * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0)
            rb.linearVelocity += Vector3.up * Physics.gravity.y * ascendMultiplier * Time.fixedDeltaTime;
    }
}
