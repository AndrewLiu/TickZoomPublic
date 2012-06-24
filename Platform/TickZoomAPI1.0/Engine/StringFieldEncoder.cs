using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace TickZoom.Api
{
    public class StringFieldEncoder : FieldEncoder
    {
        protected EncodeHelper helper;
        public StringFieldEncoder(EncodeHelper helper)
        {
            this.helper = helper;
        }
        public void EmitEncode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field, int id)
        {
            helper.LogMessage(generator, "if( " + field.Name + " != null) {");
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            generator.Emit(OpCodes.Ldfld, field);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ceq);
            var nullCheckLabel = generator.DefineLabel();
            generator.Emit(OpCodes.Brtrue_S, nullCheckLabel);

            helper.LogMessage(generator, "*ptr = " + id + "; // member id");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, id);
            generator.Emit(OpCodes.Stind_I1);
            helper.IncrementPtr(generator);

            helper.LogMessage(generator, "// starting encode of string");
            helper.LogMessage(generator, "SerializeString( ptr, &field)");
            generator.Emit(OpCodes.Ldloc_0);
            helper.LogStack(generator, "ptr addrss");
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
            var serializerMethod = this.GetType().GetMethod("SerializeString");
            generator.Emit(OpCodes.Call,serializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            helper.LogStack(generator, "// ptr address after string");
            generator.Emit(OpCodes.Stloc_0);
            generator.MarkLabel(nullCheckLabel);
        }

        public void EmitDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field)
        {
            helper.LogMessage(generator, "// starting decode String( ptr, field)");
            helper.LogMessage(generator, "DeserializeString( ptr, out field)");
            generator.Emit(OpCodes.Ldloc_0);
            helper.LogStack(generator, "// ptr address");
            generator.Emit(OpCodes.Ldloc_0);
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            generator.Emit(OpCodes.Ldflda, field);
            var deserializerMethod = this.GetType().GetMethod("DeserializeString");
            generator.Emit(OpCodes.Call, deserializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            helper.LogStack(generator, "// ptr address after string");
            generator.Emit(OpCodes.Stloc_0);
        }

        public unsafe static long LengthString(byte* ptr, string str)
        {
            var start = ptr;

            var length = str.Length * sizeof(char);
            ptr += sizeof(UInt32);

            ptr += length;

            return ptr - start;
        }

        public unsafe static long DeserializeString(byte* ptr, out string str)
        {
            //Console.WriteLine("//DeserializeString() enter");
            var start = ptr;
            //Console.WriteLine("// string start " + (IntPtr)start);
            var byteLength = *((Int32*)ptr);
            var length = byteLength/sizeof (char);
            //Console.WriteLine("// string length " + byteLength);
            ptr += sizeof(Int32);
            str = new string((char*)ptr, 0, length);
            ptr += byteLength;
            //Console.WriteLine("//DeserializeString() exit");
            return ptr - start;
        }

        public unsafe static long SerializeString(byte* ptr, string str)
        {
            //Console.WriteLine("//SerializeString() enter");
            var start = ptr;
            //Console.WriteLine("// string start " + (IntPtr)start);
            *(int*)ptr = str.Length * sizeof(char);
            //Console.WriteLine("// string length " + *(int*)ptr);
            ptr += sizeof(UInt32);

            fixed (char* fchrptr = str)
            {
                var chrptr = fchrptr;
                var chrend = chrptr + str.Length;
                for (; chrptr < chrend; chrptr++)
                {
                    *(char*)ptr = *chrptr;
                    ptr += sizeof(char);
                }
            }
            return ptr - start;
        }

    }
}