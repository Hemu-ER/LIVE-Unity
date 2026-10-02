using System;
using UnityEngine;

namespace LIVE.Prototype
{
    // Optional future SD mapping. No downloads, automatic imports or placeholder replacement.
    [CreateAssetMenu(menuName = "LIVE/Character Sprite References")]
    public sealed class PrototypeCharacterAssets : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string AssetReferenceId; public Sprite Sprite; }
        public Entry[] Entries = Array.Empty<Entry>();
        public Sprite Resolve(PrototypeUnitDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.AssetReferenceId)) return null;
            foreach (var entry in Entries)
                if (entry != null && entry.AssetReferenceId == definition.AssetReferenceId) return entry.Sprite;
            return null;
        }
    }
}
