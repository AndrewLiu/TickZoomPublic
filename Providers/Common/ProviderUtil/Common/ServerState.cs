namespace TickZoom.Provider.FIX
{
    public enum ServerState
    {
        Startup,
        Recovered,
        WaitingHeartbeat,
        ServerResend
    }
}