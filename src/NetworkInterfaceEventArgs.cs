using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace Makaretu.Mdns
{
    /// <summary>
    ///   The event data for <see cref="MdnsService.NetworkInterfaceDiscovered"/>.
    /// </summary>
    public class NetworkInterfaceEventArgs : EventArgs
    {
        /// <summary>
        ///   The sequece of detected network interfaces.
        /// </summary>
        public IEnumerable<NetworkInterface> NetworkInterfaces { get; set; }
    }
}

