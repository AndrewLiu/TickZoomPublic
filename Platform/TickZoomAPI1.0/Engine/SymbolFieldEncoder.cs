﻿using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{
    public class SymbolFieldEncoder : FieldEncoder
    {
        public void EmitEncode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field, int id)
        {
            EncodeHelper.LogMessage(generator, "*ptr = memberId;");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, id);
            generator.Emit(OpCodes.Stind_I1);
            EncodeHelper.IncrementPtr(generator);

            EncodeHelper.LogMessage(generator, "// starting encode of Symbol");
            // ptr += SerializeString( ptr, field.ToString)
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc,resultLocal);
            generator.Emit(OpCodes.Ldfld, field);
            var toStringMethod = typeof(object).GetMethod("ToString");
            generator.Emit(OpCodes.Callvirt, toStringMethod);
            var serializerMethod = typeof(StringFieldEncoder).GetMethod("SerializeString");
            generator.Emit(OpCodes.Call, serializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            EncodeHelper.LogStack(generator, "// ptr address after symbol");
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field)
        {
            EncodeHelper.LogMessage(generator, "// starting decode Symbol");
            // ptr += DeserializeString( ptr, &str)
            var stringLocal = generator.DeclareLocal(typeof(string));
            generator.Emit(OpCodes.Ldloc_0);
            EncodeHelper.LogStack(generator, "// ptr address");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloca_S, stringLocal);
            var deserializerMethod = typeof(StringFieldEncoder).GetMethod("DeserializeString");
            generator.Emit(OpCodes.Call, deserializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            EncodeHelper.LogStack(generator, "// ptr address after symbol");
            generator.Emit(OpCodes.Stloc_0);

            // field = Factory.Symbol.LookupSymbol( str);
            generator.Emit(OpCodes.Ldloc,resultLocal);
            var symbolFactoryMethod = typeof(Factory).GetMethod("get_Symbol");
            generator.Emit(OpCodes.Call, symbolFactoryMethod);
            generator.Emit(OpCodes.Ldloc_S, stringLocal);
            var lookupSymbolMethod = typeof(SymbolFactory).GetMethod("LookupSymbol",new Type[] { typeof(string)});
            generator.Emit(OpCodes.Callvirt, lookupSymbolMethod);
            generator.Emit(OpCodes.Stfld, field);
        }
    }
}