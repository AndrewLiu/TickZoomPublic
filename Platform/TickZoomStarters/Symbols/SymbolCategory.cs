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
using System.Reflection;
using TickZoom.Common;
using TickZoom.Api;

namespace TickZoom.Symbols
{
	/// <summary>
	/// Description of SymbolCategory.
	/// </summary>
	public class SymbolCategory
	{
		string name;
        private Dictionary<PropertyInfo, object> properties = new Dictionary<PropertyInfo, object>();
        List<SymbolCategory> categories = new List<SymbolCategory>();
		
		public SymbolCategory(Dictionary<PropertyInfo,object> properties)
		{
			foreach( var kvp in properties)
			{
			    Set(kvp.Key,kvp.Value);
			}
		}
		
		public SymbolCategory()
		{
		}
		
		public string Name {
			get { return name; }
			set { name = value; }
		}
		
		public List<SymbolCategory> Categories {
			get { return categories; }
		}
		
	    public Dictionary<PropertyInfo, object> Properties
	    {
	        get { return properties; }
	    }

	    public void Set(PropertyInfo property, object value)
	    {
	        Properties[property] = value;
	    }
	}
}
