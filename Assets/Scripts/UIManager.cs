using System;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class UIManager : MonoBehaviour
{
    [Header("Defaults")]
    [SerializeField] private string serverIp = "127.0.0.1";
    [SerializeField] private ushort serverPort = 7777;

    private bool isPrivateLobby;
    private string lobbyPassword = "";

    private string _passwordInput = "";
    private string _serverIpInput;
    private string _serverPortInput;

    private NetworkManager _networkManager;

    private bool _netcodeReadyChecked;
    private string _status = "";

    private const string PayloadPrefix = "L1:";

    private void Awake()
    {
        Debug.Log("[Lobby] UIManager Awake");
    }

    private void Start()
    {
        Debug.Log("[Lobby] UIManager Start");
        _serverIpInput = serverIp;
        _serverPortInput = serverPort.ToString();
        TryCacheNetworkManager();
        EnsureLobbyUi();
        Debug.Log($"[Lobby] UIManager Start | netMgrExists={_networkManager != null} | status='{_status}'");
    }

    private void TryCacheNetworkManager()
    {
        if (_networkManager != null)
            return;

        _networkManager = FindAnyObjectByType<NetworkManager>();
        if (_networkManager == null)
        {
            _status = "[Lobby] Kein NetworkManager in der Scene gefunden.";
            return;
        }

        _status = "[Lobby] NetworkManager gefunden.";
        _netcodeReadyChecked = true;
    }

    // Runtime UI (Canvas + Buttons + InputFields)
    private bool _uiCreated;

    // Unity UI InputFields (einfache Variante, damit du keine Scene-Buttons verdrahten musst)
    private InputField uiIpInput;
    private InputField uiPortInput;
    private InputField uiPasswordInput;
    private Text uiStatusText;

    private void EnsureLobbyUi()
    {
        if (_uiCreated)
            return;

        // In der Scene gibt es bereits EventSystem/Canvas - wir erstellen aber trotzdem ein eigenen Overlay-Canvas
        // damit wir sicher etwas sehen.
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("LobbyCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        else
        {
            // wenn es schon ein Canvas gibt, nutzen wir es und hängen unser UI darunter.
        }

        var root = new GameObject("LobbyPanel");
        root.transform.SetParent(canvas.transform, false);

        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0, 1);
        rootRect.anchorMax = new Vector2(0, 1);
        rootRect.pivot = new Vector2(0, 1);
        rootRect.anchoredPosition = new Vector2(10, -10);
        rootRect.sizeDelta = new Vector2(520, 320);

        // Background
        var bg = new GameObject("BG");
        bg.transform.SetParent(root.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.35f);

        Font font;
        try
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch
        {
            // Letzter Fallback: irgendeinen Builtin-Font nehmen (damit UI nicht crash-t)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        float y = -20f;

        // Title
        uiStatusText = CreateLabel(root.transform, font, "Lobby", new Vector2(10, y), new Vector2(500, 26), TextAnchor.UpperLeft);
        y -= 32f;

        // IP
        CreateTextLabel(root.transform, font, "Server IP", new Vector2(10, y));
        uiIpInput = CreateInputField(root.transform, font, serverIp, new Vector2(120, y - 10), new Vector2(390, 30));
        y -= 50f;

        // Port
        CreateTextLabel(root.transform, font, "Port", new Vector2(10, y));
        uiPortInput = CreateInputField(root.transform, font, serverPort.ToString(), new Vector2(120, y - 10), new Vector2(390, 30));
        y -= 50f;

        // Password
        CreateTextLabel(root.transform, font, "Password", new Vector2(10, y));
        uiPasswordInput = CreateInputField(root.transform, font, "", new Vector2(120, y - 10), new Vector2(390, 30));
        y -= 50f;

        // Buttons row
        var btnHostPublic = CreateButton(root.transform, font, "Host Public", new Vector2(10, y - 5), new Vector2(150, 35));
        btnHostPublic.onClick.AddListener(() => { isPrivateLobby = false; HostPublicLobby(); });

        var btnHostPrivate = CreateButton(root.transform, font, "Host Private", new Vector2(170, y - 5), new Vector2(150, 35));
        btnHostPrivate.onClick.AddListener(() => { isPrivateLobby = true; HostPrivateLobby(); });

        y -= 45f;

        var btnJoinPublic = CreateButton(root.transform, font, "Join Public", new Vector2(10, y - 5), new Vector2(150, 35));
        btnJoinPublic.onClick.AddListener(() => { isPrivateLobby = false; JoinPublicLobby(); });

        var btnJoinPrivate = CreateButton(root.transform, font, "Join Private", new Vector2(170, y - 5), new Vector2(150, 35));
        btnJoinPrivate.onClick.AddListener(() => { isPrivateLobby = true; JoinPrivateLobby(); });

        y -= 55f;

        var statusLabel = CreateLabel(root.transform, font, _status, new Vector2(10, y), new Vector2(500, 80), TextAnchor.UpperLeft);
        uiStatusText = statusLabel;

        // initial status
        uiStatusText.text = _status;

        _uiCreated = true;

        Debug.Log("[Lobby] Runtime lobby UI created.");
    }

    private static void CreateTextLabel(Transform parent, Font font, string text, Vector2 anchoredPos)
    {
        var go = new GameObject(text);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(100, 24);

        var label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = 14;
        label.color = Color.white;
        label.text = text;
        label.alignment = TextAnchor.UpperLeft;
    }

    private static Text CreateLabel(Transform parent, Font font, string text, Vector2 anchoredPos, Vector2 size, TextAnchor anchor)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = 14;
        label.color = Color.white;
        label.text = text;
        label.alignment = anchor;

        return label;
    }

    private static InputField CreateInputField(Transform parent, Font font, string defaultValue, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject("InputField");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var image = go.AddComponent<Image>();
        image.color = new Color(1, 1, 1, 0.9f);

        var inputField = go.AddComponent<InputField>();

        // Text Component
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 0);
        textRt.offsetMax = new Vector2(-10, 0);

        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.fontSize = 14;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleLeft;

        // Placeholder
        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(go.transform, false);
        var placeholderRt = placeholderGo.AddComponent<RectTransform>();
        placeholderRt.anchorMin = Vector2.zero;
        placeholderRt.anchorMax = Vector2.one;
        placeholderRt.offsetMin = new Vector2(10, 0);
        placeholderRt.offsetMax = new Vector2(-10, 0);

        var placeholder = placeholderGo.AddComponent<Text>();
        placeholder.font = font;
        placeholder.fontSize = 14;
        placeholder.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        placeholder.text = "";

        inputField.textComponent = text;
        inputField.placeholder = placeholder;

        inputField.text = defaultValue ?? "";

        return inputField;
    }

    private static Button CreateButton(Transform parent, Font font, string caption, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(caption);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.9f);

        var btn = go.AddComponent<Button>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.fontSize = 14;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = caption;

        btn.targetGraphic = img;

        return btn;
    }

    [Header("UI Wiring (optional)")]
    [SerializeField] private TMP_InputField uiServerIpField;
    [SerializeField] private TMP_InputField uiServerPortField;
    [SerializeField] private TMP_InputField uiPasswordField;

    // Button-wiring methods (Unity UI OnClick -> these)
    public void HostPublicLobby()
    {
        isPrivateLobby = false;
        HostLobby(expectedPassword: "");
    }

    public void HostPrivateLobby()
    {
        isPrivateLobby = true;
        HostLobby(expectedPassword: GetUiPasswordOrFallback());
    }

    public void JoinPublicLobby()
    {
        isPrivateLobby = false;
        JoinLobby(serverIp: GetUiServerIpOrFallback(), port: GetUiServerPortOrFallback(), clientPassword: "", assumePrivate: false);
    }

    public void JoinPrivateLobby()
    {
        isPrivateLobby = true;
        JoinLobby(serverIp: GetUiServerIpOrFallback(), port: GetUiServerPortOrFallback(), clientPassword: GetUiPasswordOrFallback(), assumePrivate: true);
    }

    private string GetUiServerIpOrFallback()
    {
        if (uiIpInput != null && !string.IsNullOrWhiteSpace(uiIpInput.text))
            return uiIpInput.text.Trim();
        if (uiServerIpField != null && !string.IsNullOrWhiteSpace(uiServerIpField.text))
            return uiServerIpField.text.Trim();
        return GetServerIp();
    }

    private ushort GetUiServerPortOrFallback()
    {
        if (uiPortInput != null && ushort.TryParse(uiPortInput.text, out var p1))
            return p1;
        if (uiServerPortField != null && ushort.TryParse(uiServerPortField.text, out var p2))
            return p2;
        return GetServerPort();
    }

    private string GetUiPasswordOrFallback()
    {
        if (uiPasswordInput != null)
            return uiPasswordInput.text ?? "";
        if (uiPasswordField != null)
            return uiPasswordField.text ?? "";
        return lobbyPassword;
    }

    private void OnGUI()
    {
        // OnGUI ist optional. Wenn du deine Unity-Buttons über OnClick verdrahtest,
        // brauchst du dieses Menü nicht.
        if (!_netcodeReadyChecked)
            TryCacheNetworkManager();

        if (_networkManager == null)
        {
            GUILayout.BeginArea(new Rect(10, 10, 520, 120));
            GUILayout.Label(_status);
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 520, 120), GUI.skin.box);
        GUILayout.Label("Lobby UI is wired via Unity Buttons (OnClick).");
        GUILayout.Label(_status);
        GUILayout.EndArea();
    }

    private string GetServerIp()
    {
        var ip = _serverIpInput;
        if (string.IsNullOrWhiteSpace(ip))
            ip = "127.0.0.1";
        return ip.Trim();
    }

    private ushort GetServerPort()
    {
        if (ushort.TryParse(_serverPortInput, out var p))
            return p;
        return 7777;
    }

    private void ResetNetworkingIfRunning()
    {
        try
        {
            if (_networkManager != null)
            {
                if (_networkManager.IsServer || _networkManager.IsClient)
                {
                    _networkManager.Shutdown(true);
                }
            }
        }
        catch
        {
            // ignore - best-effort shutdown
        }
    }

    private void HostLobby(string expectedPassword)
    {
        Debug.Log($"[Lobby] HostLobby() called | expectedPrivate={isPrivateLobby} | expectedPasswordLen={(expectedPassword ?? "").Length}");
        ResetNetworkingIfRunning();
        EnsureRuntimePlayerPrefab();

        ConfigureConnectionApproval(expectedPassword);

        var transport = GetUnityTransport(_networkManager);
        if (transport != null)
        {
            var ip = GetServerIp();
            transport.SetConnectionData(ip, GetServerPort(), listenAddress: ip);
        }

        _status = $"[Lobby] Starting Host. Private={isPrivateLobby} ...";
        var started = _networkManager.StartHost();
        _status = started ? "[Lobby] Host started." : "[Lobby] Host failed to start.";
        Debug.Log($"[Lobby] HostLobby() done | started={started} | status='{_status}'");
    }

    private void JoinLobby(string serverIp, ushort port, string clientPassword, bool assumePrivate)
    {
        Debug.Log($"[Lobby] JoinLobby() called | server={serverIp}:{port} | assumePrivate={assumePrivate} | clientPasswordLen={(clientPassword ?? "").Length}");
        ResetNetworkingIfRunning();

        EnsureRuntimePlayerPrefab();

        _networkManager.NetworkConfig.ConnectionApproval = true;
        _networkManager.NetworkConfig.ConnectionData = EncodePayload(clientPassword);

        var transport = GetUnityTransport(_networkManager);
        if (transport != null)
        {
            transport.SetConnectionData(serverIp, port);
        }

        _status = $"[Lobby] Connecting Client to {serverIp}:{port} ...";
        var started = _networkManager.StartClient();
        _status = started ? "[Lobby] Client started (waiting for approval)." : "[Lobby] Client failed to start.";
        Debug.Log($"[Lobby] JoinLobby() done | started={started} | status='{_status}'");
    }

    private void ConfigureConnectionApproval(string expectedPassword)
    {
        _networkManager.NetworkConfig.ConnectionApproval = true;

        _networkManager.ConnectionApprovalCallback = (request, response) =>
        {
            string clientToken = DecodePayload(request.Payload);

            bool expectedIsPrivate = isPrivateLobby && !string.IsNullOrEmpty(expectedPassword);
            string expectedToken = expectedIsPrivate ? expectedPassword : "";

            bool approved = !expectedIsPrivate
                ? true
                : string.Equals(clientToken, expectedToken, StringComparison.Ordinal);

            // IMPORTANT:
            // If PlayerPrefabHash is not set, NGO may fall back to the NetworkConfig default prefab,
            // which in your scene is currently wired to male prefabs WITHOUT NetworkObject.
            // So we must explicitly pass the prefab hash for our runtime Resources prefab.
            var playerPrefab = _networkManager.NetworkConfig.PlayerPrefab;
            var prefabNetObj = playerPrefab != null ? playerPrefab.GetComponent<NetworkObject>() : null;
            // NetworkObject.GlobalObjectIdHash is internal (not accessible), so we use the public PrefabIdHash wrapper.
            uint prefabHash = prefabNetObj != null ? prefabNetObj.PrefabIdHash : 0;

            response.Approved = approved;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = prefabHash != 0 ? prefabHash : null;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;

            response.Reason = approved ? "" : "Wrong lobby password (private lobby).";

            Debug.Log($"[Lobby] ConnectionApproval | Approved={approved} | expectedPrivate={expectedIsPrivate} | spawnPrefabHash={prefabHash}");
        };
    }

    private static UnityTransport GetUnityTransport(NetworkManager nm)
    {
        if (nm == null)
            return null;

        if (nm.NetworkConfig.NetworkTransport is UnityTransport utp)
            return utp;

        return nm.GetComponent<UnityTransport>();
    }

    private void EnsureRuntimePlayerPrefab()
    {
        // NGO requires a prefab asset with a non-zero NetworkObject.GlobalObjectIdHash.
        // Runtime-created GameObjects typically have hash=0 -> spawning silently fails.
        //
        // We therefore:
        // 1) try to load Assets/Resources/LobbyRuntimePlayer.prefab
        // 2) if missing, create it as a prefab asset (Editor-only), then load again.

        const string resourcesName = "LobbyRuntimePlayer";

        var resourcesPrefab = Resources.Load<GameObject>(resourcesName);
        if (resourcesPrefab == null)
        {
#if UNITY_EDITOR
            resourcesPrefab = EnsureLobbyRuntimePlayerPrefabAsset(resourcesName);
#endif
        }

        if (resourcesPrefab != null)
        {
            _networkManager.NetworkConfig.PlayerPrefab = resourcesPrefab;

            if (_networkManager.NetworkConfig.Prefabs != null)
            {
                // Best-effort: register (safe if already registered).
                try
                {
                    _networkManager.AddNetworkPrefab(resourcesPrefab);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Lobby] AddNetworkPrefab(Resources/{resourcesName}) failed (likely already registered): {ex.Message}");
                }
            }

            Debug.Log("[Lobby] Using LobbyRuntimePlayer prefab from Resources.");
            return;
        }

        Debug.LogError($"[Lobby] Could not load/create Resources/{resourcesName}. Prefab spawn will likely fail.");
    }

