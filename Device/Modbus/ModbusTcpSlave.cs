using HslCommunication.ModBus;
using Logger;
using Modbus.Data;
using Modbus.Device;
using Newtonsoft.Json;
using System;

namespace TDJS_Vision.Device.Modbus
{
    public class ModbusTcpSlave : IModbus
    {
        /// <summary>从站接口要求保留的操作超时，当前从站不主动外发请求。</summary>
        private int _operationTimeoutMs = 5000;

        private ModbusTcpServer _server;
        public IModbusParam ModbusParam { get; set; }
        public string DevName { get; set; }
        public string UserDefinedName { get; set; }
        public DevType DevType { get; set; } = DevType.ModbusTcpSlave;
        public DeviceBrand Brand { get; set; } = DeviceBrand.Unknow;
        public string ClassName { get; set; } = typeof(ModbusTcpSlave).FullName;

        public bool IsConnect { get; set; }

        /// <summary>获取或设置从站接口的有限操作超时时间。</summary>
        public int OperationTimeoutMs
        {
            get => _operationTimeoutMs;
            set => _operationTimeoutMs = Math.Max(100, Math.Min(60000, value));
        }

        public event EventHandler<bool> ConnectStatusEvent;

        private global::Modbus.Device.ModbusTcpSlave _slave;

        #region 反序列化使用

        /// <summary>
        /// 指定反序列化的构造函数
        /// </summary>
        [JsonConstructor]
        public ModbusTcpSlave() { }

        public void CreateDevice()
        {
            try
            {
                // 初始化设备
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"{ex.Message}", true);
            }
        }

        #endregion  

        public ModbusTcpSlave(ModbusTcpParam modbusParam)
        {
            ModbusParam = modbusParam;
            DevName = modbusParam.DevName;
            UserDefinedName = modbusParam.UserDefinedName;
        }

        /// <summary>
        /// 从站启动
        /// </summary>
        public void Connect()
        {
            try
            {
                // HSL库版
                _server = new ModbusTcpServer();
                _server.EnableWrite = true;
                _server.EnableIPv6 = false;
                _server.Station = 1;
                _server.StationDataIsolation = false;
                _server.UseModbusRtuOverTcp = false;
                _server.IsStringReverse = false;
                _server.EnableWriteMaskCode = true;
                _server.DataFormat = HslCommunication.Core.DataFormat.CDAB;
                _server.ActiveTimeSpan = TimeSpan.Parse("01:00:00");
                _server.EnableIPv6 = false;
                _server.ServerStart(502);

                IsConnect = true;
                ConnectStatusEvent?.Invoke(this, true);
                _slave.Listen();
            }
            catch (Exception ex)
            {
                IsConnect = false;
                ConnectStatusEvent?.Invoke(this, false);
                LogHelper.AddLog(MsgLevel.Exception, $"从站（{DevName}）启动失败: {ex.Message}", true);
                throw;
            }
        }

        /// <summary>
        /// 从站停止
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if(_slave != null && _server != null)
                {
                    _slave.Dispose();
                    _server.ServerClose();
                }
                IsConnect = false;
                ConnectStatusEvent?.Invoke(this, false);
                LogHelper.AddLog(MsgLevel.Info, $"从站（{DevName}）已停止运行", true);
            }
            catch (Exception)
            {

            }
        }
        /// <summary>
        /// 从站开始写入数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataStore_DataStoreWrittenTo(object sender, DataStoreEventArgs e)
        {
#if DEBUG
            try
            {
                switch (e.ModbusDataType)
                {
                    case ModbusDataType.Coil:
                        ModbusDataCollection<bool> discretes = _slave.DataStore.CoilDiscretes;
                        LogHelper.AddLog(MsgLevel.Info, $"从站({UserDefinedName})写入数据: " + JsonConvert.SerializeObject(discretes), true);
                        break;
                    case ModbusDataType.HoldingRegister:
                        ModbusDataCollection<ushort> holdingRegisters = _slave.DataStore.HoldingRegisters;
                        LogHelper.AddLog(MsgLevel.Info, $"从站({UserDefinedName})写入数据: " + JsonConvert.SerializeObject(holdingRegisters), true);
                        break;
                    default:
                        LogHelper.AddLog(MsgLevel.Info, $"从站({UserDefinedName})写入数据: " + JsonConvert.SerializeObject(e.Data), true);
                        break;

                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }
#endif
        }

        /// <summary>
        /// 从站接收到的请求
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Slave_ModbusSlaveRequestReceived(object sender, ModbusSlaveRequestEventArgs e)
        {
#if DEBUG
            try
            {
                LogHelper.AddLog(MsgLevel.Info, $"从站({UserDefinedName})接收请求数据: " + JsonConvert.SerializeObject(e.Message), true);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }
#endif
        }

        /// <summary>
        /// 从站写入数据完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Slave_WriteComplete(object sender, ModbusSlaveRequestEventArgs e)
        {
            try
            {
                LogHelper.AddLog(MsgLevel.Info, $"从站({UserDefinedName})写入数据完成: " + JsonConvert.SerializeObject(e.Message), true);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);
            }
        }

        #region 读取操作
        public bool[] ReadCoils(string address, ushort length)
        {
            try
            {
                var res = _server.ReadCoil(address, length);
                return res;
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
                var res = _server.ReadDiscrete(address, length);
                return res;
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
                var res = _server.ReadBool(address, length);
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
                var res = _server.ReadInt16(address, length);
                if (!res.IsSuccess)
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
                var res = _server.ReadUInt16(address, length);
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
                var res = _server.ReadInt32(address, length);
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
                var res = _server.ReadUInt32(address, length);
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
                var res = _server.ReadFloat(address, length);
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
                var res = _server.ReadInt64(address, length);
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
                var res = _server.ReadUInt64(address, length);
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
                var res = _server.ReadDouble(address, length);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, double value)
        {
            try
            {
                var res = _server.Write(address, value);
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
                var res = _server.Write(address, values);
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

