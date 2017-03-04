using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

/*
该类在PomeloClient的基础上，进行了如下功能改进
1. 所有方法的回调均在主线程（原PomeloClient回调在Socket线程，用起来很麻烦）
2. 增加了DisconnectEvent和ErrorEvent两个事件通知，方便捕捉网络断开事件和其它异常
3. 所有报文回调时，会收到一个Message对象而不是之前的仅仅是一个json对象，方便上层逻辑查询Message信息。

注意：
1. Connection对象如果不用了，必须调用Disconnect方法释放相关资源，否则会导致资源泄漏
2. 详细使用示例请参考TestConnection.cs类
*/
public class PomeloClient
{
    /// 网络断开事件
    static public string DisconnectEvent = "Disconnect";
    /// 其它错误/异常。例如在DISCONNECTED下调用request，则会抛出该事件。注意：这些错误都不是网络错误
    /// 网络错误会通过DisconnectEvent抛出
    static public string ErrorEvent = "Error";

    static protected uint SYS_MSG_CONNECTED = 1;

    private Queue<Message> receiveMsgQueue;

    // Current network state
    public enNetWorkState netWorkState { get; protected set; }   

    private EventManager eventManager;
    private Socket m_socket;
    private Protocol m_protocol;
    private uint m_reqId = 100;

    public PomeloClient()
    {
        netWorkState = enNetWorkState.Disconnected;
        eventManager = new EventManager();

        receiveMsgQueue = new Queue<Message>();
    }

    public UnityEngine.WaitUntil InitClient(string host, int port)
    {
        bool bDone = false;
        Action<Message> callback = ret => { bDone = true; };
        InitClient(host, port, callback);

        return new UnityEngine.WaitUntil(() => bDone);
    }

