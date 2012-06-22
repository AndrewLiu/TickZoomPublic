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
        internal TypeEncoderMap typeEncoders = new TypeEncoderMap();
        internal FieldEncoderMap fieldEncoders = new FieldEncoderMap();
        internal Dictionary<Type, int> typeCodesByType = new Dictionary<Type, int>();
        internal Dictionary<int, Type> typeCodesByCode = new Dictionary<int, Type>();
        internal int maxCode = 0;

        public EncodeHelper()
        {
            DefineType(typeof(LogicalOrderBinary), 1);
            DefineType(typeof(IntervalImpl), 2);
            DefineType(typeof(PhysicalOrderDefault), 3);
            DefineType(typeof(LogicalFillBinary), 4);
            DefineType(typeof(PhysicalFillDefault), 5);
            DefineType(typeof(LogicalFillBinaryBox), 6);
            DefineType(typeof(TransactionPairBinary), 7);
            DefineType(typeof(TimeStamp), 8);
            DefineType(typeof(TickSync), 9);
            DefineType(typeof(PositionChangeDetail), 10);
            DefineType(typeof(TimeFrame), 11);
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

        public int DefineTemporaryType(Type type)
        {
            var code = ++maxCode;
            typeCodesByType.Add(type, code);
            typeCodesByCode.Add(code, type);
            return code;
        }

        #region Private Methods

        private ResultPointer resultPointer = new ResultPointer();

        public unsafe void Encode(MemoryStream memory, object original)
        {
            var type = original.GetType();
            var typeEncoder = GetTypeEncoder(type);
            if( memory.Length < memory.Position + 256)
            {
                memory.SetLength(memory.Position + 256);
            }
            fixed( byte *bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                var count = typeEncoder.Encode(ptr, original);
                memory.Position += count;
            }
        }

        public unsafe object Decode(MemoryStream memory)
        {
            fixed (byte* bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                // Get the root type
                ptr++;
                var type = GetTypeByCode(*ptr);
                var typeEncoder = GetTypeEncoder(type);
                var count = typeEncoder.Decode(bptr, resultPointer);
                memory.Position += count;
                memory.SetLength(memory.Position);
                return resultPointer.Result;
            }
        }

        public Type GetTypeByCode( int typeCode)
        {
            Type type;
            if (!typeCodesByCode.TryGetValue(typeCode, out type))
            {
                throw new ApplicationException("No type defined for cod " + typeCode + ". Please add to " + this.GetType() + " in the constructor.");
            }
            return type;
        }

        public object InstantiateType(Type type)
        {
            return FormatterServices.GetUninitializedObject(type);
        }

        private int GetTypeCode(Type type)
        {
            int typeCode;
            if (!typeCodesByType.TryGetValue(type, out typeCode))
            {
                throw new ApplicationException("No type code defined to serialize " + type.FullName + ". Please add to " + this.GetType() + " in the constructor.");
            }
            return typeCode;
        }

        private MetaType GetMeta(Type type)
        {
            MetaType meta;
            if (!metaTypes.TryGetValue(type, out meta))
            {
                var typeCode = GetTypeCode(type);
                meta = new MetaType(type,typeCode);
                meta.Generate();
                metaTypes.Add(type, meta);
            }
            return meta;
        }

        /// <summary>
        /// Generic cloning method that clones an object using IL.
        /// Only the first call of a certain type will hold back performance.
        /// After the first call, the compiled IL is executed. 
        /// </summary>
        /// <param name="myObject">Type of object to clone</param>
        /// <returns>Cloned object (deeply cloned)</returns>
        public TypeEncoder GetTypeEncoder(Type type)
        {
            TypeEncoder encoder = null;
            if (!typeEncoders.TryGetValue(type, out encoder))
            {
                encoder = new TypeEncoder(this,type);
                typeEncoders.Add(type, encoder);
                // Compile Encoder

                var dynMethod = CreateDynamicEncode(type);
                var genericFunc = typeof(Func<,,>).MakeGenericType(typeof(IntPtr), type, typeof(long));
                encoder.EncoderDelegate = dynMethod.CreateDelegate(genericFunc);

                // Compile Decoder
                dynMethod = CreateDynamicDecode(type);
                genericFunc = typeof(Func<,,,>).MakeGenericType(typeof(EncodeHelper),typeof(IntPtr),typeof(ResultPointer),typeof(long));
                encoder.DecoderDelegate = dynMethod.CreateDelegate(genericFunc);

            }
            return encoder;
        }

        public class ResultPointer
        {
            public object Result;
        }

        private static bool debug = false;
        public static void LogMessage(ILGenerator generator, string message)
        {
            if( debug)
            {
                generator.Emit(OpCodes.Ldstr, message);
                var writeLineMethod = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) });
                generator.Emit(OpCodes.Call, writeLineMethod);
            }
        }

        public static void LogValue(ILGenerator generator, string message)
        {
            if (debug)
            {
                LogMessage(generator, message);
                generator.Emit(OpCodes.Conv_U8);
                var writeLineMethod = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(ulong) });
                generator.Emit(OpCodes.Call, writeLineMethod);
            }
        }

        public static void LogStack(ILGenerator generator, string message)
        {
            if (debug)
            {
                LogMessage(generator,message);
                generator.Emit(OpCodes.Dup);
                generator.Emit(OpCodes.Conv_U8);
                var writeLineMethod = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(ulong) });
                generator.Emit(OpCodes.Call, writeLineMethod);
            }
        }

        private DynamicMethod CreateDynamicEncode(Type type)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoEncoding", typeof(long), new Type[] { typeof(IntPtr), type }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));

            LogMessage(generator, "ptr = (byte*) arg0;");
            generator.Emit(OpCodes.Ldarg_0);
            var explicitCast = typeof(IntPtr).GetMethod("op_Explicit", new Type[] { typeof(int) });
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, ptrLocal);

            LogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Stloc, startLocal);

            LogMessage(generator, "// starting encode of " + type);
            var resultLocal = generator.DeclareLocal(type);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stloc, resultLocal);
            EmitTypeEncode(generator, resultLocal, type);

            LogMessage(generator, "return *start;");
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Ldind_U1);
            generator.Emit(OpCodes.Conv_U8);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private void EmitTypeEncode(ILGenerator generator, LocalBuilder resultLocal, Type type)
        {
            var startLocal = generator.DeclareLocal(typeof(byte*));

            LogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc_0);
            LogStack(generator, "// start  ptr");
            generator.Emit(OpCodes.Stloc, startLocal);

            IncrementPtr(generator);

            var meta = GetMeta(type);
            LogMessage(generator, "*ptr = typeCode;");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, meta.TypeCode);
            generator.Emit(OpCodes.Stind_I1);

            IncrementPtr(generator);

            foreach (var kvp in meta.Members)
            {
                var id = kvp.Key;
                var field = kvp.Value;

                EmitFieldEncode(generator, resultLocal, field, id);
            }

            LogMessage(generator, "*start = ptr - start");
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Ldloc_0);
            LogStack(generator, "// end ptr");
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Sub);
            generator.Emit(OpCodes.Conv_I8);
            generator.Emit(OpCodes.Conv_U1);
            generator.Emit(OpCodes.Stind_I1);
        }

        public static void IncrementPtr(ILGenerator generator)
        {
            IncrementPtr(generator, 1);
        }

        public static void IncrementPtr(ILGenerator generator, int size)
        {
            if( size == 1)
            {
                LogMessage(generator, "++ptr;");
            }
            else
            {
                LogMessage(generator, "ptr+=" + size + ";");
            }
            generator.Emit(OpCodes.Ldloc_0);
            switch( size)
            {
                case 1:
                    generator.Emit(OpCodes.Ldc_I4_1);
                    break;
                case 2:
                    generator.Emit(OpCodes.Ldc_I4_2);
                    break;
                case 3:
                    generator.Emit(OpCodes.Ldc_I4_3);
                    break;
                case 4:
                    generator.Emit(OpCodes.Ldc_I4_4);
                    break;
                case 5:
                    generator.Emit(OpCodes.Ldc_I4_5);
                    break;
                case 6:
                    generator.Emit(OpCodes.Ldc_I4_6);
                    break;
                case 7:
                    generator.Emit(OpCodes.Ldc_I4_7);
                    break;
                case 8:
                    generator.Emit(OpCodes.Ldc_I4_8);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected increment of " + size);
            }
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);
        }

        private DynamicMethod CreateDynamicDecode(Type type)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoDecoding", typeof(long), new Type[] { typeof(EncodeHelper), typeof(IntPtr), typeof(ResultPointer) }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));

            LogMessage(generator, "// starting emitting decode of " + type);
            LogMessage(generator, "ptr = (byte*) arg1");
            generator.Emit(OpCodes.Ldarg_1);
            var explicitCast = typeof(IntPtr).GetMethod("op_Explicit", new Type[] { typeof(int) });
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, ptrLocal);

            LogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Stloc, startLocal);

            var resultLocal = EmitTypeDecode(generator, type);

            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Ldloc, resultLocal);
            generator.Emit(OpCodes.Stfld, typeof(ResultPointer).GetField("Result"));


            LogMessage(generator, "return *start;");
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Ldind_U1);
            generator.Emit(OpCodes.Conv_U8);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private unsafe LocalBuilder EmitTypeDecode(ILGenerator generator, Type type)
        {
            var startLocal = generator.DeclareLocal(typeof(byte*));
            var endLocal = generator.DeclareLocal(typeof(byte*));
            var memberLocal = generator.DeclareLocal(typeof(byte*));
            LogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc_0); // ptr
            LogStack(generator, "// start ptr");
            generator.Emit(OpCodes.Stloc, startLocal);

            LogMessage(generator, "end = ptr + *ptr;");
            generator.Emit(OpCodes.Ldloc_0); // ptr
            generator.Emit(OpCodes.Ldloc_0); // ptr
            generator.Emit(OpCodes.Ldind_U1);
            generator.Emit(OpCodes.Add);
            LogStack(generator, "// End ptr");
            generator.Emit(OpCodes.Stloc, endLocal);

            // increment for the length byte.
            IncrementPtr(generator);

            LogMessage(generator, "GetTypeByCode(*ptr)");
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldloc_0); // ptr
            generator.Emit(OpCodes.Ldind_U1);
            var typeByCodeMethod = this.GetType().GetMethod("GetTypeByCode");
            generator.Emit(OpCodes.Call, typeByCodeMethod);

            LogMessage(generator, "helper.Instantiate(" + type + ")");
            var instantiateMethod = this.GetType().GetMethod("InstantiateType");
            generator.Emit(OpCodes.Callvirt, instantiateMethod);
            generator.Emit(OpCodes.Castclass, type);
            var resultLocal = generator.DeclareLocal(type);
            generator.Emit(OpCodes.Stloc, resultLocal);

            // increment for the type byte.
            IncrementPtr(generator);

            LogMessage(generator, "while( ptr < end)");
            var whileLoop = generator.DefineLabel();
            generator.Emit(OpCodes.Br, whileLoop);
            var continueLoop = generator.DefineLabel();
            generator.MarkLabel(continueLoop);

            LogMessage(generator, "member = *ptr");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldind_U1);
            generator.Emit(OpCodes.Stloc, memberLocal);

            IncrementPtr(generator);

            var meta = GetMeta(type);

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
                LogMessage(generator, "if member != " + id + " // for field " + field.Name);
                generator.Emit(OpCodes.Ldloc,memberLocal);
                LogStack(generator,"member value");
                generator.Emit(OpCodes.Ldc_I4_S,id);
                generator.Emit(OpCodes.Bne_Un, nextfieldLabel);

                EmitFieldDecode(generator, resultLocal, field);

                generator.Emit(OpCodes.Br, whileLoop);
            }
            generator.MarkLabel(nextfieldLabel);

            LogMessage(generator, "while loop");
            generator.MarkLabel(whileLoop);
            // continue while loop or not?
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc,endLocal);
            generator.Emit(OpCodes.Blt_Un, continueLoop);

            return resultLocal;
        }

        private void EmitFieldEncode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field, int id)
        {
            var lookupType = field.FieldType;
            if( lookupType.IsEnum)
            {
                lookupType = typeof (Enum);
            }
            FieldEncoder fieldEncoder;
            if (fieldEncoders.encoders.TryGetValue(lookupType, out fieldEncoder))
            {
                fieldEncoder.EmitEncode(generator, resultLocal, field, id);
            }
            else
            {
                LogMessage(generator, "*ptr = memberId;");
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldc_I4_S, id);
                generator.Emit(OpCodes.Stind_I1);

                IncrementPtr(generator);

                LogMessage(generator, "// Encoding " + lookupType + " as a field.");
                int typeCode;
                if (typeCodesByType.TryGetValue(lookupType, out typeCode))
                {
                    generator.Emit(OpCodes.Ldloc, resultLocal);
                    generator.Emit(OpCodes.Ldfld, field);
                    var fieldLocal = generator.DeclareLocal(lookupType);
                    generator.Emit(OpCodes.Stloc, fieldLocal);
                    EmitTypeEncode(generator, fieldLocal, lookupType);
                }
                else
                {
                    throw new InvalidOperationException("Can't find field or type serializer for " + field.FieldType.FullName + " on field " + field.Name + " of " + field.ReflectedType.FullName);
                }
            }
        }

        private void EmitFieldDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field)
         {
            var lookupType = field.FieldType;
            if (lookupType.IsEnum)
            {
                lookupType = typeof(Enum);
            }
            FieldEncoder fieldEncoder;
            if (fieldEncoders.encoders.TryGetValue(lookupType, out fieldEncoder))
            {
                fieldEncoder.EmitDecode(generator, resultLocal, field);
            }
            else
            {
                int typeCode;
                if (typeCodesByType.TryGetValue(lookupType, out typeCode))
                {
                    var subResultLocal = EmitTypeDecode(generator, lookupType);
                    EncodeHelper.LogMessage(generator, "ResultPointer." + field.Name + " = result;");
                    generator.Emit(OpCodes.Ldloc, resultLocal);
                    generator.Emit(OpCodes.Ldloc, subResultLocal);
                    generator.Emit(OpCodes.Stfld, field);
                }
                else
                {
                    throw new InvalidOperationException("Can't find field or type serializer for " + field.FieldType.FullName + " on field " + field.Name + " of " + field.ReflectedType.FullName);
                }
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