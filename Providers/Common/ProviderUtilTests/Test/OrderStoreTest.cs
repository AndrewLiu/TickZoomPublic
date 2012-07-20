using System.Threading;
using NUnit.Framework;
using TickZoom.Api;
using System.IO;
using System;
using System.Text;
using TickZoom.Provider.FIX;

namespace Test
{
    [TestFixture]
    public class OrderStoreTest 
    {
        public static readonly Log log = Factory.SysLog.GetLogger(typeof(OrderStoreTest));

        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void WriteAndReadByIdTest()
        {
            using( var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                var symbolInfo = Factory.Symbol.LookupSymbol("EUR/USD");
                var clientId = 010101010101L;
                var order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell, OrderType.Limit, OrderFlags.None,
                                                          124.34, 1234, 14, 100000334, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                store.SetSequences(1,1);
                var result = store.GetOrderById(clientId);
                Assert.AreEqual(order.LogicalSerialNumber, result.LogicalSerialNumber);
            }
        }

        [Test]
        public void WriteAndReadBySerialTest()
        {
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                var symbolInfo = Factory.Symbol.LookupSymbol("EUR/USD");
                var clientId = 010101010101L;
                var logicalSerial = 100000335;
                var order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell,
                                                          OrderType.Limit, OrderFlags.None,
                                                          124.34, 1234, 14, logicalSerial, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                store.SetSequences(1, 1);
                var result = store.GetOrderBySerial(logicalSerial);
                Assert.AreEqual(order.BrokerOrder, result.BrokerOrder);
            }
        }

