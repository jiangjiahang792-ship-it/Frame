using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.COM;
using TDJS_Vision.Device.Light;

namespace TDJS_Vision.Forms.COMAdd
{
    public partial class COMParamsShowControl : UserControl
    {
        public COMParamsShowControl(SerialPortConfig parms)
        {
            InitializeComponent();
            this.labelCom.Text = parms.PortName;
            this.labelBaudRate.Text = parms.BaudRate.ToString();
            this.labelDataBits.Text = parms.DataBits.ToString();
            this.labelStopBits.Text = parms.StopBits == System.IO.Ports.StopBits.One ? "1" : parms.StopBits == System.IO.Ports.StopBits.OnePointFive ? "1.5" : "2";
            this.labelPairty.Text = parms.Parity == System.IO.Ports.Parity.None ? "无" : parms.Parity == System.IO.Ports.Parity.Odd ? "奇" : "偶";
        }
    }
}
