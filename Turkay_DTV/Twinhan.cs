﻿using System;
using System.Runtime.InteropServices;
using DirectShowLib;
using DirectShowLib.Utils;
using DShowNET.Helper;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;
namespace DShowNET
{
    /// <summary>
    /// Implements Twinhan Common Interface & DiSEqC.
    /// </summary>
    public class Twinhan : IksPropertyUtils
    {
        private int _prevDisEqcType = -1;
        private int _prevFrequency = -1;
        private int _prevPolarisation = -1;
        private IBaseFilter _captureFilter;
        private IntPtr _ptrPmt;
        private IntPtr _ptrDiseqc;
        private IntPtr _ptrDwBytesReturned;
        private IntPtr _thbdaBuf;
        private IntPtr _ptrOutBuffer;
        private IntPtr _ptrOutBuffer2;

        private readonly Guid THBDA_TUNER = new Guid("E5644CC4-17A1-4eed-BD90-74FDA1D65423");
        private readonly Guid GUID_THBDA_CMD = new Guid("255E0082-2017-4b03-90F8-856A62CB3D67");
        //private readonly uint THBDA_IOCTL_CI_SEND_PMT = 0xaa000338;
        //private readonly uint THBDA_IOCTL_CHECK_INTERFACE = 0xaa0001e4;
        //private readonly uint THBDA_IOCTL_CI_GET_STATE = 0xaa000320; 
        //private readonly uint THBDA_IOCTL_SET_DiSEqC = 0xaa0001a0;        
        //private readonly uint THBDA_IOCTL_SET_LNB_DATA = 0xaa000200;   

        private bool _initialized;
        private bool _isTwinHanCard;
        private bool _camPresent;
        private static uint METHOD_BUFFERED = 0;
        private static uint FILE_ANY_ACCESS = 0;
        private static uint THBDA_IO_INDEX = 0xAA00;
        public static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return ((DeviceType << 16) | (Access << 14) | (Function << 2) | Method);
        }

