using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace TraceEventParserUtil
{
    public abstract class TraceEventBase<T> : TraceEvent where T : TraceEventBase<T>
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
            private readonly TraceEventBase<T> outer;
            public PropertyBag(TraceEventBase<T> outer)
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
                return (TValue)this.propertyBag.Value[name];
            }

            public object Get(int index)
            {
                return this.propertyBag.Value[index];
            }
        }

        public override TraceEvent Clone()
        {
            var clone = (TraceEventBase<T>)base.Clone();
            clone.target = this.target;
            clone.propertyBag = new PropertyBag(clone);
            return clone;
        }

        static TraceEventBase()
        {
            var type = typeof(T);
            var properties = type.GetProperties().Where(property => property.GetCustomAttributes(typeof(EventDataMemberAttribute)).Any()).ToArray();
            propertyNames = properties.Select(property => property.Name).ToArray();
        }

        private Action<T> target;
        protected PropertyBag propertyBag;
        protected TraceEventBase(Action<T> target, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName) : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
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
            this.target((T)this);
        }

        public override string[] PayloadNames => propertyNames;

        protected override Delegate Target
        {
            get { return this.target; }
            set { this.target = (Action<T>)value; }
        }
    }
}
