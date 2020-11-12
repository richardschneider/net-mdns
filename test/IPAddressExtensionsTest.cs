using Makaretu.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Makaretu.Dns
{

	[TestClass]
	public class IPAddressExtensionsTest
	{
		[TestMethod]
		public void SubnetMask_Ipv4Loopback()
		{
			var mask = IPAddress.Loopback.GetSubnetMask();
			Assert.AreEqual(IPAddress.Parse("255.0.0.0"), mask);
			var network = IPNetwork.Parse(IPAddress.Loopback, mask);
			Console.Write(network.ToString());
		}

		[TestMethod]
		[TestCategory("IPv6")]
		public void SubnetMask_Ipv6Loopback()
		{
			var mask = IPAddress.IPv6Loopback.GetSubnetMask();
			Assert.AreEqual(IPAddress.Parse("0.0.0.0"), mask);
		}

		[TestMethod]
		public void SubmetMask_NotLocalhost()
		{
			var mask = IPAddress.Parse("1.1.1.1").GetSubnetMask();
			Assert.IsNull(mask);
		}

		[TestMethod]
		public void SubnetMask_All()
		{
			foreach (var a in MulticastService.GetIPAddresses())
			{
				var network = IPNetwork.Parse(a, a.GetSubnetMask());

				Console.WriteLine($"{a} mask {a.GetSubnetMask()} {network}");

				Assert.IsTrue(network.Contains(a), $"{a} is not reachable");
			}
		}

		[TestMethod]
		public void LinkLocal()
		{
			foreach (var a in MulticastService.GetIPAddresses())
			{
				Console.WriteLine($"{a} ll={a.IsIPv6LinkLocal} ss={a.IsIPv6SiteLocal}");
			}
		}

		[TestMethod]
		public void Reachable_Loopback_From_Localhost()
		{
			var me = IPAddress.Loopback;
			foreach (var a in MulticastService.GetIPAddresses())
			{
				Assert.IsTrue(me.IsReachable(a), $"{a}");
			}
			Assert.IsFalse(me.IsReachable(IPAddress.Parse("1.1.1.1")));
			Assert.IsFalse(me.IsReachable(IPAddress.Parse("2606:4700:4700::1111")));

			me = IPAddress.IPv6Loopback;
			foreach (var a in MulticastService.GetIPAddresses())
			{
				Assert.IsTrue(me.IsReachable(a), $"{a}");
			}
			Assert.IsFalse(me.IsReachable(IPAddress.Parse("1.1.1.1")));
			Assert.IsFalse(me.IsReachable(IPAddress.Parse("2606:4700:4700::1111")));
		}

		[TestMethod]
		public void Reachable_Ipv4()
		{
			var me = MulticastService.GetIPAddresses()
				.First(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
			Assert.IsTrue(me.IsReachable(me));
			Assert.IsFalse(me.IsReachable(IPAddress.Parse("1.1.1.1")));

			//var nat = IPAddress.Parse("165.84.19.151"); // NAT PCP assigned address
			//Assert.IsTrue(nat.IsReachable(IPAddress.Parse("1.1.1.1")));
		}

		[TestMethod]
		public void Reachable_Ipv6_LinkLocal()
		{
			var me1 = IPAddress.Parse("fe80::1:2:3:4%1");
			var me2 = IPAddress.Parse("fe80::1:2:3:4%2");
			var me5 = IPAddress.Parse("fe80::1:2:3:5%1");
			Assert.IsTrue(me1.IsReachable(me1));
			Assert.IsTrue(me2.IsReachable(me2));
			Assert.IsFalse(me1.IsReachable(me2));
			Assert.IsFalse(me1.IsReachable(me5));
		}
	}
}