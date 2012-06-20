using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{
    public class SymbolFieldEncoder : FieldEncoder
    {
        public void EmitEncode(ILGenerator generator, FieldInfo field)
        {
            // ptr += SerializeString( ptr, field.ToString)
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldfld, field);
            var toStringMethod = typeof(object).GetMethod("ToString");
            generator.Emit(OpCodes.Callvirt, toStringMethod);
            var serializerMethod = typeof(StringFieldEncoder).GetMethod("SerializeString");
            generator.Emit(OpCodes.Call, serializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitDecode(ILGenerator generator, FieldInfo field)
        {
            // string str;
            // ptr += DeserializeString( ptr, &str)
            var stringLocal = generator.DeclareLocal(typeof(string));
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloca_S, stringLocal);
            var deserializerMethod = typeof(StringFieldEncoder).GetMethod("DeserializeString");
            generator.Emit(OpCodes.Call, deserializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);

            // field = Factory.Symbol.LookupSymbol( str);
            generator.Emit(OpCodes.Ldarg_2);
            var symbolFactoryMethod = typeof(Factory).GetMethod("get_Symbol");
            generator.Emit(OpCodes.Call, symbolFactoryMethod);
            generator.Emit(OpCodes.Ldloc_S, stringLocal);
            var lookupSymbolMethod = typeof(SymbolFactory).GetMethod("LookupSymbol",new Type[] { typeof(string)});
            generator.Emit(OpCodes.Callvirt, lookupSymbolMethod);
            generator.Emit(OpCodes.Stfld, field);
        }
    }
}