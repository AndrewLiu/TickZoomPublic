using System;

namespace TickZoom.Api
{
    public class TypeDecoder
    {
        private Func<EncodeHelper, IntPtr, object> decoderDelegate;
        private EncodeHelper helper;

        public TypeDecoder(EncodeHelper helper)
        {
            this.helper = helper;
        }

        public Func<EncodeHelper, IntPtr, object> DecoderDelegate
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

        public unsafe object Decode(byte* ptr)
        {
            return decoderDelegate(helper, (IntPtr)ptr);
        }
    }
}