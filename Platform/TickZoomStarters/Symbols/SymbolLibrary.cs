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
        private static readonly Log log = Factory.SysLog.GetLogger(typeof(SymbolLibrary));
        private readonly bool trace = log.IsTraceEnabled;
        private readonly bool debug = log.IsDebugEnabled;
        Dictionary<string, SymbolProperties> symbolMap;
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
            if (properties.Account == "default" && properties.Source == "default")
            {
                symbolMap[properties.ExpandedSymbol] = properties;
                properties.CommonSymbol = properties;
                log.InfoFormat("Assigned common {0} to {1}", properties.CommonSymbol, properties);
            }
            else
            {
                if (ReferenceEquals(properties.CommonSymbol, properties) || properties.CommonSymbol.ExpandedSymbol == properties.ExpandedSymbol)
                {
                    throw new ApplicationException("Symbol " + properties.ExpandedSymbol + " cannot have itself as the common symbol.");
                }
                try
                {
                    symbolMap.Add(properties.ExpandedSymbol, properties);
                }
                catch( Exception ex)
                {
                    var x = 0;
                }
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
			symbolMap[abbreviation] = properties;
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
		
		public bool  GetSymbolProperties(string symbolArgumentAccount, out SymbolProperties properties) {
			return symbolMap.TryGetValue(symbolArgumentAccount,out properties);
		}

		public string GetBaseSymbol(string symbol) {
			var parts = symbol.Split( '.','!');
			if( parts.Length > 1) {
				symbol = parts[0].Trim();
			}
			return symbol;
		}

        public static string GetSymbolSource(string symbol)
        {
            var countBangs = 0;
            var firstBang = symbol.IndexOf('!');
            var index = 0;
            do
            {
                ++index;
                index = symbol.IndexOf('!',index);
                if( index >= 0) ++countBangs;
            } while (index >= 0);

            var result = "default";
            if (countBangs > 2)
            {
                throw new FormatException(symbol + " has more than one '!' symbol.");
            }

            var endOfSource = firstBang >= 0 ? firstBang : symbol.Length;
            var firstDot = symbol.IndexOf(".");
            if( firstDot >= 0)
            {
                result = symbol.Substring(firstDot + 1, endOfSource - firstDot - 1);
            }
            return result;
        }

        public string GetSymbolAccount(string symbol)
        {
            var parts = symbol.Split('!');
            if (parts.Length > 2)
            {
                throw new FormatException(symbol + " has more than one '!' symbol.");
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
                throw new ApplicationException("Sorry, symbol " + symbolArgument + " was not found in any symbol dictionary. Please check for typos or else add it to the dictionary.");
            }
            return properties;
        }

	    public bool TryGetSymbolProperties(string symbolArgument, out SymbolProperties properties)
        {
            if( string.IsNullOrEmpty(symbolArgument))
            {
                properties = null;
                return false;
            }
	        var brokerSymbol = GetBaseSymbol(symbolArgument);
	        var source = GetSymbolSource(symbolArgument).ToLower();
            var account = GetSymbolAccount(symbolArgument).ToLower();

            var expandedSymbol = brokerSymbol + Symbol.SourceSeparator + source + Symbol.AccountSeparator + account;
            if (GetSymbolProperties(expandedSymbol, out properties))
            {
                return true;
			}
            expandedSymbol = brokerSymbol + Symbol.AccountSeparator + account;
            if (GetSymbolProperties(expandedSymbol, out properties))
            {
                if( source != "default")
                {
                    properties = properties.Copy();
                    expandedSymbol = brokerSymbol + Symbol.AccountSeparator + "default";
                    SymbolProperties commonProperties;
                    if( !GetSymbolProperties(expandedSymbol, out commonProperties))
                    {
                        throw new ApplicationException("Can't find common symbol " + expandedSymbol + " for " + properties);
                    }
                    properties.Source = source;
                    properties.CommonSymbol = commonProperties;
                    AddSymbol(properties);
                }
                return true;
            }
            return false;
        }

        public SymbolInfo LookupSymbol(string symbol)
        {
			return GetSymbolProperties(symbol);
		}

        public bool TryLookupSymbol(string symbol, out SymbolInfo symbolInfo)
        {
            SymbolProperties properties;
            var result = TryGetSymbolProperties(symbol, out properties);
            symbolInfo = properties;
            return result;
        }

        public SymbolInfo LookupSymbol(long universalIdentifier)
        {
			SymbolProperties symbolProperties;
			if( universalMap.TryGetValue(universalIdentifier,out symbolProperties)) {
				return symbolProperties;
			} else {
				throw new ApplicationException( "Sorry, universal id " + universalIdentifier + " was not found in any symbol dictionary.");
			}
		}

        public bool TryLookupSymbol(long universalIdentifier, out SymbolInfo symbolInfo)
        {
            SymbolProperties symbolProperties;
            var result = universalMap.TryGetValue(universalIdentifier, out symbolProperties);
            symbolInfo = symbolProperties;
            return result;
        }
    }
}
