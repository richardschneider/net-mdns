using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Metadata on resource records.
    /// </summary>
    /// <see cref="ResourceRecord"/>
    public static class ResourceRegistry
    {
        /// <summary>
        ///   All the resource records.
        /// </summary>
        /// <remarks>
        ///   The key is the DNS Resource Record TYPE.
        ///   The value is a function that returns a new <see cref="ResourceRecord"/>.
        /// </remarks>
        public static Dictionary<int, Func<ResourceRecord>> Records;

        static ResourceRegistry()
        {
            Records = new Dictionary<int, Func<ResourceRecord>>();
            Register<ARecord>();
            Register<AAAARecord>();
            Register<CNAMERecord>();
            Register<DNAMERecord>();
            Register<HINFORecord>();
            Register<MXRecord>();
            Register<NSECRecord>();
            Register<NSRecord>();
            Register<NULLRecord>();
            Register<PTRRecord>();
            Register<SOARecord>();
            Register<SRVRecord>();
            Register<TXTRecord>();
        }

        /// <summary>
        ///   Register a new resource record.
        /// </summary>
        /// <typeparam name="T">
        ///   A derived class of <see cref="ResourceRecord"/>.
        /// </typeparam>
        /// <exception cref="ArgumentException">
        ///   When RR TYPE is zero.
        /// </exception>
        public static void Register<T>() where T : ResourceRecord, new()
        {
            var rr = new T();
            if (rr.Type == 0)
            {
                throw new ArgumentException("The RR TYPE is not defined", "TYPE");
            }
            Records.Add(rr.Type, () => new T());
        }

    }
}
