using System.Reflection;
using AeroTech.Framework.Core.Domain.Extensions;

namespace AeroTech.Framework.Core.Domain.Queries;

public static class MetadataGenerator
{
    private static readonly Dictionary<string, ICollection<GridMetadataField>> PropertiesDictionary = new();
    private static readonly object Locker = new();

    private static ICollection<GridMetadataField> GetFields(Type type)
    {
        var key = type.FullName!;
        if (PropertiesDictionary.TryGetValue(key, out var fields)) return fields;
        lock (Locker)
        {
            if (PropertiesDictionary.TryGetValue(key, out var fields1)) return fields1;

            PropertiesDictionary.Add(key,
                type.GetProperties()
                    .Where(x => x.GetCustomAttribute<GridAttribute>() != null)
                    .Select(x =>
                        new GridMetadataField
                        {
                            Name = x.Name.ToCamelCase(),
                            Title = x.GetDisplayName(),
                            IsSortable = x.GetSortable(),
                        })
                    .ToList()
            );
        }
        return PropertiesDictionary[key];
    }

    public static GridMetadata Generate<T>()
    {
        return new GridMetadata
        {
            Fields = GetFields(typeof(T))
        };
    }

    private static string GetDisplayName(this PropertyInfo property)
    {
        var attr = property.GetCustomAttribute<GridAttribute>();
        return attr!.Title;
    }

    private static bool GetSortable(this PropertyInfo property)
    {
        var attr = property.GetCustomAttribute<GridAttribute>();
        return attr!.IsSortable;
    }
}
