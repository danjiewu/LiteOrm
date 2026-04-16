using System.Reflection;
using System.Reflection.Emit;
using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.WebDemo.Controllers;

public static class DynamicControllerBuilder
{
    public static Assembly BuildDynamicControllers(string defaultNamespace)
    {
        var assemblyName = new AssemblyName("DynamicControllers");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

        foreach (var definition in DynamicQueryMetadata.GetEntities(defaultNamespace))
        {
            var entityType = typeof(DemoUser).Assembly.GetType($"{typeof(DemoUser).Namespace}.{definition.EntityName}")!;
            var viewType = typeof(DemoUser).Assembly.GetType($"{typeof(DemoUser).Namespace}.{definition.ViewName}") ?? entityType;

            var controllerName = $"{entityType.Name}Controller";
            var existingController = Type.GetType($"{defaultNamespace}.Controllers.{controllerName}");
            if (existingController != null)
                continue;

            var parentType = typeof(EntityControllerBase<,>).MakeGenericType(entityType, viewType);
            var typeBuilder = moduleBuilder.DefineType(
                $"{defaultNamespace}.Controllers.{controllerName}",
                TypeAttributes.Public, parentType);

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public, CallingConventions.Standard,
                new[] { typeof(IEntityServiceAsync<>).MakeGenericType(entityType),
                        typeof(IEntityViewServiceAsync<>).MakeGenericType(viewType) });

            var il = ctorBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, parentType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, new[]
            {
                typeof(IEntityServiceAsync<>).MakeGenericType(entityType),
                typeof(IEntityViewServiceAsync<>).MakeGenericType(viewType)
            }));
            il.Emit(OpCodes.Ret);

            typeBuilder.CreateType();
        }

        return assemblyBuilder;
    }
}
