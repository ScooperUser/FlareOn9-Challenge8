using dnlib.DotNet;

namespace FlareOn8;

public record Stub(MethodDef ProxyMethod, MethodDef Method, FieldDef? InstructionInfo = null, FieldDef? Buffer = null);
public record RawMethodBody(byte[] ILBytes, byte[]? EHBytes, ushort Flags, uint CodeSize);