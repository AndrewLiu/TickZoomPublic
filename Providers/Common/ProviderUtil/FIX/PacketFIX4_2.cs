
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
	public class MessageFIX4_2 : MessageFIXT1_1 {
        private static readonly Log log = Factory.SysLog.GetLogger(typeof(MessageFIX4_2));
		private static readonly bool debug = log.IsDebugEnabled;
		private static readonly bool trace = log.IsTraceEnabled;

        string account = null;
        string massStatusRequestId = null;
        string orderStatus = null;
        string destination = null;
        int orderQuantity = 0;
        double averagePrice = 0D;
        string orderId = null;
        string positionEffect = null;
        string orderType = null;
        string originalClientOrderId = null;
        string clientOrderId = null;
        double price = 0D;
        string symbol = null;
        int cumulativeQuantity = 0;
        int lastQuantity = 0;
        double lastPrice = 0D;
        string executionId = null;
        int productType = 0;
        string side = null;
        string timeInForce = null;
        string executionType = null;
        string internalOrderId = null;
        string transactionTime = null;
        string originalSendTime = null;
        int leavesQuantity = 0;
        int longQuantity = 0;
        int shortQuantity = 0;
        private int subscriptionRequestType;
        private string tradingSessionRequestId;
        private string tradingSessionId;
        private int tradingSessionStatus;
        private string tradingSessionSubId;
	    private int maxFloor;
        private string algorithm;
        private int postQuantity;
        private int refillQuantity;

	    public override void Clear()
        {
            base.Clear();
            account = null;

            massStatusRequestId = null;
            orderStatus = null;
            destination = null;
            orderQuantity = 0;
            averagePrice = 0D;
            orderId = null;
            positionEffect = null;
            orderType = null;
            originalClientOrderId = null;
            clientOrderId = null;
            price = 0D;
            symbol = null;
            cumulativeQuantity = 0;
            lastQuantity = 0;
            lastPrice = 0D;
            executionId = null;
            productType = 0;
            side = null;
            timeInForce = null;
            executionType = null;
            internalOrderId = null;
            transactionTime = null;
            leavesQuantity = 0;
            longQuantity = 0;
            shortQuantity = 0;
            subscriptionRequestType = 0;
            tradingSessionRequestId = null;
            tradingSessionId = null;
            tradingSessionStatus = 0;
            tradingSessionSubId = null;
	        maxFloor = 0;
	        algorithm = null;
	        postQuantity = 0;
	        refillQuantity = 0;
        }

 
        protected override bool HandleKey(int key)
        {
            bool result = false;
            switch (key)
            {
                case 1:
                    result = GetString(out account);
                    break;
                case 6:
                    result = GetDouble(out averagePrice);
                    break;
                case 11:
                    result = GetString(out clientOrderId);
                    break;
                case 14:
                    result = GetInt(out cumulativeQuantity);
                    break;
                case 17:
                    result = GetString(out executionId);
                    break;
                case 31:
                    result = GetDouble(out lastPrice);
                    break;
                case 32:
                    result = GetInt(out lastQuantity);
                    break;
                case 37:
                    result = GetString(out orderId);
                    break;
                case 38:
                    result = GetInt(out orderQuantity);
                    break;
                case 39:
                    result = GetString(out orderStatus);
                    break;
                case 40:
                    result = GetString(out orderType);
                    break;
                case 41:
                    result = GetString(out originalClientOrderId);
                    break;
                case 44:
                    result = GetDouble(out price);
                    break;
                case 54:
                    result = GetString(out side);
                    break;
                case 55:
                    result = GetString(out symbol);
                    break;
                case 59:
                    result = GetString(out timeInForce);
                    break;
                case 60:
                    result = GetString(out transactionTime);
                    break;
                case 77:
                    result = GetString(out positionEffect);
                    break;
                case 100:
                    result = GetString(out destination);
                    break;
                case 111:
                    result = GetInt(out maxFloor);
                    break;
                case 122:
                    result = GetString(out originalSendTime);
                    break;
                case 150:
                    result = GetString(out executionType);
                    break;
                case 151:
                    double temp;
                    result = GetDouble(out temp);
                    leavesQuantity = (int)temp;
                    break;
                case 263:
                    result = GetInt(out subscriptionRequestType);
                    break;
                case 335:
                    result = GetString(out tradingSessionRequestId);
                    break;
                case 336:
                    result = GetString(out tradingSessionId);
                    break;
                case 340:
                    result = GetInt(out tradingSessionStatus);
                    break;
                case 460:
                    result = GetInt(out productType);
                    break;
                case 584:
                    result = GetString(out massStatusRequestId);
                    break;
                case 625:
                    result = GetString(out tradingSessionSubId);
                    break;
                case 704:
                    result = GetInt(out longQuantity);
                    break;
                case 705:
                    result = GetInt(out shortQuantity);
                    break;
                case 9144:
                    result = GetInt(out postQuantity);
                    break;
                case 9116:
                    result = GetInt(out refillQuantity);
                    break;
                case 10017:
                    result = GetString(out internalOrderId);
                    break;
                case 9100:
                    result = GetString(out algorithm);
                    break;
                default:
                    result = base.HandleKey(key);
                    break;
            }
            return result;
        }


        /// <summary>
        /// 39 Status of the order
        ///  	Identifies current status of order.
        ///	Valid values:
        ///	0 = New
        ///	1 = Partially filled
        ///	2 = Filled
        ///	3 = Done for day
        ///	4 = Canceled
        ///	5 = Replaced (Removed/Replaced)
        ///	6 = Pending Cancel (e.g. result of Order Cancel Request)
        ///	7 = Stopped
        ///	8 = Rejected
        ///	9 = Suspended
        ///	A = Pending New
        ///	B = Calculated
        ///	C = Expired
        ///	D = Accepted for bidding
        ///	E = Pending Replace (e.g. result of Order Cancel/Replace Request)
        ///	H = Break
        ///	I = Status
        ///	R = Resumed
        /// </summary>
        public string OrderStatus
        {
            get { return orderStatus; }
        }

        /// <summary>
        /// 1 Account specified for this order
        /// </summary>
        public string Account
        {
            get { return account; }
        }

        /// <summary>
        /// 100 MBTX, Execution destination as defined by institution when order is entered
        /// </summary>
        public string Destination
        {
            get { return destination; }
        }

        /// <summary>
        /// 38 Quantity entered on order
        /// </summary>
        public int OrderQuantity
        {
            get { return orderQuantity; }
        }

        /// <summary>
        /// 6 Average price of executed shares to this point
        /// </summary>
        public double AveragePrice
        {
            get { return averagePrice; }
        }

        /// <summary>
        /// 31 Price filled on this execution report.
        /// </summary>
        public double LastPrice
        {
            get { return lastPrice; }
        }

        /// <summary>
        /// 32 Quantity filled on this execution report.
        /// </summary>
        public int LastQuantity
        {
            get { return lastQuantity; }
        }

        /// <summary>
        /// 37 Unique OrderId given to the execution
        /// </summary>
        public string OrderId
        {
            get { return orderId; }
        }

        /// <summary>
        /// 584 Value assigned by issuer of Mass Status Request to uniquely identify the request (GTS)
        /// </summary>
        public string MassStatusRequestId
        {
            get { return massStatusRequestId; }
        }

        /// <summary>
        /// 77 Stated whether this order or fill results in opening a
        /// position or closing a position. Supported values: O (Open), C (Closed)
        /// </summary>
        public string PositionEffect
        {
            get { return positionEffect; }
        }

        /// <summary>
        /// 40 Type of the order
        ///	Valid values:
        ///	1 = Market
        ///	2 = Limit
        ///	3 = Stop
        ///	4 = Stop limit
        ///	6 = With or without
        ///	8 = Limit with or without
        ///	9 = On basis
        ///	D = Previously quoted
        ///	E = Previously indicated
        ///	G = Forex - Swap
        ///	I = Funari (Limit Day Order with unexecuted portion handled as Market On Close. E.g. Japan)
        ///	J = Market If Touched (MIT)
        ///	K = Market with Leftover as Limit (market order then unexecuted quantity becomes limit order at last price)
        ///	L = Previous Fund Valuation Point (Historic pricing) (for CIV)
        ///	M = Next Fund Valuation Point �(Forward pricing) (for CIV)
        ///	P = Pegged 
        /// </summary>
        public string OrderType
        {
            get { return orderType; }
        }

        /// <summary>
        /// 11 The client assigned ID to which this Execution report refers
        /// </summary>
        public string ClientOrderId
        {
            get { return clientOrderId; }
        }

        /// <summary>
        /// 44 Price entered on order
        /// </summary>
        public double Price
        {
            get { return price; }
        }

        /// <summary>
        /// 55 Stock Symbol, or Currency Pair
        /// </summary>
        public string Symbol
        {
            get { return symbol; }
        }

        /// <summary>
        /// 14 Total number of shares filled to this point
        /// </summary>
        public int CumulativeQuantity
        {
            get { return cumulativeQuantity; }
        }

        /// <summary>
        /// 17 Unique String value
        /// </summary>
        public string ExecutionId
        {
            get { return executionId; }
        }

        /// <summary>
        /// 460 Returned on pending acknowledgment of the order.
        /// Supported values are: 2 Commodity (Futures), 4 Currency (Forex), 5 (Equities), 12 Other (Options)
        /// </summary>
        public int ProductType
        {
            get { return productType; }
        }

        /// <summary>
        /// 54 Side of trade, as defined above
        ///	Valid values:
        ///	1 = Buy
        ///	2 = Sell
        ///	3 = Buy minus
        ///	4 = Sell plus
        ///	5 = Sell short
        ///	6 = Sell short exempt
        ///	7 = Undisclosed (valid for IOI and List Order messages only)
        ///	8 = Cross (orders where counterparty is an exchange, valid for all messages except IOIs)
        ///	9 = Cross short
        ///	A = Cross short exempt
        ///	B = �As Defined� (for use with multileg instruments)
        ///	C = �Opposite� (for use with multileg instruments)
        ///	D = Subscribe (e.g. CIV)
        ///	E = Redeem (e.g. CIV)
        ///	F = Lend (FINANCING - identifies direction of collateral)
        ///	G = Borrow (FINANCING - identifies direction of collateral)
        /// </summary>
        public string Side
        {
            get { return side; }
        }

        /// <summary>
        /// 59 TimeInForce of the order
        /// </summary>
        public string TimeInForce
        {
            get { return timeInForce; }
        }

        /// <summary>
        /// 150 What this execution report suggests. Possible values are:
        /// 0=New, 1=Partial Fill, 2=Fill, and 4=Canceled
        /// </summary>
        public string ExecutionType
        {
            get { return executionType; }
        }

        /// <summary>
        /// 10017 internal order id
        /// </summary>
        public string InternalOrderId
        {
            get { return internalOrderId; }
        }

        /// <summary>
        /// 60 Time of the Execution, expressed in UTC
        /// </summary>
        public string TransactionTime
        {
            get { return transactionTime; }
        }

        /// <summary>
        /// 122 Time of original message, expressed in UTC
        /// </summary>
        public string OriginalSendTime
        {
            get { return transactionTime; }
        }

        /// <summary>
        /// 151 Amount of shares still live in the Order
        /// </summary>
        public int LeavesQuantity
        {
            get { return leavesQuantity; }
        }

        /// <summary>
        /// 704 Quantity of a long position
        /// </summary>
        public int LongQuantity
        {
            get { return longQuantity; }
        }

        /// <summary>
        /// 705 Quantity of a short position
        /// </summary>
        public int ShortQuantity
        {
            get { return shortQuantity; }
        }

        /// <summary>
        /// 41 The client id originally on this order. 
        /// </summary>
        public string OriginalClientOrderId
        {
            get { return originalClientOrderId; }
        }

        public int SubscriptionRequestType
        {
            get { return subscriptionRequestType; }
        }

        public string TradingSessionRequestId
        {
            get { return tradingSessionRequestId; }
        }

        public string TradingSessionId
        {
            get { return tradingSessionId; }
        }

        public int TradingSessionStatus
        {
            get { return tradingSessionStatus; }
        }

        public string TradingSessionSubId
        {
            get { return tradingSessionSubId; }
        }

	    public int MaxFloor
	    {
	        get { return maxFloor; }
	    }

	    public string Algorithm
	    {
	        get { return algorithm; }
	    }

	    public int PostQuantity
	    {
	        get { return postQuantity; }
	    }

	    public int RefillQuantity
	    {
	        get { return refillQuantity; }
	    }
	}
}
