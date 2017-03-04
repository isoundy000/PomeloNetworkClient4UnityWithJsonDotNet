using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 
/// </summary>
public class TestPomeloClient : MonoBehaviour
{
    public Text LogText;

    public InputField IPInput;
    public InputField NameInput;
    public InputField PasswordInput;
    public InputField MessageInput;

    public static PomeloClient pomeloClient = null;

    void Start()
    {
        pomeloClient = new PomeloClient();

        IPInput.text = "localhost";
        NameInput.text = "spark";
        PasswordInput.text = "123456";

        pomeloClient.On("onTick", msg => 
        {
            _onResponseRet(msg);
        });

        //listen on network state changed event
        pomeloClient.On(PomeloClient.DisconnectEvent, msg =>
        {
            Debug.logger.Log("Network error, reason: " + msg.jsonObj["reason"]);
        });

        pomeloClient.On(PomeloClient.ErrorEvent, msg =>
        {
            Debug.logger.Log("Error, reason: " + msg.jsonObj["reason"]);
        });
    }

    //When quit, release resource
    void Update()
    {
        pomeloClient.Update();
    }

    public void Disconnect()
    {
        pomeloClient.Disconnect();
        LogText.text = "";
    }

    //Login the chat application and new PomeloClient.
    public void Connect()
    {
        if(pomeloClient.netWorkState != enNetWorkState.Disconnected) return;

        int port = 3014;

        pomeloClient.InitClient(IPInput.text, port, msgObj =>
        {
            //The user data is the handshake user params
            MessageObject user = new MessageObject();
            pomeloClient.Connect(user, data =>
            {
                //process handshake call back data
                MessageObject msg = new MessageObject();
                msg["uid"] = NameInput.text;
                msg["pwd"] = PasswordInput.text;
                pomeloClient.Request("gate.gateHandler.login", msg, _onResponseRet);
            });
        });
    }

    void _onResponseRet(Message result)
    {
        LogText.text = LogText.text + result.rawString + "\n";
    }

    public void Send()
    {
        MessageObject msg = new MessageObject();
        msg["message"] = MessageInput.text;
        pomeloClient.Request("gate.gateHandler.sendMessage", msg, _onResponseRet);

        MessageInput.ActivateInputField();
        MessageInput.Select();
    }

    //When quit, release resource
    void OnApplicationQuit()
    {
        if (pomeloClient != null) pomeloClient.Disconnect();
    }
}