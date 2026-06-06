using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

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

        // 单位参数
        private float m_fCoordXUnit = 0.02f;

        // 当前帧原始深度数据（用于校准和调试）
        private float[] m_lastSmoothedData = null;
        private int m_lastWidth = 0;
        private uint m_lastFrameNum = 0;

        // 深度最值
        private Label lblDepthMinMax;

        // 调试按钮
        private Button btnDebug;

        enum Mv3dLpImageMode
        {
            MV3D_LP_Origin_Image = 1,
            MV3D_LP_Point_Cloud_Image = 4,
            MV3D_LP_Range_Image = 7,
            MV3D_LP_Intensity_Image = 10,
        };

        // 常量
        private const int DISPLAY_NORMAL = 0;
        private const int DISPLAY_ADAPTIVE = 1;
        private const int DISPLAY_TRUE_ADAPTIVE = 2;

        public MainForm()
        {
            Mv3dLpSDK.MV3D_LP_Initialize();
            m_gapDetector = new GapDetector();
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
            // ============================================================
            // 左侧区域（原有控件）
            // ============================================================

            // === 左侧：设备控制面板 ===
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
            cmbImageMode.Items.AddRange(new object[] {
                "深度图 (Range Image)",
                "亮度图 (Intensity Image)",
                "原始图 (Origin Image)",
                "点云图 (Point Cloud)"
            });
            cmbImageMode.SelectedIndex = 0;
            groupControl.Controls.Add(cmbImageMode);

            this.Controls.Add(groupControl);

            // === 左侧：输入参数（先验知识） ===
            GroupBox groupParams = new GroupBox { Text = "输入参数（先验知识）", Left = 12, Top = 320, Width = 280, Height = 110 };

            Label lblWd = new Label { Text = "线宽 d (mm):", Left = 8, Top = 25, Width = 78 };
            groupParams.Controls.Add(lblWd);
            nudLineWidth = new NumericUpDown { Left = 88, Top = 23, Width = 60, Minimum = 0.01M, Maximum = 100M, Value = 4.10M, DecimalPlaces = 2 };
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

            this.Controls.Add(groupParams);

            // === 左侧：状态信息 ===
            GroupBox groupStatus = new GroupBox { Text = "状态信息", Left = 12, Top = 430, Width = 280, Height = 240 };
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

            // 空筒基准校准按钮
            Button btnCalibrate = new Button
            {
                Text = "校准基准（空筒）",
                Left = 10,
                Top = 165,
                Width = 120,
                Height = 30,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White
            };
            btnCalibrate.Click += BtnCalibrate_Click;
            groupStatus.Controls.Add(btnCalibrate);

            // 调试按钮
            btnDebug = new Button
            {
                Text = "调试：分析深度数据",
                Left = 140,
                Top = 165,
                Width = 130,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            btnDebug.Click += BtnDebug_Click;
            groupStatus.Controls.Add(btnDebug);

            this.Controls.Add(groupStatus);

            // === 左侧：深度图像显示区域 ===
            GroupBox groupDisplay = new GroupBox { Text = "深度图像显示", Left = 305, Top = 12, Width = 700, Height = 598 };
            pictureBoxDepth = new PictureBox
            {
                Left = 10,
                Top = 20,
                Width = 680,
                Height = 568,
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            groupDisplay.Controls.Add(pictureBoxDepth);
            this.Controls.Add(groupDisplay);

            // ============================================================
            // 右侧区域（新增控件）
            // ============================================================

            // === 右上：异常定格截图框 ===
            GroupBox groupSnap = new GroupBox { Text = "异常定格截图", Left = 1020, Top = 12, Width = 350, Height = 290 };

            pictureBoxSnap = new PictureBox
            {
                Left = 10,
                Top = 20,
                Width = 330,
                Height = 220,
                BackColor = Color.FromArgb(60, 60, 60),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            groupSnap.Controls.Add(pictureBoxSnap);

            lblSnapStatus = new Label
            {
                Text = "等待检测...",
                Left = 10,
                Top = 248,
                Width = 330,
                Height = 30,
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };
            groupSnap.Controls.Add(lblSnapStatus);

            this.Controls.Add(groupSnap);

            // === 右下：MAD实时曲线图 ===
            GroupBox groupChart = new GroupBox { Text = "MAD实时曲线 (Modified Z-score)", Left = 1020, Top = 310, Width = 350, Height = 300 };

            pictureBoxMadChart = new PictureBox
            {
                Left = 10,
                Top = 20,
                Width = 330,
                Height = 270,
                BackColor = Color.FromArgb(30, 30, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            groupChart.Controls.Add(pictureBoxMadChart);

            this.Controls.Add(groupChart);

            // 定时器轮询
            m_pollTimer = new Timer { Interval = 100 };
            m_pollTimer.Tick += PollTimer_Tick;
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            StopAcquisition();
            if (m_snapshotImage != null)
            {
                m_snapshotImage.Dispose();
                m_snapshotImage = null;
            }
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
                if (nDevNum == 0)
                {
                    lblStatus.Text = "未找到设备，请检查网络连接!";
                    return;
                }

                MV3D_LP_DEVICE_INFO_VECTOR stVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)nDevNum);
                for (UInt32 i = 0; i < nDevNum; i++)
                {
                    stVector.Add(new MV3D_LP_DEVICE_INFO());
                }

                nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(stVector[0], nDevNum, ref nDevNum);
                if (0 != nRet)
                {
                    lblStatus.Text = string.Format("获取设备列表失败, nRet: 0x{0:x}", nRet);
                    return;
                }

                for (Int32 i = 0; i < nDevNum; i++)
                {
                    string info = string.Format("[{0}] SN:{1}  IP:{2}  型号:{3}",
                        i, stVector[i].chSerialNumber, stVector[i].chCurrentIp, stVector[i].chModelName);
                    listBoxDevices.Items.Add(info);
                }

                if (nDevNum > 0)
                {
                    listBoxDevices.SelectedIndex = 0;
                    btnStartStop.Enabled = true;
                    lblStatus.Text = string.Format("找到 {0} 个设备", nDevNum);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = string.Format("加载设备失败: {0}", ex.Message);
            }
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!m_bMeasuring)
            {
                StartAcquisition();
            }
            else
            {
                StopAcquisition();
            }
        }

        private void StartAcquisition()
        {
            if (m_handle != IntPtr.Zero)
            {
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);
                m_handle = IntPtr.Zero;
            }

            int selectedIndex = listBoxDevices.SelectedIndex;
            if (selectedIndex < 0)
            {
                lblStatus.Text = "请先选择一个设备";
                return;
            }

            try
            {
                UInt32 nDevNum = 0;
                Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref nDevNum);
                if (nDevNum == 0)
                {
                    lblStatus.Text = "没有可用设备!";
                    return;
                }

                MV3D_LP_DEVICE_INFO_VECTOR stVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)nDevNum);
                for (UInt32 i = 0; i < nDevNum; i++)
                {
                    stVector.Add(new MV3D_LP_DEVICE_INFO());
                }
                Mv3dLpSDK.MV3D_LP_GetDeviceList(stVector[0], nDevNum, ref nDevNum);

                int nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_handle, stVector[selectedIndex].chSerialNumber);
                if (0 != nRet)
                {
                    lblStatus.Text = string.Format("打开设备失败! 错误码: 0x{0:x}", nRet);
                    return;
                }

                // 读取X方向单位
                MV3D_LP_PARAM pstParam = new MV3D_LP_PARAM();
                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "LSLProfileCoordXUnit", pstParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    m_fCoordXUnit = pstParam.get_floatparam().fCurValue;
                    m_gapDetector.MmPerPixel = m_fCoordXUnit;
                }

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
                if (Mv3dLpSDK.MV3D_LP_OK != nRet)
                {
                    lblStatus.Text = string.Format("设置图像模式失败! 错误码: 0x{0:x}", nRet);
                    Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);
                    return;
                }

                nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(m_handle);
                if (0 != nRet)
                {
                    lblStatus.Text = string.Format("开始测量失败! 错误码: 0x{0:x}", nRet);
                    Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);
                    return;
                }

                m_bMeasuring = true;
                m_bExitMain = false;
                btnStartStop.Text = "停止采集";
                lblStatus.Text = "采集中...";
                cmbImageMode.Enabled = false;

                // 重置状态
                m_madHistory.Clear();
                m_lastGapResult = null;
                m_hasAnomaly = false;
                m_consecutiveAnomalyFrames = 0;
                if (m_snapshotImage != null)
                {
                    m_snapshotImage.Dispose();
                    m_snapshotImage = null;
                }

                // 同步参数到检测器
                SyncParametersToDetector();

                m_pollTimer.Start();
            }
            catch (Exception ex)
            {
                lblStatus.Text = string.Format("启动失败: {0}", ex.Message);
                if (m_handle != IntPtr.Zero)
                {
                    Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);
                    m_handle = IntPtr.Zero;
                }
            }
        }

        private void StopAcquisition()
        {
            m_pollTimer.Stop();
            m_bExitMain = true;
            m_bMeasuring = false;

            if (m_handle != IntPtr.Zero)
            {
                Mv3dLpSDK.MV3D_LP_StopMeasure(m_handle);
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);
                m_handle = IntPtr.Zero;
            }

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
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            if (m_handle == IntPtr.Zero || m_bExitMain)
            {
                m_pollTimer.Stop();
                return;
            }

            try
            {
                MV3D_LP_IMAGE_DATA stImage = new MV3D_LP_IMAGE_DATA();
                int nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_handle, stImage, 500);

                if (0 == nRet)
                {
                    m_lastFrameNum = stImage.nFrameNum;

                    lblFrameInfo.Text = string.Format("帧号: {0}  尺寸: {1} x {2}  数据长度: {3}  类型: {4}",
                        stImage.nFrameNum, stImage.nWidth, stImage.nHeight, stImage.nDataLen, stImage.enImageType);

                    // 同步UI参数到检测器
                    SyncParametersToDetector();

                    // 在 DisplayImage 之前做缝隙检测
                    ProcessAnomalyDetection(stImage);

                    // 原有逻辑：SDK显示图像
                    nRet = Mv3dLpSDK.MV3D_LP_DisplayImage(
                        stImage,
                        pictureBoxDepth.Handle,
                        (uint)DISPLAY_ADAPTIVE,
                        0,
                        30000
                    );

                    if (0 != nRet)
                    {
                        lblStatus.Text = string.Format("显示图像返回: 0x{0:x}", nRet);
                    }
                    else
                    {
                        lblStatus.Text = "采集中 - 接收正常";
                    }

                    // 更新右侧UI
                    UpdateRightPanel();
                }
                else if (nRet == unchecked((int)0x80060006))
                {
                    // MV3D_LP_E_NODATA：暂无新数据，属于正常情况
                }
                else if (nRet != 1)
                {
                    lblStatus.Text = string.Format("获取图像失败: 0x{0:x}", nRet);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = string.Format("采集异常: {0}", ex.Message);
            }
        }

        private void ProcessAnomalyDetection(MV3D_LP_IMAGE_DATA stImage)
        {
            if (stImage.pData == IntPtr.Zero || stImage.nDataLen == 0 || stImage.nWidth == 0)
                return;

            try
            {
                int pixelCount = (int)(stImage.nWidth * stImage.nHeight);
                if (pixelCount <= 0)
                {
                    lblGapInfo.Text = "缝隙检测: 像素数为0";
                    return;
                }

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
                    for (int i = 0; i < pixelCount; i++)
                    {
                        depthValues[i] = BitConverter.ToInt16(buffer, i * 2);
                    }
                }
                else if (stImage.nDataLen >= pixelCount * 2 && stImage.nDataLen < pixelCount * 12)
                {
                    if (stImage.nDataLen < pixelCount * 2) return;
                    byte[] buffer = new byte[pixelCount * 2];
                    Marshal.Copy(stImage.pData, buffer, 0, pixelCount * 2);
                    depthValues = new short[pixelCount];
                    for (int i = 0; i < pixelCount; i++)
                    {
                        depthValues[i] = BitConverter.ToInt16(buffer, i * 2);
                    }
                }
                else if (stImage.nDataLen >= pixelCount * 12)
                {
                    byte[] pcBuffer = new byte[(int)stImage.nDataLen];
                    Marshal.Copy(stImage.pData, pcBuffer, 0, (int)stImage.nDataLen);
                    int pointCount = (int)stImage.nDataLen / 4;
                    float[] pcData = new float[pointCount];
                    for (int i = 0; i < pointCount; i++)
                    {
                        pcData[i] = BitConverter.ToSingle(pcBuffer, i * 4);
                    }
                    int totalPoints = (int)(stImage.nWidth * stImage.nHeight);
                    if (totalPoints <= 0 || pointCount < totalPoints * 3) return;
                    depthValues = new short[totalPoints];
                    for (int i = 0; i < totalPoints; i++)
                    {
                        float z = pcData[i * 3 + 2];
                        if (float.IsNaN(z) || float.IsInfinity(z) || z <= -99999 || z >= 99999)
                            depthValues[i] = 0;
                        else
                        {
                            double zScaled = z * 100;
                            if (zScaled > 32767) depthValues[i] = 32767;
                            else if (zScaled < -32768) depthValues[i] = -32768;
                            else depthValues[i] = (short)zScaled;
                        }
                    }
                }
                else
                {
                    lblGapInfo.Text = "不支持的图像类型用于缝隙检测";
                    return;
                }

                if (depthValues == null) return;

                // 提取中心行数据（多行平均）
                short[] depthLine;
                if (height > 1)
                {
                    depthLine = GapDetector.ExtractDepthLineAvg(depthValues, width, height, height / 2, halfWindow: 2);
                }
                else
                {
                    depthLine = depthValues;
                }

                // 预处理并缓存 smoothed 数据（用于校准和调试）
                float[] smoothed = PreprocessForDetection(depthLine, width);
                if (smoothed != null)
                {
                    m_lastSmoothedData = smoothed;
                    m_lastWidth = width;
                }

                // 执行检测
                GapDetector.GapResult result = m_gapDetector.ProcessDepthLine(depthLine, width);
                UpdateGapResult(result, stImage.nFrameNum);
            }
            catch (Exception ex)
            {
                lblGapInfo.Text = string.Format("检测异常: {0}", ex.Message);
            }
        }

        private float[] PreprocessForDetection(short[] depthData, int width)
        {
            float[] result = new float[width];
            int validCount = 0;

            for (int i = 0; i < width; i++)
            {
                if (depthData[i] > 0)
                {
                    result[i] = depthData[i];
                    validCount++;
                }
                else
                {
                    result[i] = -1;
                }
            }

            if (validCount < 20)
                return null;

            float[] smoothed = new float[width];
            for (int i = 0; i < width; i++)
            {
                if (result[i] < 0)
                {
                    smoothed[i] = -1;
                    continue;
                }

                List<float> window = new List<float>();
                for (int j = -1; j <= 1; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < width && result[idx] > 0)
                        window.Add(result[idx]);
                }
                if (window.Count > 0)
                {
                    window.Sort();
                    smoothed[i] = window[window.Count / 2];
                }
                else
                {
                    smoothed[i] = result[i];
                }
            }

            return smoothed;
        }

        private void UpdateGapResult(GapDetector.GapResult result, uint frameNum)
        {
            m_lastGapResult = result;

            if (result.HasValidData)
            {
                // MAD历史记录
                m_madHistory.Add(result.ModifiedZScore);
                if (m_madHistory.Count > MAX_MAD_HISTORY)
                {
                    m_madHistory.RemoveAt(0);
                }

                // 连续异常帧计数
                if (result.IsAnomaly)
                {
                    m_consecutiveAnomalyFrames++;
                    lblMadValue.ForeColor = Color.Red;

                    // 连续3帧异常触发截图
                    if (m_consecutiveAnomalyFrames >= ANOMALY_TRIGGER_THRESHOLD && !m_hasAnomaly)
                    {
                        m_hasAnomaly = true;
                        m_anomalyFrameNum = (int)frameNum;
                        CaptureSnapshot();

                        // 显示异常原因
                        string reasons = result.AnomalyReasons.Count > 0
                            ? string.Join(" | ", result.AnomalyReasons)
                            : "未知异常";
                        lblSnapStatus.Text = $"⚠️ 异常触发 (连续{m_consecutiveAnomalyFrames}帧) | {reasons}";
                        lblSnapStatus.ForeColor = Color.Red;
                    }

                    // 显示当前异常原因
                    if (result.AnomalyReasons.Count > 0)
                    {
                        lblMadValue.Text = $"异常: {result.AnomalyReasons[0]}";
                    }
                    else
                    {
                        lblMadValue.Text = $"检测状态: 异常 (连续{m_consecutiveAnomalyFrames}帧)";
                    }
                }
                else
                {
                    // 任意一帧正常，重置计数
                    m_consecutiveAnomalyFrames = 0;
                    m_hasAnomaly = false;
                    lblMadValue.ForeColor = Color.Green;
                    lblMadValue.Text = "检测状态: 正常";
                }

                // 显示层数和匝数信息
                string statusText = result.IsAnomaly ? "⚠️ 异常" : "✓ 正常";
                string layerText = result.CurrentLayer >= 0 ? $"第{result.CurrentLayer + 1}层" : "层数未知";
                string completeText = result.IsLayerCompleted ? "已完成" : "绕制中";

                lblGapInfo.Text = $"{layerText} | {completeText} | {statusText}";

                // 匝数显示
                if (result.InferredTurn == 0)
                {
                    lblDepthMinMax.Text = $"匝数: 未检测到 (帧{frameNum})";
                    lblDepthMinMax.ForeColor = Color.Orange;
                }
                else
                {
                    lblDepthMinMax.Text = $"匝数: {result.InferredTurn} / {m_gapDetector.TurnsPerLayer}";
                    lblDepthMinMax.ForeColor = result.InferredTurn == m_gapDetector.TurnsPerLayer ? Color.White : Color.Yellow;
                }
            }
            else
            {
                lblGapInfo.Text = "缝隙检测: 数据不足";
                lblDepthMinMax.Text = $"匝数: 无数据";
            }
        }

        private void CaptureSnapshot()
        {
            try
            {
                if (pictureBoxDepth.Image != null)
                {
                    if (m_snapshotImage != null)
                    {
                        m_snapshotImage.Dispose();
                    }
                    m_snapshotImage = new Bitmap(pictureBoxDepth.Image);
                }
                else
                {
                    Rectangle rect = pictureBoxDepth.RectangleToScreen(pictureBoxDepth.ClientRectangle);
                    if (m_snapshotImage != null)
                    {
                        m_snapshotImage.Dispose();
                    }
                    m_snapshotImage = new Bitmap(rect.Width, rect.Height);
                    using (Graphics g = Graphics.FromImage(m_snapshotImage))
                    {
                        g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
                    }
                }
            }
            catch
            {
            }
        }

        private void UpdateRightPanel()
        {
            if (m_hasAnomaly && m_snapshotImage != null)
            {
                pictureBoxSnap.Image = m_snapshotImage;
            }
            else if (!m_hasAnomaly && m_consecutiveAnomalyFrames == 0)
            {
                // 不覆盖异常状态
                if (lblSnapStatus.Text.Contains("异常"))
                {
                    // 保持异常显示
                }
                else
                {
                    lblSnapStatus.Text = "正常 - 等待异常...";
                    lblSnapStatus.ForeColor = Color.Gray;
                }
            }

            DrawMadChart();
        }

        private void DrawMadChart()
        {
            if (pictureBoxMadChart.Width <= 10 || pictureBoxMadChart.Height <= 10)
                return;

            Bitmap bmp = new Bitmap(pictureBoxMadChart.Width, pictureBoxMadChart.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(30, 30, 30));

                int width = bmp.Width;
                int height = bmp.Height;
                int marginLeft = 40;
                int marginRight = 10;
                int marginTop = 20;
                int marginBottom = 30;
                int chartWidth = width - marginLeft - marginRight;
                int chartHeight = height - marginTop - marginBottom;

                if (chartWidth <= 0 || chartHeight <= 0)
                    return;

                // 坐标轴
                using (Pen axisPen = new Pen(Color.FromArgb(100, 100, 100)))
                {
                    g.DrawLine(axisPen, marginLeft, marginTop, marginLeft, height - marginBottom);
                    g.DrawLine(axisPen, marginLeft, height - marginBottom, width - marginRight, height - marginBottom);
                }

                // 阈值红线 (y = 5.0)
                float thresholdY = marginTop + chartHeight - (float)(5.0 / 10.0 * chartHeight);
                using (Pen redPen = new Pen(Color.Red, 2))
                {
                    redPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawLine(redPen, marginLeft, thresholdY, width - marginRight, thresholdY);
                }

                using (Brush redBrush = new SolidBrush(Color.Red))
                {
                    g.DrawString("阈值 5.0", new Font("Arial", 8), redBrush, width - marginRight - 70, thresholdY - 15);
                }

                // 刻度
                using (Brush labelBrush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                using (Font font = new Font("Arial", 8))
                {
                    g.DrawString("10", font, labelBrush, marginLeft - 20, marginTop - 8);
                    g.DrawString("5", font, labelBrush, marginLeft - 15, marginTop + chartHeight / 2 - 8);
                    g.DrawString("0", font, labelBrush, marginLeft - 15, height - marginBottom - 8);
                }

                // MAD曲线
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

                    // 最近数据点
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
                {
                    using (Brush infoBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                    using (Font font = new Font("Arial", 10))
                    {
                        g.DrawString("等待数据...", font, infoBrush, width / 2 - 40, height / 2 - 10);
                    }
                }

                using (Brush axisLabelBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
                using (Font font = new Font("Arial", 7))
                {
                    g.DrawString("Z-score", font, axisLabelBrush, 2, marginTop + 5);
                    g.DrawString("帧 (最近60帧)", font, axisLabelBrush, width / 2 - 30, height - 15);
                }
            }

            if (pictureBoxMadChart.Image != null)
            {
                pictureBoxMadChart.Image.Dispose();
            }
            pictureBoxMadChart.Image = bmp;
        }

        private void BtnCalibrate_Click(object sender, EventArgs e)
        {
            if (!m_bMeasuring || m_handle == IntPtr.Zero)
            {
                lblStatus.Text = "请先开始采集再校准";
                return;
            }

            if (m_lastSmoothedData != null && m_lastWidth > 0)
            {
                m_gapDetector.CalibrateBaseline(m_lastSmoothedData, m_fCoordXUnit);
                lblStatus.Text = "✓ 基准校准完成（空筒基准已记录）";
                lblStatus.ForeColor = Color.Green;
            }
            else
            {
                lblStatus.Text = "校准失败: 无有效深度数据";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void BtnDebug_Click(object sender, EventArgs e)
        {
            if (m_lastSmoothedData == null || m_lastWidth == 0)
            {
                MessageBox.Show("暂无深度数据，请先开始采集", "调试信息",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 分析当前缓存的 smoothed 数据
            float[] data = m_lastSmoothedData;
            List<float> validVals = new List<float>();
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] > 0) validVals.Add(data[i]);
            }

            if (validVals.Count == 0)
            {
                MessageBox.Show("没有有效深度数据", "调试信息",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            float minVal = validVals.Min();
            float maxVal = validVals.Max();
            int minIdx = Array.IndexOf(data, minVal);
            int maxIdx = Array.IndexOf(data, maxVal);

            // 分析波峰波谷特征
            int peakCount = 0;
            int valleyCount = 0;
            List<int> peaks = new List<int>();
            List<int> valleys = new List<int>();

            for (int i = 3; i < data.Length - 3; i++)
            {
                if (data[i] <= 0) continue;

                // 检测局部最大值（波峰）
                bool isPeak = data[i] > data[i - 1] && data[i] > data[i + 1] &&
                              data[i] > data[i - 2] && data[i] > data[i + 2];
                if (isPeak)
                {
                    peakCount++;
                    peaks.Add(i);
                }

                // 检测局部最小值（波谷）
                bool isValley = data[i] < data[i - 1] && data[i] < data[i + 1] &&
                                data[i] < data[i - 2] && data[i] < data[i + 2];
                if (isValley)
                {
                    valleyCount++;
                    valleys.Add(i);
                }
            }

            // 计算平均间距
            float avgPeakSpacing = 0, avgValleySpacing = 0;
            if (peaks.Count >= 2)
            {
                float totalSpacing = 0;
                for (int i = 1; i < peaks.Count; i++)
                    totalSpacing += (peaks[i] - peaks[i - 1]) * m_fCoordXUnit;
                avgPeakSpacing = totalSpacing / (peaks.Count - 1);
            }
            if (valleys.Count >= 2)
            {
                float totalSpacing = 0;
                for (int i = 1; i < valleys.Count; i++)
                    totalSpacing += (valleys[i] - valleys[i - 1]) * m_fCoordXUnit;
                avgValleySpacing = totalSpacing / (valleys.Count - 1);
            }

            string msg = $"=== 深度数据分析 (帧 {m_lastFrameNum}) ===\n\n" +
                         $"有效点数: {validVals.Count}/{data.Length}\n" +
                         $"深度范围: {minVal:F0} ~ {maxVal:F0}\n" +
                         $"最小值(最高点)位置: X={minIdx}, 值={minVal:F0}\n" +
                         $"最大值(最低点)位置: X={maxIdx}, 值={maxVal:F0}\n" +
                         $"深度跨度: {maxVal - minVal:F0}\n\n" +
                         $"=== 波峰/波谷统计 ===\n" +
                         $"检测到的波峰数(局部最大): {peakCount}\n" +
                         $"波峰平均间距: {avgPeakSpacing:F2} mm\n" +
                         $"检测到的波谷数(局部最小): {valleyCount}\n" +
                         $"波谷平均间距: {avgValleySpacing:F2} mm\n\n" +
                         $"=== 判断建议 ===\n" +
                         $"预期匝数: {nudTurnsPerLayer.Value}\n" +
                         $"预期间距: {nudLineWidth.Value} mm\n\n" +
                         $"根据传感器坐标系:\n" +
                         $"- 如果线材顶部是【波峰】(局部最大)，匝数 = {peakCount}\n" +
                         $"- 如果线材顶部是【波谷】(局部最小)，匝数 = {valleyCount}\n\n" +
                         $"请观察哪个值与预期匝数({nudTurnsPerLayer.Value})和间距({nudLineWidth.Value}mm)更接近！";

            MessageBox.Show(msg, "调试信息 - 请确认线材顶部对应哪种波形",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopAcquisition();
            if (m_snapshotImage != null)
            {
                m_snapshotImage.Dispose();
                m_snapshotImage = null;
            }
            Mv3dLpSDK.MV3D_LP_Finalize();
        }
    }
}