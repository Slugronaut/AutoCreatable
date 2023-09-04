using System;

namespace Peg.AutoCreate
{
    /// <summary>
    /// Attach to any field that should attempt to resolve assingment via the AutoCreator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AutoResolveAttribute : Attribute { }
}
