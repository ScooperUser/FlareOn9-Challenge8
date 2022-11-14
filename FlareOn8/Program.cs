using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace FlareOn8;

public static class Program {
    public static void Main(string[] args) {
        if (args.Length != 1)
            return;

        var path = Path.GetFullPath(args[0]);

        var asmResolver = new AssemblyResolver();
        asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);

        var module = ModuleDefMD.Load(path, asmResolver.DefaultModuleContext);

        var setupMethod = ResolveFlareOnSetup(module);
        if (setupMethod is null) {
            throw new Exception("Error, unable to locate setup method");
        }

        var flareonRuntime = setupMethod.DeclaringType;

        Console.WriteLine($"Flare-on runtime class: 0x{flareonRuntime.MDToken}");
        Console.WriteLine($"Flare-on setup method: 0x{setupMethod.MDToken}");

        var emu = FlareOnEmulator.Initialize(setupMethod);

        var staticBuilder = ResolveStaticBuilder(flareonRuntime);
        if (staticBuilder is null) {
            throw new Exception("Error, unable to locate static builder method");
        }

        Console.WriteLine($"Flare-on static builder method: 0x{staticBuilder.MDToken}");

        var staticStubs = FindAllStubs(module, staticBuilder);

        var fstaticBuilder = new FlareOnStaticBuilder(module, emu.Fields);
        fstaticBuilder.RestoreStubs(staticStubs);

        var builder = ResolveBuilder(flareonRuntime);
        if (builder is null) {
            throw new Exception("Error, unable to locate builder method");
        }

        Console.WriteLine($"Flare-on builder method: 0x{builder.MDToken}");

        var stubs = FindAllStubs(module, builder);

        var fBuilder = new FlareOnBuilder(module);
        var markedForRemoval = fBuilder.RestoreStubs(stubs);

        Console.WriteLine("Writing file...");

        string dest = $"{Path.GetDirectoryName(path)}{Path.DirectorySeparatorChar}{Path.GetFileNameWithoutExtension(path)}-flared{Path.GetExtension(path)}";

        var options = new NativeModuleWriterOptions(module, false);
        options.MetadataOptions.Flags = MetadataFlags.PreserveAll;
        module.NativeWrite(dest, options);

        Console.WriteLine($"File written to {dest}");
        Console.ReadLine();
    }

    static List<Stub> FindAllStubs(ModuleDef module, MethodDef builder) {
        var stubs = new List<Stub>();

        foreach (var type in module.GetTypes()) {
            foreach (var method in type.Methods) {
                if (!method.HasBody)
                    continue;

                var exceptions = method.Body.ExceptionHandlers;
                if (!method.Body.HasExceptionHandlers)
                    continue;

                ExceptionHandler? exception = null;
                foreach (var x in exceptions) {
                    if (x.IsCatch && x.CatchType.FullName == "System.InvalidProgramException") {
                        exception = x;
                        break;
                    }
                }

                if (exception is null)
                    continue;

                var calls = new List<MethodDef>();
                var fields = new List<FieldDef>();
                
                bool found = false;
                foreach (var instr in method.Body.Instructions) {
                    if (instr.OpCode.Code == Code.Call && instr.Operand is MethodDef mDef) {
                        if (mDef == builder) {
                            found = true;
                            break;
                        }

                        calls.Add(mDef);
                    }
                    else if (instr.OpCode.Code == Code.Ldsfld && instr.Operand is FieldDef field) {
                        fields.Add(field);
                    }
                }

                if (!found)
                    continue;

                Stub stub = fields.Count switch {
                    1 => new Stub(method, calls[^1], null, fields[^1]),
                    2 => new Stub(method, calls[^1], fields[^2], fields[^1]),
                    _ => new Stub(method, calls[^1])
                };

                stubs.Add(stub);
            }
        }

        return stubs;
    }

    static MethodDef? ResolveBuilder(TypeDef type) {
        foreach (var method in type.Methods) {
            var parameters = method.Parameters;
            if (parameters.Count != 2)
                continue;

            // (InvalidProgramException e, object[] a)
            if (parameters[0].Type.ElementType != ElementType.Class)
                continue;
            if (parameters[1].Type.ElementType != ElementType.SZArray)
                continue;

            if (method.ReturnType.ElementType != ElementType.Object)
                continue;

            return method;
        }

        return null;
    }

    static MethodDef? ResolveStaticBuilder(TypeDef type) {
        foreach (var method in type.Methods) {
            var parameters = method.Parameters;
            if (parameters.Count != 4)
                continue;

            // (InvalidProgramException e, object[] args, Dictionary<uint, int> m, byte[] b)
            if (parameters[0].Type.ElementType != ElementType.Class)
                continue;
            if (parameters[1].Type.ElementType != ElementType.SZArray)
                continue;
            if (parameters[2].Type.ElementType != ElementType.GenericInst)
                continue;
            if (parameters[3].Type.ElementType != ElementType.SZArray)
                continue;

            if (method.ReturnType.ElementType != ElementType.Object)
                continue;

            return method;
        }

        return null;
    }

    static MethodDef? ResolveFlareOnSetup(ModuleDef module) {
        var entryPoint = module.EntryPoint;
        var body = entryPoint.Body;
        if (body.ExceptionHandlers.Count != 2)
            return null;

        var instrs = body.Instructions;
        foreach (var instr in instrs) {
            if (instr.OpCode.Code != Code.Call)
                continue;

            if (instr.Operand is MethodDef mDef && mDef.GetParamCount() == 0) {
                return mDef;
            }
        }

        return null;
    }
}