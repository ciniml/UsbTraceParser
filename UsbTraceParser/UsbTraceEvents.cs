using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace UsbTraceParser
{
    
    public abstract class UsbTraceEvent<T> : TraceEvent where T : UsbTraceEvent<T>
    {
        private static string[] propertyNames;
        
        protected class PropertyBag
        {
            private static readonly Func<OrderedDictionary, byte[], int, int, int> Initializer;

            static PropertyBag()
            {
                Initializer = (Func<OrderedDictionary, byte[], int, int, int>)EventDataParserHelper.CreateInitializer(typeof(T), true);
            }

            private readonly Lazy<OrderedDictionary> propertyBag;
            private readonly UsbTraceEvent<T> outer; 
            public PropertyBag(UsbTraceEvent<T> outer)
            {
                this.outer = outer;
                this.propertyBag = new Lazy<OrderedDictionary>(() =>
                {
                    var propertyBag_ = new OrderedDictionary();
                    Initializer(propertyBag_, this.outer.EventData(), 0, this.outer.EventDataLength);
                    return propertyBag_;
                });
            }

            public TValue Get<TValue>(string name)
            {
                return (TValue) this.propertyBag.Value[name];
            }

            public object Get(int index)
            {
                return this.propertyBag.Value[index];
            }
        }

        public override TraceEvent Clone()
        {
            var clone = (UsbTraceEvent<T>)base.Clone();
            clone.target = this.target;
            clone.propertyBag = new PropertyBag(clone);
            return clone;
        }

        static UsbTraceEvent()
        {
            var type = typeof (T);
            var properties = type.GetProperties().Where(property => property.GetCustomAttributes(typeof (EventDataMemberAttribute)).Any()).ToArray();
            propertyNames = properties.Select(property => property.Name).ToArray();
        } 

        private Action<T> target;
        protected PropertyBag propertyBag;
        protected UsbTraceEvent(Action<T> target, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName) : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.target = target;
            this.propertyBag = new PropertyBag(this);
        }

        #region MBCS string
        protected int GetMbcsStringLength(byte[] eventData, int offset, int length)
        {
            return (eventData.Skip(offset).Select((Value, Index) => new { Value, Index }).FirstOrDefault(pair => pair.Value == 0) ?? new { Value = (byte)0, Index = length }).Index;
        }
        protected int SkipMbcsString(int offset)
        {
            var eventData = this.EventData();
            var length = this.EventDataLength;
            var stringLength = this.GetMbcsStringLength(eventData, offset, length);
            return offset + length;
        }
        protected string GetMbcsStringAt(int offset, Encoding mbcsEncoding = null)
        {
            mbcsEncoding = mbcsEncoding ?? Encoding.Default;
            var eventData = this.EventData();
            var length = this.EventDataLength;
            var stringLength = this.GetMbcsStringLength(eventData, offset, length);
            return mbcsEncoding.GetString(eventData, offset, stringLength);
        }
        #endregion

        #region Additional field analysis functions

        protected ushort GetUInt16At(int offset)
        {
            var eventData = this.EventData();
            return 0 <= offset && offset + 1 < this.EventDataLength
                ? BitConverter.ToUInt16(eventData, offset)
                : (ushort)0;
        }
        protected uint GetUInt32At(int offset)
        {
            var eventData = this.EventData();
            return 0 <= offset && offset + 3 < this.EventDataLength
                ? BitConverter.ToUInt32(eventData, offset)
                : 0u;
        }
        protected ulong GetUInt64At(int offset)
        {
            var eventData = this.EventData();
            return 0 <= offset && offset + 7 < this.EventDataLength
                ? BitConverter.ToUInt16(eventData, offset)
                : (ulong)0;
        }

        #endregion
        public override object PayloadValue(int index)
        {
            if (0 <= index && index < propertyNames.Length)
            {
                return this.propertyBag.Get(index);
            }
            else
            {
                return null;
            }
        }

        protected override void Dispatch()
        {
            this.target((T) this);
        }

        public override string[] PayloadNames => propertyNames;

        protected override Delegate Target
        {
            get { return this.target; }
            set { this.target = (Action<T>)value; }
        }
    }

    public interface IEventDataType
    {
        int Length { get; }
    }

    

    public sealed class UsbPortHc : EventDataType<UsbPortHc>
    {
        [EventDataMember]
        public ulong DeviceObject { get; private set; }
        [EventDataMember]
        public uint PciBus { get; private set; }
        [EventDataMember]
        public ushort PciDevice { get; private set; }
        [EventDataMember]
        public ushort PciFunction { get; private set; }
        [EventDataMember]
        public ushort PciVendorId { get; private set; }
        [EventDataMember]
        public ushort PciDeviceId { get; private set; }

        public UsbPortHc(byte[] eventData, int offset, int length) : base(eventData, offset, length)
        {
        }
    }

    public sealed class UsbPortDevice : EventDataType<UsbPortDevice>
    {
        [EventDataMember]
        public ulong DeviceHandle {get; private set; }
        [EventDataMember]
        public ushort idVendor {get; private set; }
        [EventDataMember]
        public ushort idProduct {get; private set; }
        [EventDataMember]
        public uint PortPathDepth {get; private set; }
        [EventDataMember(6)]
        public uint[] PortPath {get; private set; }
        [EventDataMember]
        public uint DeviceSpeed {get; private set; }
        [EventDataMember]
        public uint DeviceAddress {get; private set; }


        public UsbPortDevice(byte[] eventData, int offset, int length) : base(eventData, offset, length)
        {
        }
    }

    public sealed class UsbPortEndpoint : EventDataType<UsbPortEndpoint>
    {
        [EventDataMember]
        public ulong Endpoint {get; private set; }
        [EventDataMember]
        public ulong PipeHandle {get; private set; }
        [EventDataMember]
        public ulong DeviceHandle {get; private set; }

        public UsbPortEndpoint(byte[] eventData, int offset, int length) : base(eventData, offset, length)
        {
        }
    }

    public sealed class UsbPortEndpointDescriptor : EventDataType<UsbPortEndpointDescriptor>
    {
        [EventDataMember]
        public byte bLength {get; private set; }
        [EventDataMember]
        public byte bDescriptorType {get; private set; }
        [EventDataMember]
        public byte bEndpointAddress {get; private set; }
        [EventDataMember]
        public byte bmAttributes {get; private set; }
        [EventDataMember]
        public ushort wMaxPacketSize {get; private set; }
        [EventDataMember]
        public byte bInterval {get; private set; }

        public UsbPortEndpointDescriptor(byte[] eventData, int offset, int length) : base(eventData, offset, length)
        {
        }
    }

    public sealed class UsbPortUrbControlTransfer : EventDataType<UsbPortUrbControlTransfer>
    {
        [EventDataMember]
        public ushort fid_URB_Hdr_Length {get; private set; }
        [EventDataMember]
        public ushort fid_URB_Hdr_Function {get; private set; }
        [EventDataMember]
        public uint fid_URB_Hdr_Status {get; private set; }
        [EventDataMember]
        public ulong fid_URB_Hdr_UsbDeviceHandle {get; private set; }
        [EventDataMember]
        public ulong fid_URB_Hdr_UsbdFlags {get; private set; }
        [EventDataMember]
        public ulong fid_URB_PipeHandle {get; private set; }
        [EventDataMember]
        public uint fid_URB_TransferFlags {get; private set; }
        [EventDataMember]
        public uint fid_URB_TransferBufferLength {get; private set; }
        [EventDataMember]
        public ulong fid_URB_TransferBuffer {get; private set; }
        [EventDataMember]
        public ulong fid_URB_TransferBufferMDL {get; private set; }
        [EventDataMember]
        public ulong fid_URB_ReservedMBZ {get; private set; }
        [EventDataMember(8)]
        public ulong[] fid_URB_ReservedHcd {get; private set; }
        [EventDataMember]
        public byte fid_URB_Setup_bmRequestType {get; private set; }
        [EventDataMember]
        public byte fid_URB_Setup_bRequest {get; private set; }
        [EventDataMember]
        public ushort fid_URB_Setup_wValue {get; private set; }
        [EventDataMember]
        public ushort fid_URB_Setup_wIndex {get; private set; }
        [EventDataMember]
        public ushort fid_URB_Setup_wLength {get; private set; }

        public UsbPortUrbControlTransfer(byte[] eventData, int offset, int length) : base(eventData, offset, length)
        {
        }
    }

    public sealed class UsbPortCompleteUrbFunctionControlTransferData : UsbTraceEvent<UsbPortCompleteUrbFunctionControlTransferData>
    {
        [EventDataMember]
        public UsbPortHc fid_USBPORT_HC => this.propertyBag.Get<UsbPortHc>(nameof(fid_USBPORT_HC));
        [EventDataMember]
        public UsbPortDevice fid_USBPORT_Device => this.propertyBag.Get<UsbPortDevice>(nameof(fid_USBPORT_Device));
        [EventDataMember]
        public UsbPortEndpoint fid_USBPORT_Endpoint => this.propertyBag.Get<UsbPortEndpoint>(nameof(fid_USBPORT_Endpoint));
        [EventDataMember]
        public UsbPortEndpointDescriptor fid_USBPORT_EndpointDescriptor => this.propertyBag.Get<UsbPortEndpointDescriptor>(nameof(fid_USBPORT_EndpointDescriptor));
        [EventDataMember]
        public ulong fid_IRP_Ptr => this.propertyBag.Get<ulong>(nameof(fid_IRP_Ptr));
        [EventDataMember]
        public ulong fid_URB_Ptr => this.propertyBag.Get<ulong>(nameof(fid_URB_Ptr));
        [EventDataMember]
        public UsbPortUrbControlTransfer fid_USBPORT_URB => this.propertyBag.Get<UsbPortUrbControlTransfer>(nameof(fid_USBPORT_URB));
        [EventDataMember]
        public ushort fid_URB_TransferDataLength => this.propertyBag.Get<ushort>(nameof(fid_URB_TransferDataLength));
        [EventDataMember(nameof(fid_URB_TransferDataLength))]
        public byte[] fid_URB_TransferData => this.propertyBag.Get<byte[]>(nameof(fid_URB_TransferData));

        public UsbPortCompleteUrbFunctionControlTransferData(Action<UsbPortCompleteUrbFunctionControlTransferData> target, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName) : base(target, eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
        }
    }
}
