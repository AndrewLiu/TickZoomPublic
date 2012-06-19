using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace TickZoom.Api
{
    public static class EncodeHelper
    {
        internal static Dictionary<Type, MetaType> metaTypes = new Dictionary<Type, MetaType>();
        internal static TypeEncoderMap encoders = new TypeEncoderMap();
        #region Private Methods

        private static readonly Type stringType = typeof(string);
        private static readonly Type delegateType = typeof(Delegate);

        public unsafe static void Encode(MemoryStream memory, object original)
        {
            var type = original.GetType();
            var typeEncoder = GetTypeEncoder(type);
            var length = typeEncoder.Length(original);
            memory.SetLength(memory.Position + length);
            fixed( byte *bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                var count = typeEncoder.Encode(ptr, original);
                memory.Position += count;
                memory.SetLength(memory.Position);
            }
        }

        public unsafe static object Decode(MemoryStream memory, Type type)
        {
            var typeEncoder = GetTypeEncoder(type);
            fixed (byte* bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                var result = FormatterServices.GetUninitializedObject(type);
                var count = typeEncoder.Decode(ptr, result);
                memory.Position += count;
                memory.SetLength(memory.Position);
                return result;
            }
        }

        public static object Decode(MemoryStream memory)
        {
            return null;
        }

        private static void UninitializeObject(ILGenerator generator, Type type)
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
        public static NumericTypeEncoder GetTypeEncoder(Type type)
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
                var dynMethod = CreateDynamicMethod(meta, EncodeOperation.Encode);
                var genericFunc = typeof(Func<,,>).MakeGenericType(typeof(IntPtr), type, typeof(long));
                encoder.EncoderDelegate = dynMethod.CreateDelegate(genericFunc);

                // Compile Decoder
                dynMethod = CreateDynamicMethod(meta, EncodeOperation.Decode);
                genericFunc = typeof(Func<,,>).MakeGenericType(typeof(IntPtr), type, typeof(long));
                encoder.DecoderDelegate = dynMethod.CreateDelegate(genericFunc);

                // Compile Length
                dynMethod = CreateDynamicMethod(meta, EncodeOperation.Length);
                genericFunc = typeof(Func<,,>).MakeGenericType(typeof(IntPtr), type, typeof(long));
                encoder.LengthDelegate = dynMethod.CreateDelegate(genericFunc);
            }
            return encoder;
        }

        private static DynamicMethod CreateDynamicMethod(MetaType meta, EncodeOperation operation)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoEncoding", typeof(long), new Type[] { typeof(IntPtr), meta.Type }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));

            generator.Emit(OpCodes.Ldarg_0);
            var explicitCast = typeof(IntPtr).GetMethod("op_Explicit", new Type[] { typeof(int) });
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, ptrLocal);
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Stloc, startLocal);

            foreach( var kvp in meta.Members)
            {
                var id = kvp.Key;
                var field = kvp.Value;
                EmitField(generator, field, operation);
            }
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Sub);
            generator.Emit(OpCodes.Conv_I8);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private static void EmitField(ILGenerator generator, FieldInfo field, EncodeOperation operation)
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