using System.Collections;
using System.Reflection;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class ReflectionDataExtractor
{
    public static object? TryReadValue(object? source, params string[] memberNames)
    {
        if (source == null)
            return null;

        foreach (string memberName in memberNames)
        {
            object? current = source;
            foreach (string part in memberName.Split('.'))
            {
                current = TryReadDirectValue(current, part);
                if (current == null)
                    break;
            }

            if (current != null)
                return current;
        }

        return null;
    }

    private static object? TryReadDirectValue(object? source, string memberName)
    {
        if (source == null)
            return null;

        if (int.TryParse(memberName, out int index) && source is IEnumerable enumerable and not string)
        {
            int currentIndex = 0;
            foreach (object? item in enumerable)
            {
                if (currentIndex == index)
                    return item;
                currentIndex++;
            }

            return null;
        }

        Type type = source.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo? property = type.GetProperty(memberName, flags);
        if (property?.GetIndexParameters().Length == 0)
        {
            try { return property.GetValue(source); }
            catch { return null; }
        }

        FieldInfo? field = type.GetField(memberName, flags);
        if (field != null)
        {
            try { return field.GetValue(source); }
            catch { return null; }
        }

        return null;
    }
}
