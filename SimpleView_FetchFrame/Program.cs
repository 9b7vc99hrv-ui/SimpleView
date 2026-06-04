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
                    MV3D_LP_IMAGE_DATA stImage = new MV3D_LP_IMAGE_DATA();
                    try
                    {
                        nRet = Mv3dLpSDK.MV3D_LP_GetImage(m_handle, stImage, 5);
                        if (0 == nRet)
                        {
                            Console.WriteLine(String.Format("MV3D_LP_GetImage success： framenum {0} height{1} width{2}  len {3}!\r\n", stImage.nFrameNum, stImage.nHeight, stImage.nWidth, stImage.nDataLen));
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                    //按任意键退出 | Press Any Key To Quit
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
