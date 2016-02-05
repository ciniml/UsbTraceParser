using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using EtwStream;

namespace UsbTraceExample
{
    class Program
    {
        static void Main(string[] args)
        {
            var subscription = ObservableEventListener.FromTraceEvent<UsbTraceParser.UsbTraceEventParser, UsbTraceParser.UsbPortCompleteUrbFunctionControlTransferData>()
                //.Where(evt => evt.fid_USBPORT_Device.idVendor == 0x4949 && evt.fid_USBPORT_Device.idProduct == 0x8888)
                .Do(evt =>
                {
                    var urb = evt.fid_USBPORT_URB;
                    Console.WriteLine($"{urb.fid_URB_Setup_bmRequestType:X02}, {urb.fid_URB_Setup_bRequest:X02}, {urb.fid_URB_Setup_wLength}, {evt.fid_URB_TransferDataLength}");
                })
                .Subscribe();
            using (subscription)
            {
                Console.ReadLine();
            }
        }
    }
}
