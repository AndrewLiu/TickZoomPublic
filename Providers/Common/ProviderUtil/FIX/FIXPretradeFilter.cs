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
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
	public class FIXPretradeFilter : IDisposable {
		private FIXFilter filter;
		private string localAddress = "0.0.0.0";
		private ushort localPort = 0;
		private string remoteAddress;
		private ushort remotePort;
		private static Log log = Factory.SysLog.GetLogger(typeof(FIXPretradeFilter));
		private static bool trace = log.IsTraceEnabled;
		private static bool debug = log.IsDebugEnabled;
		private Socket listener;
		private Socket localSocket;
		private Socket remoteSocket;
		private Message remoteMessage;
		private Message localMessage;
		private YieldMethod WriteToLocalMethod;
		private YieldMethod WriteToRemoteMethod;
		private Task remoteTask;
		private Task localTask;
		private FIXContext fixContext;
		
		public FIXPretradeFilter(string address, ushort port) {
			this.remoteAddress = address;
			this.remotePort = port;
			WriteToLocalMethod = WriteToLocal;
			WriteToRemoteMethod = WriteToRemote;
			ListenToLocal();
		}

        public void Initialize(Task task)
        {
            localTask = task;
            localTask.Name = "FilterLocalRead";
            localSocket.ReceiveQueue.ConnectInbound(localTask);
            localTask.Start();
        }

	    private void ListenToLocal() {
            listener = Factory.Provider.Socket(typeof(FIXPretradeFilter).Name, localAddress, localPort);
			listener.Bind();
			listener.Listen(5);
			listener.OnConnect = OnConnect;
			localPort = listener.Port;
			log.Info("Listening to " + localAddress + " on port " + localPort);
		}
		
		private void OnConnect( Socket socket) {
			if( remoteSocket == socket) {
				OnConnectRemote();
			} else {
				OnConnectLocal(socket);
			}
		}

		private void OnConnectLocal( Socket socket) {
			localSocket = socket;
			localSocket.MessageFactory = new MessageFactoryFix44();
			log.Info("Received local connection: " + socket);
			RequestRemoteConnect();
		}
		
		private void OnDisconnect( Socket socket) {
			if( this.localSocket == socket ) {
				log.Info("Local socket disconnect: " + socket);
				CloseSockets();
			}
			if( this.remoteSocket == socket) {
				log.Info("Remote socket disconnect: " + socket);
				CloseSockets();
			}
		}
		
		private void CloseSockets() {
			if( remoteTask != null) remoteTask.Stop();
			if( localTask != null) localTask.Stop();
			if( remoteSocket != null) remoteSocket.Dispose();
			if( localSocket != null) localSocket.Dispose();
		}
		
		private void RequestRemoteConnect() {

            log.Info("Requesting connect at " + remoteAddress + " and port " + remotePort + " with socket: " + localSocket);
            remoteSocket = Factory.Provider.Socket("FilterRemoteSocket", remoteAddress,remotePort);
            remoteSocket.MessageFactory = new MessageFactoryFix44();
			remoteSocket.OnConnect = OnConnect;
			remoteSocket.Connect();
			remoteConnectTimeout = Factory.TickCount + 2000;
		}
		
		private long remoteConnectTimeout;
		
		private void OnConnectRemote() {
			remoteTask = Factory.Parallel.Loop( "FilterRemoteRead", OnException, RemoteReadLoop);
			remoteTask.Start();
			fixContext = new FIXContextDefault( localSocket, remoteSocket);
			remoteSocket.ReceiveQueue.ConnectInbound( remoteTask);
			log.Info("Connected at " + remoteAddress + " and port " + remotePort + " with socket: " + localSocket);
		}
		
		private Yield RemoteReadLoop() {
			if( remoteSocket.TryGetMessage(out remoteMessage)) {
				if( trace) log.TraceFormat("Remote Read: {0}", remoteMessage);
				try {
					if( filter != null) filter.Remote( fixContext, remoteMessage);
					return Yield.DidWork.Invoke( WriteToLocalMethod);
				} catch( FilterException)
				{
				    remoteSocket.MessageFactory.Release(remoteMessage);
					CloseSockets();
					return Yield.Terminate;
				}
			} else {
				return Yield.NoWork.Repeat;
			}
		}
		
		private Yield Invoke() {
			if( remoteSocket.State == SocketState.Connected) {
				if( localSocket.TryGetMessage(out localMessage)) {
					if( trace) log.TraceFormat("Local Read: {0}", localMessage);
					try {
						if( filter != null) filter.Local( fixContext, localMessage);
						return Yield.DidWork.Invoke( WriteToRemoteMethod);
					} catch( FilterException) {
                        localSocket.MessageFactory.Release(localMessage);
                        CloseSockets();
						return Yield.Terminate;
					}
				} else {
					return Yield.NoWork.Repeat;
				}
			} else {
				if( Factory.TickCount >	remoteConnectTimeout) {
					CloseSockets();
					return Yield.Terminate;
				} else {
					return Yield.NoWork.Repeat;
				}
			}
		}
	
		private Yield WriteToLocal() {
			if( localSocket.TrySendMessage(remoteMessage)) {
				if(trace) log.TraceFormat("Local Write: {0}", remoteMessage);
                return Yield.DidWork.Return;
			} else {
				return Yield.NoWork.Repeat;
			}
		}
	
		private Yield WriteToRemote() {
			if( remoteSocket.TrySendMessage(localMessage)) {
				if(trace) log.TraceFormat("Remote Write: {0}", localMessage);
                return Yield.DidWork.Return;
			} else {
				return Yield.NoWork.Repeat;
			}
		}
		
		private void OnException( Exception ex) {
			log.Error("Exception occurred", ex);
		}
		
	 	protected volatile bool isDisposed = false;
	    public void Dispose() 
	    {
	        Dispose(true);
	        GC.SuppressFinalize(this);      
	    }
	
	    protected virtual void Dispose(bool disposing)
	    {
       		if( !isDisposed) {
	            isDisposed = true;   
	            if (disposing) {
	            	if( debug) log.DebugFormat("Dispose()");
	            	if( localTask != null) {
	            		localTask.Stop();
	            	}
	            	if( remoteTask != null) {
	            		remoteTask.Stop();
	            	}
	            	if( listener != null) {
	            		listener.Dispose();
	            	}
	            	if( localSocket != null) {
		            	localSocket.Dispose();
	            	}
	            	if( remoteSocket != null) {
	            		remoteSocket.Dispose();
	            	}
	            }
    		}
	    }    
	        
		public ushort LocalPort {
			get { return localPort; }
		}
		
		public FIXFilter Filter {
			get { return filter; }
			set { filter = value; }
		}
	}
}