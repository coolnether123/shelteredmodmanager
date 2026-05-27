using System.Collections.Generic;
using System.Net;

namespace ModAPI.Networking.Discovery
{
    public sealed class NetworkDiscoveryResultCollection
    {
        private readonly Dictionary<string, NetworkDiscoveryResult> _results =
            new Dictionary<string, NetworkDiscoveryResult>();

        public int Count { get { return _results.Count; } }

        public void AddOrUpdate(NetworkDiscoveryResult result)
        {
            if (result == null || result.EndPoint == null)
                return;

            _results[CreateKey(result.EndPoint)] = result;
        }

        public NetworkDiscoveryResult[] ToArray()
        {
            NetworkDiscoveryResult[] array = new NetworkDiscoveryResult[_results.Count];
            _results.Values.CopyTo(array, 0);
            return array;
        }

        private static string CreateKey(IPEndPoint endPoint)
        {
            return endPoint.Address.ToString() + ":" + endPoint.Port;
        }
    }
}
