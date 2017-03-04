using System;
using System.Text;

public class Protocol
{
    private MessageProtocol messageProtocol;
    private enProtocolState state;
    private Transporter transporter;
    private HandShakeService handshake;
    private HeartBeatService heartBeatService = null;
    private PomeloClient pomeloClient;

    public PomeloClient GetPomeloClient()
    {
        return this.pomeloClient;
    }

    public Protocol(PomeloClient pc, System.Net.Sockets.Socket socket)
    {
        this.pomeloClient = pc;
        this.transporter = new Transporter(socket, this.processMessage);
        this.transporter.onDisconnect = OnDisconnect;

        this.handshake = new HandShakeService(this);
        this.state = enProtocolState.start;
    }

    internal void Start(MessageObject user, Action<MessageObject> callback)
    {
        this.transporter.Start();
        this.handshake.Request(user, callback);

        this.state = enProtocolState.handshaking;
    }

    //Send notify, do not need id
    internal void Send(string route, MessageObject msg)
    {
        Send(route, 0, msg);
    }

    //Send request, user request id 
    internal void Send(string route, uint id, MessageObject msg)
    {
        if (this.state != enProtocolState.working) return;

        byte[] body = messageProtocol.Encode(route, id, msg);

        Send(enPackageType.Data, body);
    }

    internal void Send(enPackageType type)
    {
        if (this.state == enProtocolState.closed) return;
        transporter.Send(PackageProtocol.Encode(type));
    }

    //Send system message, these message do not use messageProtocol
    internal void send(enPackageType type, MessageObject msg)
    {
        //This method only used to send system package
        if (type == enPackageType.Data) return;

        byte[] body = Encoding.UTF8.GetBytes(msg.ToString());

        Send(type, body);
    }

    //Send message use the transporter
    internal void Send(enPackageType type, byte[] body)
    {
        if (this.state == enProtocolState.closed) return;

        byte[] pkg = PackageProtocol.Encode(type, body);

        transporter.Send(pkg);
    }

    //Invoke by Transporter, process the message
    internal void processMessage(byte[] bytes)
    {
        Package pkg = PackageProtocol.Decode(bytes);

        //Ignore all the message except handshading at handshake stage
        if (pkg.type == enPackageType.Handshake && this.state == enProtocolState.handshaking)
        {

            //Ignore all the message except handshading
            MessageObject data = (MessageObject)SimpleJson.SimpleJson.DeserializeObject(Encoding.UTF8.GetString(pkg.body));

            ProcessHandshakeData(data);

            this.state = enProtocolState.working;

        }
        else if (pkg.type == enPackageType.Heartbeat && this.state == enProtocolState.working)
        {
            this.heartBeatService.ResetTimeout();
        }
        else if (pkg.type == enPackageType.Data && this.state == enProtocolState.working)
        {
            this.heartBeatService.ResetTimeout();
            pomeloClient.ProcessMessage(messageProtocol.Decode(pkg.body));
        }
        else if (pkg.type == enPackageType.Kick)
        {
            this.GetPomeloClient().Disconnect();
            this.Close();
        }
    }

    private void ProcessHandshakeData(MessageObject msg)
    {
        //Handshake error
        if (!msg.ContainsKey("code") || !msg.ContainsKey("sys") || Convert.ToInt32(msg["code"]) != 200)
        {
            throw new Exception("Handshake error! Please check your handshake config.");
        }

        //Set compress data
        MessageObject sys = (MessageObject)msg["sys"];

        MessageObject dict = new MessageObject();
        if (sys.ContainsKey("dict")) dict = (MessageObject)sys["dict"];

        MessageObject protos = new MessageObject();
        MessageObject serverProtos = new MessageObject();
        MessageObject clientProtos = new MessageObject();

        if (sys.ContainsKey("protos"))
        {
            protos = (MessageObject)sys["protos"];
            serverProtos = (MessageObject)protos["server"];
            clientProtos = (MessageObject)protos["client"];
        }

        messageProtocol = new MessageProtocol(dict, serverProtos, clientProtos);

        //Init heartbeat service
        int interval = 0;
        if (sys.ContainsKey("heartbeat")) interval = Convert.ToInt32(sys["heartbeat"]);
        heartBeatService = new HeartBeatService(interval, this);

        if (interval > 0)
        {
            heartBeatService.Start();
        }

        //send ack and change protocol state
        handshake.Ack();
        this.state = enProtocolState.working;

        //Invoke handshake callback
        MessageObject user = new MessageObject();
        if (msg.ContainsKey("user")) user = (MessageObject)msg["user"];
        handshake.InvokeCallback(user);
    }

    //The socket disconnect
    private void OnDisconnect(string reason)
    {
        this.pomeloClient._onDisconnect(reason);
    }

    internal void Close()
    {
        transporter.Close();

        if (heartBeatService != null) heartBeatService.Stop();

        this.state = enProtocolState.closed;
    }
}