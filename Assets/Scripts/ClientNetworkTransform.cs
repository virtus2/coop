using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Used for syncing a transform with client side changes.
/// This includes host. Pure server as owner isn't supported by this class.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>
    /// Used to determine who can write to this transform.
    /// Returns false for client authoritative transform.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
