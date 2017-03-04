public class Protobuf
{
    private MsgDecoder decoder;
    private MsgEncoder encoder;

    public Protobuf(MessageObject encodeProtos, MessageObject decodeProtos)
    {
        this.encoder = new MsgEncoder(encodeProtos);
        this.decoder = new MsgDecoder(decodeProtos);
    }

    public byte[] encode(string route, MessageObject msg)
    {
        return encoder.Encode(route, msg);
    }

    public MessageObject Decode(string route, byte[] buffer)
    {
        return decoder.Decode(route, buffer);
    }
}