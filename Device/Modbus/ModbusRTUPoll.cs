using HslCommunication.Core.Pipe;
using HslCommunication.ModBus;
using Logger;
using Newtonsoft.Json;
using System;

namespace TDJS_Vision.Device.Modbus
{
    /// <summary>
    /// Modbus RTU 串口通信从站类（主站主动轮询）
    /// </summary>
    public class ModbusRTUPoll : IModbus
    {
        private PipeSerialPort _pipe;
        private ModbusRtu _modbus;

        public string DevName { get; set; }
        public string UserDefinedName { get; set; }

        public IModbusParam ModbusParam { get; set; }

        public bool IsConnect { get; set; }

        public DevType DevType { get; set; } = DevType.ModbusRTUPoll;
        public DeviceBrand Brand { get; set; } = DeviceBrand.Unknow;
        public string ClassName { get; set; } = typeof(ModbusRTUPoll).FullName;

        public event EventHandler<bool> ConnectStatusEvent;

        #region 构造函数

        [JsonConstructor]
        public ModbusRTUPoll() { }

        public void CreateDevice()
        {
            try
            {
                // 可用于初始化参数等
                _modbus = new ModbusRtu();
                _pipe = new PipeSerialPort();

                ModbusRTUParam modbusRTUParam = (ModbusRTUParam)ModbusParam;

                _pipe.SerialPortInni(
                    portName: modbusRTUParam.PortName,
                    baudRate: modbusRTUParam.BaudRate,
                    dataBits: modbusRTUParam.DataBits,
                    stopBits: modbusRTUParam.StopBits,
                    parity: modbusRTUParam.Parity
                );
                _pipe.RtsEnable = false;
                _pipe.DtrEnable = false;
                _pipe.SleepTime = 20;
                _pipe.ReceiveTimeOut = 5000;
                _pipe.OpenCommunication();
                _modbus.CommunicationPipe = _pipe;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"{ex.Message}", true);
            }
        }

        public ModbusRTUPoll(ModbusRTUParam param)
        {
            ModbusParam = param;
            DevName = param.DevName;
            UserDefinedName = param.UserDefinedName;

            _modbus = new ModbusRtu();
            _pipe = new PipeSerialPort();
            _pipe.SerialPortInni(
                portName: param.PortName,
                baudRate: param.BaudRate,
                dataBits: param.DataBits,
                stopBits: param.StopBits,
                parity: param.Parity
            );
            _pipe.RtsEnable = false;
            _pipe.DtrEnable = false;
            _pipe.SleepTime = 20;
            _pipe.ReceiveTimeOut = 5000;
            _pipe.OpenCommunication();
            _modbus.CommunicationPipe = _pipe;
        }

        #endregion

        #region 连接管理

        public void Connect()
        {
            try
            {
                var param = ModbusParam as ModbusRTUParam;
                // 创建并配置串口
                _pipe = new PipeSerialPort();
                _pipe.SerialPortInni(
                    portName: param.PortName,
                    baudRate: param.BaudRate,
                    dataBits: param.DataBits,
                    stopBits: param.StopBits,
                    parity: param.Parity
                );
                _pipe.RtsEnable = false;
                _pipe.DtrEnable = false;
                _pipe.SleepTime = 20;
                _pipe.ReceiveTimeOut = 5000;
                _pipe.OpenCommunication();
                _modbus.CommunicationPipe = _pipe;

                ConnectStatusEvent?.Invoke(this, true);
                IsConnect = true;
            }
            catch (Exception ex)
            {
                ConnectStatusEvent?.Invoke(this, false);
                IsConnect = false;
                throw new Exception($"Modbus串口设备【{DevName}】连接失败: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (_modbus != null)
            {
                //_modbus.Dispose();
                _modbus.Close();
                //_modbus = null;
            }

            if (_pipe != null)
            {
                _pipe.CloseCommunication();
            }

            //_pipe = null;
            IsConnect = false;
        }

        #endregion

        public bool[] ReadCoils(string address, ushort length)
        {
            try
            {
                var res = _modbus.ReadCoil(address, length);
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
                var res = _modbus.ReadDiscrete(address, length);
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
                var res = _modbus.ReadBool(address, length);
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
                var res = _modbus.ReadInt16(address, length);
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
                var res = _modbus.ReadUInt16(address, length);
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
                var res = _modbus.ReadInt32(address, length);
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
                var res = _modbus.ReadUInt32(address, length);
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
                var res = _modbus.ReadFloat(address, length);
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
                var res = _modbus.ReadInt64(address, length);
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
                var res = _modbus.ReadUInt64(address, length);
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
                var res = _modbus.ReadDouble(address, length);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
                return res.Content;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Write(string address, bool value)
        {
            try
            {
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
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
                var res = _modbus.Write(address, value);
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
                var res = _modbus.Write(address, values);
                if (!res.IsSuccess)
                    throw new Exception(res.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
