using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Collections.Generic;
using TDJS_Vision.Device.Camera;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeParamImageSoucre : INodeParam
    {
        /// <summary>保护手动测试覆盖及持久化快照的一致性。</summary>
        private readonly object _imageTestSync = new object();
        /// <summary>当前是否临时覆盖图像源。</summary>
        private bool _hasImageTestOverride;
        /// <summary>测试前的持久化图像来源。</summary>
        private string _originalImageSource;
        /// <summary>测试前的持久化单图路径。</summary>
        private string _originalImagePath;

        /// <summary>开始临时图像源覆盖，保留保存方案时应使用的原始配置。</summary>
        /// <param name="source">本次测试图像来源。</param>
        /// <param name="path">本次测试图像路径。</param>
        internal void BeginImageTestOverride(string source, string path)
        {
            lock (_imageTestSync)
            {
                if (_hasImageTestOverride)
                    throw new System.InvalidOperationException("该图像源正在测试，请等待本次测试完成。");
                _originalImageSource = ImageSource;
                _originalImagePath = ImagePath;
                _hasImageTestOverride = true;
                ImageSource = source;
                ImagePath = path;
            }
        }

        /// <summary>结束临时测试，只恢复图像源和路径，保留用户对其他参数的编辑。</summary>
        internal void EndImageTestOverride()
        {
            lock (_imageTestSync)
            {
                if (!_hasImageTestOverride)
                    return;
                ImageSource = _originalImageSource;
                ImagePath = _originalImagePath;
                _hasImageTestOverride = false;
                _originalImageSource = null;
                _originalImagePath = null;
            }
        }

        /// <summary>创建用于方案保存的快照，排除正在运行的临时测试图片配置。</summary>
        /// <returns>包含原始图像来源和路径的独立参数对象。</returns>
        internal NodeParamImageSoucre CreatePersistenceSnapshot()
        {
            lock (_imageTestSync)
            {
                var snapshot = (NodeParamImageSoucre)MemberwiseClone();
                if (_hasImageTestOverride)
                {
                    snapshot.ImageSource = _originalImageSource;
                    snapshot.ImagePath = _originalImagePath;
                }
                snapshot._hasImageTestOverride = false;
                snapshot._originalImageSource = null;
                snapshot._originalImagePath = null;
                return snapshot;
            }
        }

        /// <summary>
        /// 图像源
        /// </summary>
        public string ImageSource { get; set; }

        #region 本地图像参数
        /// <summary>
        /// 文件或文件夹路径
        /// </summary>
        public string PathText { get; set; }
        /// <summary>
        /// 图片路径
        /// </summary>
        public string ImagePath { get; set; }
        /// <summary>
        /// 触发遍历下一张图片
        /// </summary>
        public bool IsAutoLoop { get; set; }
        /// <summary>
        /// 图片路径列表
        /// </summary>
        [JsonIgnore]
        public List<string> ImagePaths { get; set; } = new List<string>();
        #endregion

        #region 相机参数

        /// <summary>
        /// 使用的相机
        /// </summary>
        [JsonIgnore]
        public ICamera Camera { get; set; }
        /// <summary>
        /// 使用的相机名称
        /// </summary>
        public string CameraName { get; set; }
        /// <summary>
        /// 触发模式；新节点默认开启，旧方案显式保存的关闭模式仍保留。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerModel TriggerModel { get; set; } = TriggerModel.On;
        /// <summary>
        /// 触发方式
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerSource TriggerSource { get; set; }
        /// <summary>
        /// 触发沿
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TriggerEdge TriggerEdge { get; set; }
        /// <summary>
        /// 触发延迟
        /// </summary>
        public int TriggerDelay { get; set; }
        /// <summary>
        /// 曝光时间
        /// </summary>
        public double ExposureTime { get; set; }
        /// <summary>
        /// 增益
        /// </summary>
        public double Gain { get; set; }
        /// <summary>
        /// 采图超时时间
        /// </summary>
        public uint TimeOut { get; set; }
        /// <summary>
        /// 是否每次设置参数
        /// </summary>
        public bool IsEveryTime { get; set; }
        

        #endregion

        #region 共享变量参数

        /// <summary>
        /// 读取图像时使用的共享变量名称。
        /// </summary>
        public string SharedVariableName { get; set; }

        #endregion
    }
}
