using System;
using System.Reflection;
using System.Reflection.Emit;

namespace TickZoom.Api
{

    public class IterableFieldEncoder : FieldEncoder
    {
        protected EncodeHelper helper;
        public IterableFieldEncoder(EncodeHelper helper)
        {
            this.helper = helper;
        }

        public void EmitEncode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field, int id)
        {
            helper.LogMessage(generator, "// Iterable field encoder");
            helper.LogMessage(generator, "*ptr = " + id + "; // member id");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldc_I4_S, id);
            generator.Emit(OpCodes.Stind_I1);

            helper.IncrementPtr(generator);

            helper.LogMessage(generator, "*(int*)ptr = field.Count;");
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
            var countMethod = field.FieldType.GetMethod("get_Count");
            generator.Emit(OpCodes.Callvirt, countMethod);
            helper.LogStack(generator, "// Count");
            generator.Emit(OpCodes.Stind_I2);
            helper.IncrementPtr(generator, sizeof(short));

            helper.LogMessage(generator, "current = list.First;");
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            generator.Emit(OpCodes.Ldfld, field);
            var fieldType = field.FieldType;
            var firstMethod = fieldType.GetMethod("get_First");
            generator.Emit(OpCodes.Callvirt, firstMethod);
            var currentLocal = generator.DeclareLocal(firstMethod.ReturnType);
            generator.Emit(OpCodes.Stloc, currentLocal);

            helper.LogMessage(generator, "// Br to check for null");
            var checkNullLabel = generator.DefineLabel();
            generator.Emit(OpCodes.Br,checkNullLabel);

            var topOfLoopLabel = generator.DefineLabel();
            generator.MarkLabel(topOfLoopLabel);
            helper.LogMessage(generator, "// top of loop;");

            helper.LogMessage(generator, "item = current.Value;");
            generator.Emit(OpCodes.Ldloc, currentLocal);
            var valueMethod = firstMethod.ReturnType.GetMethod("get_Value");
            generator.Emit(OpCodes.Callvirt, valueMethod);
            var itemLocal = generator.DeclareLocal(field.FieldType.GetGenericArguments()[0]);
            generator.Emit(OpCodes.Stloc, itemLocal);

            // Polymorphically encode the individual item.
            {
                helper.LogMessage(generator, "ptr += helper.Encode(ptr, object);");
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldloc,itemLocal);
                var encodeMethod = helper .GetType().GetMethod("Encode", new Type[] { typeof(byte).MakePointerType(), typeof(object) });
                generator.Emit(OpCodes.Call, encodeMethod);
                generator.Emit(OpCodes.Add);
                generator.Emit(OpCodes.Stloc_0);
            }

            helper.LogMessage(generator, "current = current.Next;");
            generator.Emit(OpCodes.Ldloc, currentLocal);
            var nextMethod = firstMethod.ReturnType.GetMethod("get_Next");
            generator.Emit(OpCodes.Callvirt, nextMethod);
            generator.Emit(OpCodes.Stloc, currentLocal);

            generator.MarkLabel(checkNullLabel);
            helper.LogMessage(generator, "current != null;");
            generator.Emit(OpCodes.Ldloc, currentLocal);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ceq);
            generator.Emit(OpCodes.Brfalse_S,topOfLoopLabel);

            helper.LogMessage(generator, "// current was null. End of loop;");
        }

        public void EmitDecode(ILGenerator generator, LocalBuilder resultLocal, FieldInfo field)
        {
            helper.LogMessage(generator, "var count = *(short*)ptr;");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldind_I2);
            helper.LogStack(generator, "// count ");
            var countLocal = generator.DeclareLocal(typeof(short));
            generator.Emit(OpCodes.Stloc,countLocal);
            helper.IncrementPtr(generator, sizeof(short));

            var typeArgument = field.FieldType.GetGenericArguments()[0];
            var listType = typeof(ActiveList<>).MakeGenericType(typeArgument);
            helper.LogMessage(generator, "var list = new " + listType.FullName + ";");
            generator.Emit(OpCodes.Newobj, listType.GetConstructor(new Type[] {}));
            var listLocal = generator.DeclareLocal(field.FieldType);
            generator.Emit(OpCodes.Stloc, listLocal);

            helper.LogMessage(generator, "var i=0;");
            generator.Emit(OpCodes.Ldc_I4_0);
            var iLocal = generator.DeclareLocal(typeof(int));
            generator.Emit(OpCodes.Stloc, iLocal);

            helper.LogMessage(generator, "// branch to bottom of loop");
            var bottomOfLoop = generator.DefineLabel();
            generator.Emit(OpCodes.Br, bottomOfLoop);

            helper.LogMessage(generator, "// top of loop");
            var topOfLoop = generator.DefineLabel();
            generator.MarkLabel(topOfLoop);

            helper.LogMessage(generator, "list.AddLast(helper.Decode(ptr))");
            generator.Emit(OpCodes.Ldloc, listLocal);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldloc_0);
            var decodeMethod = helper.GetType().GetMethod("Decode", new Type[] { typeof(byte).MakePointerType() });
            generator.Emit(OpCodes.Call, decodeMethod);
            var addLastMethod = listType.GetMethod("AddLast", new[] { typeArgument });
            generator.Emit(OpCodes.Callvirt, addLastMethod);

            helper.LogMessage(generator, "ptr += *(short*)ptr");
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldind_U2);
            helper.LogStack(generator, "// length");
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc_0);

            helper.LogMessage(generator, "i++;");
            generator.Emit(OpCodes.Ldloc, iLocal);
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Stloc, iLocal);

            generator.MarkLabel(bottomOfLoop);

            helper.LogMessage(generator, "if( i < count) goto top of loop");
            generator.Emit(OpCodes.Ldloc, iLocal);
            generator.Emit(OpCodes.Ldloc, countLocal);
            generator.Emit(OpCodes.Clt);
            generator.Emit(OpCodes.Brtrue_S, topOfLoop);


            helper.LogMessage(generator, "result.field = list;");
            if (resultLocal.LocalType.IsValueType)
            {
                generator.Emit(OpCodes.Ldloca, resultLocal);
            }
            else
            {
                generator.Emit(OpCodes.Ldloc, resultLocal);
            }
            generator.Emit(OpCodes.Ldloc, listLocal);
            generator.Emit(OpCodes.Stfld, field);
        }
    }
}