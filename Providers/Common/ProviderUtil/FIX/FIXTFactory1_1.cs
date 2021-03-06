#region Copyright
/*
 * Software: TickZoom Trading Platform
 * Copyright 2009 M. Wayne Walter
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 * 
 * Business use restricted to 30 days except as otherwise stated in
 * in your Service Level Agreement (SLA).
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <http://www.tickzoom.org/wiki/Licenses>
 * or write to Free Software Foundation, Inc., 51 Franklin Street,
 * Fifth Floor, Boston, MA  02110-1301, USA.
 * 
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
	public class FIXTFactory1_1 : FIXTFactory
	{
	    private string fixVersion = "FIXT1.1";
		private int nextSequence;
		private int lastSequence;
		private string sender;
		private string destination;
	    private int firstSequence = int.MaxValue;
		private Dictionary<int,FIXTMessage1_1> messageHistory = new Dictionary<int,FIXTMessage1_1>();
		public FIXTFactory1_1(string version, int nextSequence, string sender, string destination) {
			// Decrement since first message will increment.
		    this.fixVersion = version;
			this.nextSequence = nextSequence - 1;
			this.sender = sender;
			this.destination = destination;
		}
		public virtual FIXTMessage1_1 Create() {
			var message = new FIXTMessage1_1(fixVersion,sender,destination);
			message.Sequence = GetNextSequence();
			return message;
		}
        public virtual FIXTMessage1_1 Create(int previousSequence)
        {
            if( previousSequence > nextSequence)
            {
                throw new InvalidOperationException("Cannot create new fix message with sequence in the future.");
            }
            var message = new FIXTMessage1_1(fixVersion, sender, destination);
            message.Sequence = previousSequence;
            return message;
        }
        public string Sender
        {
			get { return sender; }
		}
		public string Destination {
			get { return destination; }
		}
		public int GetNextSequence() {
			return Interlocked.Increment( ref nextSequence);
		}
		public void AddHistory(FIXTMessage1_1 fixMsg) {
			messageHistory.Add( fixMsg.Sequence, fixMsg);
            if (fixMsg.Sequence < FirstSequence)
            {
                firstSequence = fixMsg.Sequence;
            }
            lastSequence = fixMsg.Sequence;
        }
		public bool TryGetHistory(int sequence, out FIXTMessage1_1 result)
		{
		    return messageHistory.TryGetValue(sequence, out result);
		}

		public int LastSequence {
			get { return lastSequence; }
		}

	    public int FirstSequence
	    {
	        get { return firstSequence; }
	    }

        private void RollbackSequence(int badSequenceNumber)
        {
            var removeList = new List<int>();
            foreach( var kvp in messageHistory)
            {
                var sequence = kvp.Key;
                if( sequence >= badSequenceNumber)
                {
                    removeList.Add(sequence);
                }
            }
            foreach( var sequence in removeList)
            {
                messageHistory.Remove(sequence);
            }
            SetNextSequence(badSequenceNumber);
        }

        public void SetNextSequence(int newSequenceNumber)
        {
            lastSequence = nextSequence = newSequenceNumber - 1;
        }

        public void RollbackLastLogin()
        {
            var lastLoginSequence = 0;
            foreach (var kvp in messageHistory)
            {
                var sequence = kvp.Key;
                var message = kvp.Value;
                var type = message.Type;
                if( type == "A" && sequence > lastLoginSequence)
                {
                    lastLoginSequence = sequence;
                }
            }
            RollbackSequence(lastLoginSequence);
        }
    }
}
