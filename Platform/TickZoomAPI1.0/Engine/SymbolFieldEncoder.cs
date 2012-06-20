using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{
    public class SymbolFieldEncoder : FieldEncoder
    {
        public void EmitLength(ILGenerator generator, FieldInfo field)
        {
            // SerializeString( ptr, &field)
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);

            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldfld, field);
            var serializerMethod = typeof(StringFieldEncoder).GetMethod("LengthString");
            generator.Emit(OpCodes.Call, serializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitEncode(ILGenerator generator, FieldInfo field)
        {
            // SerializeString( ptr, &field)
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldfld, field);
            var serializerMethod = typeof(StringFieldEncoder).GetMethod("SerializeString");
            generator.Emit(OpCodes.Call, serializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitDecode(ILGenerator generator, FieldInfo field)
        {
            // DeserializeString( ptr, &field)
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldflda, field);
            var deserializerMethod = typeof(StringFieldEncoder).GetMethod("DeserializeString");
            generator.Emit(OpCodes.Call, deserializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }
    }
}