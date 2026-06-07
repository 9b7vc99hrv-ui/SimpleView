using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing.Drawing2D;

namespace SimpleView_DepthToPointCloud
{
    public partial class MainForm : Form
    {
        // 设备相关
        private IntPtr m_handle = IntPtr.Zero;
        private bool m_bExitMain = false;
        private bool m_bMeasuring = false;
        private Timer m_pollTimer;

        // 控件 - 左侧（原有）
        private PictureBox pictureBoxDepth;
        private ListBox listBoxDevices;
        private Button btnStartStop;
        private Button btnRefresh;
        private Label lblStatus;
        private Label lblFrameInfo;
        private ComboBox cmbImageMode;

        // 控件 - 左侧（输入参数）
        private NumericUpDown nudLineWidth;
        private NumericUpDown nudLineHeight;
        private NumericUpDown nudTurnsPerLayer;
        private NumericUpDown nudLayerCount;
        private NumericUpDown nudValleyRatio;

        // 控件 - 右侧（新增）
        private PictureBox pictureBoxSnap;
        private PictureBox pictureBoxMadChart;
        private Label lblSnapStatus;
        private Label lblMadValue;
        private Label lblGapInfo;

        // 缝隙检测
        private GapDetector m_gapDetector;
        private GapDetector.GapResult m_lastGapResult;
        private List<double> m_madHistory;
        private const int MAX_MAD_HISTORY = 60;

        // 异常截图
        private bool m_hasAnomaly = false;
        private Bitmap m_snapshotImage = null;
        private int m_anomalyFrameNum = 0;
        private int m_consecutiveAnomalyFrames = 0;
        private const int ANOMALY_TRIGGER_THRESHOLD = 3;

        // 缓存当前帧的深度图像 Bitmap
        private Bitmap m_currentDepthBitmap = null;

        // 单位参数 - 根据 MV-DP2470-01P V2.0 参数
        private const float X_RANGE_MM = 315f;      // X轴测量范围 315mm
        private const int X_POINTS = 3200;          // 单轮廓点数 3200
        private const float Z_RANGE_MM = 670f;      // Z轴测量范围 670mm
        private const int Z_RAW_RANGE = 65536;      // short 范围

        private float m_fCoordXUnit = X_RANGE_MM / X_POINTS;  // 0.0984375 mm/像素
        private float m_fCoordZUnit = Z_RANGE_MM / Z_RAW_RANGE; // 0.010223 mm/raw

        // 基准轮廓校准相关
        private float[] m_baselineProfile;
        private float[] m_calibAccum;
        private int[] m_calibCounts;
        private int m_calibFrameCount;
        private bool m_isCalibrating = false;
        private bool m_hasBaseline = false;
        private const int CALIB_FRAME_TARGET = 16;

        // 当前帧原始深度数据
        private float[] m_lastSmoothedData = null;
        private int m_lastWidth = 0;
        private uint m_lastFrameNum = 0;

        // 深度最值
        private Label lblDepthMinMax;

        // 调试按钮
        private Button btnDebug;
        private Button btnCalibrate;

        // 深度图像显示区域
        private GroupBox groupDisplay;

        enum Mv3dLpImageMode
        {
            MV3D_LP_Origin_Image = 1,
            MV3D_LP_Point_Cloud_Image = 4,
            MV3D_LP_Range_Image = 7,
            MV3D_LP_Intensity_Image = 10,
        };

        private const int DISPLAY_NORMAL = 0;
        private const int DISPLAY_ADAPTIVE = 1;
        private const int DISPLAY_TRUE_ADAPTIVE = 2;

        public MainForm()
        {
            Mv3dLpSDK.MV3D_LP_Initialize();
            m_gapDetector = new GapDetector();
            m_gapDetector.CoordZUnit = m_fCoordZUnit;
            m_gapDetector.MmPerPixel = m_fCoordXUnit;
            m_madHistory = new List<double>();
            InitializeComponent();
            InitializeCustomControls();
            LoadDevices();
        }

