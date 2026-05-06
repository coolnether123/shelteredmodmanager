namespace ModAPI.Networking.Sessions
{
    public enum NetworkSessionState
    {
        Stopped = 0,
        Starting = 1,
        Listening = 2,
        Connecting = 3,
        Connected = 4,
        Disconnecting = 5,
        Failed = 6
    }
}
