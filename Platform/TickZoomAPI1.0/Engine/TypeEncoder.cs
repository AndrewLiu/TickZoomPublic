using System;

namespace TickZoom.Api
{
    public class TypeEncoder
    {
        private Delegate encoderDelegate;
        private Delegate decoderDelegate;
        private Delegate lengthDelegate;

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

        public Delegate LengthDelegate
        {
            set
            {
                if (lengthDelegate != null)
                {
                    throw new InvalidOperationException("Can't change after originally set.");
                }
                lengthDelegate = value;
            }
        }

        public unsafe long Encode(byte* ptr, object original)
        {
            return (long)encoderDelegate.DynamicInvoke((IntPtr)ptr, original);
        }

        public unsafe long Decode(byte* ptr, byte* end, object original)
        {
            return (long)decoderDelegate.DynamicInvoke((IntPtr)ptr, (IntPtr)end, original);
        }

        public long Length(object original)
        {
            return (long)lengthDelegate.DynamicInvoke((IntPtr)0, original);
        }
    }
}