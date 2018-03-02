using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   The event data for <see cref="MulticastService.NetworkInterfaceDiscovered"/>.
    /// </summary>
    public class NetworkInterfaceEventArgs : EventArgs
    {
        /// <summary>
        ///   The sequece of detected network interfaces.
        /// </summary>
        /// <value>
        ///   A sequence of network interfaces.
        /// </value>
        public IEnumerable<NetworkInterface> NetworkInterfaces { get; set; }
    }
}

