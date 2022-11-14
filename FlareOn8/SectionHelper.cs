using dnlib.PE;
using dnlib.DotNet;

namespace FlareOn8; 

public static class SectionHelper {
    public static byte[] ReadSection(ModuleDefMD module, ImageSectionHeader section) {
        var reader = module.Metadata.PEImage.CreateReader(section.VirtualAddress);
        return reader.ReadBytes((int)section.VirtualSize);
    }

    public static ImageSectionHeader? GetSection(ModuleDefMD module, string name) {
        foreach (var section in module.Metadata.PEImage.ImageSectionHeaders) {
            if (name.StartsWith(section.DisplayName)) {
                return section;
            }
        }

        return null;
    }
}