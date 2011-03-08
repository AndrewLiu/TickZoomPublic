// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision: 2318 $</version>
// </file>

using System;
using System.Collections;

namespace TickZoom.Loader
{
	/// <summary>
	/// Interface for classes that can build objects out of extensions.
	/// </summary>
	/// <remarks>http://en.wikipedia.org/wiki/Fraggle_Rock#Doozers</remarks>
	public interface IDoozer
	{
		/// <summary>
		/// Gets if the doozer handles extension conditions on its own.
		/// If this property return false, the item is excluded when the condition is not met.
		/// </summary>
		bool HandleConditions { get; }
		
		/// <summary>
		/// Construct the item.
		/// </summary>
		/// <param name="caller">The caller passed to <see cref="PluginTree.BuildItem"/>.</param>
		/// <param name="extension">The extension to build.</param>
		/// <param name="subItems">The list of objects created by (other) doozers for the sub items.</param>
		/// <returns>The constructed item.</returns>
		object BuildItem(object caller, Extension extension, ArrayList subItems);
	}
}
