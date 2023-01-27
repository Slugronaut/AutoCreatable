using System;
using UnityEngine;


namespace Toolbox.AutoCreate
{
    /// <summary>
    /// Any class marked with this attribute will automatically have an instance created at application startup.
    /// Furthermore, any other objects can automatically obtain a reference to that instance by marking any of
    /// their fields with [AutoResolve] and then calling AutoCreator.Resolve().
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AutoCreateAttribute : Attribute
    {
        public CreationActions ActionOnCreation;
        public RuntimeInitializeLoadType CreationTime;
        public Type[] ResolvableTypes;

        public AutoCreateAttribute(CreationActions actionOnCreation = CreationActions.None, params Type[] resolvableTypes)
        {
            ActionOnCreation = actionOnCreation;
            ResolvableTypes = resolvableTypes;
        }
    }

    /// <summary>
    /// The action to take upon instatiating this object via the AutoCreator.
    /// </summary>
    public enum CreationActions
    {
        None,
        DeserializeSingletonData,
    }
}
