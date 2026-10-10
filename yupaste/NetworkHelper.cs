using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Yupaste;

public static class NetworkHelper
{
    /// <summary>
    /// Returns the IPv4 addresses of active, non-loopback, non-link-local network
    /// interfaces — the addresses other devices on the same LAN could use to reach
    /// this machine.
    /// </summary>
    public static List<string> GetLanIPv4Addresses()
    {
        var addresses = new List<string>();

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            foreach (var addrInfo in nic.GetIPProperties().UnicastAddresses)
            {
                if (addrInfo.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                string ip = addrInfo.Address.ToString();
                if (ip.StartsWith("169.254.")) continue; // link-local / no DHCP

                addresses.Add(ip);
            }
        }

        return addresses;
    }
}
