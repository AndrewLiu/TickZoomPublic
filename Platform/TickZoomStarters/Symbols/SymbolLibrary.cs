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
			foreach( var properties in dictionary)
			{
			    AddSymbolProperies(properties);
			}
			dictionary = SymbolDictionary.Create("user",SymbolDictionary.UserDictionary);
			foreach( var properties in dictionary) {
                AddSymbolProperies(properties);
            }
			AddAbbreviations();
			AdjustSessions();
			CreateUniversalIds();
		}

        private void AddSymbolProperies(SymbolProperties properties)
        {
            if( properties.Account == "default")
            {
                symbolMap[properties.ExpandedSymbol] = properties;
                properties.CommonSymbol = properties;
            }
            else
            {
                var sourceSymbol = properties.Copy();
                sourceSymbol.Account = "default";
                symbolMap[sourceSymbol.ExpandedSymbol] = sourceSymbol;
                symbolMap[properties.ExpandedSymbol] = properties;
                properties.CommonSymbol = sourceSymbol;
            }
        }
		
		private void CreateUniversalIds() {
			universalMap = new Dictionary<long, SymbolProperties>();
			foreach( var kvp in symbolMap) {
				kvp.Value.BinaryIdentifier = universalIdentifier;
				universalMap.Add(universalIdentifier,kvp.Value);
				universalIdentifier ++;
			}
		}

        private void AddAbbreviations()
        {
			var tempSymbolMap = new Dictionary<string,SymbolProperties>();
			foreach( var kvp in symbolMap) {
				var properties = kvp.Value;
				var symbolAccount = kvp.Key;
                tempSymbolMap.Add(symbolAccount, properties);
                var abbreviation = properties.ExpandedSymbol.StripInvalidPathChars();
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
		
		public bool GetSymbolProperties(string symbolArgumentAccount, out SymbolProperties properties) {
			return symbolMap.TryGetValue(symbolArgumentAccount,out properties);
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

        public SymbolProperties GetSymbolProperties(string symbolArgument)
        {
            SymbolProperties properties;
            if( !TryGetSymbolProperties(symbolArgument, out properties))
            {
                throw new ApplicationException("Sorry, symbol " + symbolArgument + " was not found with default account in any symbol dictionary.");
            }
            return properties;
        }

	    public bool TryGetSymbolProperties(string symbolArgument, out SymbolProperties properties)
        {
            var brokerSymbol = GetDictionarySymbol(symbolArgument);
            var defaultSymbol = brokerSymbol + Symbol.AccountSeparator + "default";

            var account = GetDynamicAccount(symbolArgument);

            var expandedSymbol = brokerSymbol + Symbol.AccountSeparator + account;
            if (GetSymbolProperties(expandedSymbol, out properties))
            {
                return true;
			}
            if (GetSymbolProperties(defaultSymbol, out properties))
            {
                var sourceSymbol = properties;
                properties = properties.Copy();
                properties.Account = account;
                properties.CommonSymbol = sourceSymbol;
                properties.BinaryIdentifier = ++universalIdentifier;
                universalMap.Add(properties.BinaryIdentifier, properties);
                symbolMap[properties.ExpandedSymbol] = properties;
                symbolMap[properties.ExpandedSymbol.StripInvalidPathChars()] = properties;
                return true;
            }
	        return false;
        }

        public SymbolInfo LookupSymbol(string symbol)
        {
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
