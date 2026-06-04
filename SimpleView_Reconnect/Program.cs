using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
namespace SimpleView_Reconnect
{
    public static class Common
    {
        public static byte[] m_pcDataBuf = new byte[16];
    }

    class ImageDataCallBackHandle : ImageDataCallBack
    {
        public override void run(MV3D_LP_IMAGE_DATA pstImageData)
        {
            if (null != pstImageData)
            {
                Console.WriteLine(String.Format("get image success： framenum {0} height{1} width{2}  len {3}!\r\n", pstImageData.nFrameNum, pstImageData.nHeight, pstImageData.nWidth, pstImageData.nDataLen));

            }
            else
            {
                Console.WriteLine("pstImageData is null!\r\n");
            }
        }
    }

    class Program
    {
        class ExceptionCallBackHandle : ExceptionCallBack
        {
            public ExceptionCallBackHandle(IntPtr m_handle)
            {
                
            }
            public override void run(MV3D_LP_EXCEPTION_INFO pstExceptInfo)
            {
                if (null != pstExceptInfo)
                {
                    Console.WriteLine("device is offline.");
                    Mv3dLpSDK.MV3D_LP_StopMeasure(m_handle);
                    Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);

                    int nRet = 0;
                    while (true)
                    {
                        nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_handle, Encoding.UTF8.GetString(Common.m_pcDataBuf));
                        if (0 != nRet)
                        {
                            Console.WriteLine("MV3D_LP_OpenDeviceBySN failed!\n");
                            Thread.Sleep(1000 * 5);
                        }
                        else
                        {
                            Console.WriteLine(string.Format("Reconnect:SerialNum {0}.\r\n", Encoding.UTF8.GetString(Common.m_pcDataBuf)));
                            Console.WriteLine("OpenDevice success.");
                            break;
                        }
                    }

                    ImageDataCallBackHandle pImageDataCallBack = new ImageDataCallBackHandle();
                    pImageDataCallBack.Register(m_handle);
                    ExceptionCallBackHandle pExpCallBack = new ExceptionCallBackHandle(m_handle);
                    pExpCallBack.Register(m_handle);

                    // 开始工作流程 | Start Measure
                    nRet = Mv3dLpSDK.MV3D_LP_StartMeasure(m_handle);

                    if (0 != nRet)
                    {
                        Console.WriteLine("MV3D_LP_StartMeasure failed!\n");
                        return;
                    }
                    Console.WriteLine("Start measure success.\n");
                    Console.WriteLine("Press Esc to exit.\n");
                   
                }
                else
                {
                    Console.WriteLine("pstExceptInfo is null!\r\n");
                }
            }
        }

        static IntPtr m_handle = IntPtr.Zero;

        static void Main(string[] args)
        {
            ExceptionCallBackHandle pExpCallBack = new ExceptionCallBackHandle(m_handle);
            ImageDataCallBackHandle pImageDataCallBack = new ImageDataCallBackHandle();
            Console.WriteLine("program start\n");
            try
            {
                string version = Mv3dLpSDK.MV3D_LP_GetVersion();
                Console.WriteLine(string.Format("dll version: {0}.", version));

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

                // 缓存相机序列号 | Cache SerialNumber Down
                Common.m_pcDataBuf = System.Text.Encoding.Default.GetBytes(stVector[0].chSerialNumber);

                // 打开设备，默认打开第一个 | Open Device, Default First
                nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref m_handle, stVector[0].chSerialNumber);
                if (0 != nRet)
                {
                    Console.WriteLine("MV3D_LP_OpenDeviceBySN failed!\n");
                    Console.ReadKey();
                    return;
                }

                pImageDataCallBack.Register(m_handle);
                pExpCallBack.Register(m_handle);

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

                while (true)
                {
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }

                if (m_handle != null)
                {
                    Mv3dLpSDK.MV3D_LP_StopMeasure(m_handle);
                    Mv3dLpSDK.MV3D_LP_CloseDevice(ref m_handle);
                }
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
        }
    }
}
