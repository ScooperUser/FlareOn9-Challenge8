using System.Collections.ObjectModel;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using de4dot.blocks.cflow;

namespace FlareOn8;

public class FlareOnEmulator {
    private readonly MethodDef _method;
    private readonly InstructionEmulator _emulator;

    public Dictionary<FieldDef, object> Fields { get; }

    private FlareOnEmulator(MethodDef method) {
        _method = method;
        
        _emulator = new InstructionEmulator();
        _emulator.Initialize(method, false);
        
        Fields = new Dictionary<FieldDef, object>();
    }

    public static FlareOnEmulator Initialize(MethodDef method) {
        var emu = new FlareOnEmulator(method);
        emu.Emulate();

        return emu;
    }

    private void Emulate() {
        foreach (var instruction in _method.Body.Instructions) {
            switch (instruction.OpCode.Code) {
                case Code.Newobj:
                    if (HandleNewobj(instruction))
                        continue;
                    break;

                case Code.Callvirt:
                    if (HandleCallvirt(instruction))
                        continue;
                    break;

                case Code.Stsfld:
                    if (HandleStsfld(instruction))
                        continue;
                    break;

            }

            _emulator.Emulate(instruction);
        }
    }

    private bool HandleNewobj(Instruction instruction) {
        if (instruction.Operand is not MemberRef mRef)
            return false;

        if (mRef.FullName == "System.Void System.Collections.Generic.List`1<System.Byte>::.ctor()") {
            _emulator.Push(new ObjectValue(new List<byte>()));
            return true;
        }
        else if (mRef.FullName ==
                 "System.Void System.Collections.Generic.Dictionary`2<System.UInt32,System.Int32>::.ctor()") {
            _emulator.Push(new ObjectValue(new Dictionary<uint, int>()));
            return true;
        }
        else if (mRef.FullName ==
                 "System.Void System.Collections.ObjectModel.ObservableCollection`1<System.Int32>::.ctor()") {
            _emulator.Push(new ObjectValue(new ObservableCollection<int>()));
            return true;
        }
        
        return false;
    }

    private bool HandleCallvirt(Instruction instruction) {
        if (instruction.Operand is not MemberRef mRef)
            return false;
        
        if (mRef.FullName == "System.Void System.Collections.Generic.List`1<System.Byte>::Add(System.Byte)") {
            var value = (Int32Value)_emulator.Pop();
            var objectValue = (ObjectValue)_emulator.Pop();

            var list = (List<byte>)objectValue.obj;
            list.Add((byte)value.Value);
            return true;
        }
        else if (mRef.FullName == "System.Byte[] System.Collections.Generic.List`1<System.Byte>::ToArray()") {
            var objectValue = (ObjectValue)_emulator.Pop();
            var list = (List<byte>)objectValue.obj;

            _emulator.Push(new ObjectValue(list.ToArray()));
            return true;
        }
        else if (mRef.FullName ==
                 "System.Void System.Collections.Generic.Dictionary`2<System.UInt32,System.Int32>::Add(System.UInt32,System.Int32)") {
            var right = (Int32Value)_emulator.Pop();
            var left = (Int32Value)_emulator.Pop();
            var objectValue = (ObjectValue)_emulator.Pop();

            var dictionary = (Dictionary<uint, int>)objectValue.obj;
            dictionary.Add((uint)left.Value, right.Value);
            return true;
        }
        else if (mRef.FullName ==
                 "System.Void System.Collections.ObjectModel.Collection`1<System.Int32>::Add(System.Int32)") {
            var value = (Int32Value)_emulator.Pop();
            var objectValue = (ObjectValue)_emulator.Pop();

            var list = (ObservableCollection<int>)objectValue.obj;
            list.Add(value.Value);
            return true;
        }
        
        return false;
    }

    private bool HandleStsfld(Instruction instruction) {
        if (instruction.Operand is FieldDef field) {
            Fields[field] = ((ObjectValue)_emulator.Pop()).obj;
            return true;
        }

        return false;
    }
}