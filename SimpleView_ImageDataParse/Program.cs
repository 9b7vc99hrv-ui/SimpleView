using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SimpleView_FetchFrame
{
    class Program
    {
        static IntPtr m_handle = IntPtr.Zero;

        [DllImport("msvcrt.dll")]
        public static extern int _kbhit();

        static void Main(string[] args)
        {

            Console.WriteLine("program start\n");
            try
            {
                string version = Mv3dLpSDK.MV3D_LP_GetVersion();
                Console.WriteLine(string.Format("dll version: {0}.",version));

                UInt32 nDevNum = 0;
                int nRet = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref nDevNum);
                if (nDevNum == 0)
                {
                    Console.WriteLine("MV3D_LP_GetDeviceNumber is 0!\n");
                    Console.ReadKey();
                    return;
                }
                MV3D_LP_DEVICE_INFO_VECTOR stVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)nDevNum);
                for (UInt32 i = 0; i < nDevNum; i++)
                {
                    stVector.Add(new MV3D_LP_DEVICE_INFO());
                }
                // 获取网络中设备信息 | Get Devices Info
                nRet = Mv3dLpSDK.MV3D_LP_GetDeviceList(stVector[0], nDevNum, ref nDevNum);
                if (0 != nRet)
                {
                    Console.WriteLine("MV3D_LP_GetDeviceList failed, nRet{0:x}!\n", nRet);
                    Console.ReadKey();
                    return;
                }

                for (Int32 i = 0; i < nDevNum; i++)
                {
                    Console.WriteLine(string.Format("Index:{0}. SerialNum:{1} IP:{2} Name:{3}.\r\n", i, stVector[i].chSerialNumber, stVector[i].chCurrentIp, stVector[i].chModelName));
                }


                // 打开设备，默认打开第一个 | Open Device, Default First
                nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_handle, stVector[0].chSerialNumber);
                if (0 != nRet)
                {
                    Console.WriteLine("MV3D_LP_OpenDeviceBySN failed!\n");
                    Console.ReadKey();
                    return;
                }

                float fLSLProfileCoordXUnit = 1;
                float fLSLProfileCoordYUnit = 1;
                float fLSLProfileCoordZUnit = 1;

                MV3D_LP_PARAM pstParam = new MV3D_LP_PARAM(); ;
                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "LSLProfileCoordXUnit", pstParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    fLSLProfileCoordXUnit = pstParam.get_floatparam().fCurValue;
                }

                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "LSLProfileCoordYUnit", pstParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    fLSLProfileCoordYUnit = pstParam.get_floatparam().fCurValue;
                }

                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "LSLProfileCoordZUnit", pstParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    fLSLProfileCoordZUnit = pstParam.get_floatparam().fCurValue;
                }

                // 开始工作流程 | Start Measure
                nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(m_handle);
                if (0 != nRet)
                {
                    Console.WriteLine("MV3D_LP_StartMeasure failed!\n");
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("Start measure success.\n");
                Console.WriteLine("Press Esc to exit.\n");

                bool bExit_Main = false;
                while (!bExit_Main)
                {
                    MV3D_LP_IMAGE_DATA stImageData = new MV3D_LP_IMAGE_DATA();
                    try
                    {
                        nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_handle, stImageData, 5);
                        if (0 == nRet)
                        {
                            Console.WriteLine(String.Format("MV3D_LP_GetImage success： framenum {0} height{1} width{2}  len {3}!\r\n", stImageData.nFrameNum, stImageData.nHeight, stImageData.nWidth, stImageData.nDataLen));

                            if(Mv3dLpSDK.ImageType_Depth == stImageData.enImageType)  
                            {
                                byte[] ImageBuffer = new byte[(int)stImageData.nDataLen];
                                Marshal.Copy(stImageData.pData, ImageBuffer, 0, (int)stImageData.nDataLen);

                                short[] ImageBufferDepth = new short[(int)stImageData.nDataLen / 2];
                                for (int i = 0; i < stImageData.nDataLen / 2; i++)
                                {
                                    ImageBufferDepth[i] = (short)((ImageBuffer[i * 2] & 0xff) | (ImageBuffer[i * 2 + 1] & 0xff) << 8);
                                }

                                byte[] ImageBufferIntensity = new byte[(int)stImageData.nIntensityDataLen];
                                Marshal.Copy(stImageData.pIntensityData, ImageBufferIntensity, 0, (int)stImageData.nIntensityDataLen);

                                MV3D_LP_IMAGE_DATA stPointCloudImage = new MV3D_LP_IMAGE_DATA();
                                nRet = Mv3dLpSDK.MV3D_LP_MapDepthToPointCloud(stImageData, stPointCloudImage);
                                if (0 != nRet)
                                {
                                    Console.WriteLine(String.Format("MV3D_LP_MapDepthToPointCloud failed!\r\n"));
                                    break;
                                }
                                byte[] ImageBuffer2 = new byte[(int)stPointCloudImage.nDataLen];
                                Marshal.Copy(stPointCloudImage.pData, ImageBuffer2, 0, (int)stPointCloudImage.nDataLen);

                                float[] ImageBufferPointCloud = new float[(int)stPointCloudImage.nDataLen / 4];
                                for (int i = 0; i < ImageBufferPointCloud.Length; i++)
                                {
                                    ImageBufferPointCloud[i] = BitConverter.ToSingle(ImageBuffer2, i * 4);
                                }

                            }

                            if(Mv3dLpSDK.ImageType_Profile == stImageData.enImageType)  
                            {
                                byte[] ImageBuffer = new byte[(int)stImageData.nDataLen];
                                GCHandle handle = GCHandle.Alloc(ImageBuffer, GCHandleType.Pinned);
                                Marshal.Copy(stImageData.pData, ImageBuffer, 0, (int)stImageData.nDataLen);

                                short[] ImageBufferPointCloud = new short[(int)stImageData.nDataLen / 2];
                                for (int i = 0; i < stImageData.nDataLen / 2; i++)
                                {
                                    ImageBufferPointCloud[i] = (short)((ImageBuffer[i * 2] & 0xff) | (ImageBuffer[i * 2 + 1] & 0xff) << 8);
                                }

                                float[] ImageBufferPointCloudfloat = new float[(int)stImageData.nDataLen / 2];
                                for (int i = 0; i < stImageData.nWidth * stImageData.nHeight; i++)
                                {
                                    ImageBufferPointCloudfloat[i*3] = ImageBufferPointCloud[i*3] * fLSLProfileCoordXUnit;
                                    ImageBufferPointCloudfloat[i*3+1] = ImageBufferPointCloud[i*3+1] * fLSLProfileCoordYUnit;
                                    ImageBufferPointCloudfloat[i*3+2] = ImageBufferPointCloud[i*3+2] * fLSLProfileCoordZUnit;
                                }

                                byte[] ImageBufferIntensity = new byte[(int)stImageData.nIntensityDataLen];
                                Marshal.Copy(stImageData.pIntensityData, ImageBufferIntensity, 0, (int)stImageData.nIntensityDataLen);
                            }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                    //按任意键退出 | Press Any Key TO Quit
                    if (1 == _kbhit())
                    {
                        bExit_Main = true;
                    }
                }

                Mv3dLpSDK.MV3D_LP_StopMeasure(m_handle);
                Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Mv3dLpSDK.MV3D_LP_Finalize();
            }
            Console.WriteLine("done");
            Console.ReadKey();
        }
    }
}
