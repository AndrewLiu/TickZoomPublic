﻿using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{
    public class NumericFieldEncoder : FieldEncoder
    {
        protected EncodeHelper helper;
        public NumericFieldEncoder(EncodeHelper helper)
        {
            this.helper = helper;
        }
        private void EmitDataLength(ILGenerator generator, FieldInfo field)
        {
            helper.EmitLogMessage(generator, "ptr += sizeof()");
            generator.Emit(OpCodes.Ldloc_0);
            if (field.FieldType == typeof(byte) || field.FieldType == typeof(sbyte))
            {
                generator.Emit(OpCodes.Ldc_I4_1);
            }
            else if (field.FieldType == typeof(Int16) || field.FieldType == typeof(UInt16))
            {
                generator.Emit(OpCodes.Ldc_I4_2);
            }
            else if (field.FieldType == typeof(Int32) || field.FieldType == typeof(UInt32) || field.FieldType == typeof(Single))
            {
                generator.Emit(OpCodes.Ldc_I4_4);
            }
            else if (field.FieldType == typeof(Int64) || field.FieldType == typeof(UInt64) || field.FieldType == typeof(Double))
            {
                generator.Emit(OpCodes.Ldc_I4_8);
            }
            else
            {
                throw new InvalidOperationException("Unexpected type: " + field.FieldType);
            }
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitEncode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field, int id)
        {
            helper.EmitLogMessage(generator, "*ptr = " + id + "; // member id");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, id);
            generator.Emit(OpCodes.Stind_I1);
            helper.IncrementPtr(generator);

            helper.EmitLogMessage(generator, "// starting encode of numeric");
            helper.EmitLogMessage(generator, "*ptr = obj.field");
            generator.Emit(OpCodes.Ldloc_0);
            helper.EmitLogMessage(generator, "// ptr value");
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            helper.EmitLogStack(generator, "// Value at arg 1");
            helper.EmitLogMessage(generator, "// load the field value");
            generator.Emit(OpCodes.Ldfld, field);
            helper.EmitLogStack(generator, "// " + field.Name);
            helper.EmitLogMessage(generator, "// Emit store of field value");
            if (field.FieldType == typeof(byte) || field.FieldType == typeof(sbyte))
            {
                generator.Emit(OpCodes.Stind_I1);
            }
            else if(field.FieldType == typeof(Int16) || field.FieldType == typeof(UInt16))
            {
                generator.Emit(OpCodes.Stind_I2);
            }
            else if (field.FieldType == typeof(Int32) || field.FieldType == typeof(UInt32))
            {
                generator.Emit(OpCodes.Stind_I4);
            }
            else if (field.FieldType == typeof(Int64) || field.FieldType == typeof(UInt64))
            {
                generator.Emit(OpCodes.Stind_I8);
            }
            else if( field.FieldType == typeof(Single))
            {
                generator.Emit(OpCodes.Stind_R4);
            }
            else if( field.FieldType == typeof(Double))
            {
                generator.Emit(OpCodes.Stind_R8);
            }
            else
            {
                throw new InvalidOperationException("Unexpected type: " + field.FieldType);
            }

            EmitDataLength(generator,field);
        }

        public void EmitDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field)
        {
            helper.EmitLogMessage(generator, "// starting decode of numeric");
            helper.EmitLogMessage(generator, "result."+ field.Name + " = *ptr");
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            generator.Emit(OpCodes.Ldloc_0);
            helper.EmitLogMessage(generator, "// ptr value");
            if (field.FieldType == typeof(byte))
            {
                generator.Emit(OpCodes.Ldind_U1);
            }
            else if( field.FieldType == typeof(sbyte))
            {
                generator.Emit(OpCodes.Ldind_I1);
            }
            else if (field.FieldType == typeof(Int16))
            {
                generator.Emit(OpCodes.Ldind_I2);
            }
            else if (field.FieldType == typeof(UInt16))
            {
                generator.Emit(OpCodes.Ldind_U2);
            }
            else if (field.FieldType == typeof(Int32))
            {
                generator.Emit(OpCodes.Ldind_I4);
            }
            else if (field.FieldType == typeof(UInt32))
            {
                generator.Emit(OpCodes.Ldind_U4);
            }
            else if (field.FieldType == typeof(Int64))
            {
                generator.Emit(OpCodes.Ldind_I8);
            }
            else if (field.FieldType == typeof(UInt64))
            {
                generator.Emit(OpCodes.Ldind_I8);  // TODO: How to Ldind for U8.
            }
            else if (field.FieldType == typeof(Single))
            {
                generator.Emit(OpCodes.Ldind_R4);  
            }
            else if (field.FieldType == typeof(Double))
            {
                generator.Emit(OpCodes.Ldind_R8);  
            }
            else
            {
                throw new InvalidOperationException("Unexpected type: " + field.FieldType);
            }
            helper.EmitLogStack(generator, "// *ptr");
            generator.Emit(OpCodes.Stfld, field);

            EmitDataLength(generator, field);
        }
    }
}