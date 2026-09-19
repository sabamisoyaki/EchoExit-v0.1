using System;
using System.Collections.Generic;
using UnityEngine;

namespace Door666.Runtime
{
    /// <summary>Brings placed anomalies to life and feeds every ritual the same gaze and hit input each frame.
    /// Shared by the Game scene and the first-person stage editor.</summary>
    public sealed class AnomalyActorSet
    {
        private readonly SceneController owner;
        private readonly List<AnomalyActor> actors = new List<AnomalyActor>();

        public IReadOnlyList<AnomalyActor> Actors => actors;
        public event Action<AnomalyActor> Recognized;
        public event Action<AnomalyActor> Caught;

        public AnomalyActorSet(SceneController owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            this.owner = owner;
        }

        /// <summary>Adds ritual behaviour to a placed anomaly. Ordinary objects and unknown anomalies stay inert.</summary>
        public AnomalyActor Attach(StageObject placed)
        {
            if (placed == null || !placed.IsAnomaly) return null;
            var definition = owner.Definitions.FindByPrefab(placed.PrefabId);
            if (definition == null)
            {
                GameLog.Warning("異変", "「" + placed.PrefabId + "」は異変として置かれていますが、AnomalyDefinitions.json に定義がありません。儀式も手がかりも動きません。", placed);
                return null;
            }
            var actor = placed.gameObject.AddComponent<AnomalyActor>();
            actor.Initialize(definition, true, placed.VisualRoot, owner.Player.transform, owner.Player.View, owner.Settings.playerSpeed);
            actor.Recognized += OnRecognized;
            actor.Caught += OnCaught;
            actor.Subtitle += owner.UI.Subtitle;
            actors.Add(actor);
            GameLog.Detail("異変", actor.LogName + ": 儀式を開始（" + actor.DescribeSteps() + "）位置 " + GameLog.Position(placed.transform.position));
            return actor;
        }

        public void Remove(AnomalyActor actor) => actors.Remove(actor);

        /// <summary>Forgets every actor; their objects are destroyed with the placements they belong to.</summary>
        public void Clear()
        {
            Suspend(true);
            actors.Clear();
        }

        public void Suspend(bool suspend)
        {
            foreach (var actor in actors) if (actor != null) actor.Suspend(suspend);
        }

        /// <param name="gazeTarget">The placed object under the player's gaze ray, if any.</param>
        public void Tick(StageObject gazeTarget, bool tapped, float deltaTime)
        {
            var player = owner.Player;
            // Index loop: a handler may suspend actors, but must not add or remove them during the tick.
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null) continue;
                bool gazing = gazeTarget != null && actor.gameObject == gazeTarget.gameObject;
                actor.Tick(actor.GetPerception(player.transform.position, player.Speed, gazing, tapped && gazing), deltaTime);
            }
        }

        private void OnRecognized(AnomalyActor actor) => Recognized?.Invoke(actor);
        private void OnCaught(AnomalyActor actor) => Caught?.Invoke(actor);
    }
}
