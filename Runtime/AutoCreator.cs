using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Toolbox.AutoCreate
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
        static public Dictionary<Type, object> _AutoCreatables;
        static public IEnumerable<object> AutoCreatedObjects
        {
            get
            {
                if (_AutoCreatables == null)
                    Initialize();
                return _AutoCreatables.Values;
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
            _AutoCreatables = new();
            foreach (var kvp in InstantiateAllAutoCreateables())
            {
                if(!_AutoCreatables.TryAdd(kvp.Key, kvp.Value))
                    throw new Exception($"The alias type '{kvp.Key.Name}' cannot be resolved to the concrete instance of '{kvp.Value.GetType().Name}' because it is already associated with and instance of '{_AutoCreatables[kvp.Key].GetType().Name}'.");
                
            }
        }

        /// <summary>
        /// Resets internal state. Mostly just used for unit testing.
        /// </summary>
        public static void Reset()
        {
            Application.quitting -= HandleAppExit;
            var list = AutoCreatedObjects.ToList();
            foreach (var obj in list)
            {
                var type = obj.GetType();
                var method = type.GetMethod("AutoDestroy", AutoInvokedMethodBindFlags);
                method?.Invoke(obj, null);
            }
            _AutoCreatables = new();
            Initialized = false;
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

                    foreach(var subType in attr.ResolvableTypes)
                    {
                        if(subType.IsInterface && TypeHelper.ImplementsInterface(type, subType))
                            yield return new KeyValuePair<Type, object>(subType, inst);
                        else if(TypeHelper.IsSameOrSubclass(subType, type))
                            yield return new KeyValuePair<Type, object>(subType, inst);
                        else Debug.LogWarning($"The object of type '{type.Name}' cannot be auto-resolved to the type '{subType.Name}'.");
                    }

                    yield return new KeyValuePair<Type, object>(type, inst);
                }
            }

            var list = AutoCreatedObjects.ToList();
            //loop through all instantiated objects and invoke a magic 'AutoAwake()' method via reflection.
            //This is needed so that we have something that can be invoked after ALL autocreatables have been created.
            foreach (var obj in list)
            {
                var type = obj.GetType();

                Debug.Log($"Seeking AutoAwake for {type.Name}");
                var method = type.GetMethod("AutoAwake", AutoInvokedMethodBindFlags);

                if (method != null)
                    Debug.Log($"Found Auto awake method for {type.Name}");

                method?.Invoke(obj, null);
            }
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

            var obj = Sirenix.Serialization.SerializationUtility.DeserializeValue<object>(bytesAsset.bytes, Sirenix.Serialization.DataFormat.Binary, refsAsset != null ? refsAsset.SerializedRefs : null);
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
                    if (_AutoCreatables != null && _AutoCreatables.TryGetValue(field.FieldType, out object inst))
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
            _AutoCreatables.TryGetValue(type, out object inst);
            return inst;
        }

        /// <summary>
        /// Returns the singleton instance of a given autocreated type.
        /// </summary>
        /// <param name="type"></param>
        public static T AsSingleton<T>()
        {
            _AutoCreatables.TryGetValue(typeof(T), out object inst);
            return (T)inst;
        }

    }
}
