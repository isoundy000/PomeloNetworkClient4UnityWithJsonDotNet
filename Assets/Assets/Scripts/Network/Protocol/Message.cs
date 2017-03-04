public class Message
{
    public enMessageType type;
    public string route;
    public uint id;
    public MessageObject jsonObj;
    public string rawString;

    public Message(enMessageType type, string route, MessageObject data)
    {
        this.type = type;
        this.route = route;
        this.jsonObj = data;
    }

    public Message(enMessageType type, uint id)
    {
        this.type = type;
        this.id = id;
        this.route = "";
        this.jsonObj = null;
    }

    public Message(enMessageType type, uint id, string route, MessageObject data, string rawString)
    {
        this.type = type;
        this.id = id;
        this.route = route;
        this.jsonObj = data;
        this.rawString = rawString;
    }
}