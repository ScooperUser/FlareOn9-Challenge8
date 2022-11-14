using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace FlareOn8;

public class FlareOnOperandResolver : IInstructionOperandResolver {
	private readonly ModuleDefMD _module;
	private readonly uint _key;

	public FlareOnOperandResolver(ModuleDefMD module, uint key = 0) {
		_module = module;
		_key = key;
	}

	public IMDTokenProvider ResolveToken(uint token, GenericParamContext gpContext) {
		switch (token >> 24) {
			case 0x01:
			case 0x02:
			case 0x04:
			case 0x06:
			case 0x0A:
			case 0x11:
				return _module.ResolveToken(token, gpContext);

			default:
				return _module.ResolveToken(token ^ _key, gpContext);
		}
	}

	public string ReadUserString(uint token) {
		return _module.ReadUserString(token ^ _key);
	}
}