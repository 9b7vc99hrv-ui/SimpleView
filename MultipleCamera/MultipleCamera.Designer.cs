namespace BasicDemo
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            // ch:取流标志位清零 | en:Reset flow flag bit

            // ch:关闭设备 | en:Close Device
            CloseDevBtn_Click(null, null);
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
            
        }


        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.CamDisplay1 = new System.Windows.Forms.PictureBox();
            this.CamDisplay2 = new System.Windows.Forms.PictureBox();
            this.CamDisplay3 = new System.Windows.Forms.PictureBox();
            this.CamDisplay4 = new System.Windows.Forms.PictureBox();
            this.NormalInfo = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.GetDevNumEdit = new System.Windows.Forms.TextBox();
            this.VersionInfoEdit = new System.Windows.Forms.TextBox();
            this.Label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.Cam4Cbx = new System.Windows.Forms.ComboBox();
            this.Cam3Cbx = new System.Windows.Forms.ComboBox();
            this.Cam2Cbx = new System.Windows.Forms.ComboBox();
            this.Cam1Cbx = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.GroupBox3 = new System.Windows.Forms.GroupBox();
            this.CloseDevBtn = new System.Windows.Forms.Button();
            this.OpenDevBtn = new System.Windows.Forms.Button();
            this.GroupBox4 = new System.Windows.Forms.GroupBox();
            this.SaveRaw = new System.Windows.Forms.Button();
            this.SoftwareTriggerBtn = new System.Windows.Forms.Button();
            this.StartGrabBtn = new System.Windows.Forms.Button();
            this.StopGrabBtn = new System.Windows.Forms.Button();
            this.CamCbx = new System.Windows.Forms.ComboBox();
            this.EnumDeviceBtn = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay4)).BeginInit();
            this.NormalInfo.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.GroupBox3.SuspendLayout();
            this.GroupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // CamDisplay1
            // 
            resources.ApplyResources(this.CamDisplay1, "CamDisplay1");
            this.CamDisplay1.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.CamDisplay1.Name = "CamDisplay1";
            this.CamDisplay1.TabStop = false;
            // 
            // CamDisplay2
            // 
            resources.ApplyResources(this.CamDisplay2, "CamDisplay2");
            this.CamDisplay2.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.CamDisplay2.Name = "CamDisplay2";
            this.CamDisplay2.TabStop = false;
            // 
            // CamDisplay3
            // 
            resources.ApplyResources(this.CamDisplay3, "CamDisplay3");
            this.CamDisplay3.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.CamDisplay3.Name = "CamDisplay3";
            this.CamDisplay3.TabStop = false;
            // 
            // CamDisplay4
            // 
            resources.ApplyResources(this.CamDisplay4, "CamDisplay4");
            this.CamDisplay4.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.CamDisplay4.Name = "CamDisplay4";
            this.CamDisplay4.TabStop = false;
            // 
            // NormalInfo
            // 
            resources.ApplyResources(this.NormalInfo, "NormalInfo");
            this.NormalInfo.Controls.Add(this.label6);
            this.NormalInfo.Controls.Add(this.GetDevNumEdit);
            this.NormalInfo.Controls.Add(this.VersionInfoEdit);
            this.NormalInfo.Controls.Add(this.Label1);
            this.NormalInfo.Name = "NormalInfo";
            this.NormalInfo.TabStop = false;
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // GetDevNumEdit
            // 
            resources.ApplyResources(this.GetDevNumEdit, "GetDevNumEdit");
            this.GetDevNumEdit.Name = "GetDevNumEdit";
            this.GetDevNumEdit.ReadOnly = true;
            // 
            // VersionInfoEdit
            // 
            resources.ApplyResources(this.VersionInfoEdit, "VersionInfoEdit");
            this.VersionInfoEdit.Name = "VersionInfoEdit";
            this.VersionInfoEdit.ReadOnly = true;
            // 
            // Label1
            // 
            resources.ApplyResources(this.Label1, "Label1");
            this.Label1.Name = "Label1";
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Controls.Add(this.Cam4Cbx);
            this.groupBox1.Controls.Add(this.Cam3Cbx);
            this.groupBox1.Controls.Add(this.Cam2Cbx);
            this.groupBox1.Controls.Add(this.Cam1Cbx);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // Cam4Cbx
            // 
            resources.ApplyResources(this.Cam4Cbx, "Cam4Cbx");
            this.Cam4Cbx.FormattingEnabled = true;
            this.Cam4Cbx.Name = "Cam4Cbx";
            // 
            // Cam3Cbx
            // 
            resources.ApplyResources(this.Cam3Cbx, "Cam3Cbx");
            this.Cam3Cbx.FormattingEnabled = true;
            this.Cam3Cbx.Name = "Cam3Cbx";
            // 
            // Cam2Cbx
            // 
            resources.ApplyResources(this.Cam2Cbx, "Cam2Cbx");
            this.Cam2Cbx.FormattingEnabled = true;
            this.Cam2Cbx.Name = "Cam2Cbx";
            // 
            // Cam1Cbx
            // 
            resources.ApplyResources(this.Cam1Cbx, "Cam1Cbx");
            this.Cam1Cbx.FormattingEnabled = true;
            this.Cam1Cbx.Name = "Cam1Cbx";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // GroupBox3
            // 
            resources.ApplyResources(this.GroupBox3, "GroupBox3");
            this.GroupBox3.Controls.Add(this.CloseDevBtn);
            this.GroupBox3.Controls.Add(this.OpenDevBtn);
            this.GroupBox3.Name = "GroupBox3";
            this.GroupBox3.TabStop = false;
            // 
            // CloseDevBtn
            // 
            resources.ApplyResources(this.CloseDevBtn, "CloseDevBtn");
            this.CloseDevBtn.Name = "CloseDevBtn";
            this.CloseDevBtn.UseVisualStyleBackColor = true;
            this.CloseDevBtn.Click += new System.EventHandler(this.CloseDevBtn_Click);
            // 
            // OpenDevBtn
            // 
            resources.ApplyResources(this.OpenDevBtn, "OpenDevBtn");
            this.OpenDevBtn.Name = "OpenDevBtn";
            this.OpenDevBtn.UseVisualStyleBackColor = true;
            this.OpenDevBtn.Click += new System.EventHandler(this.OpenDevBtn_Click);
            // 
            // GroupBox4
            // 
            resources.ApplyResources(this.GroupBox4, "GroupBox4");
            this.GroupBox4.Controls.Add(this.SaveRaw);
            this.GroupBox4.Controls.Add(this.SoftwareTriggerBtn);
            this.GroupBox4.Controls.Add(this.StartGrabBtn);
            this.GroupBox4.Controls.Add(this.StopGrabBtn);
            this.GroupBox4.Name = "GroupBox4";
            this.GroupBox4.TabStop = false;
            // 
            // SaveRaw
            // 
            resources.ApplyResources(this.SaveRaw, "SaveRaw");
            this.SaveRaw.Name = "SaveRaw";
            this.SaveRaw.UseVisualStyleBackColor = true;
            this.SaveRaw.Click += new System.EventHandler(this.SaveRaw_Click);
            // 
            // SoftwareTriggerBtn
            // 
            resources.ApplyResources(this.SoftwareTriggerBtn, "SoftwareTriggerBtn");
            this.SoftwareTriggerBtn.Name = "SoftwareTriggerBtn";
            this.SoftwareTriggerBtn.UseVisualStyleBackColor = true;
            this.SoftwareTriggerBtn.Click += new System.EventHandler(this.SoftwareTriggerBtn_Click);
            // 
            // StartGrabBtn
            // 
            resources.ApplyResources(this.StartGrabBtn, "StartGrabBtn");
            this.StartGrabBtn.Name = "StartGrabBtn";
            this.StartGrabBtn.UseVisualStyleBackColor = true;
            this.StartGrabBtn.Click += new System.EventHandler(this.StartGrabBtn_Click);
            // 
            // StopGrabBtn
            // 
            resources.ApplyResources(this.StopGrabBtn, "StopGrabBtn");
            this.StopGrabBtn.Name = "StopGrabBtn";
            this.StopGrabBtn.UseVisualStyleBackColor = true;
            this.StopGrabBtn.Click += new System.EventHandler(this.StopGrabBtn_Click);
            // 
            // CamCbx
            // 
            resources.ApplyResources(this.CamCbx, "CamCbx");
            this.CamCbx.FormattingEnabled = true;
            this.CamCbx.Name = "CamCbx";
            // 
            // EnumDeviceBtn
            // 
            resources.ApplyResources(this.EnumDeviceBtn, "EnumDeviceBtn");
            this.EnumDeviceBtn.Name = "EnumDeviceBtn";
            this.EnumDeviceBtn.UseVisualStyleBackColor = true;
            this.EnumDeviceBtn.Click += new System.EventHandler(this.EnumDeviceBtn_Click);
            // 
            // Form1
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.EnumDeviceBtn);
            this.Controls.Add(this.CamCbx);
            this.Controls.Add(this.GroupBox4);
            this.Controls.Add(this.GroupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.NormalInfo);
            this.Controls.Add(this.CamDisplay4);
            this.Controls.Add(this.CamDisplay3);
            this.Controls.Add(this.CamDisplay2);
            this.Controls.Add(this.CamDisplay1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.CamDisplay4)).EndInit();
            this.NormalInfo.ResumeLayout(false);
            this.NormalInfo.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.GroupBox3.ResumeLayout(false);
            this.GroupBox4.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox CamDisplay1;
        private System.Windows.Forms.PictureBox CamDisplay2;
        private System.Windows.Forms.PictureBox CamDisplay3;
        private System.Windows.Forms.PictureBox CamDisplay4;
        internal System.Windows.Forms.GroupBox NormalInfo;
        internal System.Windows.Forms.TextBox GetDevNumEdit;
        internal System.Windows.Forms.TextBox VersionInfoEdit;
        internal System.Windows.Forms.Label Label1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        internal System.Windows.Forms.GroupBox GroupBox3;
        internal System.Windows.Forms.Button CloseDevBtn;
        internal System.Windows.Forms.Button OpenDevBtn;
        internal System.Windows.Forms.GroupBox GroupBox4;
        internal System.Windows.Forms.Button SaveRaw;
        internal System.Windows.Forms.Button SoftwareTriggerBtn;
        internal System.Windows.Forms.Button StartGrabBtn;
        internal System.Windows.Forms.Button StopGrabBtn;
        internal System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox CamCbx;
        private System.Windows.Forms.Button EnumDeviceBtn;
        private System.Windows.Forms.ComboBox Cam1Cbx;
        private System.Windows.Forms.ComboBox Cam4Cbx;
        private System.Windows.Forms.ComboBox Cam3Cbx;
        private System.Windows.Forms.ComboBox Cam2Cbx;
    }
}