#if UNITY_EDITOR
    private static GameObject EnsureLobbyRuntimePlayerPrefabAsset(string resourcesName)
    {
        const string prefabPath = "Assets/Resources/LobbyRuntimePlayer.prefab";

        // Try load existing prefab asset
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
            return existing;

        // Create folder if missing
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // Create a temp object using the same components as our runtime factory
        var temp = LobbyRuntimePlayerFactory.CreateRuntimePlayerPrefab();
        temp.name = "LobbyRuntimePlayer_TEMP";

        // Save as prefab asset (this is what triggers editor-generated GlobalObjectIdHash)
        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath, out bool success);
        GameObject.DestroyImmediate(temp);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!success || prefab == null)
        {
            Debug.LogError($"[Lobby] Failed to create prefab asset at {prefabPath}.");
            return null;
        }

        // Ensure it can be loaded from Resources
        return prefab;
    }
#endif

    private static byte[] EncodePayload(string password)
    {
        // Simple string payload: L1:<password>
        // Server decodes and compares.
        var token = PayloadPrefix + (password ?? "");
        return Encoding.UTF8.GetBytes(token);
    }

    private static string DecodePayload(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
            return "";

        var s = Encoding.UTF8.GetString(payload);
        if (string.IsNullOrEmpty(s))
            return "";

        if (s.StartsWith(PayloadPrefix, StringComparison.Ordinal))
            return s.Substring(PayloadPrefix.Length);

        // Backward/unknown format
        return s;
    }
}

