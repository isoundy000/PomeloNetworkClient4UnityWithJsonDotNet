public class Package
{
    public enPackageType type;
    public int length;
    public byte[] body;

    public Package(enPackageType type, byte[] body)
    {
        this.type = type;
        this.length = body.Length;
        this.body = body;
    }
}