using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Automation.BDaq;
using TwinCAT.Ads;

namespace ADS_A4704
{
    class Program
    {
        static TcAdsClient tcClient;
        static int hDigOutP0,hEndA4704Program;
        static AdsStream dataStream;
        static BinaryReader binReader;
        static uint huValHandle = 0;
        static byte portValue = 0;
        static bool bEndProgram = false;
        static InstantDoCtrl instantDoCtrl;
        static ErrorCode errorCode = ErrorCode.Success;
        static int port = 0;
        static ManualResetEvent manualResetEvent;
        static Thread heartbeatThread;

        static void Main(string[] args)
        {
            string deviceDescription = "USB-4704,BID#0";
            string profilePath = @"C:\Advantech\Profile\p2.xml";

            tcClient = new TcAdsClient();
            dataStream = new AdsStream(1);
            
            instantDoCtrl = new InstantDoCtrl();
            binReader = new BinaryReader(dataStream, System.Text.Encoding.ASCII);
            tcClient.Connect(851);
            
            try
            {              
                instantDoCtrl.SelectedDevice = new DeviceInformation(deviceDescription);
                errorCode = instantDoCtrl.LoadProfile(profilePath);//Loads a profile to initialize the device.
                if (BioFailed(errorCode))
                {
                    throw new Exception();
                }

                hDigOutP0 = tcClient.AddDeviceNotification("A4704.byDigOutP0", dataStream, 0, 1, AdsTransMode.OnChange, 10, 0, huValHandle);
                hEndA4704Program = tcClient.AddDeviceNotification("A4704.bEndA4704Program", dataStream, 0, 1, AdsTransMode.OnChange, 10, 0, huValHandle);
                tcClient.AdsNotification += new AdsNotificationEventHandler(OnNotification);

                HeartbeatThread heartbeat = new HeartbeatThread(tcClient);
                heartbeatThread = new Thread(new ThreadStart(heartbeat.beat));
                heartbeatThread.Start();

                manualResetEvent = new ManualResetEvent(false);
                manualResetEvent.WaitOne();

                //Console.ReadKey(false);
            }
            catch (Exception e)
            {
                // Something is wrong
                string errStr = BioFailed(errorCode) ? " Some error occurred. And the last error code is " + errorCode.ToString()
                                                           : e.Message;
                //Console.WriteLine(errStr);
            }
            finally
            {                
                instantDoCtrl.Dispose();
                heartbeatThread.Abort();
                heartbeatThread.Join();
                tcClient.DeleteDeviceNotification(hDigOutP0);
                tcClient.Dispose();           
            }

        }
        private static void OnNotification(object sender, AdsNotificationEventArgs e)
        {

            if (e.NotificationHandle == hDigOutP0)
            {

                portValue = binReader.ReadByte();
                errorCode = instantDoCtrl.Write(port, portValue);
            }
            else if (e.NotificationHandle == hEndA4704Program)
            {
                bEndProgram = binReader.ReadBoolean();
                if(bEndProgram)
                    manualResetEvent.Set();
            }
        }
        static bool BioFailed(ErrorCode err)
        {
            return err < ErrorCode.Success && err >= ErrorCode.ErrorHandleNotValid;
        }
    }

    class HeartbeatThread 
    {
        TcAdsClient tcClient;
        int iHandle;
        bool beatVal = false;
        public HeartbeatThread(TcAdsClient tcClient)
        {
            this.tcClient = tcClient;
            iHandle = tcClient.CreateVariableHandle("A4704.bHeartbeat");
        }
        public void beat()
        {
            try
            {
                while (true)
                {
                    beatVal = !beatVal;
                    tcClient.WriteAny(iHandle, beatVal);
                    Thread.Sleep(1000);
                }
            }
            catch (ThreadAbortException ex)
            {
                tcClient.DeleteVariableHandle(iHandle);
            }
                        
        }    
    }
}
