using System;
using System.Text;

public class HandShakeService
{
    private Protocol protocol;
    private Action<MessageObject> callback;

    public const string Version = "0.3.0";
    public const string Type = "unity-socket";


    public HandShakeService(Protocol protocol)
    {
        this.protocol = protocol;
    }

    public void Request(MessageObject user, Action<MessageObject> callback)
    {
        byte[] body = Encoding.UTF8.GetBytes(BuildMsg(user).ToString());

        protocol.Send(enPackageType.Handshake, body);

        this.callback = callback;
    }

    internal void InvokeCallback(MessageObject data)
    {
        //Invoke the handshake callback
        if (callback != null) callback.Invoke(data);
    }

    public void Ack()
    {
        protocol.Send(enPackageType.HandshakeAck, new byte[0]);
    }

    private MessageObject BuildMsg(MessageObject user)
    {
        if (user == null) user = new MessageObject();

        MessageObject msg = new MessageObject();

        //Build sys option
        MessageObject sys = new MessageObject();
        sys["version"] = Version;
        sys["type"] = Type;

        //Build handshake message
        msg["sys"] = sys;
        msg["user"] = user;

        return msg;
    }
}