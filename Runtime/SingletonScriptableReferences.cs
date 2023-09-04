using System.Collections.Generic;
using UnityEngine;

namespace Peg.AutoCreate
{
    /// <summary>
    /// Used as a means of serializing UnityEngine.Object references for singletons.
    /// </summary>
    public class SingletonScriptableReferences : ScriptableObject
    {
        public List<UnityEngine.Object> SerializedRefs = new();
    }
}
