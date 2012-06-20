using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace TickZoom.Api
{
    public class StringFieldEncoder : FieldEncoder
    {
        public void EmitEncode(ILGenerator generator, FieldInfo field)
        {
            // SerializeString( ptr, &field)
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldfld, field);
            var serializerMethod = this.GetType().GetMethod("SerializeString");
            generator.Emit(OpCodes.Call,serializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        public void EmitDecode(ILGenerator generator, FieldInfo field)
        {
            // DeserializeString( ptr, &field)
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Ldflda,field);
            var deserializerMethod = this.GetType().GetMethod("DeserializeString");
            generator.Emit(OpCodes.Call, deserializerMethod);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
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
            var start = ptr;
            var length = *((int*)ptr) / sizeof(char);
            ptr += sizeof(UInt32);
            str = new string((char*)ptr, 0, length);
            ptr += length;
            return ptr - start;
        }

        public unsafe static long SerializeString(byte* ptr, string str)
        {
            var start = ptr;

            *(int*)ptr = str.Length * sizeof(char);
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