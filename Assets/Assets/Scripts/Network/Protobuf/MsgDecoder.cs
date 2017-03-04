using System;
using System.Text;
using System.Collections.Generic;

public class MsgDecoder
{
    private MessageObject protos { set; get; }//The message format(like .proto file)
    private int offset { set; get; }
    private byte[] buffer { set; get; }//The binary message from server.
    private Util util { set; get; }

    public MsgDecoder(MessageObject protos)
    {
        if (protos == null) protos = new MessageObject();

        this.protos = protos;
        this.util = new Util();
    }

    /// <summary>
    /// Decode message from server.
    /// </summary>
    /// <param name='route'>
    /// Route.
    /// </param>
    /// <param name='buf'>
    /// MessageObject.
    /// </param>
    public MessageObject Decode(string route, byte[] buf)
    {
        this.buffer = buf;
        this.offset = 0;
        object proto = null;
        if (this.protos.TryGetValue(route, out proto))
        {
            MessageObject msg = new MessageObject();
            return this.DecodeMsg(msg, (MessageObject)proto, this.buffer.Length);
        }
        return null;
    }


    /// <summary>
    /// Decode the message.
    /// </summary>
    /// <returns>
    /// The message.
    /// </returns>
    /// <param name='msg'>
    /// MessageObject.
    /// </param>
    /// <param name='proto'>
    /// MessageObject.
    /// </param>
    /// <param name='length'>
    /// int.
    /// </param>
    private MessageObject DecodeMsg(MessageObject msg, MessageObject proto, int length)
    {
        while (this.offset < length)
        {
            Dictionary<string, int> head = this.GetHead();
            int tag;
            if (head.TryGetValue("tag", out tag))
            {
                object _tags = null;
                if (proto.TryGetValue("__tags", out _tags))
                {
                    object name;
                    if (((MessageObject)_tags).TryGetValue(tag.ToString(), out name))
                    {
                        object value;
                        if (proto.TryGetValue(name.ToString(), out value))
                        {
                            object option;
                            if (((MessageObject)(value)).TryGetValue("option", out option))
                            {
                                switch (option.ToString())
                                {
                                    case "optional":
                                    case "required":
                                        object type;
                                        if (((MessageObject)(value)).TryGetValue("type", out type))
                                        {
                                            msg.Add(name.ToString(), this.DecodeProp(type.ToString(), proto));
                                        }
                                        break;
                                    case "repeated":
                                        object _name;
                                        if (!msg.TryGetValue(name.ToString(), out _name))
                                        {
                                            msg.Add(name.ToString(), new List<object>());
                                        }
                                        object value_type;
                                        if (msg.TryGetValue(name.ToString(), out _name) && ((MessageObject)(value)).TryGetValue("type", out value_type))
                                        {
                                            DecodeArray((List<object>)_name, value_type.ToString(), proto);
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }
        return msg;
    }

    /// <summary>
    /// Decode array in message.
    /// </summary>
    private void DecodeArray(List<object> list, string type, MessageObject proto)
    {
        if (this.util.IsSimpleType(type))
        {
            int length = (int)Decoder.DecodeUInt32(this.GetBytes());
            for (int i = 0; i < length; i++)
            {
                list.Add(this.DecodeProp(type, null));
            }
        }
        else
        {
            list.Add(this.DecodeProp(type, proto));
        }
    }

    /// <summary>
    /// Decode each simple type in message.
    /// </summary>
    private object DecodeProp(string type, MessageObject proto)
    {
        switch (type)
        {
            case "uInt32":
                return Decoder.DecodeUInt32(this.GetBytes());
            case "int32":
            case "sInt32":
                return Decoder.DecodeSInt32(this.GetBytes());
            case "float":
                return this.DecodeFloat();
            case "double":
                return this.DecodeDouble();
            case "string":
                return this.DecodeString();
            default:
                return this.DecodeObject(type, proto);
        }
    }

    //Decode the user-defined object type in message.
    private MessageObject DecodeObject(string type, MessageObject proto)
    {
        if (proto != null)
        {
            object __messages;
            if (proto.TryGetValue("__messages", out __messages))
            {
                object _type;
                if (((MessageObject)__messages).TryGetValue(type, out _type) || protos.TryGetValue("message " + type, out _type))
                {
                    int l = (int)Decoder.DecodeUInt32(this.GetBytes());
                    MessageObject msg = new MessageObject();
                    return this.DecodeMsg(msg, (MessageObject)_type, this.offset + l);
                }
            }
        }
        return new MessageObject();
    }

    //Decode string type.
    private string DecodeString()
    {
        int length = (int)Decoder.DecodeUInt32(this.GetBytes());
        string msg_string = Encoding.UTF8.GetString(this.buffer, this.offset, length);
        this.offset += length;
        return msg_string;
    }

    //Decode double type.
    private double DecodeDouble()
    {
        double msg_double = BitConverter.Int64BitsToDouble((long)this.ReadRawLittleEndian64());
        this.offset += 8;
        return msg_double;
    }

    //Decode float type
    private float DecodeFloat()
    {
        float msg_float = BitConverter.ToSingle(this.buffer, this.offset);
        this.offset += 4;
        return msg_float;
    }

    //Read long in littleEndian
    private ulong ReadRawLittleEndian64()
    {
        ulong b1 = buffer[this.offset];
        ulong b2 = buffer[this.offset + 1];
        ulong b3 = buffer[this.offset + 2];
        ulong b4 = buffer[this.offset + 3];
        ulong b5 = buffer[this.offset + 4];
        ulong b6 = buffer[this.offset + 5];
        ulong b7 = buffer[this.offset + 6];
        ulong b8 = buffer[this.offset + 7];
        return b1 | (b2 << 8) | (b3 << 16) | (b4 << 24)
                | (b5 << 32) | (b6 << 40) | (b7 << 48) | (b8 << 56);
    }

    //Get the type and tag.
    private Dictionary<string, int> GetHead()
    {
        int tag = (int)Decoder.DecodeUInt32(this.GetBytes());
        Dictionary<string, int> head = new Dictionary<string, int>();
        head.Add("type", tag & 0x7);
        head.Add("tag", tag >> 3);
        return head;
    }

    //Get bytes.
    private byte[] GetBytes()
    {
        List<byte> arrayList = new List<byte>();
        int pos = this.offset;
        byte b;
        do
        {
            b = this.buffer[pos];
            arrayList.Add(b);
            pos++;
        } while (b >= 128);
        this.offset = pos;
        int length = arrayList.Count;
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = arrayList[i];
        }
        return bytes;
    }
}