        private void InitializeComponent()
        {
            this.Text = "3D激光轮廓仪 - 卷线缝隙检测";
            this.Size = new Size(1800, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += MainForm_FormClosing;
        }

        private void InitializeCustomControls()
        {
            // 设备控制面板
            GroupBox groupControl = new GroupBox { Text = "设备控制", Left = 12, Top = 12, Width = 280, Height = 300 };
            Label lblDevices = new Label { Text = "设备列表:", Left = 10, Top = 20, Width = 250 };
            groupControl.Controls.Add(lblDevices);
            listBoxDevices = new ListBox { Left = 10, Top = 40, Width = 255, Height = 120 };
            groupControl.Controls.Add(listBoxDevices);
            btnRefresh = new Button { Text = "刷新设备", Left = 10, Top = 170, Width = 120, Height = 30 };
            btnRefresh.Click += BtnRefresh_Click;
            groupControl.Controls.Add(btnRefresh);
            btnStartStop = new Button { Text = "开始采集", Left = 145, Top = 170, Width = 120, Height = 30, Enabled = false };
            btnStartStop.Click += BtnStartStop_Click;
            groupControl.Controls.Add(btnStartStop);
            Label lblMode = new Label { Text = "图像模式:", Left = 10, Top = 215, Width = 250 };
            groupControl.Controls.Add(lblMode);
            cmbImageMode = new ComboBox { Left = 10, Top = 235, Width = 255, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbImageMode.Items.AddRange(new object[] { "深度图 (Range Image)", "亮度图 (Intensity Image)", "原始图 (Origin Image)", "点云图 (Point Cloud)" });
            cmbImageMode.SelectedIndex = 0;
            groupControl.Controls.Add(cmbImageMode);
            this.Controls.Add(groupControl);

            // 输入参数面板
            GroupBox groupParams = new GroupBox { Text = "输入参数（先验知识）", Left = 12, Top = 320, Width = 280, Height = 150 };
            Label lblWd = new Label { Text = "线宽 d (mm):", Left = 8, Top = 25, Width = 78 };
            groupParams.Controls.Add(lblWd);
            nudLineWidth = new NumericUpDown { Left = 88, Top = 23, Width = 60, Minimum = 0.01M, Maximum = 100M, Value = 3.50M, DecimalPlaces = 2 };
            groupParams.Controls.Add(nudLineWidth);
            Label lblTn = new Label { Text = "每层匝数 N:", Left = 155, Top = 25, Width = 72 };
            groupParams.Controls.Add(lblTn);
            nudTurnsPerLayer = new NumericUpDown { Left = 229, Top = 23, Width = 42, Minimum = 1, Maximum = 999, Value = 8 };
            groupParams.Controls.Add(nudTurnsPerLayer);
            Label lblHt = new Label { Text = "线高 h (mm):", Left = 8, Top = 55, Width = 78 };
            groupParams.Controls.Add(lblHt);
            nudLineHeight = new NumericUpDown { Left = 88, Top = 53, Width = 60, Minimum = 0.01M, Maximum = 100M, Value = 1.70M, DecimalPlaces = 2 };
            groupParams.Controls.Add(nudLineHeight);
            Label lblLc = new Label { Text = "总层数 L:", Left = 155, Top = 55, Width = 72 };
            groupParams.Controls.Add(lblLc);
            nudLayerCount = new NumericUpDown { Left = 229, Top = 53, Width = 42, Minimum = 1, Maximum = 999, Value = 9 };
            groupParams.Controls.Add(nudLayerCount);
            Label lblValleyRatio = new Label { Text = "谷阈系数:", Left = 8, Top = 85, Width = 78 };
            groupParams.Controls.Add(lblValleyRatio);
            nudValleyRatio = new NumericUpDown { Left = 88, Top = 83, Width = 60, Minimum = 0.10M, Maximum = 1.00M, Value = 0.30M, DecimalPlaces = 2, Increment = 0.05M };
            groupParams.Controls.Add(nudValleyRatio);
            Label lblValleyTip = new Label { Text = "越小越敏感", Left = 155, Top = 85, Width = 100, Font = new Font("微软雅黑", 7) };
            groupParams.Controls.Add(lblValleyTip);
            Label lblXUnit = new Label { Text = $"X间距: {m_fCoordXUnit:F4} mm", Left = 8, Top = 115, Width = 120, Font = new Font("微软雅黑", 7), ForeColor = Color.Gray };
            groupParams.Controls.Add(lblXUnit);
            Label lblZUnit = new Label { Text = $"Z精度: {m_fCoordZUnit:F6} mm", Left = 140, Top = 115, Width = 120, Font = new Font("微软雅黑", 7), ForeColor = Color.Gray };
            groupParams.Controls.Add(lblZUnit);
            this.Controls.Add(groupParams);

            // 状态信息面板
            GroupBox groupStatus = new GroupBox { Text = "状态信息", Left = 12, Top = 480, Width = 280, Height = 210 };
            lblStatus = new Label { Text = "就绪", Left = 10, Top = 20, Width = 255, Height = 25 };
            groupStatus.Controls.Add(lblStatus);
            lblFrameInfo = new Label { Text = "", Left = 10, Top = 48, Width = 255, Height = 25 };
            groupStatus.Controls.Add(lblFrameInfo);
            lblGapInfo = new Label { Text = "缝隙检测: 未启动", Left = 10, Top = 76, Width = 255, Height = 25 };
            groupStatus.Controls.Add(lblGapInfo);
            lblMadValue = new Label { Text = "检测状态: --", Left = 10, Top = 104, Width = 255, Height = 25 };
            groupStatus.Controls.Add(lblMadValue);
            lblDepthMinMax = new Label { Text = "层数/匝数: --", Left = 10, Top = 132, Width = 255, Height = 25 };
            groupStatus.Controls.Add(lblDepthMinMax);
            btnCalibrate = new Button { Text = "校准基准（空筒）", Left = 10, Top = 165, Width = 120, Height = 30, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White };
            btnCalibrate.Click += BtnCalibrate_Click;
            groupStatus.Controls.Add(btnCalibrate);
            btnDebug = new Button { Text = "调试：分析深度数据", Left = 140, Top = 165, Width = 130, Height = 30, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
            btnDebug.Click += BtnDebug_Click;
            groupStatus.Controls.Add(btnDebug);
            this.Controls.Add(groupStatus);

            // 深度图像显示区域
            groupDisplay = new GroupBox { Text = "深度图像显示", Left = 305, Top = 12, Width = 700, Height = 680 };
            pictureBoxDepth = new PictureBox { Left = 10, Top = 20, Width = 680, Height = 648, BackColor = Color.Black, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            groupDisplay.Controls.Add(pictureBoxDepth);
            this.Controls.Add(groupDisplay);

            // 异常定格截图框
            GroupBox groupSnap = new GroupBox { Text = "异常定格截图", Left = 1020, Top = 12, Width = 350, Height = 330 };
            pictureBoxSnap = new PictureBox { Left = 10, Top = 20, Width = 330, Height = 260, BackColor = Color.FromArgb(60, 60, 60), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            groupSnap.Controls.Add(pictureBoxSnap);
            lblSnapStatus = new Label { Text = "等待检测...", Left = 10, Top = 288, Width = 330, Height = 30, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleCenter };
            groupSnap.Controls.Add(lblSnapStatus);
            this.Controls.Add(groupSnap);

            // MAD实时曲线图
            GroupBox groupChart = new GroupBox { Text = "MAD实时曲线 (Modified Z-score)", Left = 1020, Top = 350, Width = 350, Height = 342 };
            pictureBoxMadChart = new PictureBox { Left = 10, Top = 20, Width = 330, Height = 310, BackColor = Color.FromArgb(30, 30, 30), BorderStyle = BorderStyle.FixedSingle };
            groupChart.Controls.Add(pictureBoxMadChart);
            this.Controls.Add(groupChart);

            m_pollTimer = new Timer { Interval = 100 };
            m_pollTimer.Tick += PollTimer_Tick;
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            StopAcquisition();
            if (m_snapshotImage != null) { m_snapshotImage.Dispose(); m_snapshotImage = null; }
            if (m_currentDepthBitmap != null) { m_currentDepthBitmap.Dispose(); m_currentDepthBitmap = null; }
            Mv3dLpSDK.MV3D_LP_Finalize();
            Application.Restart();
        }

        private void LoadDevices()
        {
            listBoxDevices.Items.Clear();
            btnStartStop.Enabled = false;
            try
            {
                string version = Mv3dLpSDK.MV3D_LP_GetVersion();
                lblStatus.Text = string.Format("SDK版本: {0}", version);
                UInt32 nDevNum = 0;
                int nRet = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref nDevNum);
                if (nDevNum == 0) { lblStatus.Text = "未找到设备，请检查网络连接!"; return; }
                MV3D_LP_DEVICE_INFO_VECTOR stVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)nDevNum);
                for (UInt32 i = 0; i < nDevNum; i++) { stVector.Add(new MV3D_LP_DEVICE_INFO()); }
                nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(stVector[0], nDevNum, ref nDevNum);
                if (0 != nRet) { lblStatus.Text = string.Format("获取设备列表失败, nRet: 0x{0:x}", nRet); return; }
                for (Int32 i = 0; i < nDevNum; i++)
                {
                    string info = string.Format("[{0}] SN:{1}  IP:{2}  型号:{3}", i, stVector[i].chSerialNumber, stVector[i].chCurrentIp, stVector[i].chModelName);
                    listBoxDevices.Items.Add(info);
                }
                if (nDevNum > 0) { listBoxDevices.SelectedIndex = 0; btnStartStop.Enabled = true; lblStatus.Text = string.Format("找到 {0} 个设备", nDevNum); }
            }
            catch (Exception ex) { lblStatus.Text = string.Format("加载设备失败: {0}", ex.Message); }
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!m_bMeasuring) StartAcquisition();
            else StopAcquisition();
        }

        private void StartAcquisition()
        {
            if (m_handle != IntPtr.Zero) { Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle); m_handle = IntPtr.Zero; }
            int selectedIndex = listBoxDevices.SelectedIndex;
            if (selectedIndex < 0) { lblStatus.Text = "请先选择一个设备"; return; }
            try
            {
                UInt32 nDevNum = 0;
                Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref nDevNum);
                if (nDevNum == 0) { lblStatus.Text = "没有可用设备!"; return; }
                MV3D_LP_DEVICE_INFO_VECTOR stVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)nDevNum);
                for (UInt32 i = 0; i < nDevNum; i++) { stVector.Add(new MV3D_LP_DEVICE_INFO()); }
                Mv3dLpSDK.MV3D_LP_GetDeviceList(stVector[0], nDevNum, ref nDevNum);
                int nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_handle, stVector[selectedIndex].chSerialNumber);
                if (0 != nRet) { lblStatus.Text = string.Format("打开设备失败! 错误码: 0x{0:x}", nRet); return; }

                // 设置图像模式
                MV3D_LP_PARAM pstValue = new MV3D_LP_PARAM();
                MV3D_LP_ENUMPARAM enumParam = new MV3D_LP_ENUMPARAM();
                uint modeValue;
                switch (cmbImageMode.SelectedIndex)
                {
                    case 0: modeValue = (uint)Mv3dLpImageMode.MV3D_LP_Range_Image; break;
                    case 1: modeValue = (uint)Mv3dLpImageMode.MV3D_LP_Intensity_Image; break;
                    case 2: modeValue = (uint)Mv3dLpImageMode.MV3D_LP_Origin_Image; break;
                    case 3: modeValue = (uint)Mv3dLpImageMode.MV3D_LP_Point_Cloud_Image; break;
                    default: modeValue = (uint)Mv3dLpImageMode.MV3D_LP_Range_Image; break;
                }
                enumParam.nCurValue = modeValue;
                pstValue.set_enumparam(enumParam);
                nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_handle, "ImageMode", pstValue);
                if (Mv3dLpSDK.MV3D_LP_OK != nRet) { lblStatus.Text = string.Format("设置图像模式失败! 错误码: 0x{0:x}", nRet); Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle); return; }

                nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(m_handle);
                if (0 != nRet) { lblStatus.Text = string.Format("开始测量失败! 错误码: 0x{0:x}", nRet); Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle); return; }

                m_bMeasuring = true;
                m_bExitMain = false;
                btnStartStop.Text = "停止采集";
                lblStatus.Text = "采集中...";
                cmbImageMode.Enabled = false;
                m_madHistory.Clear();
                m_lastGapResult = null;
                m_hasAnomaly = false;
                m_consecutiveAnomalyFrames = 0;
                if (m_snapshotImage != null) { m_snapshotImage.Dispose(); m_snapshotImage = null; }
                if (m_currentDepthBitmap != null) { m_currentDepthBitmap.Dispose(); m_currentDepthBitmap = null; }

                // 同步单位参数到检测器
                m_gapDetector.MmPerPixel = m_fCoordXUnit;
                m_gapDetector.CoordZUnit = m_fCoordZUnit;
                SyncParametersToDetector();

                m_pollTimer.Start();
            }
            catch (Exception ex) { lblStatus.Text = string.Format("启动失败: {0}", ex.Message); if (m_handle != IntPtr.Zero) { Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle); m_handle = IntPtr.Zero; } }
        }

        private void StopAcquisition()
        {
            m_pollTimer.Stop();
            m_bExitMain = true;
            m_bMeasuring = false;
            if (m_isCalibrating) { m_isCalibrating = false; m_calibAccum = null; m_calibCounts = null; m_calibFrameCount = 0; }
            if (m_handle != IntPtr.Zero) { Mv3dLpSDK.MV3D_LP_StopMeasure(m_handle); Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle); m_handle = IntPtr.Zero; }
            btnStartStop.Text = "开始采集";
            lblStatus.Text = "已停止";
            cmbImageMode.Enabled = true;
            lblGapInfo.Text = "缝隙检测: 已停止";
            lblMadValue.Text = "检测状态: --";
            lblDepthMinMax.Text = "层数/匝数: --";
        }

        private void SyncParametersToDetector()
        {
            m_gapDetector.WireWidth = (float)nudLineWidth.Value;
            m_gapDetector.WireHeight = (float)nudLineHeight.Value;
            m_gapDetector.TurnsPerLayer = (int)nudTurnsPerLayer.Value;
            m_gapDetector.TotalLayers = (int)nudLayerCount.Value;
            m_gapDetector.ValleyRatio = (float)nudValleyRatio.Value;
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            if (m_handle == IntPtr.Zero || m_bExitMain) { m_pollTimer.Stop(); return; }
            try
            {
                MV3D_LP_IMAGE_DATA stImage = new MV3D_LP_IMAGE_DATA();
                int nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_handle, stImage, 500);
                if (0 == nRet)
                {
                    m_lastFrameNum = stImage.nFrameNum;
                    lblFrameInfo.Text = string.Format("帧号: {0}  尺寸: {1} x {2}  数据长度: {3}  类型: {4}", stImage.nFrameNum, stImage.nWidth, stImage.nHeight, stImage.nDataLen, stImage.enImageType);
                    SyncParametersToDetector();
                    ProcessDepthData(stImage);
                    nRet = Mv3dLpSDK.MV3D_LP_DisplayImage(stImage, pictureBoxDepth.Handle, (uint)DISPLAY_ADAPTIVE, 0, 30000);
                    if (0 != nRet) lblStatus.Text = string.Format("显示图像返回: 0x{0:x}", nRet);
                    else lblStatus.Text = "采集中 - 接收正常";
                    UpdateRightPanel();
                }
                else if (nRet == unchecked((int)0x80060006)) { }
                else if (nRet != 1) { lblStatus.Text = string.Format("获取图像失败: 0x{0:x}", nRet); }
            }
            catch (Exception ex) { lblStatus.Text = string.Format("采集异常: {0}", ex.Message); }
        }

        private void ProcessDepthData(MV3D_LP_IMAGE_DATA stImage)
        {
            if (stImage.pData == IntPtr.Zero || stImage.nDataLen == 0 || stImage.nWidth == 0) return;
            try
            {
                int pixelCount = (int)(stImage.nWidth * stImage.nHeight);
                if (pixelCount <= 0) return;
                short[] depthValues = null;
                int width = (int)stImage.nWidth;
                int height = (int)stImage.nHeight;

                // 解析深度数据
                if (stImage.enImageType == Mv3dLpSDK.ImageType_Depth)
                {
                    if (stImage.nDataLen < pixelCount * 2) return;
                    byte[] buffer = new byte[pixelCount * 2];
                    Marshal.Copy(stImage.pData, buffer, 0, pixelCount * 2);
                    depthValues = new short[pixelCount];
                    for (int i = 0; i < pixelCount; i++) depthValues[i] = BitConverter.ToInt16(buffer, i * 2);
                }
                else if (stImage.nDataLen >= pixelCount * 2 && stImage.nDataLen < pixelCount * 12)
                {
                    if (stImage.nDataLen < pixelCount * 2) return;
                    byte[] buffer = new byte[pixelCount * 2];
                    Marshal.Copy(stImage.pData, buffer, 0, pixelCount * 2);
                    depthValues = new short[pixelCount];
                    for (int i = 0; i < pixelCount; i++) depthValues[i] = BitConverter.ToInt16(buffer, i * 2);
                }
                else if (stImage.nDataLen >= pixelCount * 12)
                {
                    byte[] pcBuffer = new byte[(int)stImage.nDataLen];
                    Marshal.Copy(stImage.pData, pcBuffer, 0, (int)stImage.nDataLen);
                    int pointCount = (int)stImage.nDataLen / 4;
                    float[] pcData = new float[pointCount];
                    for (int i = 0; i < pointCount; i++) pcData[i] = BitConverter.ToSingle(pcBuffer, i * 4);
                    int totalPoints = (int)(stImage.nWidth * stImage.nHeight);
                    if (totalPoints <= 0 || pointCount < totalPoints * 3) return;
                    depthValues = new short[totalPoints];
                    for (int i = 0; i < totalPoints; i++)
                    {
                        float z = pcData[i * 3 + 2];
                        if (float.IsNaN(z) || float.IsInfinity(z) || z <= -99999 || z >= 99999) depthValues[i] = 0;
                        else
                        {
                            double zScaled = z / m_fCoordZUnit;
                            if (zScaled > 32767) depthValues[i] = 32767;
                            else if (zScaled < -32768) depthValues[i] = -32768;
                            else depthValues[i] = (short)zScaled;
                        }
                    }
                }
                else return;

                if (depthValues == null) return;

                // 校准模式
                if (m_isCalibrating)
                {
                    AccumulateForCalibration(depthValues, width, height);
                    return;
                }

                // 正常检测模式
                GenerateDepthBitmap(depthValues, width, height);
                short[] depthLine;
                if (height > 1) depthLine = GapDetector.ExtractDepthLineAvg(depthValues, width, height, height / 2, halfWindow: 2);
                else depthLine = depthValues;

                float[] compensatedLine;
                if (m_hasBaseline && m_baselineProfile != null && m_baselineProfile.Length == width)
                {
                    compensatedLine = ApplyBaselineCompensation(depthLine, width);
                }
                else
                {
                    compensatedLine = new float[width];
                    for (int i = 0; i < width; i++) compensatedLine[i] = depthLine[i] > 0 ? depthLine[i] : -1f;
                }

                float[] smoothed = PreprocessForDetection(compensatedLine, width);
                if (smoothed != null) { m_lastSmoothedData = smoothed; m_lastWidth = width; }

                GapDetector.GapResult result = m_gapDetector.ProcessDepthLine(compensatedLine, width);
                UpdateGapResult(result, stImage.nFrameNum);

                if (m_consecutiveAnomalyFrames >= ANOMALY_TRIGGER_THRESHOLD && !m_hasAnomaly)
                {
                    m_hasAnomaly = true;
                    m_anomalyFrameNum = (int)stImage.nFrameNum;
                    CaptureSnapshot();
                    SaveSnapshotToFile();
                    if (depthValues != null) SaveDepthDataToFile(depthValues, width, height, stImage.nFrameNum);
                }
            }
            catch (Exception ex) { lblStatus.Text = string.Format("处理异常: {0}", ex.Message); }
        }

        private void AccumulateForCalibration(short[] depthValues, int width, int height)
        {
            if (m_calibAccum == null || m_calibAccum.Length != width)
            {
                m_calibAccum = new float[width];
                m_calibCounts = new int[width];
                m_calibFrameCount = 0;
            }

            short[] depthLine;
            if (height > 1) depthLine = GapDetector.ExtractDepthLineAvg(depthValues, width, height, height / 2, halfWindow: 2);
            else depthLine = depthValues;

            for (int i = 0; i < width; i++)
            {
                if (depthLine[i] > 0)
                {
                    m_calibAccum[i] += depthLine[i];
                    m_calibCounts[i]++;
                }
            }

            m_calibFrameCount++;
            this.Invoke(new Action(() => lblStatus.Text = $"校准中... {m_calibFrameCount}/{CALIB_FRAME_TARGET} 帧"));

            if (m_calibFrameCount >= CALIB_FRAME_TARGET)
            {
                GenerateBaselineProfile(width);
                m_calibAccum = null;
                m_calibCounts = null;
                m_calibFrameCount = 0;
                m_isCalibrating = false;
                this.Invoke(new Action(() =>
                {
                    btnDebug.Enabled = true;
                    btnCalibrate.Enabled = true;
                    btnStartStop.Enabled = true;
                    int validCount = m_baselineProfile.Count(v => v != 0);
                    float minBase = m_baselineProfile.Where(v => v > 0).DefaultIfEmpty(0).Min();
                    float maxBase = m_baselineProfile.Where(v => v > 0).DefaultIfEmpty(0).Max();
                    lblStatus.Text = $"✓ 基准校准完成 | {validCount}点 范围:{minBase:F0}~{maxBase:F0} raw";
                    lblStatus.ForeColor = Color.Green;
                }));
            }
        }

        private void GenerateBaselineProfile(int width)
        {
            m_baselineProfile = new float[width];
            for (int i = 0; i < width; i++)
            {
                if (m_calibCounts[i] > 0) m_baselineProfile[i] = m_calibAccum[i] / m_calibCounts[i];
                else m_baselineProfile[i] = 0f;
            }
            m_hasBaseline = true;
        }

        private float[] ApplyBaselineCompensation(short[] depthLine, int width)
        {
            float[] compensated = new float[width];
            for (int i = 0; i < width; i++)
            {
                if (depthLine[i] > 0 && m_baselineProfile[i] > 0)
                    compensated[i] = depthLine[i] - m_baselineProfile[i];
                else
                    compensated[i] = -1f;
            }
            return compensated;
        }

        private float[] PreprocessForDetection(float[] depthData, int width)
        {
            float[] result = new float[width];
            int validCount = 0;
            for (int i = 0; i < width; i++)
            {
                if (depthData[i] != -1f) { result[i] = depthData[i]; validCount++; }
                else result[i] = -1f;
            }
            if (validCount < 20) return null;
            float[] smoothed = new float[width];
            for (int i = 0; i < width; i++)
            {
                if (result[i] == -1f) { smoothed[i] = -1f; continue; }
                List<float> window = new List<float>();
                for (int j = -1; j <= 1; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < width && result[idx] != -1f) window.Add(result[idx]);
                }
                if (window.Count > 0) { window.Sort(); smoothed[i] = window[window.Count / 2]; }
                else smoothed[i] = result[i];
            }
            return smoothed;
        }

        private void UpdateGapResult(GapDetector.GapResult result, uint frameNum)
        {
            m_lastGapResult = result;
            if (result.HasValidData)
            {
                m_madHistory.Add(result.ModifiedZScore);
                if (m_madHistory.Count > MAX_MAD_HISTORY) m_madHistory.RemoveAt(0);
                if (result.IsAnomaly)
                {
                    m_consecutiveAnomalyFrames++;
                    lblMadValue.ForeColor = Color.Red;
                    if (result.AnomalyReasons.Count > 0) lblMadValue.Text = $"异常: {result.AnomalyReasons[0]}";
                    else lblMadValue.Text = $"检测状态: 异常 (连续{m_consecutiveAnomalyFrames}帧)";
                }
                else
                {
                    m_consecutiveAnomalyFrames = 0;
                    m_hasAnomaly = false;
                    lblMadValue.ForeColor = Color.Green;
                    lblMadValue.Text = "检测状态: 正常";
                }
                string statusText = result.IsAnomaly ? "⚠️ 异常" : "✓ 正常";
                string layerText = result.CurrentLayer >= 0 ? $"第{result.CurrentLayer + 1}层" : "层数未知";
                string completeText = result.IsLayerCompleted ? "已完成" : "绕制中";
                lblGapInfo.Text = $"{layerText} | {completeText} | {statusText}";
                if (result.InferredTurn == 0) { lblDepthMinMax.Text = $"匝数: 未检测到 (帧{frameNum})"; lblDepthMinMax.ForeColor = Color.Blue; }
                else { lblDepthMinMax.Text = $"匝数: {result.InferredTurn} / {m_gapDetector.TurnsPerLayer}"; lblDepthMinMax.ForeColor = result.InferredTurn == m_gapDetector.TurnsPerLayer ? Color.White : Color.Blue; }
            }
            else { lblGapInfo.Text = "缝隙检测: 数据不足"; lblDepthMinMax.Text = $"匝数: 无数据"; }
        }

        private void GenerateDepthBitmap(short[] depthData, int width, int height)
        {
            try
            {
                if (width <= 0 || height <= 0 || depthData == null || depthData.Length < width * height) return;
                Bitmap bmp = new Bitmap(width, height);
                System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                int stride = bmpData.Stride;
                IntPtr scan0 = bmpData.Scan0;
                byte[] pixels = new byte[stride * height];
                short minVal = short.MaxValue, maxVal = short.MinValue;
                for (int i = 0; i < depthData.Length; i++) { if (depthData[i] > 0) { if (depthData[i] < minVal) minVal = depthData[i]; if (depthData[i] > maxVal) maxVal = depthData[i]; } }
                if (maxVal <= minVal)
                {
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { int idx = y * stride + x * 3; pixels[idx + 0] = 0; pixels[idx + 1] = 0; pixels[idx + 2] = 0; }
                }
                else
                {
                    float range = maxVal - minVal;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int pixelIdx = y * width + x;
                            int byteIdx = y * stride + x * 3;
                            short val = depthData[pixelIdx];
                            if (val <= 0) { pixels[byteIdx + 0] = 0; pixels[byteIdx + 1] = 0; pixels[byteIdx + 2] = 0; }
                            else
                            {
                                float t = (val - minVal) / range;
                                byte r, g, b;
                                if (t < 0.2f) { float s = t / 0.2f; r = 0; g = (byte)(s * 128); b = (byte)(128 + s * 127); }
                                else if (t < 0.4f) { float s = (t - 0.2f) / 0.2f; r = 0; g = (byte)(128 + s * 127); b = (byte)(255 - s * 255); }
                                else if (t < 0.6f) { float s = (t - 0.4f) / 0.2f; r = (byte)(s * 255); g = 255; b = 0; }
                                else if (t < 0.8f) { float s = (t - 0.6f) / 0.2f; r = 255; g = (byte)(255 - s * 255); b = 0; }
                                else { float s = (t - 0.8f) / 0.2f; r = 255; g = (byte)(s * 255); b = (byte)(s * 255); }
                                pixels[byteIdx + 0] = b; pixels[byteIdx + 1] = g; pixels[byteIdx + 2] = r;
                            }
                        }
                    }
                }
                Marshal.Copy(pixels, 0, scan0, pixels.Length);
                bmp.UnlockBits(bmpData);
                if (m_currentDepthBitmap != null) m_currentDepthBitmap.Dispose();
                m_currentDepthBitmap = bmp;
            }
            catch { }
        }

        private void CaptureSnapshot()
        {
            try
            {
                if (pictureBoxDepth == null) return;
                pictureBoxDepth.Refresh();
                Application.DoEvents();
                Bitmap snapshot = null;
                if (m_currentDepthBitmap != null)
                {
                    snapshot = new Bitmap(m_currentDepthBitmap.Width, m_currentDepthBitmap.Height);
                    using (Graphics g = Graphics.FromImage(snapshot)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(m_currentDepthBitmap, 0, 0); }
                }
                else if (pictureBoxDepth.Image != null)
                {
                    snapshot = new Bitmap(pictureBoxDepth.Image.Width, pictureBoxDepth.Image.Height);
                    using (Graphics g = Graphics.FromImage(snapshot)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(pictureBoxDepth.Image, 0, 0); }
                }
                else
                {
                    Rectangle rect = pictureBoxDepth.RectangleToScreen(pictureBoxDepth.ClientRectangle);
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        snapshot = new Bitmap(rect.Width, rect.Height);
                        using (Graphics g = Graphics.FromImage(snapshot)) { g.CopyFromScreen(rect.X, rect.Y, 0, 0, rect.Size); }
                    }
                }
                if (snapshot != null)
                {
                    using (Graphics g = Graphics.FromImage(snapshot))
                    {
                        string anomalyText = $"⚠️ 异常触发 - 帧 {m_anomalyFrameNum}";
                        if (m_lastGapResult != null && m_lastGapResult.AnomalyReasons.Count > 0) anomalyText += $" | {string.Join(",", m_lastGapResult.AnomalyReasons)}";
                        anomalyText += $" | {DateTime.Now:HH:mm:ss}";
                        using (Font font = new Font("Microsoft YaHei", 12, FontStyle.Bold))
                        using (Brush textBrush = new SolidBrush(Color.Red))
                        using (Brush bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                        {
                            SizeF textSize = g.MeasureString(anomalyText, font);
                            Rectangle bgRect = new Rectangle(10, 10, (int)textSize.Width + 20, (int)textSize.Height + 10);
                            g.FillRectangle(bgBrush, bgRect);
                            g.DrawString(anomalyText, font, textBrush, 20, 15);
                        }
                        using (Pen redPen = new Pen(Color.Red, 3)) { redPen.DashStyle = DashStyle.Dash; g.DrawRectangle(redPen, 5, 5, snapshot.Width - 10, snapshot.Height - 10); }
                    }
                    if (m_snapshotImage != null) m_snapshotImage.Dispose();
                    m_snapshotImage = snapshot;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"截图失败: {ex.Message}"); }
        }

        private void SaveSnapshotToFile()
        {
            if (m_snapshotImage != null)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string filename = $"Anomaly_{timestamp}_Frame{m_anomalyFrameNum}.png";
                    string directory = Path.Combine(Application.StartupPath, "AnomalySnapshots");
                    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                    string fullPath = Path.Combine(directory, filename);
                    m_snapshotImage.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                    lblSnapStatus.Text = $"⚠️ 异常触发 | 已保存: {filename}";
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"保存截图失败: {ex.Message}"); lblSnapStatus.Text = $"异常触发 | 保存失败: {ex.Message}"; }
            }
        }

        private void SaveDepthDataToFile(short[] depthData, int width, int height, uint frameNum)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"DepthData_{timestamp}_Frame{frameNum}.csv";
                string directory = Path.Combine(Application.StartupPath, "AnomalySnapshots");
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string fullPath = Path.Combine(directory, filename);
                using (StreamWriter writer = new StreamWriter(fullPath))
                {
                    writer.WriteLine($"Frame,{frameNum}");
                    writer.WriteLine($"Width,{width}");
                    writer.WriteLine($"Height,{height}");
                    writer.WriteLine($"Timestamp,{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    writer.WriteLine("Depth Data:");
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            writer.Write(depthData[y * width + x]);
                            if (x < width - 1) writer.Write(",");
                        }
                        writer.WriteLine();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"深度数据已保存: {fullPath}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"保存深度数据失败: {ex.Message}"); }
        }

        private void UpdateRightPanel()
        {
            if (m_hasAnomaly && m_snapshotImage != null) pictureBoxSnap.Image = m_snapshotImage;
            else if (!m_hasAnomaly && m_consecutiveAnomalyFrames == 0 && !lblSnapStatus.Text.Contains("异常"))
            { lblSnapStatus.Text = "正常 - 等待异常..."; lblSnapStatus.ForeColor = Color.Gray; }
            DrawMadChart();
        }

        private void DrawMadChart()
        {
            if (pictureBoxMadChart.Width <= 10 || pictureBoxMadChart.Height <= 10) return;
            Bitmap bmp = new Bitmap(pictureBoxMadChart.Width, pictureBoxMadChart.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int width = bmp.Width, height = bmp.Height, marginLeft = 40, marginRight = 10, marginTop = 20, marginBottom = 30;
                int chartWidth = width - marginLeft - marginRight, chartHeight = height - marginTop - marginBottom;
                if (chartWidth <= 0 || chartHeight <= 0) return;
                using (Pen axisPen = new Pen(Color.FromArgb(100, 100, 100))) { g.DrawLine(axisPen, marginLeft, marginTop, marginLeft, height - marginBottom); g.DrawLine(axisPen, marginLeft, height - marginBottom, width - marginRight, height - marginBottom); }
                float thresholdY = marginTop + chartHeight - (float)(5.0 / 10.0 * chartHeight);
                using (Pen redPen = new Pen(Color.Red, 2)) { redPen.DashStyle = DashStyle.Dash; g.DrawLine(redPen, marginLeft, thresholdY, width - marginRight, thresholdY); }
                using (Brush redBrush = new SolidBrush(Color.Red)) { g.DrawString("阈值 5.0", new Font("Arial", 8), redBrush, width - marginRight - 70, thresholdY - 15); }
                using (Brush labelBrush = new SolidBrush(Color.FromArgb(180, 180, 180))) using (Font font = new Font("Arial", 8))
                { g.DrawString("10", font, labelBrush, marginLeft - 20, marginTop - 8); g.DrawString("5", font, labelBrush, marginLeft - 15, marginTop + chartHeight / 2 - 8); g.DrawString("0", font, labelBrush, marginLeft - 15, height - marginBottom - 8); }
                if (m_madHistory.Count > 1)
                {
                    int pointCount = m_madHistory.Count;
                    using (Pen bluePen = new Pen(Color.FromArgb(0, 150, 255), 2))
                    {
                        PointF[] points = new PointF[pointCount];
                        for (int i = 0; i < pointCount; i++)
                        {
                            float x = marginLeft + (float)i / (MAX_MAD_HISTORY - 1) * chartWidth;
                            float val = Math.Max(0, Math.Min((float)m_madHistory[i], 10));
                            float y = marginTop + chartHeight - (val / 10.0f * chartHeight);
                            points[i] = new PointF(x, y);
                        }
                        g.DrawLines(bluePen, points);
                    }
                    int showDots = Math.Min(5, pointCount);
                    using (Brush dotBrush = new SolidBrush(Color.FromArgb(0, 200, 255)))
                    {
                        for (int i = pointCount - showDots; i < pointCount; i++)
                        {
                            float x = marginLeft + (float)i / (MAX_MAD_HISTORY - 1) * chartWidth;
                            float val = Math.Max(0, Math.Min((float)m_madHistory[i], 10));
                            float y = marginTop + chartHeight - (val / 10.0f * chartHeight);
                            g.FillEllipse(dotBrush, x - 3, y - 3, 6, 6);
                        }
                    }
                }
                if (m_madHistory.Count == 0)
                { using (Brush infoBrush = new SolidBrush(Color.FromArgb(100, 100, 100))) using (Font font = new Font("Arial", 10)) { g.DrawString("等待数据...", font, infoBrush, width / 2 - 40, height / 2 - 10); } }
                using (Brush axisLabelBrush = new SolidBrush(Color.FromArgb(150, 150, 150))) using (Font font = new Font("Arial", 7))
                { g.DrawString("Z-score", font, axisLabelBrush, 2, marginTop + 5); g.DrawString("帧 (最近60帧)", font, axisLabelBrush, width / 2 - 30, height - 15); }
            }
            if (pictureBoxMadChart.Image != null) pictureBoxMadChart.Image.Dispose();
            pictureBoxMadChart.Image = bmp;
        }

        private void BtnCalibrate_Click(object sender, EventArgs e)
        {
            if (!m_bMeasuring || m_handle == IntPtr.Zero)
            {
                lblStatus.Text = "请先开始采集再校准";
                lblStatus.ForeColor = Color.Red;
                return;
            }
            if (m_isCalibrating) { lblStatus.Text = "校准进行中，请稍候..."; return; }

            // 重置所有校准状态
            m_hasBaseline = false;
            m_baselineProfile = null;
            m_calibAccum = null;
            m_calibCounts = null;
            m_calibFrameCount = 0;
            m_isCalibrating = true;

            btnDebug.Enabled = false;
            btnCalibrate.Enabled = false;
            btnStartStop.Enabled = false;

            lblStatus.Text = "校准中... 0/16 帧";
            lblStatus.ForeColor = Color.Orange;
        }

        private void BtnDebug_Click(object sender, EventArgs e)
        {
            if (m_lastSmoothedData == null || m_lastWidth == 0)
            {
                MessageBox.Show("暂无深度数据，请先开始采集", "调试信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // 使用最新的检测结果
            string detectionResult = "";
            if (m_lastGapResult != null && m_lastGapResult.HasValidData)
            {
                detectionResult = $"\n=== 检测结果 ===\n" +
                                $"最终判定匝数: {m_lastGapResult.InferredTurn}\n" +
                                $"是否异常: {(m_lastGapResult.IsAnomaly ? "是" : "否")}\n" +
                                $"异常原因: {(m_lastGapResult.AnomalyReasons.Count > 0 ? string.Join("; ", m_lastGapResult.AnomalyReasons) : "无")}\n";
            }
            else
            {
                detectionResult = "\n=== 检测结果 ===\n未检测到有效数据（可能是空筒或数据不足）\n";
            }
            
            float[] data = m_lastSmoothedData;
            List<float> validVals = new List<float>();
            for (int i = 0; i < data.Length; i++) if (data[i] != -1f) validVals.Add(data[i]);
            
            float minVal = validVals.Count > 0 ? validVals.Min() : 0;
            float maxVal = validVals.Count > 0 ? validVals.Max() : 0;
            int minIdx = validVals.Count > 0 ? Array.FindIndex(data, v => v == minVal) : -1;
            int maxIdx = validVals.Count > 0 ? Array.FindIndex(data, v => v == maxVal) : -1;
            
            string compensationInfo = m_hasBaseline ? $"✓ 倾斜补偿已启用 (基准轮廓有效列数: {m_baselineProfile?.Count(v => v != 0) ?? 0})" : "✗ 倾斜补偿未启用（请先校准空筒）";
            
            string baselineInfo = "";
            if (m_hasBaseline && m_baselineProfile != null)
            {
                var validBaseline = m_baselineProfile.Where(v => v > 0).ToList();
                if (validBaseline.Count > 0)
                {
                    baselineInfo = $"\n基准深度范围: {validBaseline.Min():F0} ~ {validBaseline.Max():F0} raw";
                }
            }
            
            string msg = $"=== 深度数据分析 (帧 {m_lastFrameNum}) ===\n\n" +
                        $"补偿状态: {compensationInfo}{baselineInfo}\n\n" +
                        $"有效点数: {validVals.Count}/{data.Length}\n" +
                        $"深度范围: {minVal:F1} ~ {maxVal:F1} raw\n" +
                        $"最小值(最高点)位置: X={minIdx}, 值={minVal:F1}\n" +
                        $"最大值(最低点)位置: X={maxIdx}, 值={maxVal:F1}\n" +
                        $"深度跨度: {maxVal - minVal:F1} raw\n" +
                        $"{detectionResult}\n" +
                        $"=== 检测参数 ===\n" +
                        $"X单位: {m_fCoordXUnit:F4} mm/像素 (3200点/315mm)\n" +
                        $"Z单位: {m_fCoordZUnit:F6} mm/raw\n" +
                        $"WireHeight: {m_gapDetector.WireHeight:F2} mm\n" +
                        $"ValleyRatio: {m_gapDetector.ValleyRatio:F2}\n" +
                        $"空筒判定阈值: 80 raw (波动<80raw时判定为无线材)\n" +
                        $"当前波动: {maxVal - minVal:F1} raw\n" +
                        $"{(maxVal - minVal < 80 ? "→ 判定为无线材/空筒状态" : "→ 判定为有绕线，进行检测")}\n";
            
            MessageBox.Show(msg, "调试信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopAcquisition();
            if (m_snapshotImage != null) m_snapshotImage.Dispose();
            if (m_currentDepthBitmap != null) m_currentDepthBitmap.Dispose();
            Mv3dLpSDK.MV3D_LP_Finalize();
        }
    }
}