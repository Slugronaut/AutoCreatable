# AutoCreatable
An attribute-based way to automatically instantiate singleton classes at application startup and automatically resolve references to them.

Objects marked with the AutoCreate attribute can also pass a parameter to mark it for serialization and deserialization. This allows the object to show up in the menu under the Editor->Singletons entry. All values set in this window will be serialized and stuffed into a resources folder. At application starup, those settings will be deserialized and injected into the auto created instance.

Due to the demands of this serialization system Odin is currently required. However, I will likely add conditional compilation checks in the future to simply disable this feature if Odin is not present.

Dependenices:
  -[Odin](https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041)
  -[com.postegames.typehelper](https://github.com/Slugronaut/Toolbox-TypeHelper)  
  -[com.postegames.unityextensions](https://github.com/Slugronaut/Toolbox-UnityExtensions)  
  -[com.postegames.collections](https://github.com/Slugronaut/Toolbox-Collections)  
