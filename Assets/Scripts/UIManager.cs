using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UIManager : MonoBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocumentBinder uiBinder;

    [Header("Presentation")]
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private string cameraObjectName = "Main Camera";
    [SerializeField] private Vector3 playerMenuOffset = new Vector3(2.2f, 0f, 0.8f);
    [SerializeField] private Vector3 cameraMenuPosition = new Vector3(-1.4f, 1.5f, -4.2f);
    [SerializeField] private Vector3 cameraMenuEuler = new Vector3(12f, 25f, 0f);

    [Header("Local Server Demo")]
    [SerializeField] private float serverListRefreshSeconds = 15f;

    private GameObject playerGo;
    private Camera mainCam;

    private Vector3 originalPlayerPos;
    private Quaternion originalPlayerRot;
    private Vector3 originalCameraPos;
    private Quaternion originalCameraRot;

    private enum Panel
    {
        Main,
        Host,
        ServerList,
        Settings,
        StartOverlay
    }

    private Panel currentPanel = Panel.Main;

    private MenuSettingsManager settingsManager;
    private LocalServerRegistry serverRegistry;

    private Label startStatusLabel;

    private void Awake()
    {
        settingsManager = new MenuSettingsManager();
        serverRegistry = new LocalServerRegistry();

        if (uiBinder == null)
            uiBinder = FindFirstObjectByType<UIDocumentBinder>();
    }

    private void Start()
    {
        settingsManager.LoadAndApply();
        CacheWorldReferences();
        CaptureOriginalTransforms();
        PositionForMenu();
        BindUI();

        ShowPanel(Panel.Main);
        StartCoroutine(ServerListRefreshLoop());
    }

    private void CacheWorldReferences()
    {
        playerGo = GameObject.Find(playerObjectName);
        var camGo = GameObject.Find(cameraObjectName);
        if (camGo != null)
            mainCam = camGo.GetComponent<Camera>();
    }

    private void CaptureOriginalTransforms()
    {
        if (playerGo != null)
        {
            originalPlayerPos = playerGo.transform.position;
            originalPlayerRot = playerGo.transform.rotation;
        }

        if (mainCam != null)
        {
            originalCameraPos = mainCam.transform.position;
            originalCameraRot = mainCam.transform.rotation;
        }
    }

    private void PositionForMenu()
    {
        if (playerGo == null || mainCam == null)
            return;

        playerGo.transform.position = mainCam.transform.TransformPoint(playerMenuOffset);

        var toCam = (mainCam.transform.position - playerGo.transform.position);
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 0.0001f)
            playerGo.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);

        mainCam.transform.position = cameraMenuPosition;
        mainCam.transform.rotation = Quaternion.Euler(cameraMenuEuler);
    }

    private void BindUI()
    {
        if (uiBinder == null)
        {
            Debug.LogError("UIManager: uiBinder is missing. Add a UIDocumentBinder component to your UI root.");
            return;
        }

        uiBinder.EnsureBound();

        // cache start overlay label
        startStatusLabel = uiBinder.StartOverlayPanel.Q<Label>("startStatusLabel");
        if (startStatusLabel == null)
            Debug.LogError("UIManager: Missing Label 'startStatusLabel' in StartOverlayPanel UXML.");

        // Main menu
        uiBinder.Main.HostButton.clicked += () => ShowPanel(Panel.Host);
        uiBinder.Main.ServerListButton.clicked += () => ShowPanel(Panel.ServerList);
        uiBinder.Main.SettingsButton.clicked += () => ShowPanel(Panel.Settings);
        uiBinder.Main.ExitButton.clicked += () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        };

        // Host panel
        uiBinder.Host.PublicToggle.RegisterValueChangedCallback(_ =>
        {
            uiBinder.Host.PasswordContainer.style.display = uiBinder.Host.PublicToggle.value ? DisplayStyle.None : DisplayStyle.Flex;
        });

        uiBinder.Host.StartButton.clicked += () =>
        {
            var lobbyName = uiBinder.Host.LobbyNameField.value;
            var playerCount = uiBinder.Host.PlayerCountField.value;

            var isPublic = uiBinder.Host.PublicToggle.value;
            var password = uiBinder.Host.PasswordField.value;

            if (string.IsNullOrWhiteSpace(lobbyName))
            {
                uiBinder.Host.ErrorLabel.text = "Lobby Name fehlt.";
                return;
            }

            if (playerCount < 2 || playerCount > 4)
            {
                uiBinder.Host.ErrorLabel.text = "Spieleranzahl muss zwischen 2 und 4 liegen.";
                return;
            }

            if (!isPublic && string.IsNullOrWhiteSpace(password))
            {
                uiBinder.Host.ErrorLabel.text = "Passwort ist erforderlich (private Lobby).";
                return;
            }

            uiBinder.Host.ErrorLabel.text = string.Empty;

            // Host is server: create local server entry (stub)
            serverRegistry.HostServer(new LocalServerRegistry.ServerInfo
            {
                id = Guid.NewGuid().ToString("N"),
                lobbyName = lobbyName.Trim(),
                playerCapacity = playerCount,
                isPublic = isPublic,
                password = isPublic ? string.Empty : password,
                mapName = "Map1"
            });

            ShowPanel(Panel.StartOverlay);
            if (startStatusLabel != null)
                startStatusLabel.text = "Server gestartet. Map wird geladen…";

            StopAllCoroutines();
            StartCoroutine(FakeStartThenBackToMain());
        };

        uiBinder.Host.BackButton.clicked += () => ShowPanel(Panel.Main);

        // Server list panel
        uiBinder.ServerList.BackButton.clicked += () => ShowPanel(Panel.Main);

        uiBinder.ServerList.SearchField.RegisterValueChangedCallback(evt =>
        {
            serverRegistry.SetSearchQuery(evt.newValue);
            RefreshServerListNow();
        });

        uiBinder.ServerList.PublicOnlyToggle.RegisterValueChangedCallback(evt =>
        {
            serverRegistry.SetPublicFilter(evt.newValue);
            RefreshServerListNow();
        });

        // Settings panel
        uiBinder.Settings.BackButton.clicked += () => ShowPanel(Panel.Main);

        uiBinder.Settings.FullscreenModeDropdown.RegisterValueChangedCallback(_ =>
        {
            settingsManager.SetFullscreenMode(uiBinder.Settings.FullscreenModeDropdown.value);
        });

        uiBinder.Settings.ResolutionDropdown.RegisterValueChangedCallback<string>(evt =>
        {
            settingsManager.SetResolution(evt.newValue);
        });

        uiBinder.Settings.FpsCapDropdown.RegisterValueChangedCallback<string>(evt =>
        {
            settingsManager.SetFpsCap(evt.newValue);
        });

        uiBinder.Settings.ApplyButton.clicked += () =>
        {
            settingsManager.ApplyAndSave();
            uiBinder.Settings.ApplyLabel.text = "Settings gespeichert.";
        };

        uiBinder.Settings.ApplyDefaultButton.clicked += () =>
        {
            settingsManager.ApplyDefaults();
            settingsManager.ApplyAndSave();
            uiBinder.Settings.SyncFromSettings(settingsManager);
            uiBinder.Settings.ApplyLabel.text = "Defaults aktiv.";
        };

        uiBinder.Settings.SyncFromSettings(settingsManager);
        RefreshServerListNow();
    }

    private IEnumerator FakeStartThenBackToMain()
    {
        yield return new WaitForSeconds(1.2f);
        if (startStatusLabel != null)
            startStatusLabel.text = "Bereit. (Demo)";

        yield return new WaitForSeconds(1.2f);
        ShowPanel(Panel.Main);
    }

    private void ShowPanel(Panel panel)
    {
        currentPanel = panel;

        uiBinder.MainPanel.style.display = panel == Panel.Main ? DisplayStyle.Flex : DisplayStyle.None;
        uiBinder.HostPanel.style.display = panel == Panel.Host ? DisplayStyle.Flex : DisplayStyle.None;
        uiBinder.ServerListPanel.style.display = panel == Panel.ServerList ? DisplayStyle.Flex : DisplayStyle.None;
        uiBinder.SettingsPanel.style.display = panel == Panel.Settings ? DisplayStyle.Flex : DisplayStyle.None;
        uiBinder.StartOverlayPanel.style.display = panel == Panel.StartOverlay ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private IEnumerator ServerListRefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(serverListRefreshSeconds);
            if (currentPanel == Panel.ServerList)
                RefreshServerListNow();
        }
    }

    private void RefreshServerListNow()
    {
        var servers = serverRegistry.GetServers()
            .OrderBy(s => s.isPublic ? 0 : 1)
            .ThenByDescending(s => s.playerCapacity)
            .ThenBy(s => s.lobbyName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        uiBinder.ServerList.ListView.itemsSource = servers;

        uiBinder.ServerList.ListView.makeItem = () =>
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.height = 34;

            var left = new VisualElement();
            left.style.flexGrow = 1;

            var title = new Label();
            title.name = "title";
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.fontSize = 14;
            title.style.color = new StyleColor(Color.white);
            title.style.minWidth = 220;

            var meta = new Label();
            meta.name = "meta";
            meta.style.unityTextAlign = TextAnchor.MiddleLeft;
            meta.style.fontSize = 12;
            meta.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.75f, 1f));

            left.Add(title);
            left.Add(meta);

            var right = new VisualElement();
            right.style.flexShrink = 0;
            right.style.flexDirection = FlexDirection.Row;

            var typeTag = new Label();
            typeTag.name = "typeTag";
            typeTag.style.fontSize = 12;
            typeTag.style.paddingTop = 2;
            typeTag.style.paddingBottom = 2;
            typeTag.style.paddingLeft = 8;
            typeTag.style.paddingRight = 8;
            typeTag.style.unityTextAlign = TextAnchor.MiddleCenter;
            typeTag.style.borderTopWidth = 2;
            typeTag.style.borderLeftWidth = 2;
            typeTag.style.borderRightWidth = 2;
            typeTag.style.borderBottomWidth = 2;
            typeTag.style.borderTopColor = new Color(1f, 1f, 1f, 1f);

            right.Add(typeTag);

            row.Add(left);
            row.Add(right);

            return row;
        };

        uiBinder.ServerList.ListView.bindItem = (e, i) =>
        {
            var server = servers[i];
            var title = e.Q<Label>("title");
            var meta = e.Q<Label>("meta");
            var typeTag = e.Q<Label>("typeTag");

            title.text = server.lobbyName;
            meta.text = $"{server.playerCapacity} Spieler • {server.mapName}";

            var isPublic = server.isPublic;
            typeTag.text = isPublic ? "PUBLIC" : "PRIVATE";
            typeTag.style.backgroundColor = isPublic ? new Color(0.15f, 0.55f, 0.2f, 0.9f) : new Color(0.6f, 0.15f, 0.15f, 0.9f);
            typeTag.style.borderTopColor = Color.white;
        };

        uiBinder.ServerList.EmptyLabel.style.display = servers.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // UI binding helper
    [Serializable]
    public sealed class UIDocumentBinder : MonoBehaviour
    {
        [Header("UXML")]
        [SerializeField] private VisualTreeAsset rootAsset;

        [Header("Root UXML IDs")]
        [SerializeField] private string mainPanelName = "MainPanel";
        [SerializeField] private string hostPanelName = "HostPanel";
        [SerializeField] private string serverListPanelName = "ServerListPanel";
        [SerializeField] private string settingsPanelName = "SettingsPanel";
        [SerializeField] private string startOverlayPanelName = "StartOverlayPanel";

        private VisualElement root;

        public MainUIRefs Main { get; private set; }
        public HostUIRefs Host { get; private set; }
        public ServerListUIRefs ServerList { get; private set; }
        public SettingsUIRefs Settings { get; private set; }

        public VisualElement MainPanel => root.Q<VisualElement>(mainPanelName);
        public VisualElement HostPanel => root.Q<VisualElement>(hostPanelName);
        public VisualElement ServerListPanel => root.Q<VisualElement>(serverListPanelName);
        public VisualElement SettingsPanel => root.Q<VisualElement>(settingsPanelName);
        public VisualElement StartOverlayPanel => root.Q<VisualElement>(startOverlayPanelName);

        public void EnsureBound()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null)
            {
                Debug.LogError("UIDocumentBinder: UIDocument component missing.");
                return;
            }

            root = doc.rootVisualElement;

            // Hard reset: UXML Tree explizit in den Root klonen und hinzufügen.
            // Das verhindert Fälle, in denen UIDocument.visualTreeAsset zwar gesetzt ist,
            // aber die UI im Root beim Runtime-Render noch nicht sichtbar/nachladbar ist.
            root.Clear();

            if (rootAsset == null)
            {
                Debug.LogError("UIDocumentBinder: rootAsset (MainMenu.uxml) ist null.");
                return;
            }

            var cloned = rootAsset.CloneTree();
            if (cloned == null)
            {
                Debug.LogError("UIDocumentBinder: rootAsset.CloneTree() ist null.");
                return;
            }

            root.Add(cloned);

            Main = new MainUIRefs(root);
            Host = new HostUIRefs(root);
            ServerList = new ServerListUIRefs(root);
            Settings = new SettingsUIRefs(root);
        }
    }

    public sealed class MainUIRefs
    {
        public Button HostButton { get; }
        public Button ServerListButton { get; }
        public Button SettingsButton { get; }
        public Button ExitButton { get; }

        public MainUIRefs(VisualElement root)
        {
            HostButton = root.Q<Button>("btnHost");
            ServerListButton = root.Q<Button>("btnServerList");
            SettingsButton = root.Q<Button>("btnSettings");
            ExitButton = root.Q<Button>("btnExit");

            if (HostButton == null || ServerListButton == null || SettingsButton == null || ExitButton == null)
                Debug.LogError("MainUIRefs: missing one or more buttons in UXML.");
        }
    }

    public sealed class HostUIRefs
    {
        public IntegerField PlayerCountField { get; }
        public TextField LobbyNameField { get; }
        public Toggle PublicToggle { get; }

        public VisualElement PasswordContainer { get; }
        public TextField PasswordField { get; }

        public Button StartButton { get; }
        public Label ErrorLabel { get; }
        public Button BackButton { get; }

        public HostUIRefs(VisualElement root)
        {
            PlayerCountField = root.Q<IntegerField>("hostPlayerCount");
            LobbyNameField = root.Q<TextField>("hostLobbyName");
            PublicToggle = root.Q<Toggle>("hostPublicToggle");

            PasswordContainer = root.Q<VisualElement>("hostPasswordContainer");
            PasswordField = root.Q<TextField>("hostPasswordField");

            StartButton = root.Q<Button>("btnHostStart");
            ErrorLabel = root.Q<Label>("hostError");
            BackButton = root.Q<Button>("btnHostBack");

            if (PlayerCountField == null || LobbyNameField == null || PublicToggle == null ||
                PasswordContainer == null || PasswordField == null || StartButton == null ||
                ErrorLabel == null || BackButton == null)
            {
                Debug.LogError("HostUIRefs: missing UI elements in UXML.");
            }

            PasswordField.isPasswordField = true;
        }
    }

    public sealed class ServerListUIRefs
    {
        public TextField SearchField { get; }
        public Toggle PublicOnlyToggle { get; }

        public ListView ListView { get; }
        public Label EmptyLabel { get; }

        public Button BackButton { get; }

        public ServerListUIRefs(VisualElement root)
        {
            SearchField = root.Q<TextField>("serverSearch");
            PublicOnlyToggle = root.Q<Toggle>("serverPublicOnly");

            ListView = root.Q<ListView>("serverListView");
            EmptyLabel = root.Q<Label>("serverEmptyLabel");

            BackButton = root.Q<Button>("btnServerListBack");

            if (SearchField == null || PublicOnlyToggle == null || ListView == null || EmptyLabel == null || BackButton == null)
                Debug.LogError("ServerListUIRefs: missing UI elements in UXML.");
        }
    }

    public sealed class SettingsUIRefs
    {
        public Button BackButton { get; }
        public DropdownField FullscreenModeDropdown { get; }
        public DropdownField ResolutionDropdown { get; }
        public DropdownField FpsCapDropdown { get; }

        public Button ApplyButton { get; }
        public Button ApplyDefaultButton { get; }
        public Label ApplyLabel { get; }

        public SettingsUIRefs(VisualElement root)
        {
            BackButton = root.Q<Button>("btnSettingsBack");
            FullscreenModeDropdown = root.Q<DropdownField>("settingsFullscreenMode");
            ResolutionDropdown = root.Q<DropdownField>("settingsResolution");
            FpsCapDropdown = root.Q<DropdownField>("settingsFpsCap");
            ApplyButton = root.Q<Button>("btnSettingsApply");
            ApplyDefaultButton = root.Q<Button>("btnSettingsDefaults");
            ApplyLabel = root.Q<Label>("settingsApplyLabel");

            if (BackButton == null || FullscreenModeDropdown == null || ResolutionDropdown == null || FpsCapDropdown == null ||
                ApplyButton == null || ApplyDefaultButton == null || ApplyLabel == null)
            {
                Debug.LogError("SettingsUIRefs: missing UI elements in UXML.");
            }

            if (FullscreenModeDropdown.choices != null && FullscreenModeDropdown.choices.Count == 0)
                FullscreenModeDropdown.choices = new List<string> { "Fullscreen", "Windowed" };

            if (ResolutionDropdown.choices != null && ResolutionDropdown.choices.Count == 0)
                ResolutionDropdown.choices = new List<string> { "720p", "1080p", "1440p" };

            if (FpsCapDropdown.choices != null && FpsCapDropdown.choices.Count == 0)
                FpsCapDropdown.choices = new List<string> { "Uncapped", "60", "120", "144", "165", "240" };
        }

        public void SyncFromSettings(MenuSettingsManager mgr)
        {
            FullscreenModeDropdown.value = mgr.FullscreenModeDisplay;
            ResolutionDropdown.value = mgr.ResolutionDisplay;
            FpsCapDropdown.value = mgr.FpsCapDisplay;
        }
    }

    public sealed class MenuSettingsManager
    {
        private const string Pref_FullscreenMode = "menu.fullscreenMode";
        private const string Pref_Resolution = "menu.resolution";
        private const string Pref_FpsCap = "menu.fpsCap";

        public string FullscreenModeDisplay { get; private set; } = "Fullscreen";
        public string ResolutionDisplay { get; private set; } = "1080p";
        public string FpsCapDisplay { get; private set; } = "Uncapped";

        private FullScreenMode storedFullscreenMode = FullScreenMode.FullScreenWindow;
        private Resolution storedResolution = new Resolution { width = 1920, height = 1080, refreshRate = 60 };
        private int storedFpsCap = -1;

        public void LoadAndApply()
        {
            FullscreenModeDisplay = PlayerPrefs.GetString(Pref_FullscreenMode, "Fullscreen");
            ResolutionDisplay = PlayerPrefs.GetString(Pref_Resolution, "1080p");
            FpsCapDisplay = PlayerPrefs.GetString(Pref_FpsCap, "Uncapped");

            ApplyFromDisplay();
            Apply();
        }

        public void ApplyDefaults()
        {
            FullscreenModeDisplay = "Fullscreen";
            ResolutionDisplay = "1080p";
            FpsCapDisplay = "Uncapped";

            ApplyFromDisplay();
        }

        public void SetFullscreenMode(string display)
        {
            FullscreenModeDisplay = display;
            ApplyFromDisplay();
        }

        public void SetResolution(string display)
        {
            ResolutionDisplay = display;
            ApplyFromDisplay();
        }

        public void SetFpsCap(string display)
        {
            FpsCapDisplay = display;
            ApplyFromDisplay();
        }

        private void ApplyFromDisplay()
        {
            storedFullscreenMode = FullscreenModeDisplay == "Windowed"
                ? FullScreenMode.Windowed
                : FullScreenMode.FullScreenWindow;

            var isFullscreen = storedFullscreenMode != FullScreenMode.Windowed;

            storedResolution = ResolutionDisplay switch
            {
                "720p" => new Resolution { width = 1280, height = 720, refreshRate = 60 },
                "1440p" => new Resolution { width = 2560, height = 1440, refreshRate = 60 },
                _ => new Resolution { width = 1920, height = 1080, refreshRate = 60 },
            };

            if (FpsCapDisplay == "Uncapped")
                storedFpsCap = -1;
            else if (int.TryParse(FpsCapDisplay, out var fps))
                storedFpsCap = Mathf.Clamp(fps, 0, 10000);
            else
                storedFpsCap = -1;
        }

        public void ApplyAndSave()
        {
            Apply();
            PlayerPrefs.SetString(Pref_FullscreenMode, FullscreenModeDisplay);
            PlayerPrefs.SetString(Pref_Resolution, ResolutionDisplay);
            PlayerPrefs.SetString(Pref_FpsCap, FpsCapDisplay);
            PlayerPrefs.Save();
        }

        private void Apply()
        {
            Screen.fullScreenMode = storedFullscreenMode;
            Screen.SetResolution(storedResolution.width, storedResolution.height, storedFullscreenMode != FullScreenMode.Windowed, storedResolution.refreshRate);
            Application.targetFrameRate = storedFpsCap <= 0 ? -1 : storedFpsCap;
        }
    }

    private sealed class LocalServerRegistry
    {
        public sealed class ServerInfo
        {
            public string id;
            public string lobbyName;
            public int playerCapacity;
            public bool isPublic;
            public string password;
            public string mapName;
        }

        private readonly List<ServerInfo> servers = new List<ServerInfo>();
        private string searchQuery = string.Empty;
        private bool publicFilter = false;

        public void HostServer(ServerInfo server)
        {
            servers.Add(server);
        }

        public void SetSearchQuery(string query)
        {
            searchQuery = query ?? string.Empty;
        }

        public void SetPublicFilter(bool publicOnly)
        {
            publicFilter = publicOnly;
        }

        public IEnumerable<ServerInfo> GetServers()
        {
            var q = searchQuery.Trim();
            IEnumerable<ServerInfo> result = servers;

            if (publicFilter)
                result = result.Where(s => s.isPublic);

            if (!string.IsNullOrWhiteSpace(q))
            {
                result = result.Where(s =>
                    (!string.IsNullOrWhiteSpace(s.lobbyName) &&
                     s.lobbyName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    s.playerCapacity.ToString().Contains(q) ||
                    s.mapName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                );
            }

            return result;
        }
    }
}
