using System;
using System.Timers;

public class HeartBeatService
{
    int interval;
    public int timeout;
    Timer timer;
    DateTime lastTime;

    Protocol protocol;

    public HeartBeatService(int interval, Protocol protocol)
    {
        this.interval = interval * 1000;
        this.protocol = protocol;
    }

    internal void ResetTimeout()
    {
        this.timeout = 0;
        lastTime = DateTime.Now;
    }

    public void SendHeartBeat(object source, ElapsedEventArgs e)
    {
        TimeSpan span = DateTime.Now - lastTime;
        timeout = (int)span.TotalMilliseconds;

        //check timeout
        if (timeout > interval * 2)
        {
            protocol.GetPomeloClient().Disconnect();
            //stop();
            return;
        }

        //Send heart beat
		protocol.Send(enPackageType.Heartbeat);
    }

    public void Start()
    {
        if (interval < 1000) return;

        //start hearbeat
        this.timer = new Timer();
        timer.Interval = interval;
        timer.Elapsed += new ElapsedEventHandler(SendHeartBeat);
        timer.Enabled = true;

        //Set timeout
        timeout = 0;
        lastTime = DateTime.Now;
    }

    public void Stop()
    {
        if (this.timer != null)
        {
            this.timer.Enabled = false;
            this.timer.Dispose();
        }
    }
}