using System;
using System.Collections.Generic;

namespace TickZoom.Api
{
    public class TypeDecoderMap
    {
        public Dictionary<Type, TypeDecoder> decoders = new Dictionary<Type, TypeDecoder>();

        public bool TryGetValue(Type type, out TypeDecoder encoder)
        {
            return decoders.TryGetValue(type, out encoder);
        }

        public void Add(Type type, TypeDecoder encoder)
        {
            decoders.Add(type, encoder);
        }
    }
}