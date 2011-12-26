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
using System.IO;
using System.Security.Cryptography;
using System.Text;

using TickZoom.Api;
using TickZoom.Common;
using TickZoom.Interceptors;
using TickZoom.Reports;
using TickZoom.Transactions;

namespace TickZoom.Statistics
{
	public class DataHasher {
		public BinaryWriter Writer;
		private MemoryStream memory = new MemoryStream();
		private SHA1 sha1 = SHA1.Create();
		public DataHasher() {
			Writer = new BinaryWriter(memory);
			sha1.Initialize();
		}
		public void Clear() {
			memory.SetLength(0);
			memory.Position = 0;
		}
		public void Update() {
			sha1.TransformBlock(memory.GetBuffer(),0,(int)memory.Length,memory.GetBuffer(),0);
			Clear();
		}
		public string GetHash() {
			sha1.TransformFinalBlock(memory.GetBuffer(),0,0);
			return Convert.ToBase64String( sha1.Hash);
		}
	}
}
