namespace AlkalineThunderMud.Common;

public enum Revision : byte
{
    Begin=0,
    
    Latest
}

public class NetworkMessageData
{
    private Revision revision;
    private readonly byte[] data;

    public Revision Revision => revision;
    
    public NetworkMessageData(Revision revision, byte[] data)
    {
        this.revision = revision;
        this.data = data;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write((byte) revision);
        writer.Write(data.Length);
        writer.Write(data);
    }
}