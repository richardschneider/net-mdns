using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   The event data for <see cref="ServiceDiscovery.ServiceInstanceDiscovered"/>.
    /// </summary>
    public class ServiceInstanceDiscoveryEventArgs : MessageEventArgs
    {
        /// <summary>
        ///   The fully qualified name of the service instance.
        /// </summary>
        /// <value>
        ///   Typically of the form "<i>instance</i>._<i>service</i>._tcp.local".
        /// </value>
        /// <seealso cref="ServiceProfile.FullyQualifiedName"/>
        public DomainName ServiceInstanceName { get; set; }
    }
}

