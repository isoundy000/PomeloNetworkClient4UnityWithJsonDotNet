using System;
using System.Net.Sockets;

class StateObject
{
    public const int BufferSize = 1024;
    internal byte[] buffer = new byte[BufferSize];
}

public class Transporter
{
    public const int HeadLength = 4;

    private Socket socket;
    private Action<byte[]> messageProcesser;

    //Used for get message
    private StateObject stateObject = new StateObject();
    private enTransportState enTransportState;
    private byte[] headBuffer = new byte[4];
    private byte[] buffer;
    private int bufferOffset = 0;
    private int pkgLength = 0;
    internal Action<String> onDisconnect = null;

    public Transporter(Socket socket, Action<byte[]> processer)
    {
        this.socket = socket;
        this.messageProcesser = processer;
        enTransportState = enTransportState.readHead;
    }

    public void Start()
    {
        this.Receive();
    }

    public void Send(byte[] buffer)
    {
        if (this.enTransportState != enTransportState.closed)
        {
            socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(SendCallback), socket);
        }
    }

    private void SendCallback(IAsyncResult asyncSend)
    {
        if (this.enTransportState == enTransportState.closed) return;
        socket.EndSend(asyncSend);
    }

    public void Receive()
    {
        socket.BeginReceive(stateObject.buffer, 0, stateObject.buffer.Length, SocketFlags.None, new AsyncCallback(EndReceive), stateObject);
    }

    internal void Close()
    {
        this.enTransportState = enTransportState.closed;
    }

    private void EndReceive(IAsyncResult asyncReceive)
    {
        if (this.enTransportState == enTransportState.closed)
            return;
        StateObject state = (StateObject)asyncReceive.AsyncState;
        Socket socket = this.socket;

        try
        {
            int length = socket.EndReceive(asyncReceive);

            if (length > 0)
            {
                ProcessBytes(state.buffer, 0, length);
                //Receive next message
                if (this.enTransportState != enTransportState.closed) Receive();
            }
            else
            {
                if (this.onDisconnect != null) this.onDisconnect("Disconnected by server");
            }

        }
        catch (System.Net.Sockets.SocketException e)
        {
            if (this.onDisconnect != null) this.onDisconnect(e.Message);
        }
    }

    internal void ProcessBytes(byte[] bytes, int offset, int limit)
    {
        if (this.enTransportState == enTransportState.readHead)
        {
            ReadHead(bytes, offset, limit);
        }
        else if (this.enTransportState == enTransportState.readBody)
        {
            ReadBody(bytes, offset, limit);
        }
    }

    private bool ReadHead(byte[] bytes, int offset, int limit)
    {
        int length = limit - offset;
        int headNum = HeadLength - bufferOffset;

        if (length >= headNum)
        {
            //Write head buffer
            WriteBytes(bytes, offset, headNum, bufferOffset, headBuffer);
            //Get package length
            pkgLength = (headBuffer[1] << 16) + (headBuffer[2] << 8) + headBuffer[3];

            //Init message buffer
            buffer = new byte[HeadLength + pkgLength];
            WriteBytes(headBuffer, 0, HeadLength, buffer);
            offset += headNum;
            bufferOffset = HeadLength;
            this.enTransportState = enTransportState.readBody;

            if (offset <= limit) ProcessBytes(bytes, offset, limit);
            return true;
        }
        else
        {
            WriteBytes(bytes, offset, length, bufferOffset, headBuffer);
            bufferOffset += length;
            return false;
        }
    }

    private void ReadBody(byte[] bytes, int offset, int limit)
    {
        int length = pkgLength + HeadLength - bufferOffset;
        if ((offset + length) <= limit)
        {
            WriteBytes(bytes, offset, length, bufferOffset, buffer);
            offset += length;

            //Invoke the protocol api to handle the message
            this.messageProcesser.Invoke(buffer);
            this.bufferOffset = 0;
            this.pkgLength = 0;

            if (this.enTransportState != enTransportState.closed)
                this.enTransportState = enTransportState.readHead;
            if (offset < limit)
                ProcessBytes(bytes, offset, limit);
        }
        else
        {
            WriteBytes(bytes, offset, limit - offset, bufferOffset, buffer);
            bufferOffset += limit - offset;
            this.enTransportState = enTransportState.readBody;
        }
    }

    private void WriteBytes(byte[] source, int start, int length, byte[] target)
    {
        WriteBytes(source, start, length, 0, target);
    }

    private void WriteBytes(byte[] source, int start, int length, int offset, byte[] target)
    {
        for (int i = 0; i < length; i++)
        {
            target[offset + i] = source[start + i];
        }
    }

    private void Print(byte[] bytes, int offset, int length)
    {
        for (int i = offset; i < length; i++)
            Console.Write(Convert.ToString(bytes[i], 16) + " ");
        Console.WriteLine();
    }
}