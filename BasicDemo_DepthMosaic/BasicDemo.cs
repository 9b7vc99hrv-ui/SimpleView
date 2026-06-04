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


using STC_Object = System.IntPtr;
using STC_DataSet = System.IntPtr;


namespace BasicDemo
{
    public partial class Form1 : Form
    {
        private static readonly object Lock = new object();
        UInt32 m_nDevNum = 0;
        STC_DataSet m_DevHandle = IntPtr.Zero;
        STC_DataSet m_DevHandleSecond = IntPtr.Zero;
        MV3D_LP_DEVICE_INFO_VECTOR m_stVector;   
        bool m_bGrabbing = false;
        Thread m_hReceiveThread = null;

        static MV3D_LP_IMAGE_DATA m_stImageInfo = new MV3D_LP_IMAGE_DATA();
        static UInt32 m_MaxImageSize = 1024 * 1024 * 30;
        static byte[] m_pcDataBuf = new byte[m_MaxImageSize];

        enum Mv3dLpImageMode
        {
            MV3D_LP_Origin_Image = 1,
            MV3D_LP_Point_Cloud_Image = 4,
            MV3D_LP_Range_Image = 7,
        };

        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;

            pictureBox1.Show();
            InitCtrlData();
        }

        // 显示错误信息 | Show error message
        private void ShowErrorMsg(string csMessage, int nErrorNum)
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

        private void bnEnum_Click(object sender, EventArgs e)
        {
            DeviceListAcq();
        }

