using System;
using System.Threading;

namespace Logger
{
    /// <summary>仅随当前异步调用链流动的日志来源，不读取方案单例或按编号推断归属。</summary>
    public static class RuntimeLogContext
    {
        /// <summary>当前分支的不可变来源帧，并行分支不会共享可写路径。</summary>
        private static readonly AsyncLocal<Frame> current = new AsyncLocal<Frame>();

        /// <summary>供入队前捕获的当前来源，后台写文件线程不读取此值。</summary>
        internal static Frame Current => current.Value;

        /// <summary>进入根流程；同一流程包装入口复用来源，其它根流程重新建立身份。</summary>
        public static IDisposable BeginProcess(object process, string source)
        {
            var previous = current.Value;
            return Push(previous != null && ReferenceEquals(previous.Process, process)
                ? new Frame(previous.Root, process, previous.Source)
                : new Frame(process, process, source));
        }

        /// <summary>进入节点路径，调用者已由流程入口建立根实例。</summary>
        public static IDisposable BeginNode(object process, string source)
        {
            var previous = current.Value;
            return Push(previous != null && ReferenceEquals(previous.Process, process)
                ? new Frame(previous.Root, process, previous.Source + " / " + Limit(source))
                : new Frame(process, process, source));
        }

        /// <summary>只有组合模块显式继承父根流程，内部流程即使重复编号也不会变成其它根流程。</summary>
        public static IDisposable BeginComposite(object childProcess, string source)
        {
            var previous = current.Value;
            return Push(previous == null ? new Frame(childProcess, childProcess, source)
                : new Frame(previous.Root, childProcess, previous.Source));
        }

        /// <summary>同步压栈，确保调用者后续await与Task.Run都继承新来源。</summary>
        private static IDisposable Push(Frame frame)
        {
            var scope = new Scope(current.Value, frame);
            current.Value = frame;
            return scope;
        }

        /// <summary>限制来源字符串常驻长度，保留名称中的完整Unicode代理对。</summary>
        internal static string Limit(string text, int maximum = 1024)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= maximum) return text;
            int length = maximum - 1;
            if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
            return text.Substring(0, length) + "…";
        }

        /// <summary>入队前固定的根实例、当前流程实例及来源文本。</summary>
        internal sealed class Frame
        {
            /// <summary>最外层配置流程实例。</summary>
            internal readonly object Root;
            /// <summary>当前正在执行的流程实例。</summary>
            internal readonly object Process;
            /// <summary>已限制长度的来源路径。</summary>
            internal readonly string Source;
            /// <summary>冻结来源，不保留节点或页面对象。</summary>
            internal Frame(object root, object process, string source)
            { Root = root; Process = process; Source = Limit(source); }
        }

        /// <summary>仅恢复当前异步分支，不向父分支或并行任务回写。</summary>
        private sealed class Scope : IDisposable
        {
            /// <summary>进入前的来源。</summary>
            private readonly Frame previous;
            /// <summary>本作用域安装的来源。</summary>
            private readonly Frame installed;
            /// <summary>保留成对恢复所需的两个不可变引用。</summary>
            internal Scope(Frame previous, Frame installed) { this.previous = previous; this.installed = installed; }
            /// <summary>允许同一作用域重复释放，但不覆盖其它活动作用域。</summary>
            public void Dispose() { if (ReferenceEquals(current.Value, installed)) current.Value = previous; }
        }
    }
}
