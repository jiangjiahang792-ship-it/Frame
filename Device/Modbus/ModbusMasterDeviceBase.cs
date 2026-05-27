using System;
using Newtonsoft.Json;

namespace TDJS_Vision.Device.Modbus
{
    /// <summary>
    /// NModbus4主站设备基类，统一实现TCP主站和RTU主站共用的读写逻辑。
    /// </summary>
    public abstract class ModbusMasterDeviceBase : IModbus
    {
        /// <summary>
        /// 串行化Modbus请求的同步锁，避免多个流程节点同时访问同一个主站连接。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// NModbus4主站对象，由具体TCP或RTU子类在连接时创建。
        /// </summary>
        [JsonIgnore]
        protected global::Modbus.Device.ModbusMaster Master { get; set; }

        /// <summary>
        /// 子类连接和断开时共用的同步锁。
        /// </summary>
        [JsonIgnore]
        protected object SyncRoot => _syncRoot;

        /// <summary>
        /// 设备名称。
        /// </summary>
        public string DevName { get; set; }

        /// <summary>
        /// 用户自定义设备名称。
        /// </summary>
        public string UserDefinedName { get; set; }

        /// <summary>
        /// Modbus设备参数。
        /// </summary>
        public IModbusParam ModbusParam { get; set; }

        /// <summary>
        /// 当前连接状态。
        /// </summary>
        public bool IsConnect { get; set; }

        /// <summary>
        /// 设备类型。
        /// </summary>
        public DevType DevType { get; set; }

        /// <summary>
        /// 设备品牌。
        /// </summary>
        public DeviceBrand Brand { get; set; } = DeviceBrand.Unknow;

        /// <summary>
        /// 序列化时用于识别真实设备类型的类名。
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// 连接状态改变事件。
        /// </summary>
        public event EventHandler<bool> ConnectStatusEvent;

        /// <summary>
        /// 反序列化后创建设备资源，主站连接资源延迟到Connect中创建。
        /// </summary>
        public virtual void CreateDevice()
        {
            Master = null;
            IsConnect = false;
        }

        /// <summary>
        /// 连接Modbus设备。
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// 断开Modbus设备。
        /// </summary>
        public abstract void Disconnect();

        /// <summary>
        /// 读取线圈。
        /// </summary>
        /// <param name="address">起始地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>线圈状态数组。</returns>
        public bool[] ReadCoils(string address, ushort length)
        {
            lock (_syncRoot)
            {
                return RequireMaster().ReadCoils(GetSlaveAddress(address), GetAddress(address), length);
            }
        }

        /// <summary>
        /// 读取离散输入。
        /// </summary>
        /// <param name="address">起始地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>离散输入状态数组。</returns>
        public bool[] ReadDiscretes(string address, ushort length)
        {
            lock (_syncRoot)
            {
                return RequireMaster().ReadInputs(GetSlaveAddress(address), GetAddress(address), length);
            }
        }

        /// <summary>
        /// 读取布尔值，兼容原HSL实现中ReadBool按线圈读取的用法。
        /// </summary>
        /// <param name="address">起始地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>布尔数组。</returns>
        public bool[] ReadBool(string address, ushort length)
        {
            return ReadCoils(address, length);
        }

        /// <summary>
        /// 读取16位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>short数组。</returns>
        public short[] ReadInt16(string address, ushort length)
        {
            ushort[] registers = ReadHoldingRegisters(address, length);
            return ModbusRegisterCodec.ToInt16Array(registers);
        }

        /// <summary>
        /// 读取16位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>ushort数组。</returns>
        public ushort[] ReadUInt16(string address, ushort length)
        {
            return ReadHoldingRegisters(address, length);
        }

        /// <summary>
        /// 读取32位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>int数组。</returns>
        public int[] ReadInt32(string address, ushort length)
        {
            ushort[] registers = ReadHoldingRegisters(address, ModbusRegisterCodec.GetRegisterCount(length, 2));
            return ModbusRegisterCodec.ToInt32Array(registers, length);
        }

        /// <summary>
        /// 读取32位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>uint数组。</returns>
        public uint[] ReadUInt32(string address, ushort length)
        {
            ushort[] registers = ReadHoldingRegisters(address, ModbusRegisterCodec.GetRegisterCount(length, 2));
            return ModbusRegisterCodec.ToUInt32Array(registers, length);
        }

