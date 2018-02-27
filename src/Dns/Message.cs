using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   All communications inside of the domain protocol are carried in a single
    ///   format called a message.
    /// </summary>
    public class Message : DnsObject
    {
        /// <summary>
        /// A 16 bit identifier assigned by the program that
        /// generates any kind of query.This identifier is copied
        /// the corresponding reply and can be used by the requester
        /// to match up replies to outstanding queries.
        /// </summary>
        public ushort ID { get; set; }

        /// <summary>
        ///   A one bit field that specifies whether this message is a query(0), or a response(1).
        /// </summary>
        public bool QR { get; set; }

        public bool IsQuery { get { return !QR; } }
        public bool IsResponse { get { return QR; } }

        public enum Opcode
        {
            QUERY = 0,
            IQUERY = 1,
            STATUS = 2
        }
        /// <summary>
        ///   A four bit field that specifies kind of query in this
        ///   message.This value is set by the originator of a query
        ///   and copied into the response.
        /// </summary>
        public Opcode OPCODE { get; set; }

        /// <summary>
        ///    Authoritative Answer - this bit is valid in responses,
        ///    and specifies that the responding name server is an
        ///    authority for the domain name in question section.
        ///    
        ///    Note that the contents of the answer section may have
        ///    multiple owner names because of aliases.The AA bit
        ///    corresponds to the name which matches the query name, or
        ///    the first owner name in the answer section.
        /// </summary>
        public bool AA { get; set; }

        /// <summary>
        ///   TrunCation - specifies that this message was truncated
        ///   due to length greater than that permitted on the
        ///   transmission channel.
        /// </summary>
        public bool TC { get; set; }

        /// <summary>
        ///    Recursion Desired - this bit may be set in a query and
        ///    is copied into the response. If RD is set, it directs
        ///    the name server to pursue the query recursively.
        ///    
        ///    Recursive query support is optional.
        /// </summary>
        public bool RD { get; set; }

        /// <summary>
        ///    Recursion Available - this be is set or cleared in a
        ///    response, and denotes whether recursive query support is
        ///    available in the name server.
        /// </summary>
        public bool RA { get; set; }

        /// <summary>
        ///    Reserved for future use.  Must be zero in all queries
        ///    and responses.
        /// </summary>
        public int Z { get; set; }

        public enum Rcode
        {
            /// <summary>
            ///    No error condition
            /// </summary>
            NoError = 0,

            /// <summary>
            ///    The name server was unable to interpret the query.
            /// </summary>
            FormatError = 1,

            /// <summary>
            ///    The name server was unable to process this query due to a
            ///    problem with the name server.
            /// </summary>
            ServerFailure = 2,

            /// <summary>
            ///    Meaningful only for responses from an authoritative name
            ///    server, this code signifies that the domain name 
            ///    referenced in the query does not exist.
            /// </summary>
            NameError = 3,

            /// <summary>
            ///    The name server does not support the requested kind of query.
            /// </summary>
            NotImplemented = 4,

            /// <summary>
            ///    The name server refuses to perform the specified operation for
            ///    policy reasons. 
            /// </summary>
            Refused = 5,
        }

        /// <summary>
        ///     Response code - this 4 bit field is set as part of responses.
        /// </summary>
        public Rcode RCODE { get; set; }

        public List<Question> Questions { get; } = new List<Question>();
        public List<ResourceRecord> Answers { get; } = new List<ResourceRecord>();
        public List<ResourceRecord> AuthorityRecords { get; } = new List<ResourceRecord>();
        public List<ResourceRecord> AdditionalRecords { get; } = new List<ResourceRecord>();

        /// <inheritdoc />
        public override void Read(DnsReader reader)
        {
            ID = reader.ReadUInt16();
            var flags = reader.ReadUInt16();
            var qdcount = reader.ReadUInt16();
            var ancount = reader.ReadUInt16();
            var nscount = reader.ReadUInt16();
            var arcount = reader.ReadUInt16();
            for (var i = 0; i < qdcount; ++i)
            {
                var question = new Question();
                question.Read(reader);
                Questions.Add(question);
            }
            // TODO: resource records
        }

        /// <inheritdoc />
        public override void Write(DnsWriter writer)
        {
            writer.WriteUInt16(ID);
            ushort flags = 0; // TODO
            writer.WriteUInt16(flags);
            writer.WriteUInt16((ushort)Questions.Count);
            writer.WriteUInt16((ushort)Answers.Count);
            writer.WriteUInt16((ushort)AuthorityRecords.Count);
            writer.WriteUInt16((ushort)AdditionalRecords.Count);
            foreach (var r in Questions) r.Write(writer);
            foreach (var r in Answers) r.Write(writer);
            foreach (var r in AuthorityRecords) r.Write(writer);
            foreach (var r in AdditionalRecords) r.Write(writer);
        }
    }
}
