using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace LiteOrm.SqlToExpr;

internal sealed class RuntimeTypeFactory
{
    public Dictionary<string, Type> CreateTableTypes(DatabaseSchema schema)
    {
        var assemblyName = new AssemblyName("LiteOrm.SqlToExpr.Runtime." + Guid.NewGuid().ToString("N"));
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name!);
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in schema.Tables)
        {
            var typeBuilder = moduleBuilder.DefineType(
                $"LiteOrm.SqlToExpr.Runtime.{table.ClassName}_{Guid.NewGuid():N}",
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(ObjectBase));
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            ApplyAttribute(typeBuilder, typeof(TableAttribute).GetConstructor([typeof(string)])!, [table.Name]);

            foreach (var column in table.Columns)
            {
                DefineProperty(typeBuilder, column);
            }

            result[table.Name] = typeBuilder.CreateTypeInfo()!.AsType();
        }

        return result;
    }

    private static void DefineProperty(TypeBuilder typeBuilder, ColumnSchema column)
    {
        var propertyType = NormalizePropertyType(column.ClrType);
        var fieldBuilder = typeBuilder.DefineField($"_{column.PropertyName}", propertyType, FieldAttributes.Private);
        var propertyBuilder = typeBuilder.DefineProperty(column.PropertyName, PropertyAttributes.None, propertyType, null);

        var getter = typeBuilder.DefineMethod(
            $"get_{column.PropertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            propertyType,
            Type.EmptyTypes);
        var getterIl = getter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, fieldBuilder);
        getterIl.Emit(OpCodes.Ret);

        var setter = typeBuilder.DefineMethod(
            $"set_{column.PropertyName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null,
            [propertyType]);
        var setterIl = setter.GetILGenerator();
        setterIl.Emit(OpCodes.Ldarg_0);
        setterIl.Emit(OpCodes.Ldarg_1);
        setterIl.Emit(OpCodes.Stfld, fieldBuilder);
        setterIl.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getter);
        propertyBuilder.SetSetMethod(setter);

        var ctorInfo = typeof(ColumnAttribute).GetConstructor([typeof(string)])!;
        var columnModeProperty = typeof(ColumnAttribute).GetProperty(nameof(ColumnAttribute.ColumnMode))!;
        var columnMode = column.IsInSelect
            ? (column.IsPrimaryKey ? ColumnMode.Full : ColumnMode.Read)
            : ColumnMode.Write;
        var columnAttributeBuilder = new CustomAttributeBuilder(
            ctorInfo,
            [column.Name],
            [columnModeProperty],
            [columnMode]);
        propertyBuilder.SetCustomAttribute(columnAttributeBuilder);

        if (column.IsInSelect && column.SelectOrdinal >= 0)
        {
            var orderCtor = typeof(PropertyOrderAttribute).GetConstructor([typeof(int)])!;
            var orderBuilder = new CustomAttributeBuilder(orderCtor, [(object)column.SelectOrdinal]);
            propertyBuilder.SetCustomAttribute(orderBuilder);
        }
    }

    private static Type NormalizePropertyType(Type type)
    {
        return type == typeof(void) ? typeof(object) : type;
    }

    private static void ApplyAttribute(dynamic target, ConstructorInfo constructor, object[] arguments)
    {
        var attributeBuilder = new CustomAttributeBuilder(constructor, arguments);
        target.SetCustomAttribute(attributeBuilder);
    }
}