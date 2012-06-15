using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace TickZoom.Api
{
    public static class CloneHelper
    {
        internal static Dictionary<Type, Delegate> _cachedILDeep = new Dictionary<Type, Delegate>();

        #region Private Methods

        private static readonly Type stringType = typeof(string);
        private static readonly Type delegateType = typeof(Delegate);

        public static object Clone(object original)
        {
            var type = original.GetType();
            if( type.IsValueType || type == stringType || type.IsSubclassOf(delegateType))
            {
                return original;
            }
            var cloner = GetDelegate(type);
            return cloner.DynamicInvoke(original);
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
        public static Delegate GetDelegate(Type type)
        {
            Delegate myExec = null;
            if (!_cachedILDeep.TryGetValue(type, out myExec))
            {
                var dynMethod = CreateDynamicMethod(type);
                var genericFunc = typeof(Func<,>).MakeGenericType(type, type);
                myExec = dynMethod.CreateDelegate(genericFunc);
                _cachedILDeep.Add(type, myExec);
            }
            return myExec;
        }

        private static DynamicMethod CreateDynamicMethod(Type type)
        {
            // Create ILGenerator            
            var dymMethod = new DynamicMethod("DoDeepClone", type, new Type[] { type }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator generator = dymMethod.GetILGenerator();
            var cloneLocal = generator.DeclareLocal(type);

            UninitializeObject(generator, type);
            generator.Emit(OpCodes.Stloc, cloneLocal);

            foreach (FieldInfo field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
            {
                generator.Emit(OpCodes.Ldloc, cloneLocal);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldfld, field);
                if (!field.FieldType.IsValueType && field.FieldType != typeof(string))
                {
                    var dyn = CreateDynamicMethod(field.FieldType);
                    generator.Emit(OpCodes.Call, dyn);
                }
                generator.Emit(OpCodes.Stfld, field);
            }
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ret);
            return dymMethod;
        }

        #endregion
    }
}