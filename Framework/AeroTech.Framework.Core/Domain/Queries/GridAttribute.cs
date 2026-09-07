namespace AeroTech.Framework.Core.Domain.Queries;

public class GridAttribute(string title, bool isSortable = true) : Attribute
{
    public string Title { get; } = title;
    public bool IsSortable { get; } = isSortable;
}
