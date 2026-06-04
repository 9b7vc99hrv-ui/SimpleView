using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleView_CallBack
{

    class ImageDataCallBackHandle : ImageDataCallBack
    {
        public override void run(MV3D_LP_IMAGE_DATA pstImageData)
        {
            if (null != pstImageData )
            {
                Console.WriteLine(String.Format("get image success： framenum {0} height {1} width {2}  len {3}!\r\n",
                    pstImageData.nFrameNum, pstImageData.nHeight, pstImageData.nWidth, pstImageData.nDataLen));
            }
            else
            {
                Console.WriteLine("pstImageData is null!\r\n");
            }
        }
    }

    class Program
    {
        static IntPtr m_handle = IntPtr.Zero;

        static void Main(string[] args)
        {
            ImageDataCallBackHandle pImageDataCallBack = new ImageDataCallBackHandle();
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

                //注册回调 | Register CallBack
                pImageDataCallBack.Register(m_handle);

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
