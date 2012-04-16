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
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Symbols
{
	public class SymbolLibrary 
	{
		Dictionary<string,SymbolProperties> symbolMap;
		Dictionary<long,SymbolProperties> universalMap;
        long universalIdentifier = 1;
        public SymbolLibrary()
        {
			var dictionary = SymbolDictionary.Create("universal",SymbolDictionary.UniversalDictionary);
			symbolMap = new Dictionary<string, SymbolProperties>();
			foreach( var properties in dictionary) {
				symbolMap[properties.Symbol] = properties;
			}
			dictionary = SymbolDictionary.Create("user",SymbolDictionary.UserDictionary);
			foreach( var properties in dictionary) {
				symbolMap[properties.Symbol] = properties;
			}
			AddAbbreviations();
			AdjustSessions();
			CreateUniversalIds();
		}
		
		private void CreateUniversalIds() {
			universalMap = new Dictionary<long, SymbolProperties>();
			foreach( var kvp in symbolMap) {
				kvp.Value.BinaryIdentifier = universalIdentifier;
				universalMap.Add(universalIdentifier,kvp.Value);
				universalIdentifier ++;
			}
		}

		private void AddAbbreviations() {
			var tempSymbolMap = new Dictionary<string,SymbolProperties>();
			foreach( var kvp in symbolMap) {
				var properties = kvp.Value;
				var symbolAccount = kvp.Key;
                tempSymbolMap.Add(symbolAccount, properties);
                var abbreviation = properties.Symbol.StripInvalidPathChars();
                if (!symbolMap.ContainsKey(abbreviation))
                {
					tempSymbolMap[abbreviation] = properties;
				}
			}
			symbolMap = tempSymbolMap;
		}
		
		private void AdjustSessions() {
			foreach( var kvp in symbolMap) {
				var symbolProperties = kvp.Value;
				if( symbolProperties.TimeZone == null || symbolProperties.TimeZone.Length == 0) {
					continue;
				}
				if( symbolProperties.DisplayTimeZone == "Local" ||
				   symbolProperties.DisplayTimeZone == "UTC" ) {
					// Convert session times from Exchange to UTC.
					SymbolTimeZone timeZone = new SymbolTimeZone(symbolProperties);
					timeZone.SetExchangeTimeZone();
					int startOffset = (int) timeZone.UtcOffset(new TimeStamp());
					int endOffset = (int) timeZone.UtcOffset(new TimeStamp());
					Elapsed utcSessionStart = symbolProperties.SessionStart - new Elapsed(0,0,startOffset);
					Elapsed utcSessionEnd = symbolProperties.SessionEnd - new Elapsed(0,0,endOffset);
					// Convert UTCI session times to either Local or UTC as chosen
					// by the DisplayTimeZone property.
					timeZone = new SymbolTimeZone(symbolProperties);
					startOffset = (int) timeZone.UtcOffset(new TimeStamp());
					endOffset = (int) timeZone.UtcOffset(new TimeStamp());
					symbolProperties.SessionStart = utcSessionStart + new Elapsed(0,0,startOffset);
					symbolProperties.SessionEnd = utcSessionEnd + new Elapsed(0,0,endOffset);
				}
			}
		}
		
		public bool GetSymbolProperties(string symbolAccount, out SymbolProperties properties) {
			return symbolMap.TryGetValue(symbolAccount,out properties);
		}
		private string GetDictionarySymbol(string symbol) {
			var parts = symbol.Split( '.','@','!');
			if( parts.Length > 1) {
				symbol = parts[0].Trim();
			}
			return symbol;
		}

        private string GetDynamicAccount(string symbol)
        {
            var parts = symbol.Split('!');
            if (parts.Length > 2)
            {
                throw new FormatException(symbol + " has more than one '@' symbol.");
            }
            else if( parts.Length == 2)
            {
                return parts[1].Trim().ToLower();
            }
            return "default";
        }

        public SymbolProperties GetSymbolProperties(string symbolAccount)
        {
			var symbol  = GetDictionarySymbol(symbolAccount);
            var account = GetDynamicAccount(symbolAccount);
			SymbolProperties properties;
            symbolAccount = symbol;
            if (account != "default")
            {
                symbolAccount += "!" + account;
            }
            if (GetSymbolProperties(symbolAccount, out properties))
            {
                return properties;
			}
            if (GetSymbolProperties(symbol, out properties))
            {
                properties = properties.Copy();
                properties.Account = account;
                properties.BinaryIdentifier = ++universalIdentifier;
                symbolMap.Add(symbolAccount,properties);
                universalMap.Add(properties.BinaryIdentifier,properties);
                var abbreviation = symbolAccount.StripInvalidPathChars();
                if( !symbolMap.ContainsKey(abbreviation))
                {
                    symbolMap.Add(abbreviation, properties);
                }
                return properties;
            }
            if( account == "default")
            {
                throw new ApplicationException("Sorry, symbol " + symbolAccount + " was not found with default account in any symbol dictionary.");
            }
            else
            {
                throw new ApplicationException("Sorry, symbol " + symbolAccount + " was not found with either default or " + account + " account in any symbol dictionary and .");
            }
        }
		
		public SymbolInfo LookupSymbol(string symbol) {
			return GetSymbolProperties(symbol);
		}
	
		public SymbolInfo LookupSymbol(long universalIdentifier) {
			SymbolProperties symbolProperties;
			if( universalMap.TryGetValue(universalIdentifier,out symbolProperties)) {
				return symbolProperties;
			} else {
				throw new ApplicationException( "Sorry, universal id " + universalIdentifier + " was not found in any symbol dictionary.");
			}
		}
	}
}