        private void DeviceListAcq()
        {
            // 创建设备列表 | Create Device List
            System.GC.Collect();
            cbDeviceList.Items.Clear();
            cbDeviceList2.Items.Clear();
            m_nDevNum = 0;

            int nRet = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref m_nDevNum);
            m_stVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)m_nDevNum);
            if (m_nDevNum == 0)
            {
                return;
            }
            for (UInt32 i = 0; i < m_nDevNum; i++)
            {
                m_stVector.Add(new MV3D_LP_DEVICE_INFO());
            }
            // 获取网络中设备信息 | Get Devices Infomation
            nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(m_stVector[0], m_nDevNum, ref m_nDevNum);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Enumerate devices fail!", nRet);
                return;
            }

            // 在窗体列表中显示设备名 | Display device name in the form list
            for (int i = 0; i < m_nDevNum; i++)
            {
                string strSerialNumber = m_stVector[i].chSerialNumber;
                strSerialNumber = strSerialNumber.TrimEnd('\0');
                string strModelName = m_stVector[i].chModelName;
                strModelName = strModelName.TrimEnd('\0');
                string strCurrentIp = m_stVector[i].chCurrentIp;
                strCurrentIp = strCurrentIp.TrimEnd('\0');
                cbDeviceList.Items.Add("DEV:" + "Name:" + strModelName + " " + "IP:" + strCurrentIp + " " + "SerialNum:" + strSerialNumber);
                cbDeviceList2.Items.Add("DEV:" + "Name:" + strModelName + " " + "IP:" + strCurrentIp + " " + "SerialNum:" + strSerialNumber);
            }

            // 选择第一项 | Select the first item
            if (m_nDevNum != 0)
            {
                cbDeviceList.SelectedIndex = 0;
                cbDeviceList2.SelectedIndex = 0;
            }

            bnOpen.Enabled = true;
        }

        private void SetCtrlWhenOpen()
        {
            bnOpen.Enabled = false;
            bnClose.Enabled = true;
            bnStartGrab.Enabled = true;
            bnStopGrab.Enabled = false;
            tbExposure.Enabled = true;
            bnGetParam.Enabled = true;
            bnSetParam.Enabled = true;
            bnEnum.Enabled = false;
        }

        private void bnOpen_Click(object sender, EventArgs e)
        {
            if (m_nDevNum == 0 || cbDeviceList.SelectedIndex == -1)
            {
                ShowErrorMsg("No device, please select", 0);
                return;
            }

            // 打开设备 | Open device
            int nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_DevHandle, m_stVector[cbDeviceList.SelectedIndex].chSerialNumber);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Open Device Failed ", nRet);
                return;
            }

            nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_DevHandleSecond, m_stVector[cbDeviceList2.SelectedIndex].chSerialNumber);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Open Device 2 Failed ", nRet);
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandle);
                return;
            }

            //设置深度图模式 | Set Depth ImageMode
            MV3D_LP_PARAM pstValue = new MV3D_LP_PARAM();
            MV3D_LP_ENUMPARAM enumParam = new MV3D_LP_ENUMPARAM();
            enumParam.nCurValue = (uint)Mv3dLpImageMode.MV3D_LP_Range_Image;
            pstValue.set_enumparam(enumParam);
            nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_DevHandle, "ImageMode", pstValue);
            if (Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SetParam ImageMode failed! error code:", nRet);
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandle);
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandleSecond);
                return;
            }

            nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_DevHandleSecond, "ImageMode", pstValue);
            if (Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SetParam ImageMode failed! error code:", nRet);
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandle);
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandleSecond);
                return;
            }


            bnGetParam_Click(null, null);// ch:获取参数 | en:Get parameters

            // 控件操作 | Control operation
            SetCtrlWhenOpen();
        }

        private void SetCtrlWhenClose()
        {
            bnOpen.Enabled = true;
            bnEnum.Enabled = true;
            bnClose.Enabled = false;
            bnStartGrab.Enabled = false;
            bnStopGrab.Enabled = false;
            tbExposure.Enabled = false;
            bnStartGrab.Enabled = false;
            bnStopGrab.Enabled = false;
            tbExposure.Enabled = false;
            bnGetParam.Enabled = false;
            bnSetParam.Enabled = false;

            bnSaveRAW.Enabled = false;
            bnSavePLY.Enabled = false;
            bnSaveTIFF.Enabled = false;
            bnSaveCSV.Enabled = false;
            bnSaveOBJ.Enabled = false;
            bnSaveBMP.Enabled = false;
            bnSaveJPG.Enabled = false; 
        }

        private void bnClose_Click(object sender, EventArgs e)
        {
            // 取流标志位清零 | Reset flow flag bit
            if (m_bGrabbing == true)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
            }

            this.pictureBox1.Image = null;


            // 关闭设备 | Close Device
            Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandle);
            Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandle);

            Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandleSecond);
            Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandleSecond);

            m_stImageInfo.nFrameNum = 0;
            // 控件操作 | Control Operation
            SetCtrlWhenClose();
        }

        private void SetCtrlWhenStartGrab()
        {
            bnStartGrab.Enabled = false;
            bnStopGrab.Enabled = true;
            bnSaveRAW.Enabled = true;
            bnSavePLY.Enabled = true;
            bnSaveTIFF.Enabled = true;
            bnSaveCSV.Enabled = true;
            bnSaveOBJ.Enabled = true;
            bnSaveBMP.Enabled = true;
            bnSaveJPG.Enabled = true;
        }

        private int DisplayImage(MV3D_LP_IMAGE_DATA pstImage)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;

            {
                Monitor.Enter(Lock);
                m_stImageInfo.nWidth = pstImage.nWidth;
                m_stImageInfo.nHeight = pstImage.nHeight;
                m_stImageInfo.nDataLen = pstImage.nDataLen;
                m_stImageInfo.enImageType = pstImage.enImageType;
                m_stImageInfo.nFrameNum = pstImage.nFrameNum;
                
                if (m_MaxImageSize < pstImage.nDataLen)
                {
                    m_pcDataBuf = new byte[pstImage.nDataLen];
                    m_MaxImageSize = pstImage.nDataLen;

                }
                m_stImageInfo.pData = Marshal.UnsafeAddrOfPinnedArrayElement(m_pcDataBuf, 0);
                Marshal.Copy(pstImage.pData, m_pcDataBuf, 0, (int)pstImage.nDataLen);
                Monitor.Exit(Lock);
            }

            IntPtr hWnd = pictureBox1.Handle;
            nRet = Mv3dLpSDK.MV3D_LP_DisplayImage(pstImage, hWnd, Mv3dLpSDK.DisplayType_Auto, 0, 0);

            return nRet;
        }

        public void ReceiveThreadProcess()
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            MV3D_LP_IMAGE_DATA stImageData = new MV3D_LP_IMAGE_DATA();

            Int32 nCount =2;
            MV3D_LP_IMAGE_DATA_VECTOR stImageDataVector = new MV3D_LP_IMAGE_DATA_VECTOR(3);
            for (UInt32 i = 0; i < nCount; i++)
            {
                stImageDataVector.Add(new MV3D_LP_IMAGE_DATA());
            }

            while (this.m_bGrabbing)
            {
                int nHaveFirst = 0;
                int nHaveSecond = 0;
                nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_DevHandle, stImageDataVector[0], 10000);
                if (nRet == (int)Mv3dLpSDK.MV3D_LP_OK)
                {
                    nHaveFirst = 1;
                }
                else
                {
                    continue;
                }

                nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_DevHandleSecond, stImageDataVector[1], 10000);
                if (nRet == (int)Mv3dLpSDK.MV3D_LP_OK)
                {
                    nHaveSecond = 1;
                }
                else
                {
                    continue;
                }

                if(nHaveSecond == 1 && nHaveFirst == 1)
                {
                    MV3D_LP_IMAGE_DATA stDepthImageData = new MV3D_LP_IMAGE_DATA(); ;
                    nRet = Mv3dLpSDK.MV3D_LP_DepthMosaic(stImageDataVector[0], (uint)nCount, stDepthImageData);
                    if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
                    {
                        throw new ArgumentException(nRet.ToString());
                    }

                    nRet = DisplayImage(stDepthImageData);
                    if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
                    {
                        throw new ArgumentException(nRet.ToString());
                    }
                }
            }
        }

        private void bnStartGrab_Click(object sender, EventArgs e)
        {
         // 标志位置位true | Set Flag True
            m_bGrabbing = true;

            m_hReceiveThread = new Thread(ReceiveThreadProcess);
            m_hReceiveThread.Start();

            // 开始采集 | Start Measure
            int nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(m_DevHandle);

            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
                ShowErrorMsg("Start Measure Fail!", nRet);
                return;
            }
            nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(m_DevHandleSecond);

            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
                ShowErrorMsg("Start Measure Fail!", nRet);
                return;
            }

            // 控件操作 | Control Operation
            SetCtrlWhenStartGrab();
            return;
        }

        private void SetCtrlWhenStopGrab()
        {
            bnStartGrab.Enabled = true;
            bnStopGrab.Enabled = false;
            bnSaveRAW.Enabled = false;
            bnSavePLY.Enabled = false;
            bnSaveTIFF.Enabled = false;

            bnSaveCSV.Enabled = false;
            bnSaveOBJ.Enabled = false;
            bnSaveBMP.Enabled = false;
            bnSaveJPG.Enabled = false;
        }

        private void bnStopGrab_Click(object sender, EventArgs e)
        {
            // 标志位设为false | Set flag bit false
            m_bGrabbing = false;
            m_hReceiveThread.Join();

            // 停止采集 | Stop Grabbing
            int nRet = Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandle);
            nRet = Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandleSecond);

            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Stop Measure Fail!", nRet);
            }

            m_stImageInfo.nFrameNum = 0;
            // 控件操作 | Control Operation
            SetCtrlWhenStopGrab();
        }

        // 保存图像 | Save Image
        private Int32 SaveImage(uint nFileType)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            if (!m_bGrabbing)
            {
                ShowErrorMsg("no start work!", 0);
                return Mv3dLpSDK.MV3D_LP_E_CALLORDER;
            }

            if (null == m_pcDataBuf)
            {
                ShowErrorMsg("No data，Save Image failed!", 0);

                return Mv3dLpSDK.MV3D_LP_E_CALLORDER;
            }

            if (0 == m_stImageInfo.nFrameNum)
            {
                ShowErrorMsg("NO Data, save nothing !", 0);
                return Mv3dLpSDK.MV3D_LP_E_PARAMETER;
            }
            string strFileName = "Image_";
            strFileName += m_stImageInfo.nFrameNum;
            Monitor.Enter(Lock);
            nRet = Mv3dLpSDK.MV3D_LP_SaveImage(m_stImageInfo, (uint)nFileType, strFileName);
            Monitor.Exit(Lock);
            return nRet;
        }

        private void bnSaveRAW_Click(object sender, EventArgs e)
        {
            if (!m_bGrabbing)
            {
                ShowErrorMsg("no start work!", 0);
                return;
            }

            Monitor.Enter(Lock);
            if (0 == m_stImageInfo.nDataLen)
            {
                ShowErrorMsg("no data!", 0);
                return;
            }

            string strFileName = "Image_";
            strFileName += m_stImageInfo.nFrameNum;
            strFileName += ".raw";

            FileStream file = new FileStream(strFileName, FileMode.Create, FileAccess.Write);

            {
                Monitor.Enter(Lock);

                file.Write(m_pcDataBuf, 0, (int)m_stImageInfo.nDataLen);
                Monitor.Exit(Lock);
            }

            file.Close();

            Monitor.Exit(Lock);
            ShowErrorMsg("Success save raw file!", 0);
        }

        private void bnSavePLY_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            uint m_FileType = Mv3dLpSDK.FileType_PLY;
            nRet = (int)SaveImage(m_FileType);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SaveImage failed!", 0);
                return;
            }

            ShowErrorMsg("Save ply image success!", 0);
        }

        private void bnSaveCSV_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            uint m_FileType = Mv3dLpSDK.FileType_CSV;
            nRet = (int)SaveImage(m_FileType);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SaveImage failed!", 0);
                return;
            }

            ShowErrorMsg("Save csv image success!", 0);
        }

        private void bnSaveOBJ_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            uint m_FileType = Mv3dLpSDK.FileType_OBJ;
            nRet = (int)SaveImage(m_FileType);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SaveImage failed!", 0);
                return;
            }

            ShowErrorMsg("Save obj image success!", 0);
        }

        private void bnSaveTIFF_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            uint m_FileType = Mv3dLpSDK.FileType_TIFF;
            nRet = (int)SaveImage(m_FileType);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SaveImage failed!", 0);
                return;
            }

            ShowErrorMsg("Save tiff image success!", 0);
        }

        private void bnSaveBMP_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            uint m_FileType = Mv3dLpSDK.FileType_BMP;
            nRet = (int)SaveImage(m_FileType);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SaveImage failed!", 0);
                return;
            }

           ShowErrorMsg("Save bmp image success!", 0);
        }

        private void bnSaveJPG_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            uint m_FileType = Mv3dLpSDK.FileType_JPG;
            nRet = (int)SaveImage(m_FileType);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("SaveImage failed!", 0);
                return;
            }
           ShowErrorMsg("Save jpg image success!", 0);
        }
      
        private void InitCtrlData()
        {
            bnOpen.Enabled = false;
            bnClose.Enabled = false;
            bnStartGrab.Enabled = false;
            bnStopGrab.Enabled = false;
            bnSaveRAW.Enabled = false;
            bnSavePLY.Enabled = false;
            bnSaveTIFF.Enabled = false;
            bnSaveCSV.Enabled = false;
            bnSaveOBJ.Enabled = false;
            bnSaveBMP.Enabled = false;
            bnSaveJPG.Enabled = false;
            bnGetParam.Enabled = false;
            bnSetParam.Enabled = false;
        }

        private void bnGetParam_Click(object sender, EventArgs e)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            MV3D_LP_PARAM pstValue = new MV3D_LP_PARAM();
            MV3D_LP_ENUMPARAM enumParam = new MV3D_LP_ENUMPARAM();

            // 获取感光灵敏度 | Get Photosensitivity
            pstValue.enParamType = Mv3dLpSDK.ParamType_Enum;
            nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_DevHandle, Constants.PHOTOSENSITYVITY, pstValue);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Get Photosensitivity failed!", nRet);
                return;
            }
            else
            {
                enumParam = pstValue.get_enumparam();
                if (Constants.PHOTOSENSITYVITY_CUSTOM != enumParam.nCurValue)
                {
                    return;
                }
            }

            // 获取曝光时间 | Get Exposure Time
            MV3D_LP_FLOATPARAM floatParam = new MV3D_LP_FLOATPARAM();
            pstValue.enParamType = Mv3dLpSDK.ParamType_Float;
            nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_DevHandle, Mv3dLpSDK.MV3D_LP_FLOAT_EXPOSURETIME, pstValue);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                ShowErrorMsg("Get exposure time failed!", nRet);
            }
            else
            {
                floatParam = pstValue.get_floatparam();
                tbExposure.Text = floatParam.fCurValue.ToString("F1");
            }
        }

        private void bnSetParam_Click(object sender, EventArgs e)
        {
            try
            {
                float.Parse(tbExposure.Text);
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }
            bool bHasError = false;

            MV3D_LP_PARAM pstValue = new MV3D_LP_PARAM();

            pstValue.enParamType = Mv3dLpSDK.ParamType_Float;
            MV3D_LP_FLOATPARAM floatParam = new MV3D_LP_FLOATPARAM();

            floatParam.fCurValue = float.Parse(tbExposure.Text);
            pstValue.set_floatparam(floatParam);
            int nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_DevHandle, Mv3dLpSDK.MV3D_LP_FLOAT_EXPOSURETIME, pstValue);
            if ((int)Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                bHasError = true;
                ShowErrorMsg("Set Exposure Time Fail!", nRet);
            }

            if (false == bHasError)
            {
                string errorMsg = "Set Para Success!";
                MessageBox.Show(errorMsg, "INFO");
            }
        }
    }
}

static class Constants
{
    public const int PHOTOSENSITYVITY_CUSTOM = 4;
    public const string PHOTOSENSITYVITY = "Photosensitivity";
}