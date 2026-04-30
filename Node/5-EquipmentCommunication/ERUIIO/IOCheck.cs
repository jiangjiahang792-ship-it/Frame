using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO
{
    public partial class IOCheck : UserControl
    {
        public IOCheck()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 获取IO选中状态
        /// </summary>
        /// <returns></returns>
        public bool[] GetValues()
        {
            return new bool[8] {checkBox1.Checked, checkBox2.Checked, checkBox3.Checked, checkBox4.Checked,
                                checkBox5.Checked, checkBox6.Checked, checkBox7.Checked, checkBox8.Checked};
        }
        /// <summary>
        /// 设置IO选中状态
        /// </summary>
        /// <param name="value"></param>
        public void SetIOCheckedStatus(bool[] value)
        {
            checkBox1.Checked = value[0];
            checkBox2.Checked = value[1];
            checkBox3.Checked = value[2];
            checkBox4.Checked = value[3];
            checkBox5.Checked = value[4];
            checkBox6.Checked = value[5];
            checkBox7.Checked = value[6];
            checkBox8.Checked = value[7];
        }
    }
}
