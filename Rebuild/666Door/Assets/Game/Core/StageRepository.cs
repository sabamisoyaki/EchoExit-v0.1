using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Door666.Core
{
    public sealed class StageLoadResult
    {
        public StageFile Data { get; internal set; } = new StageFile();
        public List<string> Warnings { get; } = new List<string>();
        public string Error { get; internal set; }
        public bool IsReadOnly { get; internal set; }
        public bool Success => string.IsNullOrEmpty(Error);
        public bool CreatedDefaults { get; internal set; }
    }

    public sealed class StageSaveResult
    {
        public bool Success { get; internal set; }
        public string Error { get; internal set; }
    }

    /// <summary>JSON compatibility and durable disk writes, with no Unity or scene dependencies.</summary>
    public sealed class StageRepository
    {
        public const string SaveFileName = "anomalies.json";
        public const string SaveDirectoryName = "Saves";
        public string SavePath { get; }
        public StageFile Data { get; private set; } = new StageFile();
        public bool IsReadOnly { get; private set; }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            MaxDepth = 64
        };

        public StageRepository(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath)) throw new ArgumentException("保存先が必要です。", nameof(persistentDataPath));
            SavePath = Path.Combine(persistentDataPath, SaveDirectoryName, SaveFileName);
        }

        public StageLoadResult LoadOrCreate(string bundledJson)
        {
            try
            {
                string existing = File.Exists(SavePath) ? File.ReadAllText(SavePath, Encoding.UTF8) : null;
                bool needsDefaults = string.IsNullOrWhiteSpace(existing);
                var result = Parse(needsDefaults ? bundledJson : existing);
                if (!result.Success)
                {
                    // Make the bundled rooms playable without destroying the user's damaged file.
                    var defaults = Parse(bundledJson);
                    if (defaults.Success) result.Data = defaults.Data;
                    result.IsReadOnly = true;
                    IsReadOnly = true;
                    Data = result.Data.Clone();
                    return result;
                }

                IsReadOnly = false;
                Data = result.Data.Clone();
                if (needsDefaults)
                {
                    var save = Save(Data);
                    result.CreatedDefaults = save.Success;
                    if (!save.Success) { result.Error = save.Error; result.IsReadOnly = true; IsReadOnly = true; }
                }
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                var fallback = Parse(bundledJson);
                fallback.Error = "保存データを読み込めません: " + exception.Message;
                fallback.IsReadOnly = true;
                IsReadOnly = true;
                Data = fallback.Data.Clone();
                return fallback;
            }
        }

        public static StageLoadResult Parse(string json)
        {
            var result = new StageLoadResult();
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new JsonSerializationException("JSONが空です。");
                var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (!(root["scenes"] is JArray scenes)) throw new JsonSerializationException("scenes配列がありません。");
                foreach (var token in scenes)
                {
                    if (!(token is JObject stage)) throw new JsonSerializationException("部屋のデータ形式が不正です。");
                    if (stage["sceneId"]?.Type != JTokenType.Integer || stage["anomalyHouse"]?.Type != JTokenType.Boolean
                        || !(stage["items"] is JArray items))
                        throw new JsonSerializationException("sceneId、anomalyHouse、items配列が必要です。");
                    foreach (var item in items)
                    {
                        if (!(item is JObject entry)) throw new JsonSerializationException("配置物のデータ形式が不正です。");
                        if (entry["prefabId"]?.Type != JTokenType.String || entry["isAnomaly"]?.Type != JTokenType.Boolean
                            || !(entry["position"] is JObject) || !(entry["rotation"] is JObject))
                            throw new JsonSerializationException("配置物の必須フィールドが不足しています。");
                        ValidateVector(entry["position"]);
                        ValidateVector(entry["rotation"]);
                    }
                }
                var data = root.ToObject<StageFile>(JsonSerializer.Create(Settings));
                result.Data = Merge(data, result.Warnings);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is OverflowException)
            {
                result.Error = "ステージJSONが不正です。元のファイルは変更されません: " + exception.Message;
                result.IsReadOnly = true;
            }
            return result;
        }

        public static string Serialize(StageFile data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return JsonConvert.SerializeObject(Merge(data), Settings);
        }

        public static StageFile Merge(StageFile data, IList<string> warnings = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var merged = new SortedDictionary<int, StageData>();
            foreach (var stage in data.scenes ?? new List<StageData>())
            {
                if (stage == null) throw new ArgumentException("部屋がnullです。", nameof(data));
                if (!merged.TryGetValue(stage.sceneId, out var existing))
                {
                    merged.Add(stage.sceneId, stage.Clone());
                    continue;
                }
                existing.items.AddRange(stage.items?.Select(item => item?.Clone()) ?? Enumerable.Empty<StageItem>());
                existing.anomalyHouse |= stage.anomalyHouse;
                if (stage.AdditionalData != null)
                {
                    if (existing.AdditionalData == null) existing.AdditionalData = new Dictionary<string, JToken>();
                    foreach (var pair in stage.AdditionalData) existing.AdditionalData[pair.Key] = pair.Value?.DeepClone();
                }
                warnings?.Add("重複する部屋 " + stage.sceneId + " の配置を統合しました。");
            }
            return new StageFile { scenes = merged.Values.ToList(), AdditionalData = StageFile.CloneExtra(data.AdditionalData) };
        }

        public static StageFile Upsert(StageFile data, StageData replacement)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (replacement.sceneId < 1) throw new ArgumentOutOfRangeException(nameof(replacement), "部屋番号は1以上です。");
            var result = data.Clone();
            result.scenes.RemoveAll(stage => stage != null && stage.sceneId == replacement.sceneId);
            result.scenes.Add(replacement.Clone());
            return Merge(result);
        }

        public static int NextSceneId(StageFile data)
        {
            var used = new HashSet<int>((data?.scenes ?? new List<StageData>()).Where(stage => stage != null).Select(stage => stage.sceneId));
            int id = 1;
            while (used.Contains(id)) id = checked(id + 1);
            return id;
        }

        public StageSaveResult SaveStage(StageData stage) => Save(Upsert(Data, stage));

        public StageSaveResult Save(StageFile data)
        {
            if (IsReadOnly) return Failure("読み込みに失敗した保存ファイルを保護しています。ファイルを確認してから再読み込みしてください。");
            string temporaryPath = null;
            try
            {
                // A damaged file edited after loading must also be protected.
                if (File.Exists(SavePath))
                {
                    string existing = File.ReadAllText(SavePath, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(existing) && !Parse(existing).Success)
                    {
                        IsReadOnly = true;
                        return Failure("保存先のJSONが不正です。元のファイルを保護するため保存を中止しました。");
                    }
                }
                string json = Serialize(data);
                var verification = Parse(json);
                if (!verification.Success) return Failure(verification.Error);
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                temporaryPath = SavePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath)) File.Replace(temporaryPath, SavePath, SavePath + ".bak");
                else File.Move(temporaryPath, SavePath);
                Data = verification.Data.Clone();
                return new StageSaveResult { Success = true };
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is JsonException || exception is NotSupportedException)
            {
                return Failure("ステージを保存できません: " + exception.Message);
            }
            finally
            {
                if (temporaryPath != null && File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static StageSaveResult Failure(string message) => new StageSaveResult { Success = false, Error = message };

        private static void ValidateVector(JToken vector)
        {
            foreach (string axis in new[] { "x", "y", "z" })
            {
                var value = vector[axis];
                if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
                    throw new JsonSerializationException("位置・回転のx、y、zには数値が必要です。");
                double number = value.Value<double>();
                if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > float.MaxValue)
                    throw new JsonSerializationException("位置・回転の値が範囲外です。");
            }
        }
    }
}
