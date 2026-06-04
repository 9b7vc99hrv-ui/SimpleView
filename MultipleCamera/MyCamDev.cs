using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections.ObjectModel;


using STC_Object = System.IntPtr;
using STC_DataSet = System.IntPtr;
namespace BasicDemo
{
    class MyCamDev
    {
        private static readonly object Lock = new object();
        bool m_bGrabbing = false;
        Thread m_hReceiveThread = null;
 
        MV3D_LP_IMAGE_DATA m_stImageInfo = new MV3D_LP_IMAGE_DATA();
        static UInt32 m_MaxImageSize = 1024 * 1024 * 30;
        byte[] m_pcDataBuf = new byte[m_MaxImageSize];

        STC_DataSet m_DevHandle = IntPtr.Zero;
        IntPtr m_hWnd = IntPtr.Zero;

        public int Open(string strSerialNumber)
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_DevHandle, strSerialNumber);
            return nRet;
        }

        public int Close()
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            // 标志位设为false | Set flag bit false
            if (m_bGrabbing == true)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
            }
            nRet = Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandle);
            nRet = Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_DevHandle);
            m_DevHandle = IntPtr.Zero;
            m_hWnd = IntPtr.Zero;
            return nRet;
        }

        public void ReceiveThreadProcess()
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;

            STC_DataSet pDataSet = IntPtr.Zero;
            UInt32 nTimeOut = 50;

            while (m_bGrabbing)
            {
                MV3D_LP_IMAGE_DATA pstImage = new MV3D_LP_IMAGE_DATA();
                nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_DevHandle, pstImage, nTimeOut);
                if (0 == nRet)
                {
                    try
                    {
                        IntPtr hWnd = m_hWnd;
                        nRet = Mv3dLpSDK.MV3D_LP_DisplayImage(pstImage, hWnd, Mv3dLpSDK.DisplayType_Auto, 0, 0);
                        if (Mv3dLpSDK.MV3D_LP_OK != nRet)
                        {
                            throw new ArgumentException(nRet.ToString());
                        }
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
                    }
                    catch
                    {
                        Console.WriteLine("ERROR  !\r\n");
                    }
                }
                else
                {
                    continue;
                }
            }
        }

        public int StartGrabbing(IntPtr hWnd)
        {
            if (IntPtr.Zero != hWnd)
            {
                m_hWnd = hWnd;
            }
            
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            m_bGrabbing = true;

            nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(m_DevHandle);
            if (Mv3dLpSDK.MV3D_LP_OK != nRet)
            {
                m_bGrabbing = false;
            }
            else
            {
                m_hReceiveThread = new Thread(ReceiveThreadProcess);
                m_hReceiveThread.Start();
            }
            return nRet;
        }

        public int StopGrabbing()
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;

            // 标志位设为false | Set flag bit false
            if (m_bGrabbing == true)
            {
                m_bGrabbing = false;
                m_hReceiveThread.Join();
            }

            // 停止采集 | Stop Measure
            nRet = Mv3dLpSDK.MV3D_LP_StopMeasure(m_DevHandle);

            return nRet;
        }

        public int SoftTrigger()
        {
            int nRet = (int)Mv3dLpSDK.MV3D_LP_OK;
            nRet = Mv3dLpSDK.MV3D_LP_SoftTrigger(m_DevHandle);
            return nRet;
        }

        public Int32 SaveRAW()
        {
            if (!m_bGrabbing)
            {
                return Mv3dLpSDK.MV3D_LP_E_PARAMETER;   
            }

            if (0 == m_stImageInfo.nDataLen)
            {
                return Mv3dLpSDK.MV3D_LP_E_NODATA;
            }

            Monitor.Enter(Lock);
            string strFileName = "Image_";
            strFileName += m_stImageInfo.nFrameNum;
            strFileName += ".raw";

            FileStream file = new FileStream(strFileName, FileMode.Create, FileAccess.Write);

            {
                file.Write(m_pcDataBuf, 0, (int)m_stImageInfo.nDataLen);
            }

            file.Close();

            Monitor.Exit(Lock);

            return Mv3dLpSDK.MV3D_LP_OK;
        }

    }
}
