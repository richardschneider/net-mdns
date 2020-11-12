using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{
	/// <summary>Class for extensions regarding the message class.</summary>
	public static class MessageExtensions
	{
		/// <summary>Copies the specified original message.</summary>
		/// <param name="originalMessage">The original message.</param>
		/// <returns>A copy of the original message.</returns>
		public static Message Copy(this Message originalMessage)
		{
			var message = new Message();
			message.AA = originalMessage.AA;
			message.AD = originalMessage.AD;
			message.CD = originalMessage.AD;
			message.DO = originalMessage.DO;
			message.Id = originalMessage.Id;
			message.QR = originalMessage.QR;
			message.Opcode = originalMessage.Opcode;
			message.RA = originalMessage.RA;
			message.RD = originalMessage.RD;
			message.TC = originalMessage.TC;
			message.Status = originalMessage.Status;
			message.Z = originalMessage.Z;

			message.AdditionalRecords = new List<ResourceRecord>(originalMessage.AdditionalRecords);
			message.AuthorityRecords = new List<ResourceRecord>(originalMessage.AdditionalRecords);
			message.Answers = new List<ResourceRecord>(originalMessage.Answers);
			message.Questions.AddRange(originalMessage.Questions);

			return message;
		}

		/// <summary>Removes the unreachable records.</summary>
		/// <param name="message">The message from which the unreachable records should be removed.</param>
		/// <param name="address">The address which should be able to reach all the addresses contained in the address records.</param>
		public static void RemoveUnreachableRecords(this Message message, IPAddress address)
		{
			// Only return address records that the querier can reach.
			message.Answers.RemoveAll(rr => IsUnreachable(rr, address));
			message.AuthorityRecords.RemoveAll(rr => IsUnreachable(rr, address));
			message.AdditionalRecords.RemoveAll(rr => IsUnreachable(rr, address));
		}

		/// <summary>Determines whether the message contains address records.</summary>
		/// <param name="message">The message that should be checked for address records.</param>
		/// <returns><c>true</c> if the specified message contains address records; otherwise, <c>false</c>.</returns>
		public static bool ContainsAddressRecords(this Message message) =>
			(message.Answers.Any((rr) => rr.Type == DnsType.A || rr.Type == DnsType.AAAA) ||
			 message.AuthorityRecords.Any((rr) => rr.Type == DnsType.A || rr.Type == DnsType.AAAA) ||
			 message.AdditionalRecords.Any((rr) => rr.Type == DnsType.A || rr.Type == DnsType.AAAA));

		/// <summary>Determines whether the specified resource record is unreachable.</summary>
		/// <param name="rr">The resource record.</param>
		/// <param name="address">The address.</param>
		/// <returns><c>true</c> if the specified resource records is unreachable; otherwise, <c>false</c>.</returns>
		private static bool IsUnreachable(ResourceRecord rr, IPAddress address)
		{
			var addressRecord = rr as AddressRecord;
			return !addressRecord?.Address.IsReachable(address) ?? false;
		}
	}
}