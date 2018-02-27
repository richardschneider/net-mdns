using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Base class for all DNS objects.
    /// </summary>
    public abstract class DnsObject : IDnsSerialiser
    {
        /// <summary>
        ///   Reads the DNS object from a byte array.
        /// </summary>
        /// <param name="buffer">
        ///   The source for the DNS object.
        /// </param>
        /// <param name="offset">
        ///   The offset into the <paramref name="buffer"/>.
        /// </param>
        /// <param name="count">
        ///   The number of bytes in the <paramref name="buffer"/>.
        /// </param>
        public void Read(byte[] buffer, int offset, int count)
        {
            using (var ms = new MemoryStream(buffer, offset, count, false))
            {
                Read(new DnsReader(ms));
            }
        }

        /// <summary>
        ///   Reads the DNS object from a stream.
        /// </summary>
        /// <param name="stream">
        ///   The source for the DNS object.
        /// </param>
        public void Read(Stream stream)
        {
            Read(new DnsReader(stream));
        }

        /// <inheritdoc />
        public abstract void Read(DnsReader reader);

        /// <summary>
        ///   Writes the DNS object to a byte array.
        /// </summary>
        /// <returns>
        ///   A byte array containing the binary representaton of the DNS object.
        /// </returns>
        public byte[] ToByteArray()
        {
            using (var ms = new MemoryStream())
            {
                Write(new DnsWriter(ms));
                return ms.ToArray();
            }
        }

        /// <summary>
        ///   Writes the DNS object to a stream.
        /// </summary>
        /// <param name="stream">
        ///   The destination for the DNS object.
        /// </param>
        public void Write(Stream stream)
        {
            Write(new DnsWriter(stream));
        }

        /// <inheritdoc />
        public abstract void Write(DnsWriter writer);
    }
}
