using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Host information. 
    /// </summary>
    /// <remarks>
    ///   Standard values for CPU and OS can be found in [RFC-1010].
    ///
    ///   HINFO records are used to acquire general information about a host. The
    ///   main use is for protocols such as FTP that can use special procedures
    ///   when talking between machines or operating systems of the same type.
    /// </remarks>
    public class HINFORecord : ResourceRecord
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="HINFORecord"/> class.
        /// </summary>
        public HINFORecord() : base()
        {
            TYPE = 13;
        }

        /// <summary>
        ///  CPU type.
        /// </summary>
        public string CPU { get; set; }

        /// <summary>
        ///  Operating system type.
        /// </summary>
        public string OS { get; set; }


        /// <inheritdoc />
        protected override void ReadData(DnsReader reader, int length)
        {
            CPU = reader.ReadString();
            OS = reader.ReadString();
        }

        /// <inheritdoc />
        protected override void WriteData(DnsWriter writer)
        {
            writer.WriteString(CPU);
            writer.WriteString(OS);
        }
    }
}
