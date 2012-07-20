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
using System.Runtime.InteropServices;

namespace TickZoom.Api
{

    unsafe public class SharedMemory : IDisposable
    {
        private IntPtr handle;
        private IntPtr baseAddress = IntPtr.Zero;
        private static SimpleLock locker = new SimpleLock();
        private static Dictionary<string,SharedMemory> shares = new Dictionary<string, SharedMemory>();
        private string name;

        public static SharedMemory Create(string name, long size)
        {
            using( locker.Using())
            {
                SharedMemory share;
                if (shares.TryGetValue(name, out share))
                {
                    return share;
                }
                else
                {
                    share = new SharedMemory(name, size);
                    shares.Add(name, share);
                    return share;
                }
            }
        }

        public SharedMemory(string name, long size)
        {
            this.name = name;
            handle = NativeMappedFile.CreateFileMapping(NativeMappedFile.INVALID_HANDLE,
                                                               NativeMappedFile.NULL_HANDLE,
                                                               (int)NativeMappedFile.MapProtection.ReadWrite,
                                                               (uint)((size >> 32) & 0xFFFFFFFF),
                                                               (uint)(size & 0xFFFFFFFF), name);
            if (handle == NativeMappedFile.NULL_HANDLE)
            {
                var error = Marshal.GetHRForLastWin32Error();
                throw new IOException(string.Format("CreateFileMapping returned: 0x{0:x}", error));
            }

            long offset = 0L;
            baseAddress = NativeMappedFile.MapViewOfFile(
                handle, (int)NativeMappedFile.MapAccess.FileMapAllAccess,
                (uint)((offset >> 32) & 0xFFFFFFFF),
                (uint)(offset & 0xFFFFFFFF), (uint)size);

            if (BaseAddress == NativeMappedFile.NULL_HANDLE)
                throw new IOException(string.Format("MapViewOfFile returned: 0x{0:x}", Marshal.GetHRForLastWin32Error()));


        }

        private volatile bool isDisposed = false;
        private object taskLocker = new object();

        public IntPtr BaseAddress
        {
            get { return baseAddress; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                lock (taskLocker)
                {
                    using (locker.Using())
                    {
                        shares.Remove(name);
                        NativeMappedFile.CloseHandle(handle);
                    }
                }
            }
        }
    }
}
