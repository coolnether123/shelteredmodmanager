using System;

namespace ModAPI.Core
{
    /// <summary>
    /// Multiplayer/settings ownership policy for a mod setting.
    /// Use this to document whether the host or each client controls the value.
    /// </summary>
    public enum SyncMode 
    {
        LocalOnly = 0,      // Client preference (Volume). Never synced. Not Locked.
        HostAuthoritative,  // Host dictates value. Clients lock UI.
        ClientOptional,     // Host default, Client can override.
    }
}
