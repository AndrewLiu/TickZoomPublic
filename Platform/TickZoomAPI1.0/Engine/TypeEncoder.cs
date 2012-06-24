using System;

namespace TickZoom.Api
{
    public class TypeEncoder
    {
        private Delegate encoderDelegate;
        private Delegate decoderDelegate;
        private EncodeHelper helper;
        private Type type;

        public TypeEncoder(EncodeHelper helper, Type type)
        {
            this.helper = helper;
            this.type = type;
        }

        public Delegate EncoderDelegate
        {
            set
            {
                if (encoderDelegate != null)
                {
                    throw new InvalidOperationException("Can't change after originally set.");
                }
                encoderDelegate = value;
            }
        }

        public Delegate DecoderDelegate
        {
            set
            {
                if (decoderDelegate != null)
                {
                    throw new InvalidOperationException("Can't change after originally set.");
                }
                decoderDelegate = value;
            }
        }

        public Type Type
        {
            get { return type; }
        }

        public unsafe long Encode(byte* ptr, object original)
        {
            return (long)encoderDelegate.DynamicInvoke(helper,(IntPtr)ptr, original);
        }

        public unsafe long Decode(byte* ptr, EncodeHelper.ResultPointer resultPointer)
        {
            return (long)decoderDelegate.DynamicInvoke(helper,(IntPtr)ptr, resultPointer);
        }

    }
}