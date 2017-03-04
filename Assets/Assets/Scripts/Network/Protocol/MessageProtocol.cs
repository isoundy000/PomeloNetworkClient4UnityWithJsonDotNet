using System;
using System.Text;
using System.Collections.Generic;

public class MessageProtocol
{
    private Dictionary<string, ushort> dict = new Dictionary<string, ushort>();
    private Dictionary<ushort, string> abbrs = new Dictionary<ushort, string>();
    private MessageObject encodeProtos = new MessageObject();
    private MessageObject decodeProtos = new MessageObject();
    private Dictionary<uint, string> reqMap;
    private Protobuf protobuf;

    public const int MSG_Route_Limit = 255;
    public const int MSG_Route_Mask = 0x01;
    public const int MSG_Type_Mask = 0x07;

    public MessageProtocol(MessageObject dict, MessageObject serverProtos, MessageObject clientProtos)
    {
        ICollection<string> keys = dict.Keys;

        foreach (string key in keys)
        {
            ushort value = Convert.ToUInt16(dict[key]);
            this.dict[key] = value;
            this.abbrs[value] = key;
        }

        protobuf = new Protobuf(clientProtos, serverProtos);
        this.encodeProtos = clientProtos;
        this.decodeProtos = serverProtos;

        this.reqMap = new Dictionary<uint, string>();
    }

    public byte[] Encode(string route, MessageObject msg)
    {
        return Encode(route, 0, msg);
    }

    public byte[] Encode(string route, uint id, MessageObject msg)
    {
        int routeLength = ByteLength(route);
        if (routeLength > MSG_Route_Limit)
        {
            throw new Exception("Route is too long!");
        }

        //Encode head
        //The maximus length of head is 1 byte flag + 4 bytes message id + route string length + 1byte
        byte[] head = new byte[routeLength + 6];
        int offset = 1;
        byte flag = 0;

        if (id > 0)
        {
            byte[] bytes = Encoder.EncodeUInt32(id);

            WriteBytes(bytes, offset, head);
            flag |= ((byte)enMessageType.Request) << 1;
            offset += bytes.Length;
        }
        else
        {
            flag |= ((byte)enMessageType.Notify) << 1;
        }

        //Compress head
        if (dict.ContainsKey(route))
        {
            ushort cmpRoute = dict[route];
            WriteShort(offset, cmpRoute, head);
            flag |= MSG_Route_Mask;
            offset += 2;
        }
        else
        {
            //Write route length
            head[offset++] = (byte)routeLength;

            //Write route
            WriteBytes(Encoding.UTF8.GetBytes(route), offset, head);
            offset += routeLength;
        }

        head[0] = flag;

        //Encode body
        byte[] body;
        if (encodeProtos.ContainsKey(route))
        {
            body = protobuf.encode(route, msg);
        }
        else
        {
            body = Encoding.UTF8.GetBytes(msg.ToString());
        }

        //Construct the result
        byte[] result = new byte[offset + body.Length];
        for (int i = 0; i < offset; i++)
        {
            result[i] = head[i];
        }

        for (int i = 0; i < body.Length; i++)
        {
            result[offset + i] = body[i];
        }

        //Add id to route map
        if (id > 0) reqMap.Add(id, route);

        return result;
    }

    public Message Decode(byte[] buffer)
    {
        //Decode head
        //Get flag
        byte flag = buffer[0];
        //Set offset to 1, for the 1st byte will always be the flag
        int offset = 1;

        //Get type from flag;
        enMessageType type = (enMessageType)((flag >> 1) & MSG_Type_Mask);
        uint id = 0;
        string route;

        if (type == enMessageType.Response)
        {
            int length;
            id = (uint)Decoder.DecodeUInt32(offset, buffer, out length);
            if (id <= 0 || !reqMap.ContainsKey(id))
            {
                return null;
            }
            else
            {
                route = reqMap[id];
                reqMap.Remove(id);
            }

            offset += length;
        }
        else if (type == enMessageType.Push)
        {
            //Get route
            if ((flag & 0x01) == 1)
            {
                ushort routeId = ReadShort(offset, buffer);
                route = abbrs[routeId];

                offset += 2;
            }
            else
            {
                byte length = buffer[offset];
                offset += 1;

                route = Encoding.UTF8.GetString(buffer, offset, length);
                offset += length;
            }
        }
        else
        {
            return null;
        }

        //Decode body
        byte[] body = new byte[buffer.Length - offset];
        for (int i = 0; i < body.Length; i++)
        {
            body[i] = buffer[i + offset];
        }

        MessageObject msg;
        string rawString = null;
        if (decodeProtos.ContainsKey(route))
        {
            msg = protobuf.Decode(route, body);
        }
        else
        {
            rawString = Encoding.UTF8.GetString(body);
            msg = (MessageObject)SimpleJson.SimpleJson.DeserializeObject(rawString);
        }

        //Construct the message
        return new Message(type, id, route, msg, rawString);
    }

    private void WriteInt(int offset, uint value, byte[] bytes)
    {
        bytes[offset] = (byte)(value >> 24 & 0xff);
        bytes[offset + 1] = (byte)(value >> 16 & 0xff);
        bytes[offset + 2] = (byte)(value >> 8 & 0xff);
        bytes[offset + 3] = (byte)(value & 0xff);
    }

    private void WriteShort(int offset, ushort value, byte[] bytes)
    {
        bytes[offset] = (byte)(value >> 8 & 0xff);
        bytes[offset + 1] = (byte)(value & 0xff);
    }

    private ushort ReadShort(int offset, byte[] bytes)
    {
        ushort result = 0;

        result += (ushort)(bytes[offset] << 8);
        result += (ushort)(bytes[offset + 1]);

        return result;
    }

    private int ByteLength(string msg)
    {
        return Encoding.UTF8.GetBytes(msg).Length;
    }

    private void WriteBytes(byte[] source, int offset, byte[] target)
    {
        for (int i = 0; i < source.Length; i++)
        {
            target[offset + i] = source[i];
        }
    }
}