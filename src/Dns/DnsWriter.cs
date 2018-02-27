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
        int position;
        Dictionary<string, int> pointers = new Dictionary<string, int>();

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
            position += 2;
        }

        /// <summary>
        ///   Write an unsigned int.
        /// </summary>
        public void WriteUInt32(uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
            position += 4;
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
            // Check for name already used.
            if (pointers.TryGetValue(name, out int pointer))
            {
                WriteUInt16((ushort)(0xC000 | pointer));
                return;
            }
            pointers[name] = position;

            foreach (var label in name.Split('.'))
            {
                var bytes = Encoding.UTF8.GetBytes(label);
                if (bytes.Length > 63)
                    throw new InvalidDataException($"Label '{label}' cannot exceed 63 octets.");
                stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
                position += bytes.Length + 1;
            }
            stream.WriteByte(0); // terminating byte
            ++position;
        }

        /// <summary>
        ///   Write a string.
        /// </summary>
        /// <remarks>
        ///   Strings are encoded with a length prefixed byte.  All strings are treated
        ///   as UTF-8.
        /// </remarks>
        public void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
            position += bytes.Length + 1;
        }

        /// <summary>
        ///   Write a time span.
        /// </summary>
        /// <remarks>
        ///   Represented as 32-bit unsigned int (in seconds).
        /// </remarks>
        public void WriteTimeSpan(TimeSpan value)
        {
            WriteUInt32((uint)value.TotalSeconds);
        }
    }
}
