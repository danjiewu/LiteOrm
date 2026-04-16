using System.ComponentModel;
using System.Reflection;
using LiteOrm.Common;
using LiteOrm.WebDemo.Models;

namespace LiteOrm.WebDemo.Infrastructure;

public static class DynamicQueryMetadata
{
    public static IReadOnlyList<DynamicQueryEntityMetadata> GetEntities(string defaultNamespace)
    {
        return typeof(DemoUser).Assembly.GetTypes()
            .Where(IsDynamicQueryEntity)
            .Select(entityType => CreateMetadata(entityType, defaultNamespace))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsDynamicQueryEntity(Type entityType) =>
        entityType.IsSubclassOf(typeof(ObjectBase)) &&
        !entityType.IsAbstract &&
        !entityType.Name.EndsWith("View", StringComparison.Ordinal) &&
        entityType.GetConstructor(Type.EmptyTypes) is not null &&
        entityType.GetCustomAttribute<DisplayNameAttribute>() is not null;

    private static DynamicQueryEntityMetadata CreateMetadata(Type entityType, string defaultNamespace)
    {
        var viewType = ResolveViewType(entityType);
        var displayName = GetDisplayName(entityType);
        var routeSegment = entityType.Name;

        var fields = viewType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(CreateField)
            .Where(field => field is not null)
            .Cast<DynamicQueryFieldMetadata>()
            .ToArray();

        return new DynamicQueryEntityMetadata(
            entityType.Name,
            viewType.Name,
            displayName,
            $"{defaultNamespace}.Controllers.{routeSegment}Controller",
            routeSegment,
            $"/api/{routeSegment}/PageQuery",
            fields);
    }

    public static Type ResolveViewType(Type entityType)
    {
        var viewType = typeof(DemoUser).Assembly.GetType(entityType.FullName + "View");
        return viewType != null && viewType.IsSubclassOf(entityType) ? viewType : entityType;
    }

    private static DynamicQueryFieldMetadata? CreateField(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var kind = GetFieldKind(type);
        if (kind is null)
        {
            return null;
        }

        return new DynamicQueryFieldMetadata(property.Name, GetDisplayName(property), kind);
    }

    private static string? GetFieldKind(Type type)
    {
        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(DateTime))
        {
            return "date";
        }

        if (type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(float) ||
            type == typeof(double) ||
            type == typeof(decimal))
        {
            return "number";
        }

        return null;
    }

    private static string GetDisplayName(MemberInfo member) =>
        member.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? member.Name;
}

public sealed record DynamicQueryEntityMetadata(
    string EntityName,
    string ViewName,
    string DisplayName,
    string ControllerName,
    string RouteSegment,
    string PageQueryUrl,
    IReadOnlyList<DynamicQueryFieldMetadata> Fields);

public sealed record DynamicQueryFieldMetadata(
    string Name,
    string Label,
    string Kind);
