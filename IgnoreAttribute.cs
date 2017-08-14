namespace Flash
{
    using System;

    /// <summary>
    /// When added to a property, the property will not be used when binding to or from a DB call.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// When added to a property, the property will not be used when converting to a datatable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreSetAttribute : Attribute
    {
    }
}
