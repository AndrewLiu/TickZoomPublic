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
using TickZoom.Common;

namespace TickZoom.Interceptors
{
	/// <summary>
	/// Description of Orders.
	/// </summary>
	public class OrderHandlers
	{
        ReverseCommon reverseActiveNow;
        ReverseCommon reverseNextBar;
        ChangeCommon changeActiveNow;
        ChangeCommon changeNextBar;
        ExitCommon exitActiveNow;
        EnterCommon enterActiveNow;
        ExitCommon exitNextBar;
        EnterCommon enterNextBar;
	    ExitStrategy exitStrategy;

        EnterTiming enter;
		ExitTiming exit;
		ReverseTiming reverse;
		ChangeTiming change;
		
		public OrderHandlers(Strategy strategy)
		{
            exitStrategy = new ExitStrategy(strategy);
            exitActiveNow = new ExitCommon(strategy);
            enterActiveNow = new EnterCommon(strategy);
            enterActiveNow.processExitStrategy = ExitStrategy.OnProcessPosition;
            reverseActiveNow = new ReverseCommon(strategy);
            changeActiveNow = new ChangeCommon(strategy);
            changeNextBar = new ChangeCommon(strategy);
            changeNextBar.Orders = changeActiveNow.Orders;
            changeNextBar.IsNextBar = true;
            reverseNextBar = new ReverseCommon(strategy);
            reverseNextBar.Orders = reverseActiveNow.Orders;
            reverseNextBar.IsNextBar = true;
            exitNextBar = new ExitCommon(strategy);
            exitNextBar.Orders = exitActiveNow.Orders;
            exitNextBar.IsNextBar = true;
            enterNextBar = new EnterCommon(strategy);
            enterNextBar.processExitStrategy = ExitStrategy.OnProcessPosition;
            enterNextBar.Orders = enterActiveNow.Orders;
            enterNextBar.IsNextBar = true;
            
            this.enter = new EnterTiming(enterActiveNow, enterNextBar);
			this.exit = new ExitTiming(exitActiveNow,exitNextBar);
			this.reverse = new ReverseTiming(reverseActiveNow,reverseNextBar);
			this.change = new ChangeTiming(changeActiveNow,changeNextBar);
		}

        public void OnConfigure()
        {
            changeActiveNow.OnInitialize();
            changeNextBar.OnInitialize();
            reverseActiveNow.OnInitialize();
            reverseNextBar.OnInitialize();
            exitActiveNow.OnInitialize();
            enterActiveNow.OnInitialize();
            exitNextBar.OnInitialize();
            enterNextBar.OnInitialize();
            exitNextBar.OnInitialize();
        }
		
		public EnterTiming Enter {
			get { return enter; }
		}
		
		public ExitTiming Exit {
			get { return exit; }
		}
		
		public ReverseTiming Reverse {
			get { return reverse; }
		}
		
		public OrderHandlers.ChangeTiming Change {
			get { return change; }
		}

	    public ExitStrategy ExitStrategy
	    {
	        get { return exitStrategy; }
            set { exitStrategy = value; }
	    }

	    public class EnterTiming {
			EnterCommon activeNow;
			EnterCommon nextBar;
			
			public EnterTiming( EnterCommon now, EnterCommon nextBar) {
				this.activeNow = now;
				this.nextBar = nextBar;
			}
			
			public EnterCommon ActiveNow {
				get { return activeNow; }
			}
			
			public EnterCommon NextBar {
				get { return nextBar; }
			}

		    public bool AreBuyOrdersActive
		    {
                get { return activeNow.Orders.AreBuyOrdersActive; }
		    }

            public bool AreSellOrdersActive
            {
                get { return activeNow.Orders.AreSellOrdersActive; }
            }

            public bool AreBuyOrdersNextBar
            {
                get { return activeNow.Orders.AreBuyOrdersNextBar; }
            }

            public bool AreSellOrdersNextBar
            {
                get { return activeNow.Orders.AreSellOrdersNextBar; }
            }
        }
		
		public class ReverseTiming {
			ReverseCommon activeNow;
			ReverseCommon nextBar;
			
			public ReverseTiming( ReverseCommon now, ReverseCommon nextBar) {
				this.activeNow = now;
				this.nextBar = nextBar;
			}
			
			public ReverseCommon ActiveNow {
				get { return activeNow; }
			}
			
			public ReverseCommon NextBar {
				get { return nextBar; }
			}
		}
		
		public class ChangeTiming {
			ChangeCommon activeNow;
			ChangeCommon nextBar;
			
			public ChangeTiming( ChangeCommon now, ChangeCommon nextBar) {
				this.activeNow = now;
				this.nextBar = nextBar;
			}
			
			public ChangeCommon ActiveNow {
				get { return activeNow; }
			}
			
			public ChangeCommon NextBar {
				get { return nextBar; }
			}
		}
		
		public class ExitTiming {
			ExitCommon activeNow;
			ExitCommon nextBar;
			
			public ExitTiming( ExitCommon now, ExitCommon nextBar) {
				this.activeNow = now;
				this.nextBar = nextBar;
			}
			
			public ExitCommon ActiveNow {
				get { return activeNow; }
			}
			
			public ExitCommon NextBar {
				get { return nextBar; }
			}
		}

	    public void SetAutoCancel()
	    {
	        this.enter.ActiveNow.CancelOrders();
            this.exit.ActiveNow.CancelOrders();
            this.change.ActiveNow.CancelOrders();
            this.reverse.ActiveNow.CancelOrders();
	    }
	}
}
