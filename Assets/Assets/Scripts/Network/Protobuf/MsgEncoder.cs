using System;
using System.Text;
using System.Collections.Generic;

public class MsgEncoder
{
    private MessageObject protos { set; get; }//The message format(like .proto file)
    private Encoder encoder { set; get; }
    private Util util { set; get; }

    public MsgEncoder(MessageObject protos)
    {
        if (protos == null) protos = new MessageObject();

        this.protos = protos;
        this.util = new Util();
    }

    /// <summary>
    /// Encode the message from server.
    /// </summary>
    /// <param name='route'>
    /// Route.
    /// </param>
    /// <param name='msg'>
    /// Message.
    /// </param>
    public byte[] Encode(string route, MessageObject msg)
    {
        byte[] returnByte = null;
        object proto;
        if (this.protos.TryGetValue(route, out proto))
        {
            if (!CheckMsg(msg, (MessageObject)proto))
            {
                return null;
            }
            int length = Encoder.ByteLength(msg.ToString()) * 2;
            int offset = 0;
            byte[] buff = new byte[length];
            offset = EncodeMsg(buff, offset, (MessageObject)proto, msg);
            returnByte = new byte[offset];
            for (int i = 0; i < offset; i++)
            {
                returnByte[i] = buff[i];
            }
        }
        return returnByte;
    }

