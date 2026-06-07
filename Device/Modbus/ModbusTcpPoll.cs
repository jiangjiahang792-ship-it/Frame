using HslCommunication.ModBus;
using Logger;
using Newtonsoft.Json;
using System;

namespace TDJS_Vision.Device.Modbus
{
    /// <summary>
    /// Modbus通信设备类
    /// </summary>
    public class ModbusTcpPoll : IModbus
    {
        ModbusTcpNet modbusTcp;

        public string DevName { get; set; }
        public string UserDefinedName { get; set; }

        public IModbusParam ModbusParam { get; set; }

        public bool IsConnect {  get; set; }

        public DevType DevType { get; set; } = DevType.ModbusTcpPoll;
        public DeviceBrand Brand { get; set; } = DeviceBrand.Unknow;
        public string ClassName { get; set; } = typeof(ModbusTcpPoll).FullName;

        public event EventHandler<bool> ConnectStatusEvent;


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
            try
            {
                var param = ModbusParam as ModbusTcpParam;
                modbusTcp = new ModbusTcpNet();
                modbusTcp.Station = 1;
                modbusTcp.IsStringReverse = false;
                modbusTcp.DataFormat = HslCommunication.Core.DataFormat.CDAB;
                modbusTcp.CommunicationPipe = new HslCommunication.Core.Pipe.PipeTcpNet(param.IP, param.Port)
                {
                    ConnectTimeOut = 5000,    // 连接超时时间，单位毫秒
                    ReceiveTimeOut = 5000,    // 接收设备数据反馈的超时时间
                };
                modbusTcp.ConnectServer();

                ConnectStatusEvent?.Invoke(this, true);
                
                IsConnect = true;
            }
            catch (Exception ex)
            {
                ConnectStatusEvent?.Invoke(this, false);
                IsConnect = false;
                throw new Exception($"Mobus设备【{DevName}】连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭Modbus
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Disconnect()
        {
            if (modbusTcp != null)
            {
                modbusTcp.CommunicationPipe.CloseCommunication();
            }
            modbusTcp = null;
            IsConnect = false;
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
