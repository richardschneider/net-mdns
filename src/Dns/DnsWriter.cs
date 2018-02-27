using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Methods to write DNS data items.
    /// </summary>
    public class DnsWriter
    {
        Stream stream;

        /// <summary>
        ///   Creates a new instance of the <see cref="DnsWriter"/> on the
        ///   specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        ///   The destination for data items.
        /// </param>
        public DnsWriter(Stream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        ///   Write an unsigned short.
        /// </summary>
        public void WriteUInt16(ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        /// <summary>
        ///   Write a domain name.
        /// </summary>
        /// <remarks>
        ///   A domain name is represented as a sequence of labels, where
        ///   each label consists of a length octet followed by that
        ///   number of octets.The domain name terminates with the
        ///   zero length octet for the null label of the root.Note
        ///   that this field may be an odd number of octets; no
        ///   padding is used.
        /// </remarks>
        public void WriteDomainName(string name)
        {
            foreach (var label in name.Split('.'))
            {
                var bytes = Encoding.UTF8.GetBytes(label);
                stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
            stream.WriteByte(0); // terminating byte
        }

    }
}
