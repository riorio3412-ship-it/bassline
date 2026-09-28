using System;
using UnityEngine;

namespace BASSLINE.AuthoringData
{
    [Serializable] public sealed class AuthoringField { public string Key,Value; }
    // Lossless authoring data. This is not a completed gameplay Character/Screen service.
    public sealed class ImportedDefinition : ScriptableObject
    {
        public string StableId;
        public string RecordKind;
        public string SourceFile;
        public string SourceHash;
        public string DecisionStatus;
        public string AssetStatus="AuthoringProxy";
        public AuthoringField[] Fields;
        public string Read(string key)
        {
            foreach(var field in Fields) if(field.Key==key)return field.Value;
            throw new ArgumentException("Missing authoring field: "+key);
        }
    }
}
