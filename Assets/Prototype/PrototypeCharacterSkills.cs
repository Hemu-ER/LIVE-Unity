using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype
{
    [Serializable] public sealed class PrototypeCharacterSkillEntry
    {
        public string Id, DisplayName;
        public bool Implemented;
        public PrototypeSkillDefinition Definition;
    }
    [Serializable] public sealed class PrototypeCharacterSkillBinding
    { public string CharacterId, ActiveId, PassiveId; }

    [Serializable] public sealed class PrototypeCharacterSkills
    {
        public string SourceCommit;
        public PrototypeCharacterSkillEntry[] Definitions;
        public PrototypeCharacterSkillBinding[] Characters;
        private Dictionary<string, PrototypeCharacterSkillEntry> lookup;
        public static PrototypeCharacterSkills Load()
        {
            var text = Resources.Load<TextAsset>("CharacterSkills");
            if (text == null) throw new InvalidOperationException("Missing CharacterSkills.json");
            var data = JsonUtility.FromJson<PrototypeCharacterSkills>(text.text);
            data.Validate(); return data;
        }
        public void Validate()
        {
            lookup = new Dictionary<string, PrototypeCharacterSkillEntry>(StringComparer.Ordinal);
            if (Definitions == null || Characters == null) throw new InvalidOperationException("Incomplete character skills");
            foreach (var entry in Definitions)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !lookup.TryAdd(entry.Id, entry))
                    throw new InvalidOperationException("Duplicate/missing skill definition ID");
                if (entry.Implemented && (entry.Definition == null || entry.Definition.Id != entry.Id))
                    throw new InvalidOperationException("Implemented skill requires matching executable definition");
                if (!entry.Implemented && entry.Definition != null && (!string.IsNullOrEmpty(entry.Definition.Id) || (entry.Definition.Effects != null && entry.Definition.Effects.Length > 0)))
                    throw new InvalidOperationException("Pending skill must not contain guessed executable effects");
                if (!entry.Implemented) entry.Definition = null;
                else PrototypeGameData.ValidateMechanisms(entry.Definition);
            }
            var characters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in Characters)
                if (binding == null || !characters.Add(binding.CharacterId) || !lookup.ContainsKey(binding.ActiveId) || !lookup.ContainsKey(binding.PassiveId))
                    throw new InvalidOperationException("Invalid character skill references");
        }
        public PrototypeCharacterSkillEntry Definition(string id) => lookup[id];
        public void Attach(PrototypeUnitDefinition unit)
        {
            var binding = Array.Find(Characters, character => character.CharacterId == unit.Id);
            if (binding == null) throw new InvalidOperationException("Missing character skill binding: " + unit.Id);
            unit.ActiveDefinitionReference = binding.ActiveId; unit.PassiveDefinitionReference = binding.PassiveId;
            var executable = new List<PrototypeSkillDefinition>();
            // Passives first: combat-start modifiers are applied before basic attacks and periodic actives.
            foreach (var id in new[] { binding.PassiveId, binding.ActiveId })
                if (lookup[id].Implemented) executable.Add(lookup[id].Definition);
            unit.Abilities.Skills = executable.ToArray();
            unit.UnityImplementationStatus = executable.Count == 0 ? "BasicCombatOnly" : "SkillsImplemented";
        }
    }
}
