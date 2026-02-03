using System;

namespace ModAPI.Core
{
    public enum SyncMode 
    {
        LocalOnly = 0,      // Client preference (Volume). Never synced. Not Locked.
        HostAuthoritative,  // Host dictates value. Clients lock UI.
        ClientOptional,     // Host default, Client can override.
    }
}
