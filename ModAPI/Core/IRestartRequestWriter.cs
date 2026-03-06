namespace ModAPI.Core
{
    public interface IRestartRequestWriter
    {
        bool WriteRequest(string manifestPath, out string restartPath, out string errorMessage);
        bool WriteCurrentSessionRequest(out string restartPath, out string errorMessage);
    }
}
