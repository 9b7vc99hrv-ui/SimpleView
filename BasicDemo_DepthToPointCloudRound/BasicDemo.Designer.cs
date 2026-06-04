using System;
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
            if (m_bGrabbing == true)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
            }

            // ch:关闭设备 | en:Close Device
            Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandle);
            Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandle);
            m_DevHandle = IntPtr.Zero;

            Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandleSecond);
            Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandleSecond);
            m_DevHandleSecond = IntPtr.Zero;

            Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandleThird);
            Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandleThird);
            m_DevHandleThird = IntPtr.Zero;

            Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandleFourth);
            Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandleFourth);
            m_DevHandleFourth = IntPtr.Zero;
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
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.groupBoxGrabing = new System.Windows.Forms.GroupBox();
            this.bnStopGrab = new System.Windows.Forms.Button();
            this.bnStartGrab = new System.Windows.Forms.Button();
            this.groupBoxSaveImg = new System.Windows.Forms.GroupBox();
            this.bnSaveJPG = new System.Windows.Forms.Button();
            this.bnSaveBMP = new System.Windows.Forms.Button();
            this.bnSaveOBJ = new System.Windows.Forms.Button();
            this.bnSaveCSV = new System.Windows.Forms.Button();
            this.bnSaveRAW = new System.Windows.Forms.Button();
            this.bnSavePLY = new System.Windows.Forms.Button();
            this.bnSaveTIFF = new System.Windows.Forms.Button();
            this.groupBoxSwitch = new System.Windows.Forms.GroupBox();
            this.bnEnum = new System.Windows.Forms.Button();
            this.bnClose = new System.Windows.Forms.Button();
            this.bnOpen = new System.Windows.Forms.Button();
            this.cbDeviceList = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbExposure = new System.Windows.Forms.TextBox();
            this.bnSetParam = new System.Windows.Forms.Button();
            this.bnGetParam = new System.Windows.Forms.Button();
            this.cbDeviceList2 = new System.Windows.Forms.ComboBox();
            this.cbDeviceList3 = new System.Windows.Forms.ComboBox();
            this.cbDeviceList4 = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBoxGrabing.SuspendLayout();
            this.groupBoxSaveImg.SuspendLayout();
            this.groupBoxSwitch.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            // 
            // groupBoxGrabing
            // 
            resources.ApplyResources(this.groupBoxGrabing, "groupBoxGrabing");
            this.groupBoxGrabing.Controls.Add(this.bnStopGrab);
            this.groupBoxGrabing.Controls.Add(this.bnStartGrab);
            this.groupBoxGrabing.Name = "groupBoxGrabing";
            this.groupBoxGrabing.TabStop = false;
            // 
            // bnStopGrab
            // 
            resources.ApplyResources(this.bnStopGrab, "bnStopGrab");
            this.bnStopGrab.Name = "bnStopGrab";
            this.bnStopGrab.UseVisualStyleBackColor = true;
            this.bnStopGrab.Click += new System.EventHandler(this.bnStopGrab_Click);
            // 
            // bnStartGrab
            // 
            resources.ApplyResources(this.bnStartGrab, "bnStartGrab");
            this.bnStartGrab.Name = "bnStartGrab";
            this.bnStartGrab.UseVisualStyleBackColor = true;
            this.bnStartGrab.Click += new System.EventHandler(this.bnStartGrab_Click);
            // 
            // groupBoxSaveImg
            // 
            resources.ApplyResources(this.groupBoxSaveImg, "groupBoxSaveImg");
            this.groupBoxSaveImg.Controls.Add(this.bnSaveJPG);
            this.groupBoxSaveImg.Controls.Add(this.bnSaveBMP);
            this.groupBoxSaveImg.Controls.Add(this.bnSaveOBJ);
            this.groupBoxSaveImg.Controls.Add(this.bnSaveCSV);
            this.groupBoxSaveImg.Controls.Add(this.bnSaveRAW);
            this.groupBoxSaveImg.Controls.Add(this.bnSavePLY);
            this.groupBoxSaveImg.Controls.Add(this.bnSaveTIFF);
            this.groupBoxSaveImg.Name = "groupBoxSaveImg";
            this.groupBoxSaveImg.TabStop = false;
            // 
            // bnSaveJPG
            // 
            resources.ApplyResources(this.bnSaveJPG, "bnSaveJPG");
            this.bnSaveJPG.Name = "bnSaveJPG";
            this.bnSaveJPG.UseVisualStyleBackColor = true;
            this.bnSaveJPG.Click += new System.EventHandler(this.bnSaveJPG_Click);
            // 
            // bnSaveBMP
            // 
            resources.ApplyResources(this.bnSaveBMP, "bnSaveBMP");
            this.bnSaveBMP.Name = "bnSaveBMP";
            this.bnSaveBMP.UseVisualStyleBackColor = true;
            this.bnSaveBMP.Click += new System.EventHandler(this.bnSaveBMP_Click);
            // 
            // bnSaveOBJ
            // 
            resources.ApplyResources(this.bnSaveOBJ, "bnSaveOBJ");
            this.bnSaveOBJ.Name = "bnSaveOBJ";
            this.bnSaveOBJ.UseVisualStyleBackColor = true;
            this.bnSaveOBJ.Click += new System.EventHandler(this.bnSaveOBJ_Click);
            // 
            // bnSaveCSV
            // 
            resources.ApplyResources(this.bnSaveCSV, "bnSaveCSV");
            this.bnSaveCSV.Name = "bnSaveCSV";
            this.bnSaveCSV.UseVisualStyleBackColor = true;
            this.bnSaveCSV.Click += new System.EventHandler(this.bnSaveCSV_Click);
            // 
            // bnSaveRAW
            // 
            resources.ApplyResources(this.bnSaveRAW, "bnSaveRAW");
            this.bnSaveRAW.Name = "bnSaveRAW";
            this.bnSaveRAW.UseVisualStyleBackColor = true;
            this.bnSaveRAW.Click += new System.EventHandler(this.bnSaveRAW_Click);
            // 
            // bnSavePLY
            // 
            resources.ApplyResources(this.bnSavePLY, "bnSavePLY");
            this.bnSavePLY.Name = "bnSavePLY";
            this.bnSavePLY.UseVisualStyleBackColor = true;
            this.bnSavePLY.Click += new System.EventHandler(this.bnSavePLY_Click);
            // 
            // bnSaveTIFF
            // 
            resources.ApplyResources(this.bnSaveTIFF, "bnSaveTIFF");
            this.bnSaveTIFF.Name = "bnSaveTIFF";
            this.bnSaveTIFF.UseVisualStyleBackColor = true;
            this.bnSaveTIFF.Click += new System.EventHandler(this.bnSaveTIFF_Click);
            // 
            // groupBoxSwitch
            // 
            resources.ApplyResources(this.groupBoxSwitch, "groupBoxSwitch");
            this.groupBoxSwitch.Controls.Add(this.bnEnum);
            this.groupBoxSwitch.Controls.Add(this.bnClose);
            this.groupBoxSwitch.Controls.Add(this.bnOpen);
            this.groupBoxSwitch.Name = "groupBoxSwitch";
            this.groupBoxSwitch.TabStop = false;
            // 
            // bnEnum
            // 
            resources.ApplyResources(this.bnEnum, "bnEnum");
            this.bnEnum.Name = "bnEnum";
            this.bnEnum.UseVisualStyleBackColor = true;
            this.bnEnum.Click += new System.EventHandler(this.bnEnum_Click);
            // 
            // bnClose
            // 
            resources.ApplyResources(this.bnClose, "bnClose");
            this.bnClose.Name = "bnClose";
            this.bnClose.UseVisualStyleBackColor = true;
            this.bnClose.Click += new System.EventHandler(this.bnClose_Click);
            // 
            // bnOpen
            // 
            resources.ApplyResources(this.bnOpen, "bnOpen");
            this.bnOpen.Name = "bnOpen";
            this.bnOpen.UseVisualStyleBackColor = true;
            this.bnOpen.Click += new System.EventHandler(this.bnOpen_Click);
            // 
            // cbDeviceList
            // 
            resources.ApplyResources(this.cbDeviceList, "cbDeviceList");
            this.cbDeviceList.FormattingEnabled = true;
            this.cbDeviceList.Name = "cbDeviceList";
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.tbExposure);
            this.groupBox1.Controls.Add(this.bnSetParam);
            this.groupBox1.Controls.Add(this.bnGetParam);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // tbExposure
            // 
            resources.ApplyResources(this.tbExposure, "tbExposure");
            this.tbExposure.Name = "tbExposure";
            // 
            // bnSetParam
            // 
            resources.ApplyResources(this.bnSetParam, "bnSetParam");
            this.bnSetParam.Name = "bnSetParam";
            this.bnSetParam.UseVisualStyleBackColor = true;
            this.bnSetParam.Click += new System.EventHandler(this.bnSetParam_Click);
            // 
            // bnGetParam
            // 
            resources.ApplyResources(this.bnGetParam, "bnGetParam");
            this.bnGetParam.Name = "bnGetParam";
            this.bnGetParam.UseVisualStyleBackColor = true;
            this.bnGetParam.Click += new System.EventHandler(this.bnGetParam_Click);
            // 
            // cbDeviceList2
            // 
            resources.ApplyResources(this.cbDeviceList2, "cbDeviceList2");
            this.cbDeviceList2.FormattingEnabled = true;
            this.cbDeviceList2.Name = "cbDeviceList2";
            // 
            // cbDeviceList3
            // 
            resources.ApplyResources(this.cbDeviceList3, "cbDeviceList3");
            this.cbDeviceList3.FormattingEnabled = true;
            this.cbDeviceList3.Name = "cbDeviceList3";
            // 
            // cbDeviceList4
            // 
            resources.ApplyResources(this.cbDeviceList4, "cbDeviceList4");
            this.cbDeviceList4.FormattingEnabled = true;
            this.cbDeviceList4.Name = "cbDeviceList4";
            // 
            // Form1
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.cbDeviceList4);
            this.Controls.Add(this.cbDeviceList3);
            this.Controls.Add(this.cbDeviceList2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.cbDeviceList);
            this.Controls.Add(this.groupBoxSwitch);
            this.Controls.Add(this.groupBoxSaveImg);
            this.Controls.Add(this.groupBoxGrabing);
            this.Controls.Add(this.pictureBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBoxGrabing.ResumeLayout(false);
            this.groupBoxSaveImg.ResumeLayout(false);
            this.groupBoxSwitch.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.GroupBox groupBoxGrabing;
        private System.Windows.Forms.Button bnStopGrab;
        private System.Windows.Forms.Button bnStartGrab;
        private System.Windows.Forms.GroupBox groupBoxSaveImg;
        private System.Windows.Forms.Button bnSaveRAW;
        private System.Windows.Forms.Button bnSavePLY;
        private System.Windows.Forms.Button bnSaveTIFF;
        private System.Windows.Forms.Button bnSaveOBJ;
        private System.Windows.Forms.Button bnSaveCSV;
        private System.Windows.Forms.Button bnSaveJPG;
        private System.Windows.Forms.Button bnSaveBMP;
        private System.Windows.Forms.GroupBox groupBoxSwitch;
        private System.Windows.Forms.Button bnClose;
        private System.Windows.Forms.Button bnOpen;
        private System.Windows.Forms.Button bnEnum;
        private System.Windows.Forms.ComboBox cbDeviceList;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbExposure;
        private System.Windows.Forms.Button bnSetParam;
        private System.Windows.Forms.Button bnGetParam;
        private System.Windows.Forms.ComboBox cbDeviceList2;
        private System.Windows.Forms.ComboBox cbDeviceList3;
        private System.Windows.Forms.ComboBox cbDeviceList4;
    }
}

