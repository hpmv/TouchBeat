using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SYNCTRLLib;

namespace SynGame
{
    struct Finger {
        public bool Exists { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Force { get; set; }
    }
    class Fingers {

        private Finger[] fingers = new Finger[5];

        private int minX, minY, maxX, maxY;

        private SynAPICtrl api;
        private SynDeviceCtrl device;
        private int numForces;

        public Fingers() {
            api = new SynAPICtrlClass();
            api.Initialize();
            int deviceHandle = 0;
            deviceHandle = api.FindDevice(SynConnectionType.SE_ConnectionUSB, SynDeviceType.SE_DeviceForcePad, -1);
            device = new SynDeviceCtrlClass();
            device.Select(deviceHandle);
            device.Activate();
            device.OnPacket += device_OnPacket;

            device.SetLongProperty(SynDeviceProperty.SP_IsMultiFingerReportEnabled, 1);
            device.SetLongProperty(SynDeviceProperty.SP_IsGroupReportEnabled, 1);
            minX = device.GetLongProperty(SynDeviceProperty.SP_XLoBorder);
            maxX = device.GetLongProperty(SynDeviceProperty.SP_XHiBorder);
            minY = device.GetLongProperty(SynDeviceProperty.SP_YLoBorder);
            maxY = device.GetLongProperty(SynDeviceProperty.SP_YHiBorder);
            numForces = device.GetLongProperty(SynDeviceProperty.SP_NumForceSensors);
        }

        void device_OnPacket()
        {
            var group = new SynGroupCtrlClass();
            device.LoadGroup(group);
            for (int i = 0; i < numForces; i++) {
                if ((group.FingerState[i] & (long) SynFingerFlags.SF_FingerPresent) != 0) {
                    fingers[i].Exists = true;
                    fingers[i].X = (float) (group.XRaw[i] - minX)/(maxX - minX);
                    fingers[i].Y = (float) (group.YRaw[i] - minY)/(maxY - minY);
                    fingers[i].Force = group.ZForce[i];
                }
                else {
                    fingers[i].Exists = false;
                }
            }
            for (int i = numForces; i < 5; i++) {
                fingers[i].Exists = false;
            }
        }

        public Finger[] getFingers() {
            return fingers;
        }
    }
}
