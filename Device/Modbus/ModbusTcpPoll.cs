using HslCommunication.ModBus;
using Logger;
using Newtonsoft.Json;
using System;

namespace TDJS_Vision.Device.Modbus
{
    /// <summary>
    /// Modbus通信设备类
    /// </summary>
    public class ModbusTcpPoll : ReconnectingCommunicationDevice, IModbus
    {
        /// <summary>Modbus操作允许的最小超时时间。</summary>
        private const int MinimumOperationTimeoutMs = 100;

        /// <summary>Modbus操作允许的最大超时时间。</summary>
        private const int MaximumOperationTimeoutMs = 60000;

        /// <summary>当前连接和收发操作的有限超时时间。</summary>
        private int _operationTimeoutMs = 5000;

        ModbusTcpNet modbusTcp;

        public string DevName { get; set; }
        public string UserDefinedName { get; set; }

        public IModbusParam ModbusParam { get; set; }

        /// <inheritdoc />
        protected override string CommunicationName => UserDefinedName;
        /// <inheritdoc />
        protected override HslCommunication.Core.Device.DeviceCommunication CommunicationClient => modbusTcp;

        /// <summary>获取或设置Modbus TCP连接与收发操作的有限超时时间。</summary>
        public int OperationTimeoutMs
        {
            get => _operationTimeoutMs;
            set
            {
                _operationTimeoutMs = Math.Max(
                    MinimumOperationTimeoutMs,
                    Math.Min(MaximumOperationTimeoutMs, value));
                ApplyOperationTimeout();
            }
        }

        public DevType DevType { get; set; } = DevType.ModbusTcpPoll;
        public DeviceBrand Brand { get; set; } = DeviceBrand.Unknow;
        public string ClassName { get; set; } = typeof(ModbusTcpPoll).FullName;



        #region 反序列化专用函数

        /// <summary>
        /// 指定反序列化的构造函数
        /// </summary>
        [JsonConstructor]
        public ModbusTcpPoll() { }

        public void CreateDevice()
        {
            try
            {

            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"{ex.Message}", true);
            }
        }


        #endregion

        public ModbusTcpPoll(ModbusTcpParam param)
        {
            ModbusParam = param;
            DevName = param.DevName;
            UserDefinedName = param.UserDefinedName;
        }

        /// <summary>
        /// 连接Modbus
        /// </summary>
        /// <param name="IP"></param>
        /// <param name="port"></param>
        /// <exception cref="Exception"></exception>
        public void  Connect()
        {
            if (!ConnectWithRecovery())
                throw new InvalidOperationException($"Modbus设备【{DevName}】连接失败，后台将自动重试。");
        }

        /// <inheritdoc />
        protected override HslCommunication.OperateResult OpenCommunicationCore()
        {
            if (modbusTcp == null)
            {
                var param = ModbusParam as ModbusTcpParam;
                modbusTcp = new ModbusTcpNet();
                modbusTcp.Station = 1;
                modbusTcp.IsStringReverse = false;
                modbusTcp.DataFormat = HslCommunication.Core.DataFormat.CDAB;
                modbusTcp.CommunicationPipe = new HslCommunication.Core.Pipe.PipeTcpNet(param.IP, param.Port)
                {
                    ConnectTimeOut = _operationTimeoutMs,
                    ReceiveTimeOut = _operationTimeoutMs,
                };
            }
            // 手动修改通信参数后仍复用同一个管线锁，不能让后台恢复绕过已有读写。
            var currentParam = (ModbusTcpParam)ModbusParam;
            var currentPipe = (HslCommunication.Core.Pipe.PipeTcpNet)modbusTcp.CommunicationPipe;
            currentPipe.IpAddress = currentParam.IP;
            currentPipe.Port = currentParam.Port;
            HslCommunication.OperateResult connectResult = modbusTcp.ConnectServer();
            EnsureConnectionSucceeded(connectResult);
            return connectResult;
        }

        /// <summary>
        /// 关闭Modbus
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Disconnect()
        {
            DisconnectWithRecovery();
        }

        /// <inheritdoc />
        protected override void CloseCommunicationCore() => modbusTcp?.ConnectClose();

        /// <summary>把统一有限超时应用到当前Modbus TCP通信管线。</summary>
        private void ApplyOperationTimeout()
        {
            if (modbusTcp?.CommunicationPipe is HslCommunication.Core.Pipe.PipeTcpNet pipe)
            {
                pipe.ConnectTimeOut = _operationTimeoutMs;
                pipe.ReceiveTimeOut = _operationTimeoutMs;
            }
        }

        /// <summary>检查HSL连接结果，禁止失败返回继续发布已连接状态。</summary>
        /// <param name="connectResult">HSL TCP连接结果。</param>
        internal static void EnsureConnectionSucceeded(HslCommunication.OperateResult connectResult)
        {
            if (connectResult == null)
                throw new InvalidOperationException("Modbus TCP连接API返回空结果。");
            if (!connectResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(connectResult.Message)
                        ? "Modbus TCP连接失败。"
                        : $"Modbus TCP连接失败：{connectResult.Message}");
            }
        }

        #region 读取操作


        public bool[] ReadCoils(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadCoil(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool[] ReadDiscretes(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadDiscrete(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool[] ReadBool(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadBool(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public short[] ReadInt16(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadInt16(address, length);
                if(!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public ushort[] ReadUInt16(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadUInt16(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public int[] ReadInt32(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadInt32(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public uint[] ReadUInt32(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadUInt32(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public float[] ReadFloat(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadFloat(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public long[] ReadInt64(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadInt64(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public ulong[] ReadUInt64(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadUInt64(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public double[] ReadDouble(string address, ushort length)
        {
            try
            {
                var res = modbusTcp.ReadDouble(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region 写入操作

        public void Write(string address, bool value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, bool[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, short value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, short[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, ushort value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, ushort[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, int value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, int[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, uint value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, uint[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, float value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, float[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, long value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, long[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, ulong value)
        {
            try
            {
                var res = modbusTcp.Write(address, value);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, ulong[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, double values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, double[] values)
        {
            try
            {
                var res = modbusTcp.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion
    }
}
