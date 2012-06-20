using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{
    public class CastingFieldEncoder : FieldEncoder
    {
        private Type wireType;

        public CastingFieldEncoder(Type wireType)
        {
            this.wireType = wireType;
        }

        private void EmitDataLength(ILGenerator generator, FieldInfo field)
        {
            // ptr += sizeof()
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_8);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitEncode(ILGenerator generator, FieldInfo field)
        {
            // *ptr = obj.field
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldfld, field);

            var cast = FindCast(field.FieldType, field.FieldType, wireType);
            generator.Emit(OpCodes.Call, cast);

            generator.Emit(OpCodes.Stind_I8);

            EmitDataLength(generator, field);
        }

        private MethodInfo FindCast( Type baseType, Type from, Type to)
        {
            var methods = baseType.GetMethods();
            MethodInfo cast = null;
            var type = from.BaseType;
            foreach (var method in methods)
            {
                if (method.Name == "op_Explicit" && method.ReturnType == to)
                {
                    var parms = method.GetParameters();
                    if (parms.Length == 1 && parms[0].ParameterType == from)
                    {
                        cast = method;
                        break;
                    }
                }
                if (method.Name == "op_Implicit" && method.ReturnType == to)
                {
                    var parms = method.GetParameters();
                    if (parms.Length == 1 && parms[0].ParameterType == from)
                    {
                        cast = method;
                        break;
                    }
                }
            }

            if( cast == null)
            {
                throw new InvalidCastException("No explicit or implicit cast was found from " + from + " to " + to);
            }
            return cast;
        }

        public void EmitDecode(ILGenerator generator, FieldInfo field)
        {
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldind_I8);
            var cast = FindCast(field.FieldType, wireType, field.FieldType);
            generator.Emit(OpCodes.Call, cast);
            generator.Emit(OpCodes.Stfld, field);

            EmitDataLength(generator, field);
        }
    }
}