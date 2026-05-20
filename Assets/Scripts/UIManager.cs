using UnityEngine;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    public void StartGame()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void JoinGame()
    {
        NetworkManager.Singleton.StartClient();
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}