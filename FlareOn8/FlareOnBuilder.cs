using System.Text;
using System.Security.Cryptography;
using dnlib.PE;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace FlareOn8;

public class FlareOnBuilder : IFullNameFactoryHelper {
    private readonly ModuleDefMD _module;
    private readonly RawMethodBodyReader _rawMethodBodyReader;
    private readonly FlareOnOperandResolver _resolver;

    public FlareOnBuilder(ModuleDefMD module) {
        _module = module;
        _rawMethodBodyReader = new RawMethodBodyReader(module);
        _resolver = new FlareOnOperandResolver(module, 2727913149U);
    }

    public List<ImageSectionHeader> RestoreStubs(List<Stub> stubs) {
        var markedForRemoval = new List<ImageSectionHeader>();

        foreach (var stub in stubs) {
            var method = stub.Method;
            var methodData = _rawMethodBodyReader.ReadMethod(method);
            
            string name = HashMethod(method, methodData);
            var section = SectionHelper.GetSection(_module, name);
            if (section is null) {
                Console.WriteLine($"Section {name} could not be found.");
                continue;
            }
            
            var data = SectionHelper.ReadSection(_module, section);
            data = RC4.Apply(data, new byte[] { 18, 120, 171, 223 });

            var gpContext = new GenericParamContext(method);
            var body = MethodBodyReader.CreateCilBody(_resolver, data, methodData.EHBytes, method.Parameters,
                methodData.Flags, method.Body.MaxStack, (uint)data.Length, method.Body.LocalVarSigTok, gpContext);

            // Set the body in the proxy
            stub.ProxyMethod.Body = body;
            
            // Remove the original
            method.DeclaringType.Remove(method);
            
            // Mark the section for removal
            markedForRemoval.Add(section);
        }
        
        return markedForRemoval;
    }

    private string HashMethod(MethodDef method, RawMethodBody rawMethodBody) {
        string text = "";
        string text2 = "";
        byte[] bytes = Encoding.ASCII.GetBytes(((System.Reflection.MethodAttributes)method.Attributes).ToString());
        byte[] bytes2 = Encoding.ASCII.GetBytes(FullNameFactory.FullName(method.ReturnType, true, this));

        // byte[] bytes3 = Encoding.ASCII.GetBytes(((System.Reflection.CallingConventions)method.CallingConvention).ToString());
        if (method.CallingConvention != CallingConvention.Default)
            throw new NotSupportedException("Calling convention is not supported.");

        byte[] bytes3 = Encoding.ASCII.GetBytes("Standard");
        foreach (var parameterInfo in method.Parameters) {
            string text3 = text2;
            var parameterType = FullNameFactory.FullName(parameterInfo.Type, true, this);
            text2 = text3 + parameterType;
        }

        var body = method.Body;

        byte[] bytes4 = Encoding.ASCII.GetBytes(((int)body.MaxStack).ToString());
        byte[] bytes5 = BitConverter.GetBytes(rawMethodBody.ILBytes.Length);
        foreach (var localVariableInfo in body.Variables) {
            string text4 = text;
            var localType = FullNameFactory.FullName(localVariableInfo.Type, true, this);
            text = text4 + localType;
        }

        byte[] bytes6 = Encoding.ASCII.GetBytes(text);
        byte[] bytes7 = Encoding.ASCII.GetBytes(text2);
        IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        incrementalHash.AppendData(bytes5);
        incrementalHash.AppendData(bytes);
        incrementalHash.AppendData(bytes2);
        incrementalHash.AppendData(bytes4);
        incrementalHash.AppendData(bytes6);
        incrementalHash.AppendData(bytes7);
        incrementalHash.AppendData(bytes3);
        byte[] hashAndReset = incrementalHash.GetHashAndReset();
        StringBuilder stringBuilder = new StringBuilder(hashAndReset.Length * 2);
        for (int j = 0; j < hashAndReset.Length; j++) {
            stringBuilder.Append(hashAndReset[j].ToString("x2"));
        }

        return stringBuilder.ToString();
    }

    public bool MustUseAssemblyName(IType type) {
        return false;
    }
}