    /// 初始化连接
    public void InitClient(string host, int port, Action<Message> callback)
    {
        _assert(netWorkState == enNetWorkState.Disconnected);

        UnityEngine.Debug.Log("Connect to " + host + " with port " + port);

        netWorkState = enNetWorkState.Connecting;

        IPAddress ipAddress = null;

        try
        {
            if (!IPAddress.TryParse(host, out ipAddress))
            {
                IPAddress[] addresses = Dns.GetHostEntry(host).AddressList;
                foreach (var item in addresses)
                {
                    if (item.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddress = item;
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _onDisconnect(e.Message);
            return;
        }

        if (ipAddress == null) throw new Exception("Cannot parse host : " + host);

        m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ie = new IPEndPoint(ipAddress, port);

        eventManager.RemoveCallback(SYS_MSG_CONNECTED);
        eventManager.AddCallback(SYS_MSG_CONNECTED, callback);

        m_socket.BeginConnect(ie, _onConnectCallback, m_socket);
    }

    public void Connect()
    {
        Connect(null, null);
    }

    public void Connect(MessageObject user)
    {
        Connect(user, null);
    }

    public void Connect(Action<MessageObject> handshakeCallback)
    {
        Connect(null, handshakeCallback);
    }

    public bool Connect(MessageObject user, Action<MessageObject> handshakeCallback)
    {
        try
        {
            m_protocol.Start(user, handshakeCallback);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            return false;
        }
    }

    private MessageObject emptyMsg = new MessageObject();
    public void Request(string route, Action<Message> action)
    {
		if (netWorkState != enNetWorkState.Connected)
        {
            _onError("Network is down, cannot send request now!");
            return;
        }

        Request(route, emptyMsg, action);
    }

    public void Request(string route, MessageObject msg, Action<Message> action)
    {
        if(netWorkState != enNetWorkState.Connected)
        {
            _onError("Network is down, cannot send request now!");
            return;
        }

        UnityEngine.Debug.Log(">>> Send: " + route + " data: " + msg.ToString());

        eventManager.AddCallback(m_reqId, action);
        m_protocol.Send(route, m_reqId, msg);

        m_reqId++;
        if(m_reqId >= uint.MaxValue) m_reqId = 100;
    }

    public void Notify(string route, MessageObject msg)
    {
        if (netWorkState != enNetWorkState.Connected)
        {
            _onError("Network is down, cannot send request now!");
            return;
        }

        m_protocol.Send(route, msg);
    }

    public void On(string eventName, Action<Message> action)
    {
        eventManager.AddOnEvent(eventName, action);
    }

    public void RemoveEventListeners(string eventName)
    {
        eventManager.RemoveOnEvent(eventName);
    }

    public void RemoveAllEventListeners()
    {
        eventManager.ClearEventMap();
    }

    internal void ProcessMessage(Message msg)
    {
        receiveMsgQueue.Enqueue(msg);
    }

    public void Update()
    {
        while(receiveMsgQueue.Count != 0)
        {
            var msg = receiveMsgQueue.Dequeue();

            switch(msg.type)
            {
                case enMessageType.Response:
                {
                    UnityEngine.Debug.Log("<<< Receive: " + msg.route + " data: " + msg.rawString);

                    UnityEngine.Debug.Assert(eventManager.GetCallbackCount() != 0);

                    eventManager.InvokeCallBack(msg.id, msg);
                    eventManager.RemoveCallback(msg.id);
                }
                    break;
                case enMessageType.Push:
                {
                    UnityEngine.Debug.Log("<<< Receive event: " + msg.route + " data: " + msg.rawString);
                    eventManager.InvokeOnEvent(msg.route, msg);
                }
                    break;
                case enMessageType.Sys:
                {
                     if (msg.id != 0)
                    {
                        eventManager.InvokeCallBack(msg.id, msg);
                        eventManager.RemoveCallback(msg.id);
                    }
                    else
                    {
                        eventManager.InvokeOnEvent(msg.route, msg);
                    }
                }
                    break;
            }
        }
    }
    
    public void Disconnect()
    {
        if (netWorkState == enNetWorkState.Disconnected) return;

        /// Force update to make sure all received messages been dispatched.
        Update();

        // free managed resources
        if (m_protocol != null) m_protocol.Close();

        try
        {
            m_socket.Shutdown(SocketShutdown.Both);
            m_socket.Close();
            m_socket = null;
        }
        catch (Exception)
        {
            //todo : 有待确定这里是否会出现异常，这里是参考之前官方github上pull request。emptyMsg
        }

        netWorkState = enNetWorkState.Disconnected;

        eventManager.ClearCallBackMap();
        eventManager.ClearCallBackMap();

        m_reqId = 100;
    }

    protected void _assert(bool bOperation, string msg = "")
    {
        if (!bOperation)
        {
            throw new Exception(msg);
        }
    }

    internal void _onError(string reason)
    {
        MessageObject jsonObj = new MessageObject();
        jsonObj.Add("reason", reason);
        Message msg = new Message(enMessageType.Sys, ErrorEvent, jsonObj);
        receiveMsgQueue.Enqueue(msg);
    }

    internal void _onDisconnect(string reason)
    {
        netWorkState = enNetWorkState.Disconnected;

        MessageObject jsonObj = new MessageObject();
        jsonObj.Add("reason", reason);
        Message msg = new Message(enMessageType.Sys, DisconnectEvent, jsonObj);
        receiveMsgQueue.Enqueue(msg);

        m_socket.Close();
        m_socket = null;
    }

    protected void _onConnectCallback(IAsyncResult result)
    {
        try
        {
            netWorkState = enNetWorkState.Connected;

            m_socket.EndConnect(result);
            m_protocol = new Protocol(this, m_socket);

            Message msg = new Message(enMessageType.Sys, SYS_MSG_CONNECTED);
            receiveMsgQueue.Enqueue(msg);
        }
        catch (SocketException e)
        {
            _onDisconnect(e.Message);
        }
    }
}