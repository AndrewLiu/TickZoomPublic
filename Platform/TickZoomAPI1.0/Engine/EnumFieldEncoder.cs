using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace TickZoom.Api
{
    public class EnumFieldEncoder : FieldEncoder
    {
        protected EncodeHelper helper;
        public EnumFieldEncoder(EncodeHelper helper)
        {
            this.helper = helper;
        }
        private void EmitDataLength(ILGenerator generator, FieldInfo field)
        {
            // ptr += sizeof()
            generator.Emit(OpCodes.Ldloc_0);
            var underlying = Enum.GetUnderlyingType(field.FieldType);
            var size = Marshal.SizeOf(underlying);
            switch (size)
            {
                case 1:
                    generator.Emit(OpCodes.Ldc_I4_1);
                    break;
                case 2:
                    generator.Emit(OpCodes.Ldc_I4_2);
                    break;
                case 4:
                    generator.Emit(OpCodes.Ldc_I4_4);
                    break;
                case 8:
                    generator.Emit(OpCodes.Ldc_I4_8);
                    break;
                default: 
                    throw new ArgumentOutOfRangeException("Unexpected enum size: " + size);
            }
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitEncode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field, int id)
        {
            helper.LogMessage(generator, "*ptr = " + id + "; // member id");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, id);
            generator.Emit(OpCodes.Stind_I1);
            helper.IncrementPtr(generator);

            helper.LogMessage(generator, "// starting encode of enum");
            // *ptr = obj.field
            generator.Emit(OpCodes.Ldloc_0);
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            generator.Emit(OpCodes.Ldfld, field);

            var underlying = Enum.GetUnderlyingType(field.FieldType);
            var size = Marshal.SizeOf(underlying);
            switch (size)
            {
                case 1:
                    generator.Emit(OpCodes.Stind_I1);
                    break;
                case 2:
                    generator.Emit(OpCodes.Stind_I2);
                    break;
                case 4:
                    generator.Emit(OpCodes.Stind_I4);
                    break;
                case 8:
                    generator.Emit(OpCodes.Stind_I8);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected enum size: " + size);
            }

            EmitDataLength(generator, field);
        }

        public void EmitDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field)
        {
            helper.LogMessage(generator, "// starting decode of enum");
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            generator.Emit(OpCodes.Ldloc_0);
            var underlying = Enum.GetUnderlyingType(field.FieldType);
            var size = Marshal.SizeOf(underlying);
            switch (size)
            {
                case 1:
                    generator.Emit(OpCodes.Ldind_U1);
                    break;
                case 2:
                    generator.Emit(OpCodes.Ldind_U2);
                    break;
                case 4:
                    generator.Emit(OpCodes.Ldind_U4);
                    break;
                case 8:
                    generator.Emit(OpCodes.Ldind_I8);
                    generator.Emit(OpCodes.Conv_U8);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected enum size: " + size);
            }
            generator.Emit(OpCodes.Stfld, field);

            EmitDataLength(generator, field);
        }
    }
}