        [Test]
        public void ReplaceAndReadIdTest()
        {
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                var symbolInfo = Factory.Symbol.LookupSymbol("EUR/USD");
                var clientId = 010101010101L;
                var logicalSerial = 100000335;
                var order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell,
                                                          OrderType.Limit, OrderFlags.None,
                                                          124.34, 1234, 14, logicalSerial, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell, OrderType.Limit, OrderFlags.None,
                                                      124.34, 1234, 14, logicalSerial, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                var result = store.GetOrderBySerial(logicalSerial);
                Assert.AreEqual(order.BrokerOrder, result.BrokerOrder);
            }
        }

        [Test]
        public void DumpDataBase()
        {
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                var list = store.GetOrders((x) => true);
                foreach (var order in list)
                {
                    log.Info(order.ToString());
                }
            }
        }

        [Test]
        public void ReplaceAndReadSerialTest()
        {
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                var symbolInfo = Factory.Symbol.LookupSymbol("EUR/USD");
                var clientId = 010101010101L;
                var logicalSerial = 100000335;
                var order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell,
                                                          OrderType.Limit, OrderFlags.None,
                                                          124.34, 1234, 14, logicalSerial, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                clientId = 010101010101L;
                order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell, OrderType.Limit, OrderFlags.None,
                                                      124.34, 1234, 14, logicalSerial, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                var result = store.GetOrderBySerial(logicalSerial);
                Assert.AreEqual(clientId, result.BrokerOrder);
            }
        }

        [Test]
        public void SelectBySymbolTest()
        {
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                var symbolInfo = Factory.Symbol.LookupSymbol("EUR/USD");
                var clientId = 010101010101L;
                var logicalSerial = 100000335;
                var order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell,
                                                          OrderType.Limit, OrderFlags.None,
                                                          124.34, 1234, 14, logicalSerial, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                order = Factory.Utility.PhysicalOrder(OrderAction.Create, OrderState.Active, symbolInfo, OrderSide.Sell, OrderType.Limit, OrderFlags.None,
                                                      124.34, 1234, 14, logicalSerial + 1, clientId, null, TimeStamp.UtcNow);
                store.SetOrder(order);
                var list = store.GetOrders((o) => o.Symbol.BaseSymbol == "EUR/USD");
                var enumerator = list.GetEnumerator();
                var count = 0;
                PhysicalOrder firstItem = null;
                if( enumerator.MoveNext())
                {
                    count++;
                    firstItem = enumerator.Current;

                }
                
                Assert.AreEqual(1, count);
                Assert.AreEqual(order.BrokerOrder, firstItem.BrokerOrder);
                Assert.AreEqual(logicalSerial + 1, firstItem.LogicalSerialNumber);
            }
        }

        [Test]
        public void ReadOrders()
        {
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                var list = store.GetOrders((x) => true);
                foreach (var order in list)
                {
                    log.Info(order.ToString());
                }
            }
        }

        [Test]
        public void WriteSnapShotTest()
        {
            string dbpath;
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                dbpath = store.DatabasePath;
            }
            File.Delete(dbpath);
            var symbolInfo = Factory.Symbol.LookupSymbol("EUR/USD");
            var clientId1 = 010101010101L;
            var logicalSerial = 100000335;
            var price = 124.34;
            var size = 1234;
            var logicalId = 14;
            var state = OrderState.Active;
            var side = OrderSide.Sell;
            var type = OrderType.Limit;
            var order1 = Factory.Utility.PhysicalOrder(OrderAction.Create, state, symbolInfo, side,
                                                      type, OrderFlags.None,
                                                      price, size, logicalId, logicalSerial, clientId1, null, TimeStamp.UtcNow);
            var clientId2 = 020202020202L;
            logicalSerial = 100000336;
            price = 432.13;
            size = 4321;
            logicalId = 41;
            state = OrderState.Active;
            side = OrderSide.Sell;
            type = OrderType.Limit;
            var order2 = Factory.Utility.PhysicalOrder(OrderAction.Create, state, symbolInfo, side, type, OrderFlags.None,
                                                      price, size, logicalId, logicalSerial, clientId2, null, TimeStamp.UtcNow);
            order1.ReplacedBy = order2;

            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                store.SetOrder(order1);
                store.TrySnapshot();
                store.WaitForSnapshot();

                // Replace order in store to make new snapshot.
                store.SetOrder(order2);
                store.TrySnapshot();
                store.WaitForSnapshot();
            }

            using (var fs = new FileStream(dbpath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Console.WriteLine("File size = " + fs.Length);
            }

            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                store.Recover();

                Assert.AreEqual(2,store.Count());

                var result1 = store.GetOrderById(clientId1);
                Assert.AreEqual(order1.Price, result1.Price);
                Assert.AreEqual(order1.RemainingSize, result1.RemainingSize);
                Assert.AreEqual(order1.BrokerOrder, result1.BrokerOrder);
                Assert.AreEqual(order1.Symbol, result1.Symbol);
                Assert.AreEqual(order1.OrderState, result1.OrderState);
                Assert.AreEqual(order1.Side, result1.Side);
                Assert.AreEqual(order1.Type, result1.Type);
                Assert.AreEqual(order1.LogicalSerialNumber, result1.LogicalSerialNumber);

                var result2 = store.GetOrderById(clientId2);
                Assert.AreEqual(order2.Price, result2.Price);
                Assert.AreEqual(order2.RemainingSize, result2.RemainingSize);
                Assert.AreEqual(order2.BrokerOrder, result2.BrokerOrder);
                Assert.AreEqual(order2.Symbol, result2.Symbol);
                Assert.AreEqual(order2.OrderState, result2.OrderState);
                Assert.AreEqual(order2.Side, result2.Side);
                Assert.AreEqual(order2.Type, result2.Type);
                Assert.AreEqual(order2.LogicalSerialNumber, result2.LogicalSerialNumber);
                Assert.IsTrue(object.ReferenceEquals(result1.ReplacedBy,result2));
            }
        }

        [Test]
        public void SnapShotRollOverTest()
        {
            string dbpath;
            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                dbpath = store.DatabasePath;
            }
            File.Delete(dbpath);
            var symbolInfo = Factory.Symbol.LookupSymbol("EUR/USD");
            var clientId1 = 010101010101L;
            var logicalSerial = 100000335;
            var price = 124.34;
            var size = 1234;
            var logicalId = 14;
            var state = OrderState.Active;
            var side = OrderSide.Sell;
            var type = OrderType.Limit;
            var order1 = Factory.Utility.PhysicalOrder(OrderAction.Create, state, symbolInfo, side,
                                                      type, OrderFlags.None,
                                                      price, size, logicalId, logicalSerial, clientId1, null, TimeStamp.UtcNow);
            var clientId2 = 020202020202L;
            logicalSerial = 100000336;
            price = 432.13;
            size = 4321;
            logicalId = 41;
            state = OrderState.Active;
            side = OrderSide.Sell;
            type = OrderType.Limit;
            var order2 = Factory.Utility.PhysicalOrder(OrderAction.Create, state, symbolInfo, side, type, OrderFlags.None,
                                                      price, size, logicalId, logicalSerial, clientId2, null, TimeStamp.UtcNow);
            order1.ReplacedBy = order2;

            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                using( store.BeginTransaction())
                {
                    store.SnapshotRolloverSize = 1000;
                    store.SetOrder(order1);
                }
                store.WaitForSnapshot();

                using (store.BeginTransaction())
                {
                    // Replace order in store to make new snapshot.
                    store.SetOrder(order2);
                }
                store.TrySnapshot();
                store.WaitForSnapshot();

                for (int i = 0; i < 20; i++)
                {
                    store.TrySnapshot();
                    store.WaitForSnapshot();
                }
            }

            Exception deleteException = null;
            for (var end = Environment.TickCount + 5000; Environment.TickCount < end; )
            {
                try
                {
                    File.Delete(dbpath);
                    deleteException = null;
                    break;
                }
                catch( Exception ex)
                {
                    Thread.Sleep(1000);
                    deleteException = ex;
                }
            }
            if( deleteException != null)
            {
                throw new ApplicationException("Failed to delete file.", deleteException);
            }
            File.WriteAllText(dbpath,"This is a test for corrupt snapshot file.");

            using (var store = Factory.Utility.PhyscalOrderStore("OrderStoreTest"))
            {
                store.Recover();

                Assert.AreEqual(2, store.Count());

                PhysicalOrder result1;
                PhysicalOrder result2;
                using (store.BeginTransaction())
                {
                    result1 = store.GetOrderById(clientId1);
                }
                Assert.AreEqual(order1.Price, result1.Price);
                Assert.AreEqual(order1.RemainingSize, result1.RemainingSize);
                Assert.AreEqual(order1.BrokerOrder, result1.BrokerOrder);
                Assert.AreEqual(order1.Symbol, result1.Symbol);
                Assert.AreEqual(order1.OrderState, result1.OrderState);
                Assert.AreEqual(order1.Side, result1.Side);
                Assert.AreEqual(order1.Type, result1.Type);
                Assert.AreEqual(order1.LogicalSerialNumber, result1.LogicalSerialNumber);

                using (store.BeginTransaction())
                {
                    result2 = store.GetOrderById(clientId2);
                }
                Assert.AreEqual(order2.Price, result2.Price);
                Assert.AreEqual(order2.RemainingSize, result2.RemainingSize);
                Assert.AreEqual(order2.BrokerOrder, result2.BrokerOrder);
                Assert.AreEqual(order2.Symbol, result2.Symbol);
                Assert.AreEqual(order2.OrderState, result2.OrderState);
                Assert.AreEqual(order2.Side, result2.Side);
                Assert.AreEqual(order2.Type, result2.Type);
                Assert.AreEqual(order2.LogicalSerialNumber, result2.LogicalSerialNumber);
                Assert.IsTrue(object.ReferenceEquals(result1.ReplacedBy, result2));
            }
        }
    }
}