    /// <summary>
    /// Check the message.
    /// </summary>
    private bool CheckMsg(MessageObject msg, MessageObject proto)
    {
        ICollection<string> protoKeys = proto.Keys;
        foreach (string key in protoKeys)
        {
            MessageObject value = (MessageObject)proto[key];
            object proto_option;
            if (value.TryGetValue("option", out proto_option))
            {
                switch (proto_option.ToString())
                {
                    case "required":
                        if (!msg.ContainsKey(key))
                        {
                            return false;
                        }
                        else
                        {

                        }
                        break;
                    case "optional":
                        object value_type;

                        MessageObject messages = (MessageObject)proto["__messages"];

                        value_type = value["type"];

                        if (msg.ContainsKey(key))
                        {
                            Object value_proto;

                            if (messages.TryGetValue(value_type.ToString(), out value_proto) || protos.TryGetValue("message " + value_type.ToString(), out value_proto))
                            {
                                CheckMsg((MessageObject)msg[key], (MessageObject)value_proto);
                            }
                        }
                        break;
                    case "repeated":
                        object msg_name;
                        object msg_type;
                        if (value.TryGetValue("type", out value_type) && msg.TryGetValue(key, out msg_name))
                        {
                            if (((MessageObject)proto["__messages"]).TryGetValue(value_type.ToString(), out msg_type) || protos.TryGetValue("message " + value_type.ToString(), out msg_type))
                            {
                                List<object> o = (List<object>)msg_name;
                                foreach (object item in o)
                                {
                                    if (!CheckMsg((MessageObject)item, (MessageObject)msg_type))
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Encode the message.
    /// </summary>
    private int EncodeMsg(byte[] buffer, int offset, MessageObject proto, MessageObject msg)
    {
        ICollection<string> msgKeys = msg.Keys;
        foreach (string key in msgKeys)
        {
            object value;
            if (proto.TryGetValue(key, out value))
            {
                object value_option;
                if (((MessageObject)value).TryGetValue("option", out value_option))
                {
                    switch (value_option.ToString())
                    {
                        case "required":
                        case "optional":
                            object value_type, value_tag;
                            if (((MessageObject)value).TryGetValue("type", out value_type) && ((MessageObject)value).TryGetValue("tag", out value_tag))
                            {
                                offset = this.WriteBytes(buffer, offset, this.EncodeTag(value_type.ToString(), Convert.ToInt32(value_tag)));
                                offset = this.EncodeProp(msg[key], value_type.ToString(), offset, buffer, proto);
                            }
                            break;
                        case "repeated":
                            object msg_key;
                            if (msg.TryGetValue(key, out msg_key))
                            {
                                if (((List<object>)msg_key).Count > 0)
                                {
                                    offset = EncodeArray((List<object>)msg_key, (MessageObject)value, offset, buffer, proto);
                                }
                            }
                            break;
                    }
                }

            }
        }
        return offset;
    }

    /// <summary>
    /// Encode the array type.
    /// </summary>
    private int EncodeArray(List<object> msg, MessageObject value, int offset, byte[] buffer, MessageObject proto)
    {
        object value_type, value_tag;
        if (value.TryGetValue("type", out value_type) && value.TryGetValue("tag", out value_tag))
        {
            if (this.util.IsSimpleType(value_type.ToString()))
            {
                offset = this.WriteBytes(buffer, offset, this.EncodeTag(value_type.ToString(), Convert.ToInt32(value_tag)));
                offset = this.WriteBytes(buffer, offset, Encoder.EncodeUInt32((uint)msg.Count));
                foreach (object item in msg)
                {
                    offset = this.EncodeProp(item, value_type.ToString(), offset, buffer, null);
                }
            }
            else
            {
                foreach (object item in msg)
                {
                    offset = this.WriteBytes(buffer, offset, this.EncodeTag(value_type.ToString(), Convert.ToInt32(value_tag)));
                    offset = this.EncodeProp(item, value_type.ToString(), offset, buffer, proto);
                }
            }
        }
        return offset;
    }

    /// <summary>
    /// Encode each item in message.
    /// </summary>
    private int EncodeProp(object value, string type, int offset, byte[] buffer, MessageObject proto)
    {
        switch (type)
        {
            case "uInt32":
                this.WriteUInt32(buffer, ref offset, value);
                break;
            case "int32":
            case "sInt32":
                this.WriteInt32(buffer, ref offset, value);
                break;
            case "float":
                this.WriteFloat(buffer, ref offset, value);
                break;
            case "double":
                this.WriteDouble(buffer, ref offset, value);
                break;
            case "string":
                this.WriteString(buffer, ref offset, value);
                break;
            default:
                object __messages;
                object __message_type;

                if (proto.TryGetValue("__messages", out __messages))
                {
                    if (((MessageObject)__messages).TryGetValue(type, out __message_type) || protos.TryGetValue("message " + type, out __message_type))
                    {
                        byte[] tembuff = new byte[Encoder.ByteLength(value.ToString()) * 3];
                        int length = 0;
                        length = this.EncodeMsg(tembuff, length, (MessageObject)__message_type, (MessageObject)value);
                        offset = WriteBytes(buffer, offset, Encoder.EncodeUInt32((uint)length));
                        for (int i = 0; i < length; i++)
                        {
                            buffer[offset] = tembuff[i];
                            offset++;
                        }
                    }
                }
                break;
        }
        return offset;
    }

    //Encode string.
    private void WriteString(byte[] buffer, ref int offset, object value)
    {
        int le = Encoding.UTF8.GetByteCount(value.ToString());
        offset = WriteBytes(buffer, offset, Encoder.EncodeUInt32((uint)le));
        byte[] bytes = Encoding.UTF8.GetBytes(value.ToString());
        this.WriteBytes(buffer, offset, bytes);
        offset += le;
    }

    //Encode double.
    private void WriteDouble(byte[] buffer, ref int offset, object value)
    {
        WriteRawLittleEndian64(buffer, offset, (ulong)BitConverter.DoubleToInt64Bits(double.Parse(value.ToString())));
        offset += 8;
    }

    //Encode float.
    private void WriteFloat(byte[] buffer, ref int offset, object value)
    {
        this.WriteBytes(buffer, offset, Encoder.EncodeFloat(float.Parse(value.ToString())));
        offset += 4;
    }

    ////Encode UInt32.
    private void WriteUInt32(byte[] buffer, ref int offset, object value)
    {
        offset = WriteBytes(buffer, offset, Encoder.EncodeUInt32(value.ToString()));
    }

    //Encode Int32
    private void WriteInt32(byte[] buffer, ref int offset, object value)
    {
        offset = WriteBytes(buffer, offset, Encoder.EncodeSInt32(value.ToString()));
    }

    //Write bytes to buffer.
    private int WriteBytes(byte[] buffer, int offset, byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            buffer[offset] = bytes[i];
            offset++;
        }
        return offset;
    }

    //Encode tag.
    private byte[] EncodeTag(string type, int tag)
    {
        int flag = this.util.ContainType(type);
        return Encoder.EncodeUInt32((uint)(tag << 3 | flag));
    }


    private void WriteRawLittleEndian64(byte[] buffer, int offset, ulong value)
    {
        buffer[offset++] = ((byte)value);
        buffer[offset++] = ((byte)(value >> 8));
        buffer[offset++] = ((byte)(value >> 16));
        buffer[offset++] = ((byte)(value >> 24));
        buffer[offset++] = ((byte)(value >> 32));
        buffer[offset++] = ((byte)(value >> 40));
        buffer[offset++] = ((byte)(value >> 48));
        buffer[offset++] = ((byte)(value >> 56));
    }
}