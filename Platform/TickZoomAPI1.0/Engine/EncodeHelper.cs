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
        private static Log log = Factory.SysLog.GetLogger(typeof (EncodeHelper));
        internal Dictionary<Type, MetaType> metaTypes = new Dictionary<Type, MetaType>();
        internal TypeEncoderMap typeEncoders = new TypeEncoderMap();
        internal TypeDecoderMap typeDecoders = new TypeDecoderMap();
        internal FieldEncoderMap fieldEncoders;
        internal Dictionary<Type, int> typeCodesByType = new Dictionary<Type, int>();
        internal Dictionary<int, Type> typeCodesByCode = new Dictionary<int, Type>();
        internal int maxCode = 0;
        private bool debug = false;
        private bool isInitialized;

        public EncodeHelper()
        {
            fieldEncoders = new FieldEncoderMap(this);
        }

        public void Initialize()
        {
            isInitialized = true;
            DefineType<LogicalOrderBinary>(1);
            DefineType<IntervalImpl>(2);
            DefineType<PhysicalOrderDefault>(3);
            DefineType<LogicalFillDefault>(4);
            DefineType<PhysicalFillDefault>(5);
            DefineType<LogicalFillBinaryBox>(6);
            //DefineType<TransactionPairBinary>(7);
            DefineType<PositionChangeDetail>(8);
            DefineType<TimeFrame>(9);
            DefineType<LogicalOrderDefault>(10);
            DefineType<PhysicalOrderBinary>(11);
        }

        public bool Debug
        {
            get { return debug; }
            set { debug = value; }
        }

        public void DefineType<T>(int code)
        {
            var type = typeof (T);
            try
            {
                typeCodesByType.Add(type, code);
            }
            catch( ArgumentException)
            {
                throw new ArgumentException("The key " + type + " with code " + code + " already exists.");
            }
            try
            {
                typeCodesByCode.Add(code, type);
            }
            catch( ArgumentException)
            {
                throw new ArgumentException("The key " + code + " with value " + type + " already exists.");
            }
            if( code > maxCode)
            {
                maxCode = code;
            }
            CreateMeta(type);
            CreateTypeEncoder<T>();
        }

        #region Private Methods

        public unsafe void Encode<T>(MemoryStream memory, T original)
        {
            var type = original.GetType();
            if( !isInitialized) Initialize();
            objects.Clear();
            objectsById.Clear();
            if (memory.Length < memory.Position + 1024)
            {
                memory.SetLength(memory.Position + 1024);
            }
            fixed (byte* bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                var typeEncoder = GetTypeEncoder(type);
                memory.Position += typeEncoder.Encode(ptr, original);
            }
        }

        private Dictionary<object, int> objects = new Dictionary<object, int>();
        private Dictionary<int,object> objectsById = new Dictionary<int,object>();

        public byte nextObjectId;

        public byte GetNextObjectId(object obj)
        {
            var id = ++nextObjectId;
            if (Debug) log.Info("GetNextObjectId " + id);
            AddObjectOnly(obj,id);
            return id;
        }

        public object GetObject(byte id)
        {
            object result = null;
            objectsById.TryGetValue(id, out result);
            if (Debug) log.Info("GetObject( " + id + ") return " + result);
            return result;
        }

        public unsafe long Encode(byte* ptr, object original)
        {
            int id;
            if (objects.TryGetValue(original, out id))
            {
                var start = ptr;
                ptr += sizeof(short); // skip length;
                *ptr = (byte)id;
                ptr++;
                var length = (short)(ptr - start);
                *(short*)start = length;
                if (Debug) log.Info("Writing id " + id + " with length " + length);
                return length;
            }
            var typeEncoder = GetTypeEncoder(original.GetType());
            return typeEncoder.Encode(ptr, original);
        }

        public unsafe long Encode<T>(byte* ptr, T original)
        {
            int id;
            if( objects.TryGetValue(original, out id))
            {
                var start = ptr;
                ptr += sizeof (short); // skip length;
                *ptr = (byte) id;
                ptr++;
                var length = (short) (ptr - start);
                *(short*) start = length;
                if( Debug) log.Info("Writing id " + id + " with length " + length);
                return length;
            }
            else
            {
                var typeEncoder = GetTypeEncoder<T>();
                return typeEncoder.Encode(ptr, original);
            }
        }

        public unsafe object Decode(MemoryStream memory)
        {
            objects.Clear();
            objectsById.Clear();
            fixed (byte* bptr = &memory.GetBuffer()[0])
            {
                var ptr = bptr + memory.Position;
                var result = Decode(ptr);
                memory.Position += *(short*)ptr;
                return result;
            }
        }

        public void AddObjectOnly(object obj, int objectId)
        {
            if( Debug) log.Info("AddObjectOnly(" + obj + ", " + objectId + ")");
            objects.Add(obj, objectId);
        }

        public void AddObjectId(object obj, int objectId)
        {
            if (Debug) log.Info("AddObjectId(" + obj.GetType() + ", " + objectId + ")");
            objectsById.Add(objectId, obj);
        }

        public unsafe object Decode(byte* ptr)
        {
            // type is after size and object id.
            var start = ptr;
            var length = *(short*) ptr;
            if( Debug) log.Info("Decode length " + length);
            var end = start + length;
            ptr += sizeof (short);
            var objectId = *ptr;
            if (Debug) log.Info("Decode object id " + objectId);
            ptr++;
            if( ptr < end)
            {
                var typeCode = *ptr;
                ptr++;
                var type = GetTypeByCode(typeCode);
                var typeDecoder = GetTypeDecoder(type);
                return typeDecoder.Decode(start);
            }
            return GetObject(objectId);
        }

        public Type GetTypeByCode( int typeCode)
        {
            Type type;
            if (!typeCodesByCode.TryGetValue(typeCode, out type))
            {
                throw new ApplicationException("No type defined for code " + typeCode + ". Please add to " + this.GetType() + " in the constructor.");
            }
            return type;
        }

        public object InstantiateType(Type type, byte id)
        {
            if( Debug) log.Info("Instantiate Type " + type + ", id " + id);
            var result = FormatterServices.GetUninitializedObject(type);
            // Skip adding value types.
            AddObjectId(result, id);
            if(Debug)
            {
                log.Info("Type was " + type);
                if( result == null )
                {
                    log.Info("Instantiate " + type.FullName + " returned null.");
                }
                else
                {
                    log.Info("Instantiate " + type.FullName + " succeeded.");
                }
            }
            return result;
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
                meta = CreateMeta(type);
            }
            return meta;
        }

        private MetaType CreateMeta(Type type)
        {
            MetaType meta;
            var typeCode = GetTypeCode(type);
            meta = new MetaType(type,typeCode);
            meta.Generate();
            metaTypes.Add(type, meta);
            return meta;
        }

        public TypeEncoder GetTypeEncoder(Type type)
        {
            TypeEncoder encoder = null;
            if (!typeEncoders.TryGetValue(type, out encoder))
            {
                throw new ApplicationException("Cannot find type encoder for " + type.FullName);
            }
            return encoder;
        }

        public TypeEncoder GetTypeEncoder<T>()
        {
            var type = typeof (T);
            TypeEncoder encoder = null;
            if (!typeEncoders.TryGetValue(type, out encoder))
            {
                encoder = CreateTypeEncoder<T>();
            }
            return encoder;
        }

        private TypeEncoder CreateTypeEncoder<T>()
        {
            var type = typeof (T);
            TypeEncoder encoder;
            var newEncoder = new TypeEncoder<T>(this);
            encoder = newEncoder;
            typeEncoders.Add(type, encoder);

            // Compile Encoder
            var dynMethod = CreateDynamicEncode(type);
            var genericFunc = typeof(Func<,,,>).MakeGenericType(typeof(EncodeHelper),typeof(IntPtr), type, typeof(long));
            newEncoder.EncoderDelegate = (Func<EncodeHelper, IntPtr, T, long>) dynMethod.CreateDelegate(genericFunc);
            return encoder;
        }

        public TypeDecoder GetTypeDecoder(Type type)
        {
            TypeDecoder decoder = null;
            if (!typeDecoders.TryGetValue(type, out decoder))
            {
                decoder = new TypeDecoder(this);
                typeDecoders.Add(type, decoder);

                // Compile Decoder
                var dynMethod = CreateDynamicDecode(type);
                var genericFunc = typeof(Func<,,>).MakeGenericType(typeof(EncodeHelper), typeof(IntPtr), typeof(object));
                decoder.DecoderDelegate = (Func<EncodeHelper, IntPtr, object>)dynMethod.CreateDelegate(genericFunc);

            }
            return decoder;
        }

        public class ResultPointer
        {
            public object Result;
        }

        public static void LogMessage( string message)
        {
            log.Info(message);
        }

        public static void LogMessage(ulong message)
        {
            log.Info(message);
        }

        public static void LogMessage(double message)
        {
            log.Info(message);
        }

        public void EmitLogMessage(ILGenerator generator, string message)
        {
            if( Debug)
            {
                generator.Emit(OpCodes.Ldstr, message);
                var infoMethod = this.GetType().GetMethod("LogMessage", new[] { typeof(string) });
                generator.Emit(OpCodes.Call, infoMethod);
            }
        }

        public void EmitLogValue(ILGenerator generator, string message)
        {
            if (Debug)
            {
                EmitLogMessage(generator, message);
                generator.Emit(OpCodes.Conv_U8);
                var infoMethod = this.GetType().GetMethod("LogMessage", new[] { typeof(ulong) });
                generator.Emit(OpCodes.Call, infoMethod);
            }
        }

        public void EmitLogStack(ILGenerator generator, string message)
        {
            if (Debug)
            {
                EmitLogMessage(generator,message);
                generator.Emit(OpCodes.Dup);
                generator.Emit(OpCodes.Conv_R8);
                var infoMethod = this.GetType().GetMethod("LogMessage", new[] { typeof(double) });
                generator.Emit(OpCodes.Call, infoMethod);
            }
        }

        private DynamicMethod CreateDynamicEncode(Type type)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoEncoding", typeof(long), new Type[] { typeof(EncodeHelper), typeof(IntPtr), type }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));

            EmitLogMessage(generator, "ptr = (byte*) arg0;");
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stloc, ptrLocal);

            EmitLogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Stloc, startLocal);

            EmitLogMessage(generator, "// starting encode of " + type);
            var resultLocal = generator.DeclareLocal(type);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Stloc, resultLocal);

            EmitTypeEncode(generator, resultLocal, type);

            EmitLogMessage(generator, "return *(short*)start;");
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Ldind_U2);
            generator.Emit(OpCodes.Conv_U8);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private void EmitTypeEncode(ILGenerator generator, LocalBuilder resultLocal, Type type)
        {
            var startLocal = generator.DeclareLocal(typeof(byte*));

            EmitLogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc_0);
            EmitLogStack(generator, "// start  ptr");
            generator.Emit(OpCodes.Stloc, startLocal);

            // Skip size space
            IncrementPtr(generator, 2);

            if (resultLocal.LocalType.IsValueType)
            {
                EmitLogMessage(generator, "// for value type write object id 0");
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldc_I4_0);
                generator.Emit(OpCodes.Stind_I1);
            }
            else
            {
                EmitLogMessage(generator, "*ptr = helper.GetNextObjectId( obj);");
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldloc, resultLocal);
                var getObjectIdMethod = this.GetType().GetMethod("GetNextObjectId");
                generator.Emit(OpCodes.Call, getObjectIdMethod);
                EmitLogStack(generator, "// object id");
                generator.Emit(OpCodes.Stind_I1);
            }

            IncrementPtr(generator); // for object id

            var meta = GetMeta(type);
            EmitLogMessage(generator, "*ptr = " + meta.TypeCode + "; // type code");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, meta.TypeCode);
            generator.Emit(OpCodes.Stind_I1);

            IncrementPtr(generator);

            if(Debug) log.Info( type + " members count " + meta.Members.Count);
            foreach (var kvp in meta.Members)
            {
                var id = kvp.Key;
                var field = kvp.Value;
                EmitLogMessage(generator, "Emitting field " + field.Name);
                EmitFieldEncode(generator, resultLocal, field, id);
            }

            EmitLogMessage(generator, "*(short*)start = ptr - start");
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Ldloc_0);
            EmitLogStack(generator, "// end ptr");
            generator.Emit(OpCodes.Ldloc, startLocal);
            generator.Emit(OpCodes.Sub);
            generator.Emit(OpCodes.Conv_I8);
            generator.Emit(OpCodes.Conv_U2);
            generator.Emit(OpCodes.Stind_I2);
        }

        public void IncrementPtr(ILGenerator generator)
        {
            IncrementPtr(generator, 1);
        }

        public void IncrementPtr(ILGenerator generator, int size)
        {
            if( size == 1)
            {
                EmitLogMessage(generator, "++ptr;");
            }
            else
            {
                EmitLogMessage(generator, "ptr+=" + size + ";");
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
            var dymMethod = new DynamicMethod("DoDecoding", typeof(object), new Type[] { typeof(EncodeHelper), typeof(IntPtr)}, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var ptrLocal = generator.DeclareLocal(typeof(byte*));
            var startLocal = generator.DeclareLocal(typeof(byte*));

            EmitLogMessage(generator, "// starting decode of " + type);
            EmitLogMessage(generator, "ptr = (byte*) arg1");
            generator.Emit(OpCodes.Ldarg_1);
            var explicitCast = typeof(IntPtr).GetMethod("op_Explicit", new Type[] { typeof(int) });
            generator.Emit(OpCodes.Call, explicitCast);
            generator.Emit(OpCodes.Stloc, ptrLocal);

            EmitLogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc, ptrLocal);
            generator.Emit(OpCodes.Stloc, startLocal);

            var resultLocal = EmitTypeDecode(generator, type);

            generator.Emit(OpCodes.Ldloc, resultLocal);
            if (type.IsValueType)
            {
                generator.Emit(OpCodes.Box, type);
            }
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        private unsafe LocalBuilder EmitTypeDecode(ILGenerator generator, Type type)
        {
            var startLocal = generator.DeclareLocal(typeof(byte*));
            var endLocal = generator.DeclareLocal(typeof(byte*));
            var memberLocal = generator.DeclareLocal(typeof(byte*));
            var resultLocal = generator.DeclareLocal(type);

            EmitLogMessage(generator, "start = ptr;");
            generator.Emit(OpCodes.Ldloc_0); // ptr
            EmitLogStack(generator, "// start ptr");
            generator.Emit(OpCodes.Stloc, startLocal);

            EmitLogMessage(generator, "end = ptr + *(short*)ptr;");
            generator.Emit(OpCodes.Ldloc_0); // ptr
            generator.Emit(OpCodes.Ldloc_0); // ptr
            generator.Emit(OpCodes.Ldind_U2);
            EmitLogStack(generator, "// *(short*)ptr;");
            generator.Emit(OpCodes.Add);
            //LogStack(generator, "// End ptr");
            generator.Emit(OpCodes.Stloc, endLocal);

            // increment for the length byte.
            IncrementPtr(generator,2);

            // Increment for the object id.
            EmitLogMessage(generator, "objectId = *ptr;");
            generator.Emit(OpCodes.Ldloc_0); // ptr
            generator.Emit(OpCodes.Ldind_U1);
            var objectIdLocal = generator.DeclareLocal(typeof (byte));
            generator.Emit(OpCodes.Stloc, objectIdLocal);
            IncrementPtr(generator);

            if (!type.IsValueType)
            {
                EmitLogMessage(generator, "GetTypeByCode(*ptr)");
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldloc_0); // ptr
                generator.Emit(OpCodes.Ldind_U1);
                generator.Emit(OpCodes.Conv_I);
                EmitLogStack(generator, "// type code *ptr");
                var typeByCodeMethod = this.GetType().GetMethod("GetTypeByCode");
                generator.Emit(OpCodes.Call, typeByCodeMethod);
                generator.Emit(OpCodes.Ldloc, objectIdLocal);

                EmitLogMessage(generator, "helper.Instantiate(" + type + ", object id )");
                var instantiateMethod = this.GetType().GetMethod("InstantiateType");
                generator.Emit(OpCodes.Callvirt, instantiateMethod);
                EmitLogMessage(generator, "After Instantiate.");
                generator.Emit(OpCodes.Castclass, type);
                EmitLogMessage(generator, "After cast.");
                if (type.IsValueType)
                {
                    generator.Emit(OpCodes.Unbox_Any, type);
                }
                generator.Emit(OpCodes.Stloc, resultLocal);
            }

            // increment for the type byte.
            IncrementPtr(generator);

            EmitLogMessage(generator, "while( ptr < end)");
            var bottomOfLoopLabel = generator.DefineLabel();
            generator.Emit(OpCodes.Br, bottomOfLoopLabel);
            var continueLoop = generator.DefineLabel();
            generator.MarkLabel(continueLoop);

            EmitLogMessage(generator, "typeCode = *ptr");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldind_U1);
            EmitLogStack(generator, "// type code ");
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
                EmitLogMessage(generator, "if member != " + id + " // for field " + field.Name);
                generator.Emit(OpCodes.Ldloc,memberLocal);
                EmitLogStack(generator,"member value");
                generator.Emit(OpCodes.Ldc_I4_S,id);
                generator.Emit(OpCodes.Bne_Un, nextfieldLabel);

                EmitFieldDecode(generator, resultLocal, field);

                generator.Emit(OpCodes.Br, bottomOfLoopLabel);
            }
            generator.MarkLabel(nextfieldLabel);

            EmitLogMessage(generator, "while loop");
            generator.MarkLabel(bottomOfLoopLabel);
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
            if( lookupType.IsGenericType)
            {
                lookupType = lookupType.GetGenericTypeDefinition();
            }
            if (fieldEncoders.encoders.TryGetValue(lookupType, out fieldEncoder))
            {
                fieldEncoder.EmitEncode(generator, resultLocal, field, id);
            }
            else
            {
                var nullCheckLabel = generator.DefineLabel();
                if (!field.FieldType.IsValueType)
                {
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
                    generator.Emit(OpCodes.Brtrue, nullCheckLabel);
                }

                EmitLogMessage(generator, "*ptr = " + id + "; // member id");
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldc_I4_S, id);
                generator.Emit(OpCodes.Stind_I1);

                IncrementPtr(generator);

                EmitLogMessage(generator, "ptr += helper.Encode(ptr, object);");
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldarg_0);
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
                //if (field.FieldType.IsValueType)
                //{
                //    generator.Emit(OpCodes.Box, field.FieldType);
                //}
                CallEncode(generator, field.FieldType);
                generator.Emit(OpCodes.Add);
                generator.Emit(OpCodes.Stloc_0);
                generator.MarkLabel(nullCheckLabel);
            }
        }

        public void CallEncode(ILGenerator generator, Type type)
        {
            if( type.IsValueType)
            {
                CallEncodeValue(type, generator);
            }
            else
            {
                var method = GetType().GetMethod("Encode", new [] { typeof (byte).MakePointerType(), typeof (object)});
                generator.Emit(OpCodes.Call, method);
            }
        }

        private void CallEncodeValue(Type type, ILGenerator generator)
        {
            MethodInfo methodInfo = null;
            var methods = GetType().GetMethods();
            var bytePtrType = typeof (byte).MakePointerType();
            foreach (var method in methods)
            {
                var parms = method.GetParameters();
                if (method.Name == "Encode" && method.IsGenericMethod)
                {
                    var parm0 = parms[0].ParameterType;
                    if( parm0 == bytePtrType)
                    {
                        methodInfo = method;
                        break;
                    }
                }
            }
            if( methodInfo == null)
            {
                throw new ApplicationException("Encode method was not found.");
            }
            var genericEncodeMethod = methodInfo.MakeGenericMethod(type);
            generator.Emit(OpCodes.Call,genericEncodeMethod);
        }

        private void EmitFieldDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field)
        {
            var lookupType = field.FieldType;
            if (lookupType.IsEnum)
            {
                lookupType = typeof(Enum);
            }
            FieldEncoder fieldEncoder;
            if (lookupType.IsGenericType)
            {
                lookupType = lookupType.GetGenericTypeDefinition();
            }
            if (fieldEncoders.encoders.TryGetValue(lookupType, out fieldEncoder))
            {
                fieldEncoder.EmitDecode(generator, resultLocal, field);
            }
            else
            {
                //int typeCode;
                //if (!typeCodesByType.TryGetValue(lookupType, out typeCode))
                //{
                //    throw new InvalidOperationException("Can't find field or type serializer for " + field.FieldType.FullName + " on field " + field.Name + " of " + field.ReflectedType.FullName);
                //}
                EmitLogMessage(generator, "fld = helper.Decode(ptr);");
                if (resultLocal.LocalType.IsValueType)
                {
                    generator.Emit(OpCodes.Ldloca, resultLocal);
                }
                else
                {
                    generator.Emit(OpCodes.Ldloc, resultLocal);
                }
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldloc_0);
                var decodeMethod = this.GetType().GetMethod("Decode", new Type[] { typeof(byte).MakePointerType()});
                generator.Emit(OpCodes.Call, decodeMethod);
                if (field.FieldType.IsValueType)
                {
                    generator.Emit(OpCodes.Unbox_Any, field.FieldType);
                }
                generator.Emit(OpCodes.Stfld, field);

                EmitLogMessage(generator, "ptr += *ptr");
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldind_U2);
                generator.Emit(OpCodes.Conv_I);
                generator.Emit(OpCodes.Add);
                generator.Emit(OpCodes.Stloc_0);
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