        ////////TFBDA/////////////////////////////////////////////////////
        private readonly uint THBDA_IOCTL_CI_SEND_PMT     = CTL_CODE(THBDA_IO_INDEX, 206, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private readonly uint THBDA_IOCTL_CHECK_INTERFACE = CTL_CODE(THBDA_IO_INDEX, 121, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private readonly uint THBDA_IOCTL_SET_DiSEqC      = CTL_CODE(THBDA_IO_INDEX, 104, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private readonly uint THBDA_IOCTL_SET_LNB_DATA    = CTL_CODE(THBDA_IO_INDEX, 128, METHOD_BUFFERED, FILE_ANY_ACCESS); 
        private readonly uint THBDA_IOCTL_CI_GET_STATE    = CTL_CODE(THBDA_IO_INDEX, 200, METHOD_BUFFERED, FILE_ANY_ACCESS);      
        private readonly uint THBDA_IOCTL_SET_TUNER_POWER = CTL_CODE(THBDA_IO_INDEX, 100, METHOD_BUFFERED, FILE_ANY_ACCESS); 
        private readonly uint THBDA_IOCTL_GET_TUNER_POWER = CTL_CODE(THBDA_IO_INDEX, 101, METHOD_BUFFERED, FILE_ANY_ACCESS); 
        private readonly uint THBDA_IOCTL_GET_DiSEqC      = CTL_CODE(THBDA_IO_INDEX, 105, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private readonly uint THBDA_IOCTL_LOCK_TUNER      = CTL_CODE(THBDA_IO_INDEX, 106, METHOD_BUFFERED, FILE_ANY_ACCESS); 
        private readonly uint THBDA_IOCTL_GET_SIGNAL_Q_S  = CTL_CODE(THBDA_IO_INDEX, 108, METHOD_BUFFERED, FILE_ANY_ACCESS); 
        private readonly uint THBDA_IOCTL_RESET_DEVICE    = CTL_CODE(THBDA_IO_INDEX, 120, METHOD_BUFFERED, FILE_ANY_ACCESS); 
        private readonly uint THBDA_IOCTL_GET_DEVICE_INFO = CTL_CODE(THBDA_IO_INDEX, 124, METHOD_BUFFERED, FILE_ANY_ACCESS);
        private readonly uint THBDA_IOCTL_GET_DRIVER_INFO = CTL_CODE(THBDA_IO_INDEX, 125, METHOD_BUFFERED, FILE_ANY_ACCESS); 
       
        /////////////////////////////////////////////////777777777777
      
      
        public Twinhan(IBaseFilter filter)
            : base(filter)
        {
            _initialized = false;
            _camPresent = false;
            _isTwinHanCard = false;
            _ptrPmt = Marshal.AllocCoTaskMem(8192);
            _ptrDwBytesReturned = Marshal.AllocCoTaskMem(20);
            _thbdaBuf = Marshal.AllocCoTaskMem(8192);
            _ptrOutBuffer = Marshal.AllocCoTaskMem(8192);
            _ptrOutBuffer2 = Marshal.AllocCoTaskMem(8192);
            _ptrDiseqc = Marshal.AllocCoTaskMem(8192);
            _captureFilter = filter;
            if (filter != null)
            {
                _isTwinHanCard = IsTwinhan;
                if (_isTwinHanCard)
                {
                    _camPresent = IsCamPresent();
                }
            }
            _initialized = true;
        }

        public bool IsTwinhan
        {
            get
            {
                if (_initialized)
                {
                    return _isTwinHanCard;
                }

                bool result = IsTwinhanCard();
                if (result)
                {
                    if (IsCamPresent())
                    {
                        throw new ApplicationException("Twinhan: CAM inserted");
                    }
                }
                return result;
            }
        }

        public void GetCAMStatus(out uint CIState, out uint MMIState)
        {
            CIState = 0;
            MMIState = 0;
            IPin pin = DsFindPin.ByDirection(captureFilter, PinDirection.Input, 0);
            if (pin != null)
            {
                DirectShowLib.IKsPropertySet propertySet = pin as DirectShowLib.IKsPropertySet;
                if (propertySet != null)
                {
                    Guid propertyGuid = THBDA_TUNER;
                    IntPtr thbdaBuf = Marshal.AllocCoTaskMem(1024);
                    IntPtr ptrDwBytesReturned = Marshal.AllocCoTaskMem(4);
                    IntPtr ptrOutBuffer = Marshal.AllocCoTaskMem(4096);
                    try
                    {
                        int thbdaLen = 0x28;
                        Marshal.WriteInt32(thbdaBuf, 0, 0x255e0082);
                        //GUID_THBDA_CMD  = new Guid( "255E0082-2017-4b03-90F8-856A62CB3D67" );
                        Marshal.WriteInt16(thbdaBuf, 4, 0x2017);
                        Marshal.WriteInt16(thbdaBuf, 6, 0x4b03);
                        Marshal.WriteByte(thbdaBuf, 8, 0x90);
                        Marshal.WriteByte(thbdaBuf, 9, 0xf8);
                        Marshal.WriteByte(thbdaBuf, 10, 0x85);
                        Marshal.WriteByte(thbdaBuf, 11, 0x6a);
                        Marshal.WriteByte(thbdaBuf, 12, 0x62);
                        Marshal.WriteByte(thbdaBuf, 13, 0xcb);
                        Marshal.WriteByte(thbdaBuf, 14, 0x3d);
                        Marshal.WriteByte(thbdaBuf, 15, 0x67);
                        Marshal.WriteInt32(thbdaBuf, 16, (int)THBDA_IOCTL_CI_GET_STATE); //control code
                        Marshal.WriteInt32(thbdaBuf, 20, (int)IntPtr.Zero); //LPVOID inbuffer
                        Marshal.WriteInt32(thbdaBuf, 24, 0); //DWORD inbuffersize
                        Marshal.WriteInt32(thbdaBuf, 28, ptrOutBuffer.ToInt32()); //LPVOID outbuffer
                        Marshal.WriteInt32(thbdaBuf, 32, 4096); //DWORD outbuffersize
                        Marshal.WriteInt32(thbdaBuf, 36, (int)ptrDwBytesReturned); //LPVOID bytesreturned

                        int hr = propertySet.Set(propertyGuid, 0, thbdaBuf, thbdaLen, thbdaBuf, thbdaLen);
                        if (hr == 0)
                        {
                            int bytesReturned = Marshal.ReadInt32(ptrDwBytesReturned);
                            CIState = (uint)Marshal.ReadInt32(ptrOutBuffer, 0);
                            MMIState = (uint)Marshal.ReadInt32(ptrOutBuffer, 4);
                            //throw new ApplicationException("Twinhan: CI State:{0:X} MMI State:{1:X}", CIState, MMIState);
                        }
                        else
                        {
                            //throw new ApplicationException("Twinhan: unable to get CI State hr:{0:X}", hr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(thbdaBuf);
                        Marshal.FreeCoTaskMem(ptrDwBytesReturned);
                        Marshal.FreeCoTaskMem(ptrOutBuffer);
                    }
                }
               ReleaseComObject(pin);
            }
        }

        public bool IsCamPresent()
        {
            if (_initialized)
            {
                return _camPresent;
            }
            uint CIState;
            uint MMIState;
            GetCAMStatus(out CIState, out MMIState);
            if (CIState != 0)
            {
                return true;
            }
            return false;
        }

        public bool IsCamReady()
        {
            return IsCamPresent();
        }

        public bool IsTwinhanCard()
        {
            if (_initialized)
            {
                return _isTwinHanCard;
            }          
            bool success = false;
            IntPtr ptrDwBytesReturned = Marshal.AllocCoTaskMem(4);
            try
            {
                int thbdaLen = 0x28;
                IntPtr thbdaBuf = Marshal.AllocCoTaskMem(thbdaLen);
                try
                {                   
                    Marshal.WriteInt32(thbdaBuf, 0, 0x255e0082);
                    //GUID_THBDA_CMD  = new Guid( "255E0082-2017-4b03-90F8-856A62CB3D67" );
                    Marshal.WriteInt16(thbdaBuf, 4, 0x2017);
                    Marshal.WriteInt16(thbdaBuf, 6, 0x4b03);
                    Marshal.WriteByte(thbdaBuf, 8, 0x90);
                    Marshal.WriteByte(thbdaBuf, 9, 0xf8);
                    Marshal.WriteByte(thbdaBuf, 10, 0x85);
                    Marshal.WriteByte(thbdaBuf, 11, 0x6a);
                    Marshal.WriteByte(thbdaBuf, 12, 0x62);
                    Marshal.WriteByte(thbdaBuf, 13, 0xcb);
                    Marshal.WriteByte(thbdaBuf, 14, 0x3d);
                    Marshal.WriteByte(thbdaBuf, 15, 0x67);
                    Marshal.WriteInt32(thbdaBuf, 16, (int)THBDA_IOCTL_CHECK_INTERFACE); //control code
                    Marshal.WriteInt32(thbdaBuf, 20, (int)IntPtr.Zero);
                    Marshal.WriteInt32(thbdaBuf, 24, 0);
                    Marshal.WriteInt32(thbdaBuf, 28, (int)IntPtr.Zero);
                    Marshal.WriteInt32(thbdaBuf, 32, 0);
                    Marshal.WriteInt32(thbdaBuf, 36, (int)ptrDwBytesReturned);

                    IPin pin = DsFindPin.ByDirection(captureFilter, PinDirection.Input, 0);
                    if (pin != null)
                    {
                        DirectShowLib.IKsPropertySet propertySet = pin as DirectShowLib.IKsPropertySet;
                        if (propertySet != null)
                        {
                            Guid propertyGuid = THBDA_TUNER;

                            int hr = propertySet.Set(propertyGuid, 0, thbdaBuf, thbdaLen, thbdaBuf, thbdaLen);
                            if (hr == 0)
                            {
                                //throw new ApplicationException("Twinhan card detected");
                                success = true;
                            }
                           ReleaseComObject(propertySet);
                        }
                        ReleaseComObject(pin);
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(thbdaBuf);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptrDwBytesReturned);
            }
            return success;
        }
        public void SendPMT(string camType, uint videoPid, uint audioPid, byte[] caPMT, int caPMTLen)
        {
            if (IsCamPresent() == false)
            {
                return;
            }
            int camNumber = 0;
            camType = camType.ToLower();
            if (camType.ToLower() == "default")
            {
                camNumber = 0;
            }
            if (camType.ToLower() == "viaccess")
            {
                camNumber = 1;
            }
            else if (camType.ToLower() == "aston")
            {
                camNumber = 2;
            }
            else if (camType.ToLower() == "conax")
            {
                camNumber = 3;
            }
            else if (camType.ToLower() == "cryptoworks")
            {
                camNumber = 4;
            }

            IntPtr ptrPMT = Marshal.AllocCoTaskMem(caPMTLen);
            //throw new ApplicationException("Twinhan: send PMT cam:{0} {1} len:{2} video:0x{3:X} audio:0x{4:X}", camType, camNumber, caPMTLen,
                    // videoPid, audioPid);

            string line = "";
            for (int i = 0; i < caPMTLen; ++i)
            {
                string tmp = String.Format("{0:X} ", caPMT[i]);
                line += tmp;
            }
            //throw new ApplicationException("Twinhan: capmt:{0}", line);
            if (caPMT.Length == 0)
            {
                return;
            }
            Marshal.Copy(caPMT, 0, ptrPMT, caPMTLen);
            IntPtr ptrDwBytesReturned = Marshal.AllocCoTaskMem(4);
            if (ptrDwBytesReturned == IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(ptrPMT);
                return;
            }
            IntPtr ksBla = Marshal.AllocCoTaskMem(0x18);
            if (ksBla == IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(ptrPMT);
                Marshal.FreeCoTaskMem(ptrDwBytesReturned);
                return;
            }
            int thbdaLen = 0x28;
            IntPtr thbdaBuf = Marshal.AllocCoTaskMem(thbdaLen);
            if (thbdaBuf == IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(ptrPMT);
                Marshal.FreeCoTaskMem(ptrDwBytesReturned);
                Marshal.FreeCoTaskMem(ksBla);
                return;
            }
            Marshal.WriteInt32(thbdaBuf, 0, 0x255e0082);
            //GUID_THBDA_CMD  = new Guid( "255E0082-2017-4b03-90F8-856A62CB3D67" );
            Marshal.WriteInt16(thbdaBuf, 4, 0x2017);
            Marshal.WriteInt16(thbdaBuf, 6, 0x4b03);
            Marshal.WriteByte(thbdaBuf, 8, 0x90);
            Marshal.WriteByte(thbdaBuf, 9, 0xf8);
            Marshal.WriteByte(thbdaBuf, 10, 0x85);
            Marshal.WriteByte(thbdaBuf, 11, 0x6a);
            Marshal.WriteByte(thbdaBuf, 12, 0x62);
            Marshal.WriteByte(thbdaBuf, 13, 0xcb);
            Marshal.WriteByte(thbdaBuf, 14, 0x3d);
            Marshal.WriteByte(thbdaBuf, 15, 0x67);
            Marshal.WriteInt32(thbdaBuf, 16, (int)THBDA_IOCTL_CI_SEND_PMT); //dwIoControlCode
            Marshal.WriteInt32(thbdaBuf, 20, (int)ptrPMT); //lpInBuffer
            Marshal.WriteInt32(thbdaBuf, 24, caPMTLen); //nInBufferSize
            Marshal.WriteInt32(thbdaBuf, 28, 0); //lpOutBuffer
            Marshal.WriteInt32(thbdaBuf, 32, 0); //nOutBufferSize
            Marshal.WriteInt32(thbdaBuf, 36, (int)ptrDwBytesReturned); //lpBytesReturned

            IPin pin = DsFindPin.ByDirection(captureFilter, PinDirection.Input, 0);
            if (pin != null)
            {
                IKsPropertySet propertySet = pin as IKsPropertySet;
                if (propertySet != null)
                {
                    Guid propertyGuid = THBDA_TUNER;
                    int hr = propertySet.RemoteSet(ref propertyGuid, 0, ksBla, 0x18, thbdaBuf, (uint)thbdaLen);
                    int back = Marshal.ReadInt32(ptrDwBytesReturned);
                    int ksBlaVal = Marshal.ReadInt32(ksBla);
                    if (hr != 0)
                    {
                        //throw new ApplicationException("Twinhan: SetStructure() failed 0x{0:X}", hr);
                    }
                    else
                    {
                        //throw new ApplicationException("Twinhan: SetStructure() returned ok 0x{0:X}", hr);
                    }
                   ReleaseComObject(propertySet);
                }
                ReleaseComObject(pin);
            }
            Marshal.FreeCoTaskMem(thbdaBuf);
            Marshal.FreeCoTaskMem(ptrDwBytesReturned);
            Marshal.FreeCoTaskMem(ptrPMT);
            Marshal.FreeCoTaskMem(ksBla);
        }

        protected override void SetStructure(Guid guidPropSet, uint propId, Type structureType, object structValue)
        {
            Guid propertyGuid = guidPropSet;
            IPin pin = DsFindPin.ByDirection(captureFilter, PinDirection.Input, 0);
            if (pin == null)
            {
                return;
            }
            IKsPropertySet propertySet = pin as IKsPropertySet;
            if (propertySet == null)
            {
                throw new ApplicationException("Twinhan: SetStructure() properySet=null");
                return;
            }
            int iSize = Marshal.SizeOf(structureType);
            //throw new ApplicationException("Twinhan: size:{0}", iSize);
            IntPtr pDataReturned = Marshal.AllocCoTaskMem(iSize);
            Marshal.StructureToPtr(structValue, pDataReturned, true);
            int hr = propertySet.RemoteSet(ref propertyGuid, propId, IntPtr.Zero, 0, pDataReturned,
                                           (uint)Marshal.SizeOf(structureType));
            if (hr != 0)
            {
               // throw new ApplicationException("Twinhan: SetStructure() failed 0x{0:X}", hr);
            }
            else
            {
                //throw new ApplicationException("Twinhan: SetStructure() returned ok 0x{0:X}", hr);
            }
            Marshal.FreeCoTaskMem(pDataReturned);
        }

        public void SendDiseqCommand(int antennaNr, int frequency, int switchingFrequency, int polarisation, int disEqcType,
                                     int lowOsc, int hiOsc)
        {
            //Log.Debug("Twinhan: DiSEqC command for type={0}, freq={1}, pol={2}", disEqcType, frequency, polarisation);
            //if (_prevDisEqcType == disEqcType && _prevFrequency == frequency && _prevPolarisation == polarisation)
            //{
            //    throw new ApplicationException("Twinhan: Skipping DiSEqC command for type={0}, freq={1}, pol={2}", disEqcType, frequency, polarisation);
            //    return;
            //}
            //Only universal
            Int32 LNBLOFLowBand = lowOsc;
            Int32 LNBLOFHighBand = hiOsc;
            Int32 LNBLOFHiLoSW = switchingFrequency / 1000;

            byte disEqcPort = 0;
            switch (disEqcType)
            {
                case 0: // none
                    disEqcPort = 0;
                    break;
                case 1: // Simple A
                    disEqcPort = 1;
                    break;
                case 2: // Simple B
                    disEqcPort = 2;
                    break;
                case 3: // Level 1 A/A
                    disEqcPort = 1;
                    break;
                case 4: // Level 1 B/A
                    disEqcPort = 2;
                    break;
                case 5: // Level 1 A/B
                    disEqcPort = 3;
                    break;
                case 6: // Level 1 B/B
                    disEqcPort = 4;
                    break;
            }
            byte turnon22Khz = 0;
            bool isHiBand = false;
            if (frequency >= (LNBLOFHiLoSW * 1000))
            {
                isHiBand = true;
                turnon22Khz = 2;
            }
            else
            {
                turnon22Khz = 1;
            }
            SetLnbData(true, LNBLOFLowBand, LNBLOFHighBand, LNBLOFHiLoSW, turnon22Khz, disEqcPort);
            SendDiseqcCommandTest(disEqcPort, isHiBand, frequency, polarisation);
            _prevDisEqcType = disEqcType;
            _prevFrequency = frequency;
            _prevPolarisation = polarisation;
        }

        private void SetLnbData(bool lnbPower, int LNBLOFLowBand, int LNBLOFHighBand, int LNBLOFHiLoSW, int turnon22Khz,
                                int disEqcPort)
        {
            //throw new ApplicationException("Twinhan: SetLnb diseqc port:{0} 22khz:{1} low:{2} hi:{3} switch:{4} power:{5}", disEqcPort, turnon22Khz,
                    // LNBLOFLowBand, LNBLOFHighBand, LNBLOFHiLoSW, lnbPower);
            int thbdaLen = 0x28;
            int disEqcLen = 20;
            Marshal.WriteByte(_ptrDiseqc, 0, (byte)(lnbPower ? 1 : 0)); // 0: LNB_POWER
            Marshal.WriteByte(_ptrDiseqc, 1, 0); // 1: Tone_Data_Burst (Tone_Data_OFF:0 | Tone_Burst_ON:1 | Data_Burst_ON:2)
            Marshal.WriteByte(_ptrDiseqc, 2, 0);
            Marshal.WriteByte(_ptrDiseqc, 3, 0);
            Marshal.WriteInt32(_ptrDiseqc, 4, LNBLOFLowBand); // 4: ulLNBLOFLowBand   LNBLOF LowBand MHz
            Marshal.WriteInt32(_ptrDiseqc, 8, LNBLOFHighBand); // 8: ulLNBLOFHighBand  LNBLOF HighBand MHz
            Marshal.WriteInt32(_ptrDiseqc, 12, LNBLOFHiLoSW); //12: ulLNBLOFHiLoSW   LNBLOF HiLoSW MHz
            Marshal.WriteByte(_ptrDiseqc, 16, (byte)turnon22Khz);
            //16: f22K_Output (F22K_Output_HiLo:0 | F22K_Output_Off:1 | F22K_Output_On:2
            Marshal.WriteByte(_ptrDiseqc, 17, (byte)disEqcPort); //17: DiSEqC_Port
            Marshal.WriteByte(_ptrDiseqc, 18, 0);
            Marshal.WriteByte(_ptrDiseqc, 19, 0);
            Marshal.WriteInt32(_thbdaBuf, 0, 0x255e0082);
            //GUID_THBDA_CMD  = new Guid( "255E0082-2017-4b03-90F8-856A62CB3D67" );
            Marshal.WriteInt16(_thbdaBuf, 4, 0x2017);
            Marshal.WriteInt16(_thbdaBuf, 6, 0x4b03);
            Marshal.WriteByte(_thbdaBuf, 8, 0x90);
            Marshal.WriteByte(_thbdaBuf, 9, 0xf8);
            Marshal.WriteByte(_thbdaBuf, 10, 0x85);
            Marshal.WriteByte(_thbdaBuf, 11, 0x6a);
            Marshal.WriteByte(_thbdaBuf, 12, 0x62);
            Marshal.WriteByte(_thbdaBuf, 13, 0xcb);
            Marshal.WriteByte(_thbdaBuf, 14, 0x3d);
            Marshal.WriteByte(_thbdaBuf, 15, 0x67);
            Marshal.WriteInt32(_thbdaBuf, 16, (int)THBDA_IOCTL_SET_LNB_DATA); //dwIoControlCode
            Marshal.WriteInt32(_thbdaBuf, 20, (int)_ptrDiseqc.ToInt32()); //lpInBuffer
            Marshal.WriteInt32(_thbdaBuf, 24, disEqcLen); //nInBufferSize
            Marshal.WriteInt32(_thbdaBuf, 28, (int)IntPtr.Zero); //lpOutBuffer
            Marshal.WriteInt32(_thbdaBuf, 32, 0); //nOutBufferSize
            Marshal.WriteInt32(_thbdaBuf, 36, (int)_ptrDwBytesReturned); //lpBytesReturned

            IPin pin = DsFindPin.ByDirection(_captureFilter, PinDirection.Input, 0);
            if (pin != null)
            {
                DirectShowLib.IKsPropertySet propertySet = pin as DirectShowLib.IKsPropertySet;
                if (propertySet != null)
                {
                    Guid propertyGuid = THBDA_TUNER;
                    int hr = propertySet.Set(propertyGuid, 0, _ptrOutBuffer2, 0x18, _thbdaBuf, thbdaLen);
                    if (hr != 0)
                    {
                        //throw new ApplicationException("TwinHan SetLNB failed 0x{0:X}", hr);
                    }
                    else
                    {
                        //throw new ApplicationException("TwinHan SetLNB ok 0x{0:X}", hr);
                    }
                    ReleaseComObject(propertySet);
                }
                ReleaseComObject(pin);
            }
        }

        public void SendDiseqcCommandTest(byte disEqcPort, bool isHiBand, int frequency, int polarisation)
        {
            int antennaNr = (int)disEqcPort;
            //"01,02,03,04,05,06,07,08,09,0a,0b,cc,cc,cc,cc,cc,cc,cc,cc,cc,cc,cc,cc,cc,cc,"   


            //bit 0   (1)     : 0=low band, 1 = hi band
            //bit 1 (2) : 0=vertical, 1 = horizontal
            //bit 3 (4) : 0=satellite position A, 1=satellite position B
            //bit 4 (8) : 0=switch option A, 1=switch option  B
            // LNB    option  position
            // 1        A         A
            // 2        A         B
            // 3        B         A
            // 4        B         B
            bool hiBand = isHiBand;

            bool isHorizontal = (polarisation == 0); // Polarisation.LinearH                                 
            byte cmd = 0xf0;
            cmd |= (byte)(hiBand ? 1 : 0);
            cmd |= (byte)((isHorizontal) ? 2 : 0);
            cmd |= (byte)((antennaNr - 1) << 2);
            byte[] diseqc = new byte[4];
            diseqc[0] = 0xe0;
            diseqc[1] = 0x10;
            diseqc[2] = 0x38;
            diseqc[3] = cmd;
            SendDiSEqCCommand(diseqc);
        }

        /// <summary>
        /// Sends the DiSEqC command.
        /// </summary>
        /// <param name="diSEqC">The DiSEqC command.</param>
        /// <returns>true if succeeded, otherwise false</returns>
       
        public bool SendDiSEqCCommand(byte[] diSEqC)
        {
            int thbdaLen = 0x28;
            int disEqcLen = 16;
            for (int i = 0; i < 12; ++i)
            {
                Marshal.WriteByte(_ptrDiseqc, 4 + i, 0);
            }
            Marshal.WriteInt32(_ptrDiseqc, 0, (int)diSEqC.Length); //command len
            for (int i = 0; i < diSEqC.Length; ++i)
            {
                Marshal.WriteByte(_ptrDiseqc, 4 + i, diSEqC[i]);
            }
            string line = "";
            for (int i = 0; i < disEqcLen; ++i)
            {
                byte k = Marshal.ReadByte(_ptrDiseqc, i);
                line += String.Format("{0:X} ", k);
            }
            Marshal.WriteInt32(_thbdaBuf, 0, 0x255e0082);
            //GUID_THBDA_CMD  = new Guid( "255E0082-2017-4b03-90F8-856A62CB3D67" );
            Marshal.WriteInt16(_thbdaBuf, 4, 0x2017);
            Marshal.WriteInt16(_thbdaBuf, 6, 0x4b03);
            Marshal.WriteByte(_thbdaBuf, 8, 0x90);
            Marshal.WriteByte(_thbdaBuf, 9, 0xf8);
            Marshal.WriteByte(_thbdaBuf, 10, 0x85);
            Marshal.WriteByte(_thbdaBuf, 11, 0x6a);
            Marshal.WriteByte(_thbdaBuf, 12, 0x62);
            Marshal.WriteByte(_thbdaBuf, 13, 0xcb);
            Marshal.WriteByte(_thbdaBuf, 14, 0x3d);
            Marshal.WriteByte(_thbdaBuf, 15, 0x67);
            Marshal.WriteInt32(_thbdaBuf, 16, (int)THBDA_IOCTL_SET_DiSEqC); //dwIoControlCode
            Marshal.WriteInt32(_thbdaBuf, 20, (int)_ptrDiseqc.ToInt32()); //lpInBuffer
            Marshal.WriteInt32(_thbdaBuf, 24, disEqcLen); //nInBufferSize
            Marshal.WriteInt32(_thbdaBuf, 28, (int)IntPtr.Zero); //lpOutBuffer
            Marshal.WriteInt32(_thbdaBuf, 32, 0); //nOutBufferSize
            Marshal.WriteInt32(_thbdaBuf, 36, (int)_ptrDwBytesReturned); //lpBytesReturned

            bool success = false;
            IPin pin = DsFindPin.ByDirection(_captureFilter, PinDirection.Input, 0);
            if (pin != null)
            {
                DirectShowLib.IKsPropertySet propertySet = pin as DirectShowLib.IKsPropertySet;
                if (propertySet != null)
                {
                    Guid propertyGuid = THBDA_TUNER;
                    int hr = propertySet.Set(propertyGuid, 0, _ptrOutBuffer2, 0x18, _thbdaBuf, thbdaLen);
                    if (hr != 0)
                    {
                        //throw new ApplicationException("TwinHan DiSEqC cmd:{0} failed 0x{1:X}", line, hr);
                    }
                    else
                    {
                        //throw new ApplicationException("TwinHan DiSEqC cmd:{0} succeeded", line);
                        success = true;
                    }
                   ReleaseComObject(propertySet);
                }
               ReleaseComObject(pin);
            }
            return success;
        }
      
        public static int ReleaseComObject(object obj)
        {
            if (obj != null)
            {
                return Marshal.ReleaseComObject(obj);
            }

            StackTrace st = new StackTrace(true);
            //Log.Error("Exception while releasing COM object (NULL) - stacktrace: {0}", st);

            return 0;
        }
        private static char ToSafeAscii(byte b)
        {
            if (b >= 32 && b <= 126)
            {
                return (char)b;
            }
            return '_';
        }
        public static void DumpBinary(byte[] sourceData, int offset, int length)
        {
            StringBuilder row = new StringBuilder();
            StringBuilder rowText = new StringBuilder();

            for (int position = offset; position < offset + length; position++)
            {
                if (position == offset || position % 0x10 == 0)
                {
                    if (row.Length > 0)
                    {
                        //Log.Log.WriteFile(String.Format("{0}|{1}", row.ToString().PadRight(55, ' '),
                        //                                rowText.ToString().PadRight(16, ' ')));
                    }
                    rowText.Length = 0;
                    row.Length = 0;
                    row.AppendFormat("{0:X4}|", position);
                }
                row.AppendFormat("{0:X2} ", sourceData[position]); // the hex code
                rowText.Append(ToSafeAscii(sourceData[position])); // the ascii char
            }
            if (row.Length > 0)
            {
                //Log.Log.WriteFile(String.Format("{0}|{1}", row.ToString().PadRight(55, ' '),
                //                                rowText.ToString().PadRight(16, ' ')));
            }
        }
        public static void DumpBinary(IntPtr sourceData, int offset, int length)
        {
            byte[] tmpBuffer = IntPtrToByteArray(sourceData, offset, length);
            DumpBinary(tmpBuffer, offset, length);
        }
        public static byte[] IntPtrToByteArray(IntPtr sourceData, int offset, int length)
        {
            byte[] tmpBuffer = new byte[length];
            Marshal.Copy(sourceData, tmpBuffer, offset, length);
            return tmpBuffer;
        }
      
    }
}