public static class LobbyRuntimePlayerFactory
{
    public static GameObject CreateRuntimePlayerPrefab()
    {
        var root = new GameObject("RuntimeNetworkPlayer");

        // IMPORTANT: PickupItem expects Tag "Player"
        try { root.tag = "Player"; } catch { /* tag may not exist; ignore for now */ }

        // Must have NetworkObject so NGO can spawn it.
        root.AddComponent<NetworkObject>();

        // Player script is a NetworkBehaviour.
        root.AddComponent<Player>();

        // Player movement/collision requirements.
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.useGravity = true;

        var col = root.AddComponent<CapsuleCollider>();
        col.radius = 0.25f;
        col.height = 1.0f;

        // Weapon system required by PickupItem/WeaponSlots.
        var weaponSlots = root.AddComponent<WeaponSlots>();

        // Create slot transforms (WeaponSlots.AddWeapon will use these).
        var primarySlot = new GameObject("PrimarySlot").transform;
        var secondarySlot = new GameObject("SecondarySlot").transform;
        var meleeSlot = new GameObject("MeleeSlot").transform;

        primarySlot.SetParent(root.transform, false);
        secondarySlot.SetParent(root.transform, false);
        meleeSlot.SetParent(root.transform, false);

        // Basic local positions/rotations (adjust later if needed)
        primarySlot.localPosition = new Vector3(0.15f, 1.0f, 0.5f);
        primarySlot.localRotation = Quaternion.identity;

        secondarySlot.localPosition = new Vector3(-0.15f, 1.0f, 0.5f);
        secondarySlot.localRotation = Quaternion.identity;

        meleeSlot.localPosition = new Vector3(0.0f, 1.0f, 0.6f);
        meleeSlot.localRotation = Quaternion.identity;

        weaponSlots.primarySlot = primarySlot;
        weaponSlots.secondarySlot = secondarySlot;
        weaponSlots.meleeSlot = meleeSlot;

        // Existing scripts you already have.
        root.AddComponent<ClientNetworkTransform>();

        // Kein ClientNetworkAnimator hinzufügen: der Runtime-Player hat kein Animator und
        // sonst crasht Unity.Netcode.NetworkAnimator.
        return root;
    }
}
