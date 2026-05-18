using System.Collections.Generic;
using UnityEngine;

#if GAMEOBJECTS_NETCODE_2_AVAILABLE
using Unity.Netcode;
#endif

namespace Multiplayer
{
#if GAMEOBJECTS_NETCODE_2_AVAILABLE
    public class NetcodeGameBootstrap : MonoBehaviour
    {
        [Header("Player Prefab (must have NetworkObject + Player script)")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Map Select (Host decides)")]
        [SerializeField] private MapType chosenMap = MapType.Map1;

        [Header("Spawn Points for Map1")]
        [SerializeField] private Transform[] map1Spawns;

        private readonly Dictionary<ulong, int> clientToSpawnIndex = new();

        private void OnEnable()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("NetworkManager.Singleton ist null. Stelle sicher, dass in der Scene ein Netcode NetworkManager existiert.");
                return;
            }

            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;

            // If we're already server, ensure the host/local client gets a player object too.
            if (nm.IsServer)
            {
                HandleClientConnected(nm.LocalClientId);
            }
        }

        private void OnDisable()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
                return;

            nm.OnClientConnectedCallback -= HandleClientConnected;
            nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        public void SetChosenMap(MapType map)
        {
            chosenMap = map;
        }

        [ContextMenu("Host Start (Map1)")]
        public void HostStartMap1()
        {
            chosenMap = MapType.Map1;
            StartHostIfNeeded();
        }

        public void ClientStart()
        {
            StartClientIfNeeded();
        }

        private void StartHostIfNeeded()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (!nm.IsHost && !nm.IsServer)
            {
                nm.StartHost();
            }
        }

        private void StartClientIfNeeded()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (!nm.IsClient && !nm.IsHost)
            {
                nm.StartClient();
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            // Server/Host only: spawn player objects.
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return;

            if (!IsSpawnManagerReady())
                return;

            if (clientToSpawnIndex.ContainsKey(clientId))
                return;

            var spawnIndex = clientToSpawnIndex.Count;
            var spawn = GetSpawnTransform(spawnIndex);

            if (spawn == null)
            {
                Debug.LogError("Kein SpawnPoint gefunden. Prüfe map1Spawns (inspector).");
                return;
            }

            var instance = Instantiate(playerPrefab, spawn.position, spawn.rotation);
            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("playerPrefab hat kein NetworkObject-Komponenten. Bitte im Prefab hinzufügen.");
                Destroy(instance);
                return;
            }

            // Ownership to the connecting client:
            netObj.SpawnWithOwnership(clientId);

            clientToSpawnIndex.Add(clientId, spawnIndex);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientToSpawnIndex.ContainsKey(clientId))
            {
                clientToSpawnIndex.Remove(clientId);
            }
        }

        private bool IsSpawnManagerReady()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("playerPrefab ist nicht gesetzt (Inspector).");
                return false;
            }

            if (chosenMap == MapType.Map1 && (map1Spawns == null || map1Spawns.Length == 0))
            {
                Debug.LogError("map1Spawns ist leer (Inspector).");
                return false;
            }

            return true;
        }

        private Transform GetSpawnTransform(int spawnIndex)
        {
            switch (chosenMap)
            {
                case MapType.Map1:
                    if (map1Spawns.Length == 0) return null;
                    var idx = spawnIndex % map1Spawns.Length;
                    return map1Spawns[idx];
                default:
                    return null;
            }
        }
    }
#else
    // Netcode for GameObjects ist in deinem Projekt aktuell NICHT verfügbar.
    // Diese Dummy-Klasse sorgt nur dafür, dass das Projekt kompiliert.
    public class NetcodeGameBootstrap : MonoBehaviour { }
#endif
}
