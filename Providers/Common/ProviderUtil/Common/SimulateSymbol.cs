using System;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public interface SimulateSymbol : AgentPerformer
    {
        bool IsOnline { get; set; }
        int ActualPosition { get; }
        FillSimulator FillSimulator { get; }
        SymbolInfo Symbol { get; }
        void CreateOrder(PhysicalOrder order);
        void TryProcessAdjustments();
        bool ChangeOrder(PhysicalOrder order);
        void CancelOrder(PhysicalOrder order);
        PhysicalOrder GetOrderById(long clientOrderId);
    }
}