using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace FlareOn8;

public class FlareOnStaticBuilder {
    private readonly Dictionary<FieldDef, object> _fields;
    
    private readonly RawMethodBodyReader _rawMethodBodyReader;
    private readonly FlareOnOperandResolver _resolver;

    public FlareOnStaticBuilder(ModuleDefMD module, Dictionary<FieldDef, object> fields) {
        _fields = fields;
        
        _rawMethodBodyReader = new RawMethodBodyReader(module);
        _resolver = new FlareOnOperandResolver(module);
    }

    public void RestoreStubs(List<Stub> stubs) {
        foreach (var stub in stubs) {
            if (stub.Buffer is null) {
                Console.WriteLine(
                    $"Error (static builder): Method ({stub.ProxyMethod.MDToken}) is missing additional info!");
                continue;
            }

            var data = SetOperands(stub);

            var method = stub.Method;
            var methodData = _rawMethodBodyReader.ReadMethod(method);
            var gpContext = new GenericParamContext(method);

            var body = MethodBodyReader.CreateCilBody(_resolver, data, methodData.EHBytes, method.Parameters,
                methodData.Flags, method.Body.MaxStack, (uint)data.Length, method.Body.LocalVarSigTok, gpContext);
            
            // Set the body in the proxy method
            stub.ProxyMethod.Body = body;
            
            // Remove the original
            method.DeclaringType.Remove(method);
        }
    }

    private byte[] SetOperands(Stub stub) {
        var b = (byte[])_fields[stub.Buffer!];

        if (stub.InstructionInfo is null)
            return b;

        var m = (Dictionary<uint, int>)_fields[stub.InstructionInfo!];

        foreach (KeyValuePair<uint, int> keyValuePair in m) {
            int operand = keyValuePair.Value;
            uint offset = keyValuePair.Key;

            b[offset] = (byte)operand;
            b[offset + 1] = (byte)(operand >> 8);
            b[offset + 2] = (byte)(operand >> 16);
            b[offset + 3] = (byte)(operand >> 24);
        }

        return b;
    }
}