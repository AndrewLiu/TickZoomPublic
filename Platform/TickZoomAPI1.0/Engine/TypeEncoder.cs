using System;

namespace TickZoom.Api
{
    public unsafe interface TypeEncoder
    {
        long Encode(byte* ptr, object original);
    }

    public class TypeEncoder<T> : TypeEncoder
    {
        private Func<EncodeHelper,IntPtr,T,long> encoderDelegate;
        private EncodeHelper helper;

        public TypeEncoder(EncodeHelper helper)
        {
            this.helper = helper;
        }

        public Func<EncodeHelper, IntPtr, T, long> EncoderDelegate
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

        public unsafe long Encode(byte* ptr, object original)
        {
            return encoderDelegate(helper, (IntPtr)ptr, (T) original);
        }

    }
}