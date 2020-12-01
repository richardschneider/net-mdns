using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Makaretu.Dns
{
	/// <summary>
	///   Extensions for <see cref="IPAddress"/>.
	/// </summary>
	public static class IPAddressExtensions
	{
		/// <summary>
		///   Gets the subnet mask associated with the IP address.
		/// </summary>
		/// <param name="address">
		///   An IP Addresses.
		/// </param>
		/// <returns>
		///   The subnet mask; ror example "127.0.0.1" returns "255.0.0.0".
		///   Or <b>null</b> When <paramref name="address"/> does not belong to 
		///   the localhost.
		/// s</returns>
		public static IPAddress GetSubnetMask(this IPAddress address)
		{
			return NetworkInterface.GetAllNetworkInterfaces()
								   .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
								   .Where(a => a.Address.Equals(address))
								   .Select(a => a.IPv4Mask)
								   .FirstOrDefault();
		}

		/// <summary>
		///   Determines if the local IP address can be used by the
		///   remote address.
		/// </summary>
		/// <param name="local"></param>
		/// <param name="remote"></param>
		/// <param name="searchOnNeighborAddresses">If other addresses from the same NIC should als be checked or not.</param>
		/// <param name="nics">The available nics on the system. Providing them reduces computation time.</param>
		/// <returns>
		///   <b>true</b> if <paramref name="local"/> can be used by <paramref name="remote"/>;
		///   otherwise, <b>false</b>.
		/// </returns>
		public static bool IsReachable(this IPAddress local, IPAddress remote, bool searchOnNeighborAddresses = true, NetworkInterface[] nics = null)
		{
			// Loopback addresses are only reachable when the remote is
			// the same host.
			if (local.Equals(IPAddress.Loopback) || local.Equals(IPAddress.IPv6Loopback))
			//if (IPAddress.IsLoopback(local) && IPAddress.IsLoopback(remote))
			{
				return MulticastService.GetIPAddresses().Contains(remote);
			}

			// IPv4 addresses are reachable when on the same subnet.
			if (local.AddressFamily == AddressFamily.InterNetwork && remote.AddressFamily == AddressFamily.InterNetwork)
			{
				var mask = local.GetSubnetMask();
				if (mask != null)
				{
					var network = IPNetwork.Parse(local, mask);
					return network.Contains(remote);
				}

				// A mask of null should not occur. Is the IP address available on the actual machine or are the local and remote addresses switched in the function call?
				return false;
			}

			// IPv6 link local addresses are reachable when using the same scope id.
			if (local.AddressFamily == AddressFamily.InterNetworkV6 && remote.AddressFamily == AddressFamily.InterNetworkV6)
			{
				if (local.IsIPv6LinkLocal && remote.IsIPv6LinkLocal)
				{
					return (local.ScopeId == remote.ScopeId);
				}

				// only interested in link local addresses
				return false;
			}


			// mix of IPv4 and IPv6 -> try if the remote is reachable from one of the other IP addresses of the same NIC
			if (!searchOnNeighborAddresses)
				return false;

			if(nics == null)
				nics = NetworkInterface.GetAllNetworkInterfaces();

			foreach (var nic in nics)
			{
				var localAddressInformation = nic.GetIPProperties().UnicastAddresses.Where(a => a.Address.Equals(local)).FirstOrDefault();
				if (localAddressInformation != null)
				{
					var otherAddresses = nic.GetIPProperties().UnicastAddresses.Where(a => a != localAddressInformation).Select(a => a.Address);
					return otherAddresses.Any(otherAddress => otherAddress.IsReachable(remote, searchOnNeighborAddresses: false, nics));
				}
			}

			// If nothing of the above matched, they are probably not reachable.
			return false;
		}
	}
}