        /// <summary>
        /// 读取单精度浮点数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>float数组。</returns>
        public float[] ReadFloat(string address, ushort length)
        {
            ushort[] registers = ReadHoldingRegisters(address, ModbusRegisterCodec.GetRegisterCount(length, 2));
            return ModbusRegisterCodec.ToFloatArray(registers, length);
        }

        /// <summary>
        /// 读取64位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>long数组。</returns>
        public long[] ReadInt64(string address, ushort length)
        {
            ushort[] registers = ReadHoldingRegisters(address, ModbusRegisterCodec.GetRegisterCount(length, 4));
            return ModbusRegisterCodec.ToInt64Array(registers, length);
        }

        /// <summary>
        /// 读取64位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>ulong数组。</returns>
        public ulong[] ReadUInt64(string address, ushort length)
        {
            ushort[] registers = ReadHoldingRegisters(address, ModbusRegisterCodec.GetRegisterCount(length, 4));
            return ModbusRegisterCodec.ToUInt64Array(registers, length);
        }

        /// <summary>
        /// 读取双精度浮点数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取数量。</param>
        /// <returns>double数组。</returns>
        public double[] ReadDouble(string address, ushort length)
        {
            ushort[] registers = ReadHoldingRegisters(address, ModbusRegisterCodec.GetRegisterCount(length, 4));
            return ModbusRegisterCodec.ToDoubleArray(registers, length);
        }

        /// <summary>
        /// 写入单个线圈。
        /// </summary>
        /// <param name="address">线圈地址。</param>
        /// <param name="value">线圈状态。</param>
        public void Write(string address, bool value)
        {
            lock (_syncRoot)
            {
                RequireMaster().WriteSingleCoil(GetSlaveAddress(address), GetAddress(address), value);
            }
        }

