using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Defines a service that can be discovered.
    /// </summary>
    public class ServiceProfile
    {
        public ServiceProfile()
        {

        }

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

        public string Domain { get; set; } = "local";

        public string ServiceName { get; set; }

        public string InstanceName { get; set; }

        public string QualifiedServiceName => $"{ServiceName}.{Domain}";

        public string FullyQualifiedName => $"{InstanceName}.{QualifiedServiceName}";

        public List<ResourceRecord> Resources { get; set; } = new List<ResourceRecord>();
    }
}
