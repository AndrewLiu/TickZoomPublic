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
            symbolMap = new Dictionary<string, SymbolProperties>();
            universalMap = new Dictionary<long, SymbolProperties>();
            SymbolDictionary.Create(this, "universal", SymbolDictionary.UniversalDictionary);
            SymbolDictionary.Create(this, "user", SymbolDictionary.UserDictionary);
		}

        public void AddSymbol(SymbolProperties properties)
        {
            if (properties.Account == "default")
            {
                symbolMap.Add(properties.ExpandedSymbol,properties);
                properties.CommonSymbol = properties;
            }
            else
            {
                if (ReferenceEquals(properties.CommonSymbol, properties) || properties.CommonSymbol.ExpandedSymbol == properties.ExpandedSymbol)
                {
                    throw new ApplicationException("Symbol " + properties.ExpandedSymbol + " cannot have itself as the common symbol.");
                }
                symbolMap.Add(properties.ExpandedSymbol, properties);
            }
            AddAbbreviation(properties);
            AdjustSession(properties);
            CreateUniversalId(properties);
        }
		
		private void CreateUniversalId(SymbolProperties properties) {
			properties.BinaryIdentifier = universalIdentifier;
            universalMap.Add(universalIdentifier, properties);
			universalIdentifier ++;
		}

        private void AddAbbreviation(SymbolProperties properties)
        {
            var abbreviation = properties.ExpandedSymbol.StripInvalidPathChars();
            if (!symbolMap.ContainsKey(abbreviation))
            {
				symbolMap[abbreviation] = properties;
			}
		}
		
		private void AdjustSession(SymbolProperties symbolProperties) {
			if( symbolProperties.TimeZone == null || symbolProperties.TimeZone.Length == 0)
			{
			    return;
			}
			if( symbolProperties.DisplayTimeZone == "Local" || symbolProperties.DisplayTimeZone == "UTC" ) {
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
		
		public bool GetSymbolProperties(string symbolArgumentAccount, out SymbolProperties properties) {
			return symbolMap.TryGetValue(symbolArgumentAccount,out properties);
		}

		public string GetBaseSymbol(string symbol) {
			var parts = symbol.Split( '.','@','!');
			if( parts.Length > 1) {
				symbol = parts[0].Trim();
			}
			return symbol;
		}

        public string GetSymbolAccount(string symbol)
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
                throw new ApplicationException("Sorry, symbol " + symbolArgument + " was not found in any symbol dictionary. Please heck for typos or else add it to the dictionary.");
            }
            return properties;
        }

	    public bool TryGetSymbolProperties(string symbolArgument, out SymbolProperties properties)
        {
            var brokerSymbol = GetBaseSymbol(symbolArgument);

            var account = GetSymbolAccount(symbolArgument);

            var expandedSymbol = brokerSymbol + Symbol.AccountSeparator + account;
            if (GetSymbolProperties(expandedSymbol, out properties))
            {
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
