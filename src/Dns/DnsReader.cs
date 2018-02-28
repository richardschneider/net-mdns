using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Methods to read DNS data items.
    /// </summary>
    public class DnsReader
    {
        Stream stream;
        int position;
        Dictionary<int, string> names = new Dictionary<int, string>();

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
        ///   Read a byte.
        /// </summary>
        /// <returns></returns>
        public byte ReadByte()
        {
            var value = stream.ReadByte();
            if (value < 0)
                throw new EndOfStreamException();
            ++position;
            return (byte)value;
        }
        
        /// <summary>
        ///   Read the specified number of bytes.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public byte[] ReadBytes(int length)
        {
            var buffer = new byte[length];
            for (var offset = 0; length > 0; )
            {
                var n = stream.Read(buffer, offset, length);
                if (n == 0)
                    throw new EndOfStreamException();
                offset += n;
                length -= n;
                position += n;
            }
            
            return buffer;
        }
        /// <summary>
        ///   Read an unsigned short.
        /// </summary>
        /// <returns>
        ///   The two byte little-endian value as an unsigned short.
        /// </returns>
        public ushort ReadUInt16()
        {
            int value = ReadByte();
            value = value << 8 | ReadByte();
            return (ushort)value;
        }

        /// <summary>
        ///   Read an unsigned int.
        /// </summary>
        /// <returns>
        ///   The four byte little-endian value as an unsigned int.
        /// </returns>
        public uint ReadUInt32()
        {
            int value = ReadByte();
            value = value << 8 | ReadByte();
            value = value << 8 | ReadByte();
            value = value << 8 | ReadByte();
            return (uint)value;
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
            var pointer = position;
            var length = ReadByte();

            // Do we have a compressed pointer?
            if ((length & 0xC0) == 0xC0)
            {
                pointer = (length ^ 0xC0) << 8 | ReadByte();
                return names[pointer];
            }

            while (length != 0)
            {
                var label = ReadBytes(length);
                s.Append(Encoding.UTF8.GetString(label, 0, length));
                s.Append('.');
                length = ReadByte();
            }

            // Remove trailing '.'
            if (s.Length > 0)
                s.Length = s.Length - 1;

            // Add to compressed names
            var name = s.ToString();
            names[pointer] = name;

            return name;
        }

        /// <summary>
        ///   Read a string.
        /// </summary>
        /// <remarks>
        ///   Strings are encoded with a length prefixed byte.  All strings are treated
        ///   as UTF-8.
        /// </remarks>
        public string ReadString()
        {
            var length = ReadByte();
            var buffer = ReadBytes(length);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        /// <summary>
        ///   Read a time span (interval)
        /// </summary>
        /// <returns>
        ///   A <see cref="TimeSpan"/> with second resolution.
        /// </returns>
        public TimeSpan ReadTimeSpan()
        {
            return TimeSpan.FromSeconds(ReadUInt32());
        }

        /// <summary>
        ///   Read an Internet address.
        /// </summary>
        /// <returns>
        ///   An <see cref="IPAddress"/>.
        /// </returns>
        public IPAddress ReadIPAddress(int length = 4)
        {
            var address = ReadBytes(length);
            return new IPAddress(address);
        }
    }
}
