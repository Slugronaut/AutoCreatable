#define AUTOCREATOR_PREINITTYPES
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Peg.AutoCreate
{
    /// <summary>
    /// At application startup, automatically scans all assemblies for types
    /// with the [AutoCreate] attribute and instantiates a copy of them if found.
    /// an internal reference to these auto-created objects is kep around for the
    /// lifetime of the application so that GC doesn't collect any.
    /// </summary>
    public static class AutoCreator
    {
        static readonly public BindingFlags AutoInvokedMethodBindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance;
        static public readonly string SerialBytesExtension = ".bytes";
        static readonly string SerializedFilesPath = "Serialization/Singletons";
        static bool Initialized;
        static public Dictionary<Type, object> _AutoCreatedObjects;
        static public IEnumerable<object> AutoCreatedObjects
        {
            get
            {
                if (_AutoCreatedObjects == null)
                    Initialize();
                return _AutoCreatedObjects.Values;
            }
        }


        /// <summary>
        /// Runs automatically at application startup. Should only be called manually when performing Unit Tests.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Initialize()
        {
            if (Initialized) return;
            Initialized = true;

            Application.quitting += HandleAppExit;
            _AutoCreatedObjects = new();
            foreach (var kvp in InstantiateAllAutoCreateables())
            {
                if(!_AutoCreatedObjects.TryAdd(kvp.Key, kvp.Value))
                    throw new Exception($"The alias type '{kvp.Key.Name}' cannot be resolved to the concrete instance of '{kvp.Value.GetType().Name}' because it is already associated with and instance of '{_AutoCreatedObjects[kvp.Key].GetType().Name}'.");
                
            }
        }

        /// <summary>
        /// Resets internal state. Mostly just used for unit testing.
        /// </summary>
        public static void Reset()
        {
            Application.quitting -= HandleAppExit;
            var list = AutoCreatedObjects.ToList();
            InvokeMethodOnAutoCreatables(list, "AutoDestroy");
            _AutoCreatedObjects = new();
            Initialized = false;
        }

        /// <summary>
        /// Helper method for invoking a method on a given list of objects.
        /// If the method is not found nothing happens. If an object in the
        /// list is also in the Aliased list then it is skipped.
        /// </summary>
        /// <param name="list"></param>
        public static void InvokeMethodOnAutoCreatables(List<object> list, string methodName)
        {
            foreach (var obj in list.Distinct())
            {
                //skip aliased types or we'll invoke multiple times
                var type = obj.GetType();
                var method = type.GetMethod(methodName, AutoInvokedMethodBindFlags);
                method?.Invoke(obj, null);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        static void HandleAppExit()
        {
            Reset();
        }

        /// <summary>
        /// Returns all types in all assemblies that 
        /// </summary>
        /// <returns></returns>
        public static Type[] FindAllAutoCreatableTypes()
        {
            return TypeHelper.GetTypesWithAttribute(typeof(AutoCreateAttribute));
        }

        /// <summary>
        /// Helper method that automatically instantiates all types with the [AutoCreate] attribute.
        /// </summary>
        /// <returns>A list containing all instances created.</returns>
        public static IEnumerable<KeyValuePair<Type, object>> InstantiateAllAutoCreateables()
        {
            var types = FindAllAutoCreatableTypes();
            foreach (var type in types)
            {
                if (!type.IsAbstract)
                {
                    object inst;
                    var attr = type.GetCustomAttribute<AutoCreateAttribute>();

                    if (attr.ActionOnCreation == CreationActions.None)
                        inst = Activator.CreateInstance(type);
                    else inst = DeserializeSingleton(BuildAssetPath(type));
                    if (inst == null) continue;

                    //Here we are generating aliases for each type based on what was passed into their AutoCreate attribute.
                    //This allows us to autoresolve covarient types.
                    foreach(var subType in attr.ResolvableTypes)
                    {
                        var kvp = new KeyValuePair<Type, object>(subType, inst);
                        if (subType.IsInterface && TypeHelper.ImplementsInterface(type, subType))
                            yield return kvp;
                        else if (TypeHelper.IsSameOrSubclass(subType, type))
                            yield return kvp;
                        else Debug.LogWarning($"The object of type '{type.Name}' cannot be auto-resolved to the type '{subType.Name}'.");
                    }

                    //return this type itself after we've iterated over the alias types in the attribute
                    yield return new KeyValuePair<Type, object>(type, inst);
                }
            }

#if AUTOCREATOR_PREINITTYPES
            //TODO: We have a BIG bug here. Invokes can happen multiple times if a class is registered under several different types.

            //var list = AutoCreatedObjects.ToList();
            var list = _AutoCreatedObjects.ToList();
            //loop through all instantiated objects and invoke a magic 'AutoAwake()' method via reflection.
            //This is needed so that we have something that can be invoked after ALL autocreatables have been created.
            InvokeMethodOnAutoCreatables(list.Select(x => x.Value).ToList(), "AutoAwake");

            //a second loop is needed so that if any autocreated types need to access *other* autocreated types
            //during their startup, there is a way to ensure that all of them have had AutoAwake invoked already.
            //Especially useful for singletons where we may be setting the Instance static value during AutoAwake
            //but access it during startup in another autocreated type. welcome to the hell that is the singleton life :p
            InvokeMethodOnAutoCreatables(list.Select(x => x.Value).ToList(), "AutoStart");
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pathType"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        static string BuildAssetPath(Type type)
        {
            return $"{SerializedFilesPath}/{type.Name}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourcePath"></param>
        /// <returns></returns>
        public static object DeserializeSingleton(string assetPath)
        {
            var refsAsset = Resources.Load<SingletonScriptableReferences>(assetPath); //.asset file
            var bytesAsset = Resources.Load<TextAsset>(assetPath);//.bytes file
            if (bytesAsset == null)
                return null;

            if (bytesAsset.bytes == null)
            {
                Debug.LogError($"Null byte stream in Singleton Deserializer while loading {assetPath}.");
                return null;
            }
            if (refsAsset == null)
                Debug.LogWarning($"Missing objects ref asset. UnityEngine. Objects will not be deserialized and linked properly while loading {assetPath}.");

            var obj = SerializationUtility.DeserializeValue<object>(bytesAsset.bytes, DataFormat.Binary, refsAsset != null ? refsAsset.SerializedRefs : null);
            Resources.UnloadAsset(refsAsset);
            return obj;
        }

        /// <summary>
        /// Automatically resolves assingement of any fields in the src object that have been
        /// marked with the [AutoResolve] attribute. If the field is a type marked with [AutoCreate]
        /// then it will be assigned the value of that type that was created.
        /// </summary>
        /// <param name="src"></param>
        public static void Resolve(object src)
        {
            var srcType = src.GetType();
            var fields = srcType.GetFields(System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.FlattenHierarchy |
                System.Reflection.BindingFlags.Static);

            foreach(var field in fields)
            {
                if(field.CustomAttributes.Any(x => x.AttributeType == typeof(AutoResolveAttribute)))
                {
                    if (_AutoCreatedObjects != null && _AutoCreatedObjects.TryGetValue(field.FieldType, out object inst))
                        field.SetValue(src, inst);
                    else Debug.LogWarning($"Could not autoresolve the field '{field.DeclaringType.Name}.{field.Name}' of the type '{field.FieldType}'.");
                }
            }
        }

        /// <summary>
        /// Returns the singleton instance of a given autocreated type.
        /// </summary>
        /// <param name="type"></param>
        public static object AsSingleton(Type type)
        {
            _AutoCreatedObjects.TryGetValue(type, out object inst);
            return inst;
        }

        /// <summary>
        /// Returns the singleton instance of a given autocreated type.
        /// </summary>
        /// <param name="type"></param>
        public static T AsSingleton<T>()
        {
            _AutoCreatedObjects.TryGetValue(typeof(T), out object inst);
            return (T)inst;
        }

    }
}
