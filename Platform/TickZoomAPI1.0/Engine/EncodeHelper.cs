using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace TickZoom.Api
{
    public class EncodeHelper
    {
        internal Dictionary<Type, MetaType> metaTypes = new Dictionary<Type, MetaType>();
        internal TypeEncoderMap encoders = new TypeEncoderMap();
        internal Dictionary<Type, int> typeCodesByType = new Dictionary<Type, int>();
        internal Dictionary<int, Type> typeCodesByCode = new Dictionary<int, Type>();
        internal int maxCode = 0;

        public EncodeHelper()
        {
            DefineType(typeof(LogicalOrderBinary),1);
        }

        public void DefineType( Type type, int code)
        {
            typeCodesByType.Add(type,code);
            typeCodesByCode.Add(code,type);
            if( code > maxCode)
            {
                maxCode = code;
            }
        }

        public int DefineType(Type type)
        {
            var code = ++maxCode;
            typeCodesByType.Add(type, code);
            typeCodesByCode.Add(code, type);
            return code;
        }

        #region Private Methods

        public unsafe void Encode(MemoryStream memory, object original)
        {
            var type = original.GetType();
            var typeEncoder = GetTypeEncoder(type);
            var length = typeEncoder.Length(original);
            if( length > 255)
            {
                throw new InvalidOperationException("Length must be less than 256 bytes.");
            }
            var lengthSize = 1; // one byte for length.
            memory.SetLength(memory.Position + length + lengthSize);
            fixed( byte *bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                *ptr = (byte) length;
                ptr++;
                var count = typeEncoder.Encode(ptr, original);
                if( length != count)
                {
                    throw new InvalidOperationException("Predicted length " + length + " differs from actual " + count);
                }
                memory.Position += count;
                memory.SetLength(memory.Position);
            }
        }

        public unsafe object Decode(MemoryStream memory)
        {
            fixed (byte* bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                var length = *ptr;
                ptr++;
                var end = ptr + length;
                var type = typeCodesByCode[*ptr];
                ptr++;
                var typeEncoder = GetTypeEncoder(type);
                var result = FormatterServices.GetUninitializedObject(type);
                var count = typeEncoder.Decode(ptr, end, result);
                memory.Position += count + 1;
                memory.SetLength(memory.Position);
                return result;
            }
        }

        private void UninitializeObject(ILGenerator generator, Type type)
        {
            generator.Emit(OpCodes.Ldarg_0);
            var methodInfo = typeof(object).GetMethod("GetType");
            generator.Emit(OpCodes.Call, methodInfo);

            methodInfo = typeof(FormatterServices).GetMethod("GetUninitializedObject");
            generator.Emit(OpCodes.Call, methodInfo);
            generator.Emit(OpCodes.Castclass, type);
        }

        /// <summary>
        /// Generic cloning method that clones an object using IL.
        /// Only the first call of a certain type will hold back performance.
        /// After the first call, the compiled IL is executed. 
        /// </summary>
        /// <param name="myObject">Type of object to clone</param>
        /// <returns>Cloned object (deeply cloned)</returns>
        public NumericTypeEncoder GetTypeEncoder(Type type)
        {
            NumericTypeEncoder encoder = null;
            if (!encoders.TryGetValue(type, out encoder))
            {
                encoder = new NumericTypeEncoder();
                encoders.Add(type, encoder);
                MetaType meta;
                if(!metaTypes.TryGetValue(type, out meta))
                {
                    meta = new MetaType(type);
                    meta.Generate();
                    metaTypes.Add(type,meta);
                }
                // Compile Encoder
                var dynMethod = CreateDynamicEncode(meta);
                var genericFunc = typeof(Func<,,>).MakeGenericType(typeof(IntPtr), type, typeof(long));
                encoder.EncoderDelegate = dynMethod.CreateDelegate(genericFunc);

                // Compile Decoder
                dynMethod = CreateDynamicDecode(meta);
                genericFunc = typeof(Func<,,,>).MakeGenericType(typeof(IntPtr), typeof(IntPtr), type, typeof(long));
                encoder.DecoderDelegate = dynMethod.CreateDelegate(genericFunc);

                // Compile Length
                dynMethod = CreateDynamicLength(meta);
                genericFunc = typeof(Func<,,>).MakeGenericType(typeof(IntPtr), type, typeof(long));
                encoder.LengthDelegate = dynMethod.CreateDelegate(genericFunc);
            }
            return encoder;
        }

        private DynamicMethod CreateDynamicEncode(MetaType meta)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoEncoding", typeof(long), new Type[] { typeof(IntPtr), meta.Type }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));

            // ptr = (byte*) arg0;
            generator.Emit(OpCodes.Ldarg_0);
            var explicitCast = typeof(IntPtr).GetMethod("op_Explicit", new Type[] { typeof(int) });
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, ptrLocal);

            // start = ptr;
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Stloc, startLocal);

            // *ptr = typeCode;
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, typeCodesByType[meta.Type]);
            generator.Emit(OpCodes.Stind_I1);

            // ptr++
            IncrementPtr(generator);

            foreach (var kvp in meta.Members)
            {
                var id = kvp.Key;
                var field = kvp.Value;

                // *ptr = memberId;
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldc_I4_S, id);
                generator.Emit(OpCodes.Stind_I1);

                IncrementPtr(generator);

                EmitField(generator, field, EncodeOperation.Encode);
            }

            // return ptr - start;
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Sub);
            generator.Emit(OpCodes.Conv_I8);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private void IncrementPtr(ILGenerator generator)
        {
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        private DynamicMethod CreateDynamicLength(MetaType meta)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoEncoding", typeof(long), new Type[] { typeof(IntPtr), meta.Type }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));

            // ptr = (byte*) arg0;
            generator.Emit(OpCodes.Ldarg_0);
            var explicitCast = typeof(IntPtr).GetMethod("op_Explicit", new Type[] { typeof(int) });
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, ptrLocal);

            // start = ptr;
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Stloc, startLocal);

            IncrementPtr(generator);  // typeCode

            foreach (var kvp in meta.Members)
            {
                var id = kvp.Key;
                var field = kvp.Value;

                IncrementPtr(generator);

                EmitField(generator, field, EncodeOperation.Length);
            }

            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Sub);
            generator.Emit(OpCodes.Conv_I8);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private DynamicMethod CreateDynamicDecode(MetaType meta)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoDecoding", typeof(long), new Type[] { typeof(IntPtr), typeof(IntPtr), meta.Type }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));
            var endLocal = generator.DeclareLocal(typeof(byte*));
            var memberLocal = generator.DeclareLocal(typeof(byte));


            // ptr = (byte*) arg0;
            generator.Emit(OpCodes.Ldarg_0);
            var explicitCast = typeof(IntPtr).GetMethod("op_Explicit", new Type[] { typeof(int) });
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, ptrLocal);

            // start = ptr;
            generator.Emit(OpCodes.Ldloc_0); // ptr
            generator.Emit(OpCodes.Stloc, startLocal);

            // end = (byte*) arg1;
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, endLocal);

            // while( ptr < end) 
            var whileLoop = generator.DefineLabel();
            generator.Emit(OpCodes.Br, whileLoop);
            var continueLoop = generator.DefineLabel();
            generator.MarkLabel(continueLoop);

            // member = *ptr
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldind_U1);
            generator.Emit(OpCodes.Stloc_3);

            IncrementPtr(generator);

            var nextfieldLabel = generator.DefineLabel();
            var firstField = true;
            foreach (var kvp in meta.Members)
            {
                if (firstField)
                {
                    firstField = false;
                }
                else
                {
                    generator.MarkLabel(nextfieldLabel);
                    nextfieldLabel = generator.DefineLabel();
                }
                var id = kvp.Key;
                var field = kvp.Value;
                // if member != id skip it.
                generator.Emit(OpCodes.Ldloc_3);
                generator.Emit(OpCodes.Ldc_I4_S,id);
                generator.Emit(OpCodes.Bne_Un_S, nextfieldLabel);

                EmitField(generator, field, EncodeOperation.Decode);

                generator.Emit(OpCodes.Br, whileLoop);
            }
            generator.MarkLabel(nextfieldLabel);
            // while loop
            generator.MarkLabel(whileLoop);
            // continue while loop or not?
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_2);
            generator.Emit(OpCodes.Blt_Un, continueLoop);

            // return ptr - start;
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_1);
            generator.Emit(OpCodes.Sub);
            generator.Emit(OpCodes.Conv_I8);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private void EmitField(ILGenerator generator, FieldInfo field, EncodeOperation operation)
        {
            NumericTypeEncoder typeEncoder;
            if (!encoders.encoders.TryGetValue(field.FieldType, out typeEncoder))
            {
                throw new InvalidOperationException("Can't find serializer for: " + field.FieldType.FullName);
            }
            switch (operation)
            {
                case EncodeOperation.Encode:
                    typeEncoder.EmitEncode(generator, field);
                    break;
                case EncodeOperation.Decode:
                    typeEncoder.EmitDecode(generator, field);
                    break;
                case EncodeOperation.Length:
                    typeEncoder.EmitLength(generator, field);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unknown direction: " + operation);
            }
        }

        public enum EncodeOperation
        {
            Encode,
            Decode,
            Length
        }

        #endregion
    }
}