using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Resources;

namespace AeroTech.Framework.Core.Domain.Extensions
{
    public static class EnumTools
    {
        public static bool IsNotValid<TEnum>(this TEnum enumValue)
        {
            return !Enum.IsDefined(typeof(TEnum), enumValue);
        }

        public static string GetDisplayName(this Enum enumValue) => enumValue.GetDisplayName(null);

        public static string GetDisplayName(this Enum enumValue, Type? resourceType)
        {
            try
            {
                if (enumValue == null)
                    return null!;
                var item = enumValue.GetType().GetMember(enumValue.ToString())
                    .First()
                    .GetCustomAttribute<DisplayAttribute>();
                resourceType ??= item!.ResourceType;
                if (resourceType != null)
                {
                    try
                    {
                        var myManager = new ResourceManager(resourceType);

                        var temp = myManager.GetString(item!.Name!);
                        if (!string.IsNullOrEmpty(temp))
                        {
                            return temp;
                        }
                    }
                    catch { }
                }
                return item!.Name!;
            }
            catch (Exception)
            {
                return enumValue.ToString();
            }
        }

        public static string GetDisplayDescription(this Enum enumValue, Type? resourceType = null)
        {
            string desc = "";
            try
            {
                var item = enumValue.GetType().GetMember(enumValue.ToString())
                    .First()
                    .GetCustomAttribute<DisplayAttribute>();
                resourceType ??= item!.ResourceType;
                if (resourceType != null)
                {
                    try
                    {
                        var myManager = new ResourceManager(resourceType);
                        var temp = myManager.GetString(item!.Description!);
                        if (!string.IsNullOrEmpty(temp))
                        {
                            desc = temp;
                        }
                    }
                    catch { }
                }
                else
                {
                    desc = item!.Description!;
                }
            }
            catch (Exception)
            {
                desc = string.Empty;
            }
            if (string.IsNullOrEmpty(desc))
                return enumValue.GetDisplayName();
            return desc;
        }
    }
}
