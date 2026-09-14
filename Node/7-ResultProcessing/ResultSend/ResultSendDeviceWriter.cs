using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HslCommunication;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend
{
    /// <summary>设备适配器注册表，通信对象扩展时增加工厂即可。</summary>
    public static class ResultSendWriterFactory
    {
        /// <summary>保护适配器注册及查找。</summary>
        private static readonly object Sync = new object();
        /// <summary>扩展适配器优先于内置适配器。</summary>
        private static readonly List<Func<IDevice, IResultSendWriter>> Factories = new List<Func<IDevice, IResultSendWriter>>();
        /// <summary>注册插件的设备写入工厂，不支持的设备返回null。</summary>
        public static void Register(Func<IDevice, IResultSendWriter> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (Sync) Factories.Insert(0, factory);
        }
        /// <summary>为已有设备创建轻量适配器，复用原连接。</summary>
        public static IResultSendWriter Create(IDevice device)
        {
            Func<IDevice, IResultSendWriter>[] factories;
            lock (Sync) factories = Factories.ToArray();
            foreach (var factory in factories)
            {
                var result = factory(device);
                if (result != null) return result;
            }
            if (device is IPlc plc) return new PlcResultSendWriter(plc);
            if (device is IModbus modbus) return new ModbusResultSendWriter(modbus);
            throw new NotSupportedException("请选择支持写入的PLC或Modbus设备。");
        }
    }

    /// <summary>PLC结果写入适配器。</summary>
    public sealed class PlcResultSendWriter : IResultSendWriter
    {
        /// <summary>共享的PLC连接。</summary>
        private readonly IPlc _plc;
        /// <summary>构造设备适配器。</summary>
        public PlcResultSendWriter(IPlc plc) { _plc = plc ?? throw new ArgumentNullException(nameof(plc)); }
        /// <summary>绑定设备。</summary>
        public IDevice Device => _plc;
        /// <summary>与PLC写入节点使用完全相同的端点键。</summary>
        public string EndpointKey => NodePlcWrite.CreateEndpointKey(_plc);
        /// <summary>检查基础地址和类型能力，不进行网络通信。</summary>
        public void Validate(string address, ResultSendType type, Array values)
        {
            if (string.IsNullOrWhiteSpace(address) || address != address.Trim() || address.IndexOfAny(new[] { ' ', '\r', '\n', '\t' }) >= 0)
                throw new InvalidOperationException("PLC地址为空或含有空白字符。");
            if (values == null || values.GetType().GetElementType() != ResultSendValueConverter.GetValueType(type))
                throw new InvalidOperationException("写入值类型与配置不一致。");
            if ((type == ResultSendType.Int16 || type == ResultSendType.UInt16 || type == ResultSendType.UInt32 || type == ResultSendType.Double) && !(_plc is IResultSendTypedPlc))
                throw new NotSupportedException("此PLC尚未实现该基础类型的写入能力。");
        }
        /// <summary>执行PLC写入，检查协议返回值；字符串按明确编码后的原始字节写入。</summary>
        public async Task WriteAsync(string address, ResultSendType type, Array values)
        {
            Validate(address, type, values);
            if (!_plc.IsConnect) throw new TDJS_Vision.ResourceManagement.DeviceSendNotStartedException("PLC设备未连接，本次写入尚未开始。");
            if (values.Length == 0) return;
            OperateResult result;
            switch (type)
            {
                case ResultSendType.Boolean: result = await _plc.WriteBoolAsync(address, (bool[])values).ConfigureAwait(false); break;
                case ResultSendType.Int32: result = await _plc.WriteIntAsync(address, (int[])values).ConfigureAwait(false); break;
                case ResultSendType.Single: result = await _plc.WriteFloatAsync(address, (float[])values).ConfigureAwait(false); break;
                case ResultSendType.String: result = await _plc.WriteBytesAsync(address, (byte[])values).ConfigureAwait(false); break;
                default: result = await ((IResultSendTypedPlc)_plc).WriteTypedValuesAsync(address, values).ConfigureAwait(false); break;
            }
            if (result == null || !result.IsSuccess) throw new InvalidOperationException("PLC写入失败：" + result?.Message);
        }
    }

    /// <summary>Modbus主站适配器，保持项目原有数字字序，字符串按高字节在前装入寄存器。</summary>
    public sealed class ModbusResultSendWriter : IResultSendWriter
    {
        /// <summary>共享的Modbus连接。</summary>
        private readonly IModbus _modbus;
        /// <summary>构造适配器。</summary>
        public ModbusResultSendWriter(IModbus modbus) { _modbus = modbus ?? throw new ArgumentNullException(nameof(modbus)); }
        /// <summary>绑定设备。</summary>
        public IDevice Device => _modbus;
        /// <summary>与现有Modbus写入节点共享物理连接键，并拒绝从站主动发送。</summary>
        public string EndpointKey => NodeModbusWrite.CreateEndpointKey(_modbus);
        /// <summary>按实际类型计算寄存器数量。</summary>
        public static int AddressSpan(ResultSendType type, int length)
        {
            switch (type)
            {
                case ResultSendType.String: return (length + 1) / 2;
                case ResultSendType.Int32: case ResultSendType.UInt32: case ResultSendType.Single: return checked(length * 2);
                case ResultSendType.Double: return checked(length * 4);
                default: return length;
            }
        }
        /// <summary>严格校验Modbus地址边界和转换结果。</summary>
        public void Validate(string address, ResultSendType type, Array values)
        {
            ushort start;
            if (!ushort.TryParse(address, NumberStyles.None, CultureInfo.InvariantCulture, out start))
                throw new InvalidOperationException("Modbus地址必须为0至65535的整数。");
            if (values == null || values.GetType().GetElementType() != ResultSendValueConverter.GetValueType(type))
                throw new InvalidOperationException("Modbus发送类型不一致。");
            if ((long)start + AddressSpan(type, values.Length) > 65536) throw new InvalidOperationException("连续写入超过Modbus地址范围。");
        }
        /// <summary>按协议单次长度限制分批发送，每批保持完整业务值，失败停止。</summary>
        public Task WriteAsync(string address, ResultSendType type, Array values)
        {
            Validate(address, type, values);
            if (!_modbus.IsConnect) throw new TDJS_Vision.ResourceManagement.DeviceSendNotStartedException("Modbus设备未连接，本次写入尚未开始。");
            // Modbus为同步接口，后台执行避免阻塞参数窗体；本次调用内按地址依次分批写入。
            return Task.Run(() =>
            {
                int start = int.Parse(address, CultureInfo.InvariantCulture);
                int limit = type == ResultSendType.Boolean ? 1968 : type == ResultSendType.String ? 246 : 123 / AddressSpan(type, 1);
                for (int offset = 0; offset < values.Length; offset += limit)
                {
                    int count = Math.Min(limit, values.Length - offset);
                    var chunk = Array.CreateInstance(values.GetType().GetElementType(), count);
                    Array.Copy(values, offset, chunk, 0, count);
                    string target = (start + AddressSpan(type, offset)).ToString(CultureInfo.InvariantCulture);
                    switch (type)
                    {
                        case ResultSendType.Boolean: _modbus.Write(target, (bool[])chunk); break;
                        case ResultSendType.Int16: _modbus.Write(target, (short[])chunk); break;
                        case ResultSendType.UInt16: _modbus.Write(target, (ushort[])chunk); break;
                        case ResultSendType.Int32: _modbus.Write(target, (int[])chunk); break;
                        case ResultSendType.UInt32: _modbus.Write(target, (uint[])chunk); break;
                        case ResultSendType.Single: _modbus.Write(target, (float[])chunk); break;
                        case ResultSendType.Double: _modbus.Write(target, (double[])chunk); break;
                        case ResultSendType.String:
                            byte[] bytes = (byte[])chunk;
                            var registers = new ushort[(bytes.Length + 1) / 2];
                            for (int index = 0; index < bytes.Length; index++) registers[index / 2] |= (ushort)(bytes[index] << (index % 2 == 0 ? 8 : 0));
                            _modbus.Write(target, registers);
                            break;
                    }
                }
            });
        }
    }
}
