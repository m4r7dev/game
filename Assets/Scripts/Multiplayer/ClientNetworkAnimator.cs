using Unity.Netcode.Components;
using UnityEngine;

public class ClientNetworkAnimator : NetworkAnimator
{
    
    protected override void Awake()
    {
        // Your male prefabs appear to be missing an Animator component.
        // Unity.Netcode's NetworkAnimator will throw NullReferenceException otherwise.
        var animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"[ClientNetworkAnimator] Missing Animator on {gameObject.name}. Disabling component to avoid NetworkAnimator NRE.");
            enabled = false;
        }
    }

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
