using System;
public class ClientTest
{
    public static PomeloClient pomeloClient = null;

    public static void loginTest(string host, int port)
    {
        pomeloClient = new PomeloClient();

        //listen on network state changed event
        pomeloClient.On(PomeloClient.DisconnectEvent, jsonObj =>
        {
            Console.WriteLine("CurrentState is:" + pomeloClient.netWorkState);
        });

        pomeloClient.InitClient(host, port, jsonObj =>
        {
            pomeloClient.Connect(null, data =>
            {

                Console.WriteLine("on data back" + data.ToString());
                MessageObject msg = new MessageObject();
                msg["uid"] = 111;
                pomeloClient.Request("gate.gateHandler.queryEntry", msg, OnQuery);
            });
        });
    }

    public static void OnQuery(Message msg)
    {
        var result = msg.jsonObj;

        if (Convert.ToInt32(result["code"]) == 200)
        {
            pomeloClient.Disconnect();

            string host = (string)result["host"];
            int port = Convert.ToInt32(result["port"]);
            pomeloClient = new PomeloClient();

            pomeloClient.On(PomeloClient.DisconnectEvent, jsonObj =>
            {
                Console.WriteLine(pomeloClient.netWorkState);
            });

            pomeloClient.InitClient(host, port, jsonObj =>
            {
                pomeloClient.Connect(null, (data) =>
                {
                    //MessageObject userMessage = new MessageObject();
                    Console.WriteLine("on connect to connector!");

                    //Login
                    MessageObject enterMsg = new MessageObject();
                    enterMsg["userName"] = "test";
                    enterMsg["rid"] = "pomelo";

                    pomeloClient.Request("connector.entryHandler.enter", enterMsg, OnEnter);
                });
            });
        }
    }

    public static void OnEnter(Message msg)
    {
        Console.WriteLine("on login " + msg.jsonObj.ToString());
    }

    public static void onDisconnect(MessageObject result)
    {
        Console.WriteLine("on sockect disconnected!");
    }

    public static void Run()
    {
        string host = "192.168.0.156";
        int port = 3014;

        loginTest(host, port);
    }
}