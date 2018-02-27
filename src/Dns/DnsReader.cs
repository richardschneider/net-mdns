using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Methods to read DNS data items.
    /// </summary>
    public class DnsReader
    {
        Stream stream;

        /// <summary>
        ///   Creates a new instance of the <see cref="DnsReader"/> on the
        ///   specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        ///   The source for data items.
        /// </param>
        public DnsReader(Stream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        ///   Read an unsigned short.
        /// </summary>
        /// <returns>
        ///   The two byte big-endian value as an unsigned short.
        /// </returns>
        public ushort ReadUInt16()
        {
            var msb = stream.ReadByte() << 8;
            return (ushort)(msb | stream.ReadByte());
        }

        /// <summary>
        ///   Read a domain name.
        /// </summary>
        /// <returns>
        ///   The domain name as a string.
        /// </returns>
        /// <remarks>
        ///   A domain name is represented as a sequence of labels, where
        ///   each label consists of a length octet followed by that
        ///   number of octets.The domain name terminates with the
        ///   zero length octet for the null label of the root.Note
        ///   that this field may be an odd number of octets; no
        ///   padding is used.
        /// </remarks>
        public string ReadDomainName()
        {
            var s = new StringBuilder();
            var label = new byte[byte.MaxValue + 1];

            while (true)
            {
                var length = stream.ReadByte();
                if (length < 1)
                    break;
                stream.Read(label, 0, length);
                s.Append(Encoding.UTF8.GetString(label, 0, length));
                s.Append('.');
            }

            // Remove trailing '.'
            if (s.Length > 0)
                s.Length = s.Length - 1;
            return s.ToString();
        }
    }
}
