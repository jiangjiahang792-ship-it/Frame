using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Reflection;

namespace TDJS_Vision
{
    public class JsonProjectSerializer
    {
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 保存工程到指定文件
        /// </summary>
        /// <typeparam name="T">工程类型，一般是 VisionProject</typeparam>
        /// <param name="project">工程对象</param>
        /// <param name="filePath">保存路径</param>
        public static void SaveProject<T>(T project, string filePath)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var json = JsonConvert.SerializeObject(project, _settings);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// 从文件加载工程
        /// </summary>
        /// <typeparam name="T">工程类型，一般是 VisionProject</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <returns>反序列化的工程对象</returns>
        public static T LoadProject<T>(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("项目文件不存在", filePath);

            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var project = JsonConvert.DeserializeObject<T>(json, _settings);
            return project;
        }

        /// <summary>
        /// 将任意对象转换为 JSON 字符串（可选方法）
        /// </summary>
        public static string ToJson<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化对象（可选方法）
        /// </summary>
        public static T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }
    }

    // 1. 自定义契约解析器：1.忽略 ImageSaver；2.只处理属性（不处理字段）
    public class IgnoreImageSaverAndFieldsResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            // 先忽略 ImageSaver 属性（解决之前的异常）
            var property = base.CreateProperty(member, memberSerialization);

            // 关键：排除“字段（Field）”，只保留“属性（Property）”
            if (member.MemberType == MemberTypes.Field)
            {
                property.Ignored = true; // 字段直接忽略
            }

            return property;
        }
    }
}
