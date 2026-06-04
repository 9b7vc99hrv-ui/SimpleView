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

                MV3D_LP_PARAM stParam = new MV3D_LP_PARAM();
                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "LaserEnable", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Get LaserEnable {0} Success", stParam.get_boolparam());
                }

                nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_handle, "LaserEnable", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Set LaserEnable {0} Success", stParam.get_boolparam());
                }


                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "DeviceStreamChannelPacketSize", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Get DeviceStreamChannelPacketSize {0} Success", stParam.get_intparam().nCurValue);
                }

                nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_handle, "DeviceStreamChannelPacketSize", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Set DeviceStreamChannelPacketSize {0} Success", stParam.get_intparam().nCurValue);
                }


                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "LSLStepDistance", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Get LSLStepDistance {0} Success", stParam.get_floatparam().fCurValue);
                }

                nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_handle, "LSLStepDistance", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Set LSLStepDistance {0} Success", stParam.get_floatparam().fCurValue);
                }


                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "DeviceLinkHeartbeatMode", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Get DeviceLinkHeartbeatMode {0} Success", stParam.get_enumparam().nCurValue);
                }

                nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_handle, "DeviceLinkHeartbeatMode", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Set DeviceLinkHeartbeatMode {0} Success", stParam.get_enumparam().nCurValue);
                }


                nRet = Mv3dLpSDK.MV3D_LP_GetParam(m_handle, "DeviceUserID", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Get DeviceUserID {0} Success", stParam.get_stringparam().chCurValue);
                }

                nRet = Mv3dLpSDK.MV3D_LP_SetParam(m_handle, "DeviceUserID", stParam);
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Set DeviceUserID {0} Success", stParam.get_stringparam().chCurValue);
                }


                nRet = Mv3dLpSDK.MV3D_LP_Execute(m_handle, "DeviceReset");
                if (Mv3dLpSDK.MV3D_LP_OK == nRet)
                {
                    Console.WriteLine("Execute DeviceReset Success");
                }

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
