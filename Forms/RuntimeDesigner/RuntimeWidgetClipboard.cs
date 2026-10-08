using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>可替换的组件剪贴板，仅传递文本；测试和设计器不依赖系统剪贴板状态。</summary>
    public interface IRuntimeWidgetClipboard
    {
        /// <summary>写入组件文本载荷。</summary>
        void Write(string payload);
        /// <summary>读取专用格式文本，格式不存在时返回空值。</summary>
        string Read();
    }

    /// <summary>专用Windows剪贴板文本格式，不调用对象序列化或对象反序列化。</summary>
    public sealed class RuntimeSystemWidgetClipboard : IRuntimeWidgetClipboard
    {
        /// <summary>组件专用格式，不读取或覆盖为普通文本格式。</summary>
        public const string FormatName = "TDJS.RuntimeDesigner.Component.v1";
        /// <summary>严格UTF8文本编码，拒绝损坏字节而不是替换后继续解析。</summary>
        private static readonly UTF8Encoding TextEncoding = new UTF8Encoding(false, true);

        /// <summary>以原生UTF8文本和结束零字节写入，系统成功接管后不再释放其内存。</summary>
        public void Write(string payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.IndexOf('\0') >= 0 || TextEncoding.GetByteCount(payload) > RuntimeLayoutSerializer.MaximumDocumentBytes)
                throw new InvalidDataException("组件剪贴板文本无效或超过16MB。");
            byte[] bytes = TextEncoding.GetBytes(payload + "\0");
            uint format = GetFormat();
            IntPtr memory = GlobalAlloc(0x0042, new UIntPtr((uint)bytes.Length));
            if (memory == IntPtr.Zero) throw new OutOfMemoryException("无法分配组件剪贴板内存。");
            var owner = new NativeWindow();
            bool opened = false;
            try
            {
                IntPtr pointer = GlobalLock(memory);
                if (pointer == IntPtr.Zero) throw ClipboardError();
                try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
                finally { GlobalUnlock(memory); }
                // SetClipboardData要求有效所有者；仅创建不可见消息窗口，不创建窗体或控件。
                owner.CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
                if (!OpenClipboard(owner.Handle)) throw ClipboardError();
                opened = true;
                if (!EmptyClipboard()) throw ClipboardError();
                if (SetClipboardData(format, memory) == IntPtr.Zero) throw ClipboardError();
                memory = IntPtr.Zero;
            }
            finally
            {
                if (opened) CloseClipboard();
                if (memory != IntPtr.Zero) GlobalFree(memory);
                if (owner.Handle != IntPtr.Zero) owner.DestroyHandle();
            }
        }

        /// <summary>只读取有界原生文本；即使外部程序放入对象格式，也绝不启动.NET对象反序列化。</summary>
        public string Read()
        {
            uint format = GetFormat();
            if (!IsClipboardFormatAvailable(format)) return null;
            if (!OpenClipboard(IntPtr.Zero)) throw ClipboardError();
            try
            {
                IntPtr memory = GetClipboardData(format);
                if (memory == IntPtr.Zero) return null;
                ulong size = GlobalSize(memory).ToUInt64();
                // 原生分配器可能补齐块大小，文本本身仍严格限制为16MB。
                if (size == 0 || size > RuntimeLayoutSerializer.MaximumDocumentBytes + 4096UL)
                    throw new InvalidDataException("组件剪贴板文本为空或超过16MB。");
                IntPtr pointer = GlobalLock(memory);
                if (pointer == IntPtr.Zero) throw ClipboardError();
                try
                {
                    var bytes = new byte[(int)Math.Min(size, (ulong)RuntimeLayoutSerializer.MaximumDocumentBytes + 1)];
                    Marshal.Copy(pointer, bytes, 0, bytes.Length);
                    int length = Array.IndexOf(bytes, (byte)0);
                    if (length < 0) throw new InvalidDataException("组件剪贴板文本没有合法结束标记。");
                    try { return TextEncoding.GetString(bytes, 0, length); }
                    catch (DecoderFallbackException ex) { throw new InvalidDataException("组件剪贴板不是有效UTF8文本。", ex); }
                }
                finally { GlobalUnlock(memory); }
            }
            finally { CloseClipboard(); }
        }

        /// <summary>按需注册格式，构造替身或读取类型不会接触系统剪贴板。</summary>
        private static uint GetFormat()
        {
            uint format = RegisterClipboardFormat(FormatName);
            if (format == 0) throw ClipboardError();
            return format;
        }
        /// <summary>将系统占用或调用错误交由画布显示，不静默改用普通文本。</summary>
        private static ExternalException ClipboardError() => new ExternalException("无法访问组件剪贴板，请稍后重试。", Marshal.GetLastWin32Error());
        /// <summary>注册专用文本格式。</summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint RegisterClipboardFormat(string name);
        /// <summary>判断专用格式是否存在。</summary>
        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);
        /// <summary>打开剪贴板；写入时传入有效消息窗口。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr owner);
        /// <summary>释放剪贴板访问权。</summary>
        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();
        /// <summary>用户明确复制时替换旧剪贴板内容。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();
        /// <summary>成功后将文本内存所有权交给系统。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
        /// <summary>取得原生数据句柄，不反序列化对象。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint format);
        /// <summary>分配可移动且清零的原生文本内存。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        /// <summary>锁定文本内存以复制字节。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr memory);
        /// <summary>释放文本内存锁。</summary>
        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr memory);
        /// <summary>释放未成功移交系统的内存。</summary>
        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalFree(IntPtr memory);
        /// <summary>读取内存块大小，在分配托管数组前验证上限。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr GlobalSize(IntPtr memory);
    }

    /// <summary>单组件剪贴板编码器，复用模板校验，绝不读写方案或设备。</summary>
    public static class RuntimeWidgetClipboardCodec
    {
        /// <summary>用源页面尺寸包装独立组件；编码前同样验证嵌入图片。</summary>
        public static string Encode(RuntimeLayoutDocument document, RuntimeLayoutElement element)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (element == null) throw new ArgumentNullException(nameof(element));
            return Encode(document, new[] { element });
        }

        /// <summary>把最多200个组件以源页面尺寸包装，保持顺序与坐标并复用图片完整校验。</summary>
        public static string Encode(RuntimeLayoutDocument document, IEnumerable<RuntimeLayoutElement> elements)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (elements == null) throw new ArgumentNullException(nameof(elements));
            var values = elements.Take(201).ToArray();
            if (values.Length == 0 || values.Length > 200 || values.Any(value => value == null))
                throw new InvalidDataException("组件剪贴板必须包含1至200个有效组件。");
            var wrapper = new RuntimeLayoutDocument
            {
                Version = document.Version, Name = document.Name, Width = document.Width,
                Height = document.Height, Background = document.Background
            };
            wrapper.Elements.AddRange(values.Select(value => value.Copy()));
            string payload = RuntimeLayoutSerializer.Serialize(wrapper);
            DecodeMany(payload);
            return payload;
        }

        /// <summary>严格限制16MB、深度及单组件数量；返回绑定路径均独立的快照。</summary>
        public static RuntimeLayoutElement Decode(string payload)
        {
            var elements = DecodeMany(payload);
            if (elements.Count != 1) throw new InvalidDataException("组件剪贴板必须且只能包含一个组件。");
            return elements[0];
        }

        /// <summary>读取独立组件集合，沿用16MB、深度、标识唯一性、绑定和图片校验。</summary>
        public static IReadOnlyList<RuntimeLayoutElement> DecodeMany(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || Encoding.UTF8.GetByteCount(payload) > RuntimeLayoutSerializer.MaximumDocumentBytes)
                throw new InvalidDataException("组件剪贴板为空或超过16MB。");
            try
            {
                // 不仅禁用多态反序列化，还明确拒绝所有层级的类型元数据。
                using (var text = new StringReader(payload))
                using (var reader = new JsonTextReader(text) { MaxDepth = 24, DateParseHandling = DateParseHandling.None })
                    while (reader.Read())
                        if (reader.TokenType == JsonToken.PropertyName && string.Equals((string)reader.Value, "$type", StringComparison.Ordinal))
                            throw new InvalidDataException("组件剪贴板不能包含类型元数据。");
                var wrapper = RuntimeLayoutSerializer.Deserialize(payload);
                if (wrapper.Elements.Count == 0) throw new InvalidDataException("组件剪贴板不能为空集合。");
                return wrapper.Elements.Select(value => value.Copy()).ToArray();
            }
            catch (JsonException ex) { throw new InvalidDataException("组件剪贴板格式不正确。", ex); }
        }
    }
}
