using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;


namespace TraceEventParserUtil
{
    public interface IEventDataType
    {
        int Length { get; }
    }

    public abstract class EventDataType<TDerived> : IEventDataType where TDerived : EventDataType<TDerived>
    {
        private readonly int eventDataLength;

        private static readonly Func<TDerived, byte[], int, int, int> Initializer = EventDataParserHelper.CreateInitializer<TDerived>();

        int IEventDataType.Length => this.eventDataLength;

        protected EventDataType(byte[] eventData, int offset, int length)
        {
            this.eventDataLength = Initializer((TDerived)this, eventData, offset, length);
        }
    }
}
