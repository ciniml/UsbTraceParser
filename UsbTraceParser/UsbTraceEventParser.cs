using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace UsbTraceParser
{
    public class UsbTraceEventParser : TraceEventParser
    {
        public static readonly string ProviderName = "Microsoft-Windows-USB-USBPORT";
        public static readonly Guid ProviderGuid = new Guid("{c88a4ef5-d048-4013-9408-e04b7db2814a}");
        public static readonly Lazy<TraceEvent[]> Templates;

        static UsbTraceEventParser()
        {
            Templates = new Lazy<TraceEvent[]>(() => new[]
            {
                CompleteUrbFunctionControlTransferDataTemplate(null),    
            });
        }
         
        public enum Keywords : long
        {
            Diagnostics = 0x01,
            PowerDiagnostics = 0x02,
        }

        public UsbTraceEventParser(TraceEventSource source) : base(source, false)
        {
        }


        protected override string GetProviderName()
        {
            return ProviderName;
        }

        protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            foreach (var template in Templates.Value)
            {
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                {
                    callback(template);
                }
            }
        }

        public event Action<UsbPortCompleteUrbFunctionControlTransferData> CompleteUrbFunctionControlTransferData
        {
            add { this.source.RegisterEventTemplate(CompleteUrbFunctionControlTransferDataTemplate(value)); }
            remove { this.source.UnregisterEventTemplate(value, 68, ProviderGuid);}
        }

        private static UsbPortCompleteUrbFunctionControlTransferData CompleteUrbFunctionControlTransferDataTemplate(Action<UsbPortCompleteUrbFunctionControlTransferData> action)
        {
            return new UsbPortCompleteUrbFunctionControlTransferData(action, 68, 10, "URB_FUNCTION_CONTROL_TRANSFER", Guid.Empty, 27, "Opcode27", ProviderGuid, ProviderName);
        }
    }
}
