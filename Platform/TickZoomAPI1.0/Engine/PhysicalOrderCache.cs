using System;
using System.Collections.Generic;

namespace TickZoom.Api
{
    public interface PhysicalOrderCache : IDisposable
    {
        void SetOrder(PhysicalOrder order);
        PhysicalOrder RemoveOrder(long clientOrderId);
        IEnumerable<PhysicalOrder> GetActiveOrders(SymbolInfo symbol);
        IEnumerable<PhysicalOrder> GetOrders(Func<PhysicalOrder, bool> select);
        bool TryGetOrderById(long brokerOrder, out PhysicalOrder order);
        bool TryGetOrderBySequence(int sequence, out PhysicalOrder order);
        PhysicalOrder GetOrderById(long brokerOrder);
        bool TryGetOrderBySerial(long logicalSerialNumber, out PhysicalOrder order);
        PhysicalOrder GetOrderBySerial(long logicalSerialNumber);
        bool HasCancelOrder(PhysicalOrder order);
        bool HasCreateOrder(PhysicalOrder order);
        void ResetLastChange();
        void SetActualPosition(SymbolInfo symbol, long position);
        long GetActualPosition(SymbolInfo symbol);
        long IncreaseActualPosition(SymbolInfo symbol, long increase);
        void SetStrategyPosition(SymbolInfo symbol, int strategyId, long position);
        long GetStrategyPosition(int strategyId);
        void SyncPositions(Iterable<StrategyPosition> strategyPositions);
        string StrategyPositionsToString();
        string SymbolPositionsToString();
        void LogOrders(Log log);
        List<PhysicalOrder> GetOrdersList(Func<PhysicalOrder, bool> func);
        void PurgeOriginalOrder(PhysicalOrder order);
        int GetHighestSequence();
    }

    public interface PhysicalOrderStore : PhysicalOrderCache
    {
        string DatabasePath { get; }
        long SnapshotRolloverSize { get; set; }
        int RemoteSequence { get; }
        int LocalSequence { get; }
        void WaitForSnapshot();
        void ForceSnapshot();
        bool Recover();
        void UpdateLocalSequence(int localSequence);
        void UpdateRemoteSequence(int remoteSequence);
        void SetSequences(int remoteSequence, int localSequence);
        TimeStamp LastSequenceReset { get; set; }
        int Count();
        void TrySnapshot();
        IDisposable BeginTransaction();
        void EndTransaction();
        void AssertAtomic();
    }
}