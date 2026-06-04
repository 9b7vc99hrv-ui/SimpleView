using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
namespace SimpleView_DirectOpenByIP
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
                // 从键盘获取IP | Get IP From KeyBoard
                Console.WriteLine("Please enter the camera IP to be connect：\n");
                string chIP = Console.ReadLine();

                // 根据IP打开设备 | OpenDevice By IP
                nRet = Mv3dLpSDK.MV3D_LP_OpenDeviceByIP(ref m_handle, chIP);
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
                            Console.WriteLine(String.Format("MV3D_LP_GetImage success： framenum {0} height{1} width{2} len {3}!\r\n", stImage.nFrameNum, stImage.nHeight, stImage.nWidth, stImage.nDataLen));
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
