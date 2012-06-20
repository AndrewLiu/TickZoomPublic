using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{
    public interface FieldEncoder
    {
        void EmitEncode(ILGenerator generator, FieldInfo field);
        void EmitDecode(ILGenerator generator, FieldInfo field);
    }
}