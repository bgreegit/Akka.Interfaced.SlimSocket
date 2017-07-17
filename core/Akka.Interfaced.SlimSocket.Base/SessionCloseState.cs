namespace Akka.Interfaced.SlimSocket
{
    // Adopted from tcp's close states
    public enum SessionCloseState
    {
        None = 0,
        // For active close
        FinWait1 = 1,
        FinWait2 = 2,
        Closing = 3,
        TimeWait = 4,
        // For passice close
        CloseWait = 5,
        LastAck = 6,
        Closed = 7,
    }
}
