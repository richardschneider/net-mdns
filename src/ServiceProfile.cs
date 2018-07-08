using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Defines a specific service that can be discovered.
    /// </summary>
    /// <seealso cref="ServiceDiscovery.Advertise(ServiceProfile)"/>
    public class ServiceProfile
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        /// <remarks>
        ///   All details must be filled in by the caller.
        /// </remarks>
        public ServiceProfile()
        {
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class
        ///   with the specified details.
        /// </summary>
        /// <param name="instanceName">
        ///    A unique identifier for the specific service instance.
        /// </param>
        /// <param name="serviceName">
        ///   The <see cref="ServiceName">name</see> of the service.
        /// </param>
        /// <param name="port">
        ///   The TCP/UDP port of the service.
        /// </param>
        /// <param name="addresses">
        ///   The IP addresses of the specific service instance.
        /// </param>
        public ServiceProfile(string instanceName, string serviceName, ushort port, IEnumerable<IPAddress> addresses = null)
        {
            InstanceName = instanceName;
            ServiceName = serviceName;
            var fqn = FullyQualifiedName;

            Resources.Add(new SRVRecord
            {
                Name = fqn,
                Port = port,
                Target = fqn
            });
            Resources.Add(new TXTRecord
            {
                Name = fqn,
                Strings = { "foo=bar" }
            });

            if (addresses == null)
            {
                addresses = new MulticastService().GetIPAddresses();
            }
            foreach (var address in addresses)
            {
                switch (address.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        Resources.Add(new ARecord { Name = fqn, Address = address });
                        break;
                    case AddressFamily.InterNetworkV6:
                        if (address.ScopeId == 0)
                        {
                            Resources.Add(new AAAARecord { Name = fqn, Address = address });
                        }
                        break;
                    default:
                        throw new  NotSupportedException();
                }
            }
        }

        /// <summary>
        ///   The domain name of the service.
        /// </summary>
        /// <value>
        ///   Defaults to "local".
        /// </value>
        public string Domain { get; } = "local";

        /// <summary>
        ///   A unique name for the service.
        /// </summary>
        /// <value>
        ///   Typically of the form "_<i>service</i>._tcp".
        /// </value>
        /// <remarks>
        ///   It consists of a pair of DNS labels, following the
        ///   <see href="https://www.ietf.org/rfc/rfc2782.txt">SRV records</see> convention.
        ///   The first label of the pair is an underscore character (_) followed by 
        ///   the <see cref="https://tools.ietf.org/html/rfc6335">service name</see>. 
        ///   The second label is either "_tcp" (for application
        ///   protocols that run over TCP) or "_udp" (for all others). 
        /// </remarks>
        public string ServiceName { get; set; }

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public string InstanceName { get; set; }

        /// <summary>
        ///   The service name and domain.
        /// </summary>
        /// <value>
        ///   <see cref="ServiceName"/>.<see cref="Domain"/>
        /// </value>
        public string QualifiedServiceName => $"{ServiceName}.{Domain}";

        /// <summary>
        ///   The instance name, service name and domain.
        /// </summary>
        /// <value>
        ///   <see cref="InstanceName"/>.<see cref="ServiceName"/>.<see cref="Domain"/>
        /// </value>
        public string FullyQualifiedName => $"{InstanceName}.{QualifiedServiceName}";

        /// <summary>
        ///   DNS resource records that are used to locate the service instance.
        /// </summary>
        /// <remarks>
        ///   All records should have the <see cref="ResourceRecord.Name"/> equal
        ///   to the <see cref="FullyQualifiedName"/>.
        ///   <para>
        ///   At a minimum the SRV and TXT records must be present.  Typically A/AAAA
        ///   records are also present.
        ///   </para>
        /// </remarks>
        public List<ResourceRecord> Resources { get; set; } = new List<ResourceRecord>();
    }
}
