using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace BasicDemo
{
    public partial class Form1 : Form
    {        
        // 可用的设备数目 | Available Device Number
        uint m_nDevNum = 0;   
        uint m_nCanOpenDevNum = 0; 
        MV3D_LP_DEVICE_INFO_VECTOR m_stVector; 
        String[] m_strDevSN = new String[4];
        MyCamDev[] m_pcMyCamDev = new MyCamDev[4];
        IntPtr[] m_pHwnd = new IntPtr[4];
        int[] m_nCurSelIndex = new int[4];


        Graphics gra = null;
        Pen pen = new Pen(Color.Blue, 3);                 
        Point[] stPointList = new Point[4];               


        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern Int32 ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
            m_pcMyCamDev = new MyCamDev[4];
            VersionInfoEdit.Text = Mv3dLpSDK.MV3D_LP_GetVersion();

            CreateDisplay();
            InitCtrlStat();
        }

        //将Byte转换为结构体类型 | Byte To Struct
        public static object ByteToStruct(byte[] bytes, Type type)
        {
            int size = Marshal.SizeOf(type);
            if (size > bytes.Length)
            {
                return null;
            }

            IntPtr structPtr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, structPtr, size);
            object obj = Marshal.PtrToStructure(structPtr, type);
            Marshal.FreeHGlobal(structPtr);
            return obj;
        }

        private void EnumDeviceBtn_Click(object sender, EventArgs e)
        {
            CamCbx.Items.Clear();
            Cam1Cbx.Items.Clear();
            Cam2Cbx.Items.Clear();
            Cam3Cbx.Items.Clear();
            Cam4Cbx.Items.Clear();

            m_nDevNum = 0;
            int nRet = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref m_nDevNum);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Fail to GetDeviceNumber!!", 0);
            }
            else
            {
                GetDevNumEdit.Text = m_nDevNum.ToString();
            }
            if (m_nDevNum == 0)
            {
                return;
            }

            m_stVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)m_nDevNum);
            for (UInt32 i = 0; i < m_nDevNum; i++)
            {
                m_stVector.Add(new MV3D_LP_DEVICE_INFO());
            }

            // 获取网络中设备信息 | Get Device Info
            nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(m_stVector[0], m_nDevNum, ref m_nDevNum);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Enumerate devices fail!", nRet);
                return;
            }

            Cam1Cbx.Items.Add("None");
            Cam2Cbx.Items.Add("None");
            Cam3Cbx.Items.Add("None");
            Cam4Cbx.Items.Add("None");
            // 在窗体列表中显示设备名 | Display device name in the form list
            for (int i = 0; i < m_nDevNum; i++)
            {
                string strSerialNumber = m_stVector[i].chSerialNumber;
                strSerialNumber = strSerialNumber.TrimEnd('\0');

                string strModelName = m_stVector[i].chModelName;
                strModelName = strModelName.TrimEnd('\0');

                string strCurrentIp = m_stVector[i].chCurrentIp;
                strCurrentIp = strCurrentIp.TrimEnd('\0');
                CamCbx.Items.Add("DEV:" + "Name:" + strModelName + " " + "IP:" + strCurrentIp + " " + "SerialNum:" + strSerialNumber);

                Cam1Cbx.Items.Add(strSerialNumber);
                Cam2Cbx.Items.Add(strSerialNumber);
                Cam3Cbx.Items.Add(strSerialNumber);
                Cam4Cbx.Items.Add(strSerialNumber);
            }

            // 选择第一项 | Select the first item
            if (0 < m_nDevNum)
            {
                CamCbx.SelectedIndex = 0;
                Cam1Cbx.SelectedIndex = 0;
                Cam2Cbx.SelectedIndex = 0;
                Cam3Cbx.SelectedIndex = 0;
                Cam4Cbx.SelectedIndex = 0;

                Cam1Cbx.Enabled = true;
                Cam2Cbx.Enabled = true;
                Cam3Cbx.Enabled = true;
                Cam4Cbx.Enabled = true;
                OpenDevBtn.Enabled = true;
            }
        }

        private void OpenDevBtn_Click(object sender, EventArgs e)
        {
            bool bIsRepeat = IsRepeatIndex();
            if (true == bIsRepeat)
            {
                ShowErrorMsg("Please select right camera!", 0);
                return;
            }

            int nCurDevIndex = 0;
            m_nCanOpenDevNum = 0;
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            for (int i = 0; i < 4; i++)
            {
                nCurDevIndex = m_nCurSelIndex[i];
                if (0 == nCurDevIndex)
                {
                    continue;
                }

                // 打开设备 | Open Device
                if (null == m_pcMyCamDev[i])
                {
                    m_pcMyCamDev[i] = new MyCamDev();
                    if (null == m_pcMyCamDev[i])
                    {
                        return;
                    }
                }

                nRet = m_pcMyCamDev[i].Open(m_stVector[nCurDevIndex - 1].chSerialNumber);
                if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
                {
                    ShowErrorMsgAndCamIndex(i, "Fail to Open!!", nRet);
                }
                else
                {
                    m_nCanOpenDevNum++;
                }
            }
            if ((0 != m_nCanOpenDevNum))
            {
                EnableCtrlBySwitch(true);
            }
            else
            {
                ShowErrorMsg("Please select right camera!", 0);
            }
        }

        private void CloseDevBtn_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            int nCurDevIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                nCurDevIndex = m_nCurSelIndex[i];
                if (0 == nCurDevIndex)
                {
                    continue;
                }
                if (null != m_pcMyCamDev[i])
                {
                    nRet = m_pcMyCamDev[i].Close();
                    if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
                    {
                        ShowErrorMsgAndCamIndex(i, "Fail to Close!!", nRet);
                    }
                }
                m_pcMyCamDev[i] = null;
            }

            if ((0 != m_nCanOpenDevNum) && ((int)Mv3dLpSDK.MV3D_LP_OK == nRet))
            {
                EnableCtrlBySwitch(false);
            }

            Mv3dLpSDK.MV3D_LP_Finalize();
            m_nCanOpenDevNum = 0;
            CamDisplay1.Image = null;
            CamDisplay2.Image = null;
            CamDisplay3.Image = null;
            CamDisplay4.Image = null;
        }

        private void StartGrabBtn_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            int nCurDevIndex = 0;
            int nSuccedGrabCameraNum = 0;
            for (int i = 0; i < 4; i++)
            {
                nCurDevIndex = m_nCurSelIndex[i];
                if (0 == nCurDevIndex)
                {
                    continue;
                }

                nRet = m_pcMyCamDev[i].StartGrabbing(m_pHwnd[i]);
                if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
                {
                    ShowErrorMsgAndCamIndex(i, "Start Measure Fail!!", nRet);
                }
                else
                {
                    nSuccedGrabCameraNum++;
                }
            }
            if (nSuccedGrabCameraNum>0)
            {
                EnableCtrlByGrabStat(true);
            }
        }

        private void StopGrabBtn_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            int nCurDevIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                nCurDevIndex = m_nCurSelIndex[i];
                if (0 == nCurDevIndex)
                {
                    continue;
                }

                nRet = m_pcMyCamDev[i].StopGrabbing();
                if (Mv3dLpSDK.MV3D_LP_OK != nRet)
                {
                    ShowErrorMsgAndCamIndex(i, "Stop Measure Fail!!", nRet);
                }
            }
            EnableCtrlByGrabStat(false);
        }

        private void SoftwareTriggerBtn_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            int nCurDevIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                nCurDevIndex = m_nCurSelIndex[i];
                if (0 == nCurDevIndex)
                {
                    continue;
                }
                nRet = (int)m_pcMyCamDev[i].SoftTrigger();
                if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
                {
                    ShowErrorMsgAndCamIndex(i, "SoftwareTrigger Fail!!", nRet);
                }
            }
        }

        private void SaveRaw_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            int nCurDevIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                nCurDevIndex = m_nCurSelIndex[i];
                if (0 == nCurDevIndex)
                {
                    continue;
                }
                nRet = (int)m_pcMyCamDev[i].SaveRAW();
                if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
                {
                    ShowErrorMsgAndCamIndex(i, "Save raw file failed!!", nRet);
                }
                else
                {
                    ShowErrorMsgAndCamIndex(i, "Success save raw file!!", nRet);
                }
                
            }
        }

        private void CreateDisplay()
        {
            CamDisplay1.Show();
            gra = CamDisplay1.CreateGraphics();
            CamDisplay2.Show();
            gra = CamDisplay2.CreateGraphics();
            CamDisplay3.Show();
            gra = CamDisplay3.CreateGraphics();
            CamDisplay4.Show();
            gra = CamDisplay4.CreateGraphics();

            m_pHwnd[0] = CamDisplay1.Handle;
            m_pHwnd[1] = CamDisplay2.Handle;
            m_pHwnd[2] = CamDisplay3.Handle;
            m_pHwnd[3] = CamDisplay4.Handle;

        }

        // 显示错误信息 | Show error message
        public void ShowErrorMsg(string csMessage, int nErrorNum)
        {
            string errorMsg;
            if (nErrorNum == Mv3dLpSDK.MV3D_LP_OK)
            {
                errorMsg = csMessage;
            }
            else
            {
                errorMsg = csMessage + ": Error =" + String.Format("{0:X}", nErrorNum);
            }
            int nErrNum = nErrorNum;
            switch (nErrNum)
            {
                case Mv3dLpSDK.MV3D_LP_E_HANDLE: errorMsg += " Error or invalid handle "; break;
                case Mv3dLpSDK.MV3D_LP_E_SUPPORT: errorMsg += " Not supported function "; break;
                case Mv3dLpSDK.MV3D_LP_E_BUFOVER: errorMsg += " Cache is full "; break;
                case Mv3dLpSDK.MV3D_LP_E_CALLORDER: errorMsg += " Function calling order error "; break;
                case Mv3dLpSDK.MV3D_LP_E_PARAMETER: errorMsg += " Incorrect parameter "; break;
                case Mv3dLpSDK.MV3D_LP_E_RESOURCE: errorMsg += " Applying resource failed "; break;
                case Mv3dLpSDK.MV3D_LP_E_NODATA: errorMsg += " No data "; break;
                case Mv3dLpSDK.MV3D_LP_E_PRECONDITION: errorMsg += " Precondition error, or running environment changed "; break;
                case Mv3dLpSDK.MV3D_LP_E_VERSION: errorMsg += " Version mismatches "; break;
                case Mv3dLpSDK.MV3D_LP_E_NOENOUGH_BUF: errorMsg += " Insufficient memory "; break;
                case Mv3dLpSDK.MV3D_LP_E_ABNORMAL_IMAGE: errorMsg += " error image "; break;
                case Mv3dLpSDK.MV3D_LP_E_LOAD_LIBRARY: errorMsg += " load dll  error "; break;
                case Mv3dLpSDK.MV3D_LP_E_UNKNOW: errorMsg += " Unknown error "; break;
            }

            MessageBox.Show(errorMsg, "PROMPT");
        }

        private void ShowErrorMsgAndCamIndex(int nCamIndex, string csMessage, int nErrorNum)
        {
            string strCamInfo = "";
            if ((4 <= nCamIndex) && (0 > nCamIndex))
            {
                return;
            }
            else
            {
                strCamInfo = "Cam" + Convert.ToString(nCamIndex+1);
            }

            csMessage = strCamInfo + csMessage;
            ShowErrorMsg(csMessage, nErrorNum);
        }

        private bool IsRepeatIndex()
        {
            m_nCurSelIndex[0] = Cam1Cbx.SelectedIndex;
            m_nCurSelIndex[1] = Cam2Cbx.SelectedIndex;
            m_nCurSelIndex[2] = Cam3Cbx.SelectedIndex;
            m_nCurSelIndex[3] = Cam4Cbx.SelectedIndex;

            for (int i = 0; i < 4 - 1; i++)
            {
                if (0 == m_nCurSelIndex[i])
                {
                    continue;
                }

                for (int j = i + 1; j < 4; j++)
                {
                    if ((m_nCurSelIndex[i] == m_nCurSelIndex[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
                
        private void InitCtrlStat()
        {
            OpenDevBtn.Enabled = false;
            CloseDevBtn.Enabled = false;

            StartGrabBtn.Enabled = false;
            StopGrabBtn.Enabled = false;
            SoftwareTriggerBtn.Enabled = false;
            SaveRaw.Enabled = false;
        }

        private void EnableCtrlBySwitch(bool bOpenDev)
        {
            OpenDevBtn.Enabled = !bOpenDev;
            CloseDevBtn.Enabled = bOpenDev;
                
            StartGrabBtn.Enabled = bOpenDev;
            StopGrabBtn.Enabled = false;
            SoftwareTriggerBtn.Enabled = false;
            SaveRaw.Enabled = false;
        }

        private void EnableCtrlByGrabStat(bool bStartGrab)
        {
            StartGrabBtn.Enabled = !bStartGrab;
            StopGrabBtn.Enabled = bStartGrab;
            SoftwareTriggerBtn.Enabled = bStartGrab;
            SaveRaw.Enabled = bStartGrab;
        }
    }
}
