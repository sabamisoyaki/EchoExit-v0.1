using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Door666.Core
{
    [Serializable]
    public sealed class AnomalyDefinition
    {
        public string anomalyId;
        public string prefabId;
        public string displayName;
        public string behaviorClass;
        public int dangerLevel;
        public RitualStep[] ritualSteps = Array.Empty<RitualStep>();
        public AnomalyEffectDefinition recognitionEffect = new AnomalyEffectDefinition();
        public AnomalyClueDefinition clue = new AnomalyClueDefinition();
        public ThreatProfile threatProfile = new ThreatProfile();
        public AnomalyPlacementRules placementRules = new AnomalyPlacementRules();
        public string endingId;
        public bool userStageAllowed = true;
        public bool IsThreat => threatProfile != null && threatProfile.enabled;
    }

    [Serializable]
    public sealed class AnomalyEffectDefinition
    {
        public string kind;
        public float duration = 1;
        public float scale = 1;
        public float restoreDistance;
        public float returnDelay;
        public string sound;
        public string subtitle;
    }

    [Serializable]
    public sealed class AnomalyClueDefinition
    {
        public string kind;
        public string sound;
        public string subtitle;
        public float interval = 7;
        public float distance = 4;
        public float volume = 0.15f;
    }

    [Serializable]
    public sealed class AnomalyPlacementRules
    {
        public bool automaticActivation;
        public float activationRadius;
        public float minimumSpawnDistance = 2.5f;
        public float minimumDoorDistance = 2;
        public float clearanceRadius = 1;
        public float retreatDistance;
    }

    public sealed class AnomalyCatalog
    {
        [Serializable]
        private sealed class Document { public AnomalyDefinition[] anomalies; }

        private readonly Dictionary<string, AnomalyDefinition> byPrefab = new Dictionary<string, AnomalyDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, AnomalyDefinition> byId = new Dictionary<string, AnomalyDefinition>(StringComparer.Ordinal);
        public IReadOnlyList<AnomalyDefinition> Definitions { get; }

        public AnomalyCatalog(IEnumerable<AnomalyDefinition> definitions)
        {
            var list = new List<AnomalyDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.prefabId) || string.IsNullOrWhiteSpace(definition.anomalyId))
                    throw new ArgumentException("異変の ID が空です。");
                if (byPrefab.ContainsKey(definition.prefabId) || byId.ContainsKey(definition.anomalyId))
                    throw new ArgumentException("異変の ID が重複しています: " + definition.anomalyId);
                if (definition.ritualSteps == null || definition.ritualSteps.Length == 0)
                    throw new ArgumentException("儀式が定義されていません: " + definition.anomalyId);
                foreach (var step in definition.ritualSteps) RitualRuntime.Validate(step, definition.ritualSteps.Length);
                byPrefab.Add(definition.prefabId, definition);
                byId.Add(definition.anomalyId, definition);
                list.Add(definition);
            }
            Definitions = list.AsReadOnly();
        }

        public static AnomalyCatalog FromJson(string json)
        {
            var document = JsonConvert.DeserializeObject<Document>(json);
            if (document == null || document.anomalies == null) throw new ArgumentException("異変定義 JSON が空です。");
            return new AnomalyCatalog(document.anomalies);
        }

        public AnomalyDefinition FindByPrefab(string prefabId) => prefabId != null && byPrefab.TryGetValue(prefabId, out var value) ? value : null;
        public AnomalyDefinition FindById(string anomalyId) => anomalyId != null && byId.TryGetValue(anomalyId, out var value) ? value : null;
    }
}
