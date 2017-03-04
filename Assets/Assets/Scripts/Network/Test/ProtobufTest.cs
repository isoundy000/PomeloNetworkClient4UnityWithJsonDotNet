using System;
using System.Collections.Generic;
using System.IO;

#pragma warning disable 0219

public class ProtobufTest
{
    public static MessageObject read(string name)
    {
        StreamReader file = new StreamReader(name);

        String str = file.ReadToEnd();

        return (MessageObject)SimpleJson.SimpleJson.DeserializeObject(str);
    }

    public static bool equal(MessageObject a, MessageObject b)
    {
        ICollection<string> keys0 = a.Keys;
        ICollection<string> keys1 = b.Keys;

        foreach (string key in keys0)
        {
            Console.WriteLine(a[key].GetType());
            if (a[key].GetType().ToString() == "SimpleJson.MessageObject")
            {
                if (!equal((MessageObject)a[key], (MessageObject)b[key])) return false;
            }
            else if (a[key].GetType().ToString() == "SimpleJson.JsonArray")
            {
                continue;
            }
            else
            {
                if (!a[key].ToString().Equals(b[key].ToString())) return false;
            }
        }

        return true;
    }

    public static void Run()
    {
        MessageObject protos = read("../../json/rootProtos.json");
        MessageObject msgs = read("../../json/rootMsg.json");

        Protobuf protobuf = new Protobuf(protos, protos);

        ICollection<string> keys = msgs.Keys;

        foreach (string key in keys)
        {
            MessageObject msg = (MessageObject)msgs[key];
            byte[] bytes = protobuf.encode(key, msg);
            MessageObject result = protobuf.Decode(key, bytes);
            if (!equal(msg, result))
            {
                Console.WriteLine("protobuf test failed!");
                return;
            }
        }

        Console.WriteLine("Protobuf test success!");
    }

    private static void print(byte[] bytes, int offset, int length)
    {
        for (int i = offset; i < length; i++)
            Console.Write(Convert.ToString(bytes[i], 16) + " ");
        Console.WriteLine();
    }
}