        /// <summary>
        /// 写入多个线圈。
        /// </summary>
        /// <param name="address">起始线圈地址。</param>
        /// <param name="values">线圈状态数组。</param>
        public void Write(string address, bool[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            lock (_syncRoot)
            {
                RequireMaster().WriteMultipleCoils(GetSlaveAddress(address), GetAddress(address), values);
            }
        }

        /// <summary>
        /// 写入单个16位有符号整数。
        /// </summary>
        /// <param name="address">寄存器地址。</param>
        /// <param name="value">short值。</param>
        public void Write(string address, short value)
        {
            WriteSingleRegister(address, unchecked((ushort)value));
        }

        /// <summary>
        /// 写入多个16位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">short数组。</param>
        public void Write(string address, short[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, ModbusRegisterCodec.FromInt16Array(values));
        }

        /// <summary>
        /// 写入单个16位无符号整数。
        /// </summary>
        /// <param name="address">寄存器地址。</param>
        /// <param name="value">ushort值。</param>
        public void Write(string address, ushort value)
        {
            WriteSingleRegister(address, value);
        }

        /// <summary>
        /// 写入多个16位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">ushort数组。</param>
        public void Write(string address, ushort[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, values);
        }

        /// <summary>
        /// 写入单个32位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="value">int值。</param>
        public void Write(string address, int value)
        {
            Write(address, new[] { value });
        }

        /// <summary>
        /// 写入多个32位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">int数组。</param>
        public void Write(string address, int[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, ModbusRegisterCodec.FromInt32Array(values));
        }

        /// <summary>
        /// 写入单个32位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="value">uint值。</param>
        public void Write(string address, uint value)
        {
            Write(address, new[] { value });
        }

        /// <summary>
        /// 写入多个32位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">uint数组。</param>
        public void Write(string address, uint[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, ModbusRegisterCodec.FromUInt32Array(values));
        }

        /// <summary>
        /// 写入单个单精度浮点数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="value">float值。</param>
        public void Write(string address, float value)
        {
            Write(address, new[] { value });
        }

        /// <summary>
        /// 写入多个单精度浮点数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">float数组。</param>
        public void Write(string address, float[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, ModbusRegisterCodec.FromFloatArray(values));
        }

        /// <summary>
        /// 写入单个64位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="value">long值。</param>
        public void Write(string address, long value)
        {
            Write(address, new[] { value });
        }

        /// <summary>
        /// 写入多个64位有符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">long数组。</param>
        public void Write(string address, long[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, ModbusRegisterCodec.FromInt64Array(values));
        }

        /// <summary>
        /// 写入单个64位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="value">ulong值。</param>
        public void Write(string address, ulong value)
        {
            Write(address, new[] { value });
        }

        /// <summary>
        /// 写入多个64位无符号整数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">ulong数组。</param>
        public void Write(string address, ulong[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, ModbusRegisterCodec.FromUInt64Array(values));
        }

        /// <summary>
        /// 写入单个双精度浮点数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="value">double值。</param>
        public void Write(string address, double value)
        {
            Write(address, new[] { value });
        }

        /// <summary>
        /// 写入多个双精度浮点数。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="values">double数组。</param>
        public void Write(string address, double[] values)
        {
            EnsureNotEmpty(values, nameof(values));
            WriteRegisters(address, ModbusRegisterCodec.FromDoubleArray(values));
        }

        /// <summary>
        /// 释放主站对象。
        /// </summary>
        protected void DisposeMaster()
        {
            if (Master != null)
                Master.Dispose();

            Master = null;
        }

        /// <summary>
        /// 触发连接状态改变事件。
        /// </summary>
        /// <param name="isConnected">当前是否已连接。</param>
        protected void RaiseConnectStatus(bool isConnected)
        {
            ConnectStatusEvent?.Invoke(this, isConnected);
        }

        /// <summary>
        /// 配置NModbus4传输层超时和重试策略。
        /// </summary>
        /// <param name="transport">NModbus4传输层。</param>
        protected void ConfigureTransport(global::Modbus.IO.ModbusTransport transport)
        {
            if (transport == null)
                return;

            transport.ReadTimeout = 5000;
            transport.WriteTimeout = 5000;
            transport.Retries = 0;
            transport.WaitToRetryMilliseconds = 50;
            transport.SlaveBusyUsesRetryCount = true;
        }

        /// <summary>
        /// 读取保持寄存器。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="length">读取寄存器数量。</param>
        /// <returns>保持寄存器数组。</returns>
        private ushort[] ReadHoldingRegisters(string address, ushort length)
        {
            lock (_syncRoot)
            {
                return RequireMaster().ReadHoldingRegisters(GetSlaveAddress(address), GetAddress(address), length);
            }
        }

        /// <summary>
        /// 写入单个保持寄存器。
        /// </summary>
        /// <param name="address">寄存器地址。</param>
        /// <param name="value">寄存器值。</param>
        private void WriteSingleRegister(string address, ushort value)
        {
            lock (_syncRoot)
            {
                RequireMaster().WriteSingleRegister(GetSlaveAddress(address), GetAddress(address), value);
            }
        }

        /// <summary>
        /// 写入多个保持寄存器。
        /// </summary>
        /// <param name="address">起始寄存器地址。</param>
        /// <param name="registers">寄存器数组。</param>
        private void WriteRegisters(string address, ushort[] registers)
        {
            EnsureNotEmpty(registers, nameof(registers));
            lock (_syncRoot)
            {
                RequireMaster().WriteMultipleRegisters(GetSlaveAddress(address), GetAddress(address), registers);
            }
        }

        /// <summary>
        /// 获取当前可用的NModbus4主站对象。
        /// </summary>
        /// <returns>NModbus4主站对象。</returns>
        private global::Modbus.Device.ModbusMaster RequireMaster()
        {
            if (Master == null || !IsConnect)
                throw new InvalidOperationException($"Modbus设备【{DevName}】未连接。");

            return Master;
        }

        /// <summary>
        /// 从地址字符串解析站号。
        /// </summary>
        /// <param name="address">地址字符串。</param>
        /// <returns>站号。</returns>
        private byte GetSlaveAddress(string address)
        {
            return ModbusRegisterCodec.GetSlaveAddress(ModbusParam, address);
        }

        /// <summary>
        /// 从地址字符串解析零基地址。
        /// </summary>
        /// <param name="address">地址字符串。</param>
        /// <returns>零基地址。</returns>
        private ushort GetAddress(string address)
        {
            return ModbusRegisterCodec.ParseAddress(address);
        }

        /// <summary>
        /// 校验数组写入值不能为空。
        /// </summary>
        /// <typeparam name="T">数组元素类型。</typeparam>
        /// <param name="values">待写入数组。</param>
        /// <param name="paramName">参数名称。</param>
        private static void EnsureNotEmpty<T>(T[] values, string paramName)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("写入数据不能为空。", paramName);
        }
    }
}
