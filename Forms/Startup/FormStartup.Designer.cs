namespace TDJS_Vision.Forms.Startup
{
    partial class FormStartup
    {
        /// <summary>
        /// 设计器组件容器。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 释放窗体占用的托管资源。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 初始化启动窗体中的全部可视控件。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelError = new System.Windows.Forms.Panel();
            this.buttonExit = new System.Windows.Forms.Button();
            this.buttonRetry = new System.Windows.Forms.Button();
            this.labelError = new System.Windows.Forms.Label();
            this.labelErrorTitle = new System.Windows.Forms.Label();
            this.labelPercent = new System.Windows.Forms.Label();
            this.panelProgressTrack = new System.Windows.Forms.Panel();
            this.panelProgressValue = new System.Windows.Forms.Panel();
            this.labelDetail = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelSubtitle = new System.Windows.Forms.Label();
            this.labelTitle = new System.Windows.Forms.Label();
            this.panelBlinkMask = new System.Windows.Forms.Panel();
            this.panelAntennaGlow = new System.Windows.Forms.Panel();
            this.pictureBoxMascot = new System.Windows.Forms.PictureBox();
            this.timerAnimation = new System.Windows.Forms.Timer(this.components);
            this.panelMain.SuspendLayout();
            this.panelError.SuspendLayout();
            this.panelProgressTrack.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxMascot)).BeginInit();
            this.SuspendLayout();
            // 
            // panelMain
            // 
            this.panelMain.BackColor = System.Drawing.Color.Transparent;
            this.panelMain.Controls.Add(this.panelError);
            this.panelMain.Controls.Add(this.labelPercent);
            this.panelMain.Controls.Add(this.panelProgressTrack);
            this.panelMain.Controls.Add(this.labelDetail);
            this.panelMain.Controls.Add(this.labelStatus);
            this.panelMain.Controls.Add(this.labelSubtitle);
            this.panelMain.Controls.Add(this.labelTitle);
            this.panelMain.Controls.Add(this.panelBlinkMask);
            this.panelMain.Controls.Add(this.panelAntennaGlow);
            this.panelMain.Controls.Add(this.pictureBoxMascot);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(720, 430);
            this.panelMain.TabIndex = 0;
            // 
            // panelError
            // 
            this.panelError.BackColor = System.Drawing.Color.FromArgb(255, 247, 247);
            this.panelError.Controls.Add(this.buttonExit);
            this.panelError.Controls.Add(this.buttonRetry);
            this.panelError.Controls.Add(this.labelError);
            this.panelError.Controls.Add(this.labelErrorTitle);
            this.panelError.Location = new System.Drawing.Point(190, 219);
            this.panelError.Name = "panelError";
            this.panelError.Size = new System.Drawing.Size(340, 172);
            this.panelError.TabIndex = 9;
            this.panelError.Visible = false;
            // 
            // buttonExit
            // 
            this.buttonExit.BackColor = System.Drawing.Color.White;
            this.buttonExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonExit.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.buttonExit.ForeColor = System.Drawing.Color.FromArgb(70, 79, 92);
            this.buttonExit.Location = new System.Drawing.Point(178, 119);
            this.buttonExit.Name = "buttonExit";
            this.buttonExit.Size = new System.Drawing.Size(130, 36);
            this.buttonExit.TabIndex = 3;
            this.buttonExit.Text = "退出软件";
            this.buttonExit.UseVisualStyleBackColor = false;
            this.buttonExit.Click += new System.EventHandler(this.ButtonExit_Click);
            // 
            // buttonRetry
            // 
            this.buttonRetry.BackColor = System.Drawing.Color.FromArgb(22, 143, 210);
            this.buttonRetry.FlatAppearance.BorderSize = 0;
            this.buttonRetry.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonRetry.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.buttonRetry.ForeColor = System.Drawing.Color.White;
            this.buttonRetry.Location = new System.Drawing.Point(32, 119);
            this.buttonRetry.Name = "buttonRetry";
            this.buttonRetry.Size = new System.Drawing.Size(130, 36);
            this.buttonRetry.TabIndex = 2;
            this.buttonRetry.Text = "重试";
            this.buttonRetry.UseVisualStyleBackColor = false;
            this.buttonRetry.Click += new System.EventHandler(this.ButtonRetry_Click);
            // 
            // labelError
            // 
            this.labelError.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.labelError.ForeColor = System.Drawing.Color.FromArgb(176, 54, 54);
            this.labelError.Location = new System.Drawing.Point(29, 45);
            this.labelError.Name = "labelError";
            this.labelError.Size = new System.Drawing.Size(279, 61);
            this.labelError.TabIndex = 1;
            this.labelError.Text = "启动初始化失败。";
            this.labelError.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // labelErrorTitle
            // 
            this.labelErrorTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.labelErrorTitle.ForeColor = System.Drawing.Color.FromArgb(196, 52, 52);
            this.labelErrorTitle.Location = new System.Drawing.Point(20, 14);
            this.labelErrorTitle.Name = "labelErrorTitle";
            this.labelErrorTitle.Size = new System.Drawing.Size(300, 28);
            this.labelErrorTitle.TabIndex = 0;
            this.labelErrorTitle.Text = "软件启动失败";
            this.labelErrorTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelPercent
            // 
            this.labelPercent.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.labelPercent.ForeColor = System.Drawing.Color.FromArgb(22, 143, 210);
            this.labelPercent.Location = new System.Drawing.Point(594, 373);
            this.labelPercent.Name = "labelPercent";
            this.labelPercent.Size = new System.Drawing.Size(58, 22);
            this.labelPercent.TabIndex = 8;
            this.labelPercent.Text = "0%";
            this.labelPercent.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // panelProgressTrack
            // 
            this.panelProgressTrack.BackColor = System.Drawing.Color.FromArgb(210, 221, 235);
            this.panelProgressTrack.Controls.Add(this.panelProgressValue);
            this.panelProgressTrack.Location = new System.Drawing.Point(68, 403);
            this.panelProgressTrack.Name = "panelProgressTrack";
            this.panelProgressTrack.Size = new System.Drawing.Size(584, 6);
            this.panelProgressTrack.TabIndex = 7;
            // 
            // panelProgressValue
            // 
            this.panelProgressValue.BackColor = System.Drawing.Color.FromArgb(22, 143, 210);
            this.panelProgressValue.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelProgressValue.Location = new System.Drawing.Point(0, 0);
            this.panelProgressValue.Name = "panelProgressValue";
            this.panelProgressValue.Size = new System.Drawing.Size(1, 6);
            this.panelProgressValue.TabIndex = 0;
            this.panelProgressValue.Visible = false;
            // 
            // labelDetail
            // 
            this.labelDetail.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.labelDetail.ForeColor = System.Drawing.Color.FromArgb(103, 113, 128);
            this.labelDetail.Location = new System.Drawing.Point(68, 373);
            this.labelDetail.Name = "labelDetail";
            this.labelDetail.Size = new System.Drawing.Size(514, 22);
            this.labelDetail.TabIndex = 6;
            this.labelDetail.Text = "正在准备运行环境";
            this.labelDetail.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelStatus
            // 
            this.labelStatus.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.labelStatus.ForeColor = System.Drawing.Color.FromArgb(35, 44, 57);
            this.labelStatus.Location = new System.Drawing.Point(68, 339);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(584, 31);
            this.labelStatus.TabIndex = 5;
            this.labelStatus.Text = "正在启动软件……";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelSubtitle
            // 
            this.labelSubtitle.Font = new System.Drawing.Font("微软雅黑", 9.5F);
            this.labelSubtitle.ForeColor = System.Drawing.Color.FromArgb(103, 113, 128);
            this.labelSubtitle.Location = new System.Drawing.Point(210, 56);
            this.labelSubtitle.Name = "labelSubtitle";
            this.labelSubtitle.Size = new System.Drawing.Size(300, 27);
            this.labelSubtitle.TabIndex = 4;
            this.labelSubtitle.Text = "智能视觉 · 稳定运行 · 高效检测";
            this.labelSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelTitle
            // 
            this.labelTitle.Font = new System.Drawing.Font("微软雅黑", 19F, System.Drawing.FontStyle.Bold);
            this.labelTitle.ForeColor = System.Drawing.Color.FromArgb(25, 34, 46);
            this.labelTitle.Location = new System.Drawing.Point(110, 16);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(500, 42);
            this.labelTitle.TabIndex = 3;
            this.labelTitle.Text = "TDJS-Vision 工业视觉平台";
            this.labelTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panelBlinkMask
            // 
            this.panelBlinkMask.BackColor = System.Drawing.Color.FromArgb(17, 40, 62);
            this.panelBlinkMask.Location = new System.Drawing.Point(344, 199);
            this.panelBlinkMask.Name = "panelBlinkMask";
            this.panelBlinkMask.Size = new System.Drawing.Size(32, 4);
            this.panelBlinkMask.TabIndex = 2;
            this.panelBlinkMask.Visible = false;
            // 
            // panelAntennaGlow
            // 
            this.panelAntennaGlow.BackColor = System.Drawing.Color.FromArgb(239, 75, 67);
            this.panelAntennaGlow.Location = new System.Drawing.Point(354, 128);
            this.panelAntennaGlow.Name = "panelAntennaGlow";
            this.panelAntennaGlow.Size = new System.Drawing.Size(12, 12);
            this.panelAntennaGlow.TabIndex = 1;
            // 
            // pictureBoxMascot
            // 
            this.pictureBoxMascot.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxMascot.Image = global::TDJS_Vision.Properties.Resources.StartupMascot;
            this.pictureBoxMascot.Location = new System.Drawing.Point(260, 83);
            this.pictureBoxMascot.Name = "pictureBoxMascot";
            this.pictureBoxMascot.Size = new System.Drawing.Size(200, 244);
            this.pictureBoxMascot.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxMascot.TabIndex = 0;
            this.pictureBoxMascot.TabStop = false;
            // 
            // timerAnimation
            // 
            this.timerAnimation.Enabled = true;
            this.timerAnimation.Interval = 33;
            this.timerAnimation.Tick += new System.EventHandler(this.TimerAnimation_Tick);
            // 
            // FormStartup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(245, 249, 255);
            this.ClientSize = new System.Drawing.Size(720, 430);
            this.ControlBox = false;
            this.Controls.Add(this.panelMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormStartup";
            this.ShowIcon = false;
            this.ShowInTaskbar = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TDJS-Vision 正在启动";
            this.TopMost = true;
            this.panelMain.ResumeLayout(false);
            this.panelError.ResumeLayout(false);
            this.panelProgressTrack.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxMascot)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        /// <summary>
        /// 启动页主容器。
        /// </summary>
        private System.Windows.Forms.Panel panelMain;

        /// <summary>
        /// 机器人吉祥物图片。
        /// </summary>
        private System.Windows.Forms.PictureBox pictureBoxMascot;

        /// <summary>
        /// 软件标题标签。
        /// </summary>
        private System.Windows.Forms.Label labelTitle;

        /// <summary>
        /// 品牌副标题标签。
        /// </summary>
        private System.Windows.Forms.Label labelSubtitle;

        /// <summary>
        /// 当前启动阶段标签。
        /// </summary>
        private System.Windows.Forms.Label labelStatus;

        /// <summary>
        /// 当前加载对象明细标签。
        /// </summary>
        private System.Windows.Forms.Label labelDetail;

        /// <summary>
        /// 进度条轨道面板。
        /// </summary>
        private System.Windows.Forms.Panel panelProgressTrack;

        /// <summary>
        /// 进度条填充面板。
        /// </summary>
        private System.Windows.Forms.Panel panelProgressValue;

        /// <summary>
        /// 百分比标签。
        /// </summary>
        private System.Windows.Forms.Label labelPercent;

        /// <summary>
        /// 启动失败信息面板。
        /// </summary>
        private System.Windows.Forms.Panel panelError;

        /// <summary>
        /// 启动失败标题标签。
        /// </summary>
        private System.Windows.Forms.Label labelErrorTitle;

        /// <summary>
        /// 启动失败详细信息标签。
        /// </summary>
        private System.Windows.Forms.Label labelError;

        /// <summary>
        /// 重试启动按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonRetry;

        /// <summary>
        /// 退出软件按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonExit;

        /// <summary>
        /// 机器人眨眼遮罩。
        /// </summary>
        private System.Windows.Forms.Panel panelBlinkMask;

        /// <summary>
        /// 机器人天线呼吸灯。
        /// </summary>
        private System.Windows.Forms.Panel panelAntennaGlow;

        /// <summary>
        /// 启动动画定时器。
        /// </summary>
        private System.Windows.Forms.Timer timerAnimation;
    }
}
