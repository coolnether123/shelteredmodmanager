using System.IO;
using System.Web.Script.Serialization;
using Manager.Core.Models;
using GameModding.Shared.Restart;

namespace Manager.Core.Services
{
    public sealed class RestartRequestReader
    {
        public string GetRestartRequestPath(AppSettings settings)
        {
            if (settings == null || !settings.IsGamePathValid)
            {
                return string.Empty;
            }

            var gameDir = Path.GetDirectoryName(settings.GamePath);
            return Path.Combine(Path.Combine(Path.Combine(gameDir, "SMM"), "Bin"), "restart.json");
        }

        public bool TryRead(AppSettings settings, out RestartRequest request)
        {
            request = null;
            var path = GetRestartRequestPath(settings);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                request = new JavaScriptSerializer().Deserialize<RestartRequest>(json);
                return request != null;
            }
            catch
            {
                return false;
            }
        }

        public void Delete(AppSettings settings)
        {
            var path = GetRestartRequestPath(settings);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
