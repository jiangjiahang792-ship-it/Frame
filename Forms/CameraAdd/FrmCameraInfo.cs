using Basler.Pylon;
using Logger;
using MvCameraControl;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Forms.CameraAdd
{
    public partial class FrmCameraInfo : FormBase
    {
        /// <summary>
        /// 添加相机设备事件
        /// </summary>
        public static event EventHandler<CameraParam> AddCameraDevEvent;

        /// <summary>
        /// 添加 3D 相机设备事件。
        /// </summary>
        public static event EventHandler<Camera3DParam> AddCamera3DDevEvent;

        /// <summary>
        /// 海康名称计数
        /// </summary>
        private static int _HikCount = 1;

        /// <summary>
        /// 巴斯勒名称计数
        /// </summary>
        private static int _Baslercount = 1;

        /// <summary>
        /// 大恒名称计数
        /// </summary>
        private static int _DaHengcount = 1;

        /// <summary>
        /// 大华名称计数
        /// </summary>
        private static int _DaHuacount = 1;

        /// <summary>
        /// 海康 3D 名称计数。
        /// </summary>
        private static int _Hik3DCount = 1;

        /// <summary>
        /// 相机信息列表
        /// </summary>
        private List<IDeviceInfo> infoList = CameraHik.FindCamera();

        /// <summary>
        /// 3D 相机信息列表。
        /// </summary>
        private List<Camera3DDeviceInfo> _camera3DInfoList = CameraHik3D.FindCamera();

        /// <summary>
        /// 用来保存设备名对应的设备信息
        /// </summary>
        private Dictionary<string, IDeviceInfo> _mapCamera = new Dictionary<string, IDeviceInfo>();

        /// <summary>
        /// 用来保存 3D 设备名对应的设备信息。
        /// </summary>
        private Dictionary<string, Camera3DDeviceInfo> _mapCamera3D = new Dictionary<string, Camera3DDeviceInfo>();

        public FrmCameraInfo()
        {
            InitializeComponent();
            infoList = CameraHik.FindCamera();
            _mapCamera.Clear();
            foreach (var info in infoList)
                _mapCamera[CameraHik.GetDevNameByDevInfo(info)] = info;
            RefreshCamera3DMap();
        }
        private void FrmCameraInfo_Load(object sender, EventArgs e)
        {
            // 初始化相机品牌
            InitCameraBrandList();
            // 初始化相机列表
            InitCameraList(comboBoxCameraBrand.Text);
        }

        /// <summary>
        /// 初始相机品牌下拉框
        /// </summary>
        private void InitCameraBrandList()
        {
            var addedBrands = new HashSet<string>();
            comboBoxCameraBrand.Items.Clear();
            foreach (var info in infoList)
            {
                string brand;
                switch (info.ManufacturerName)
                {
                    case "GEV":
                    case "Hikrobot":
                        brand = "海康";
                        if (addedBrands.Add(brand))
                        {
                            comboBoxCameraBrand.Items.Add(brand);
                        }
                        break;
                    case "Basler":
                        brand = "巴斯勒";
                        if (addedBrands.Add(brand))
                        {
                            comboBoxCameraBrand.Items.Add(brand);
                        }
                        break;
                    case "Daheng Imaging":
                        brand = "大恒";
                        if (addedBrands.Add(brand))
                        {
                            comboBoxCameraBrand.Items.Add(brand);
                        }
                        break;
                    case "Dahua Technology":
                        brand = "大华";
                        if (addedBrands.Add(brand))
                        {
                            comboBoxCameraBrand.Items.Add(brand);
                        }
                        break;
                    case "Huaray Technology":
                        brand = "华睿";
                        if (addedBrands.Add(brand))
                        {
                            comboBoxCameraBrand.Items.Add(brand);
                        }
                        break;
                    default:
                        brand = "其他";
                        if (addedBrands.Add(brand))
                        {
                            comboBoxCameraBrand.Items.Add(brand);
                        }
                        break;
                }
            }

            if (addedBrands.Add("海康3D"))
                comboBoxCameraBrand.Items.Add("海康3D");

            if (comboBoxCameraBrand.Items.Count > 0)
                comboBoxCameraBrand.SelectedIndex = 0;
        }

        /// <summary>
        /// 初始化相机下拉框数据
        /// </summary>
        private void InitCameraList(string brand)
        {
            comboBoxCameraList.Items.Clear();
            switch (brand)
            {
                case "海康3D":
                    foreach (var info in _camera3DInfoList)
                    {
                        comboBoxCameraList.Items.Add(CameraHik3D.GetDevNameByDevInfo(info));
                    }
                    break;
                case "海康":
                    foreach (var info in infoList)
                    {
                        if (info.ManufacturerName == "Hikrobot" || info.ManufacturerName == "GEV")
                        {
                            comboBoxCameraList.Items.Add(CameraHik.GetDevNameByDevInfo(info));
                        }
                    }
                    break;
                case "巴斯勒":
                    foreach (var info in infoList)
                    {
                        if (info.ManufacturerName == "Basler")
                        {
                            comboBoxCameraList.Items.Add(CameraHik.GetDevNameByDevInfo(info));
                        }
                    }
                    break;
                case "大恒":
                    foreach (var info in infoList)
                    {
                        if (info.ManufacturerName == "Daheng Imaging")
                        {
                            comboBoxCameraList.Items.Add(CameraHik.GetDevNameByDevInfo(info));
                        }
                    }
                    break;
                case "大华":
                    foreach (var info in infoList)
                    {
                        if (info.ManufacturerName == "Dahua Technology")
                        {
                            comboBoxCameraList.Items.Add(CameraHik.GetDevNameByDevInfo(info));
                        }
                    }
                    break;
                case "华睿":
                    foreach (var info in infoList)
                    {
                        if (info.ManufacturerName == "Huaray Technology")
                        {
                            comboBoxCameraList.Items.Add(CameraHik.GetDevNameByDevInfo(info));
                        }
                    }
                    break;
                default:
                    foreach (var info in infoList)
                    {
                        comboBoxCameraList.Items.Add(CameraHik.GetDevNameByDevInfo(info));
                    }
                    break;
            }
            if (comboBoxCameraList.Items.Count > 0)
                comboBoxCameraList.SelectedIndex = 0;
        }

        /// <summary>
        /// 点击搜索相机
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSearch_Click(object sender, EventArgs e)
        {
            infoList = CameraHik.FindCamera();
            _mapCamera.Clear();
            foreach (var info in infoList)
                _mapCamera[CameraHik.GetDevNameByDevInfo(info)] = info;
            _camera3DInfoList = CameraHik3D.FindCamera();
            RefreshCamera3DMap();
            // 初始化相机品牌和相机设备下拉列表
            InitCameraBrandList();
            InitCameraList(comboBoxCameraBrand.Text);
        }

        /// <summary>
        /// 相机品牌选中改变
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxCameraBrand_SelectedIndexChanged(object sender, EventArgs e)
        {
            InitCameraList(comboBoxCameraBrand.Text);
            textBoxUserName.Text = comboBoxCameraBrand.Text == "海康3D"
                ? $"{comboBoxCameraBrand.Text}相机{_Hik3DCount}"
                : $"{comboBoxCameraBrand.Text}相机{_HikCount}";
        }

        /// <summary>
        /// 刷新 3D 相机设备名称映射。
        /// </summary>
        private void RefreshCamera3DMap()
        {
            _mapCamera3D.Clear();
            foreach (Camera3DDeviceInfo info in _camera3DInfoList)
            {
                string devName = CameraHik3D.GetDevNameByDevInfo(info);
                if (!string.IsNullOrWhiteSpace(devName))
                    _mapCamera3D[devName] = info;
            }
        }

        /// <summary>
        /// 点击发送相机设备信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDevAdd_Click(object sender, EventArgs e)
        {
            //参数完整性判断
            if (comboBoxCameraList.Text.IsNullOrEmpty() || textBoxUserName.Text.IsNullOrEmpty())
            {
                LogHelper.AddLog(MsgLevel.Warn, "相机设备名或用户自定义名称不能为空！", true);
                MessageBoxTD.Show("相机设备名或用户自定义名称不能为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            //设备重复性判断
            foreach (var device in Solution.Instance.AllDevices)
            {
                if (device.DevName == comboBoxCameraList.Text || device.UserDefinedName == textBoxUserName.Text)
                {
                    LogHelper.AddLog(MsgLevel.Warn, "当前相机设备已存在或用户自定义名称已存在！", true);
                    MessageBoxTD.Show("当前相机设备已存在或用户自定义名称已存在！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            CameraParam info = new CameraParam();
            try
            {
                if (comboBoxCameraBrand.Text == "海康3D")
                {
                    Camera3DParam camera3DParam = new Camera3DParam
                    {
                        DevInfo = _mapCamera3D[comboBoxCameraList.Text],
                        UserDefinedName = textBoxUserName.Text
                    };
                    _Hik3DCount++;
                    AddCamera3DDevEvent?.Invoke(this, camera3DParam);
                    this.Hide();
                    return;
                }

                //通过事件传递相机参数
                switch (comboBoxCameraBrand.Text)
                {
                    case "海康":
                        info.Brand = CameraBrand.HiKVision;
                        _HikCount++;
                        break;
                    case "巴斯勒":
                        info.Brand = CameraBrand.Basler;
                        _Baslercount++;
                        break;
                    case "大恒":
                        info.Brand = CameraBrand.DaHeng;
                        _DaHengcount++;
                        break;
                    case "大华":
                        info.Brand = CameraBrand.DaHua;
                        _DaHuacount++;
                        break;
                    case "华睿":
                        info.Brand = CameraBrand.HuaRui;
                        _DaHuacount++;
                        break;
                    default:
                        info.Brand = CameraBrand.Other;
                        _DaHuacount++;
                        break;
                }
                info.DevInfo = new CameraDevInfo(_mapCamera[comboBoxCameraList.Text]);
                info.UserDefinedName = textBoxUserName.Text;

                AddCameraDevEvent?.Invoke(this, info);
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("添加相机异常：" + ex.Message);
            }
        }

    }

    /// <summary>
    /// 参数结构体
    /// </summary>
    public struct CameraParam
    {
        /// <summary>
        /// 相机品牌。
        /// </summary>
        public CameraBrand Brand;

        /// <summary>
        /// 相机设备信息。
        /// </summary>
        public CameraDevInfo DevInfo;

        /// <summary>
        /// 用户自定义设备名。
        /// </summary>
        public string UserDefinedName;
    }

    /// <summary>
    /// 3D 相机添加参数结构体。
    /// </summary>
    public struct Camera3DParam
    {
        /// <summary>
        /// 3D 相机设备信息。
        /// </summary>
        public Camera3DDeviceInfo DevInfo;

        /// <summary>
        /// 用户自定义设备名。
        /// </summary>
        public string UserDefinedName;
    }
}
