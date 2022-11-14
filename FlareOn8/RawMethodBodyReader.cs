using dnlib.IO;
using dnlib.DotNet;

namespace FlareOn8; 

public class RawMethodBodyReader {
    private readonly ModuleDefMD _module;
    
    public RawMethodBodyReader(ModuleDefMD module) {
        _module = module;
    }

    public RawMethodBody ReadMethod(MethodDef method) {
        var reader = _module.Metadata.PEImage.CreateReader(method.RVA);

        // read code size
        ushort flags = 0;
        uint codeSize = 0;
        byte b = reader.ReadByte();
        switch (b & 7) {
            case 2:
            case 6:
                codeSize = (uint)(b >> 2);
                flags = 2;
                break;
            case 3:
                flags = (ushort)(reader.ReadByte() << 8 | b);
                int headerSize = (flags >> 12) * sizeof(uint);
                reader.ReadUInt16();
                codeSize = reader.ReadUInt32();
                reader.Position = (uint)headerSize;
                
                if (headerSize < 3)
                    flags &= 0xFFF7;
                break;
        }

        // read actual body
        byte[] ilBytes = new byte[codeSize];
        if (codeSize > 0)
            reader.ReadBytes(ilBytes, 0, ilBytes.Length);
        
        if ((flags & 8) == 0) {
            // Method doesn't have exceptions
            return new RawMethodBody(ilBytes, null, flags, codeSize);
        }

        reader.Position = (reader.Position + 3) & ~3U;
        
        var ehSize = ReadExceptionHandlerSize(reader);
        byte[] ehBytes = new byte[ehSize];
        reader.ReadBytes(ehBytes, 0, ehBytes.Length);

        return new RawMethodBody(ilBytes, ehBytes, flags, codeSize);
    }
    
    uint ReadExceptionHandlerSize(DataReader reader) {
        byte b = reader.ReadByte();
        
        return (b & 0x40) != 0
            ? ReadFatExceptionHandlers(ref reader)
            : ReadSmallExceptionHandlers(ref reader);
    }

    uint ReadFatExceptionHandlers(ref DataReader ehReader) {
        ehReader.Position--;
        int num = (int)((ehReader.ReadUInt32() >> 8) / 24);
        return (uint)(sizeof(uint) + (num * (sizeof(uint) * 6)));
    }

    uint ReadSmallExceptionHandlers(ref DataReader ehReader) {
        int num = (int)((uint)ehReader.ReadByte() / 12);
        return (uint)(sizeof(int) + (num * 12));
    }
}