using System;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.IO.Ports;
using Logger;
using TDJS_Vision.Device.Light;
using TDJS_Vision.Forms.LightAdd;
using System.Text;
using System.Windows.Forms;

namespace TDJS_Vision.Device.COM
{
    public class ComDevice : IDevice
    {
        public string DevName { get; set; }

        public string UserDefinedName { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public DevType DevType { get; set; } = DevType.COM;

        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceBrand Brand { get; set; } =  DeviceBrand.Unknow;

        public SerialPortConfig ComParams { get; set; }

        public bool IsOpen { get; set; }

        public string ClassName { get; set; } = typeof(ComDevice).FullName;

        public event EventHandler<bool> ConnectStatusEvent;

        private SerialPort _serialPort = new SerialPort();


        #region 反序列化专用函数

        /// <summary>
        /// 指定反序列化的构造函数
        /// </summary>
        [JsonConstructor]
        public ComDevice() { }

        public void CreateDevice()
        {
            try
            {
                _serialPort = new SerialPort();
                ComParams.ApplyTo(_serialPort);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"{ex.Message}", true);
            }
        }


        #endregion


        public ComDevice(SerialPortConfig comParam)
        {
            try
            {
                DevName = comParam.PortName;
                UserDefinedName = comParam.PortName;
                ComParams = comParam;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }

        /// <summary>
        /// 连接串口
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="dataBits"></param>
        /// <param name="stopBits"></param>
        /// <param name="parity"></param>
        /// <returns></returns>
        public void Connenct()
        {
            if (_serialPort.IsOpen) { return; }
            try
            {
                ComParams.ApplyTo(_serialPort);
                _serialPort.Open();
                IsOpen = true;
                ConnectStatusEvent?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            _serialPort.Close();
            IsOpen = false;
            ConnectStatusEvent?.Invoke(this, false);
        }

        private readonly object _sendLock = new object(); // 专用锁对象

        /// <summary>
        /// 线程安全地发送字符串数据
        /// </summary>
        /// <param name="data">要发送的字符串</param>
        public void Send(string data, string encoding)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("串口发送数据不能为空。", nameof(data));

            lock (_sendLock) // 确保同一时间只有一个线程在写
            {
                try
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        byte[] bytes;

                        if (encoding == "ASCII")
                            bytes = Encoding.ASCII.GetBytes(data);

                        else if (encoding == "UTF8")
                            bytes = Encoding.UTF8.GetBytes(data);

                        else if (encoding == "Unicode")
                            bytes = Encoding.Unicode.GetBytes(data);

                        else if (encoding == "BigEndianUnicode")
                            bytes = Encoding.BigEndianUnicode.GetBytes(data);

                        else
                            bytes = Encoding.Default.GetBytes(data);

                        _serialPort.Write(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        throw new InvalidOperationException("串口未打开，无法发送数据");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"发送数据失败: {ex.Message}, 数据: {data}");
                }
            }
        }
        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            lock (_sendLock)
            {
                try
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Write(data, 0, data.Length);
                    }
                    else
                    {
                        throw new InvalidOperationException("串口未打开，无法发送数据");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"发送数据失败: {ex.Message}, 数据: {data}");
                }
            }
        }


    }

    public class SerialPortConfig
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public Parity Parity { get; set; } = Parity.None;
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>同步串口写入的有限超时毫秒数。</summary>
        public int WriteTimeout { get; set; } = 2000;

        // 可选：从 SerialPort 实例加载配置
        public static SerialPortConfig FromSerialPort(SerialPort port)
        {
            return new SerialPortConfig
            {
                PortName = port.PortName,
                BaudRate = port.BaudRate,
                DataBits = port.DataBits,
                Parity = port.Parity,
                StopBits = port.StopBits,
                WriteTimeout = port.WriteTimeout
            };
        }

        // 可选：应用配置到 SerialPort 实例
        public void ApplyTo(SerialPort port)
        {
            port.PortName = PortName;
            port.BaudRate = BaudRate;
            port.DataBits = DataBits;
            port.Parity = Parity;
            port.StopBits = StopBits;
            port.WriteTimeout = Math.Min(60000, Math.Max(100, WriteTimeout));
        }
    }
}
