using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{
    public interface FieldEncoder
    {
        void EmitEncode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field);
        void EmitDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field);
    }
}